using System.Data;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using rinha_backend;
using rinha_backend.Jobs;
using rinha_backend.Model;
using rinha_backend.Repository;
using rinha_backend.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateSlimBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default") ??
                      Environment.GetEnvironmentVariable("ConnectionStrings__Default") ??
                      "Host=db;Database=rinha;Username=postgres;Password=postgres;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=50";

Console.WriteLine($"Using connection string: {connectionString}");
Console.WriteLine($"Estou na aplicação: {Constants.APP_NAME}");

builder.Services.AddScoped<IDbConnection>(_ => new NpgsqlConnection(connectionString));
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var redisConnectionString = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
        ? "localhost:6379"
        : "redis:6379";
    return ConnectionMultiplexer.Connect(redisConnectionString);
});
builder.Services.AddHttpClient<IPaymentProcessorService, PaymentProcessorService>();
builder.Services.AddHostedService<Consumer>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

try
{
    using var scope = app.Services.CreateScope();
    var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
    paymentRepo.EnsureTableCreated();
    Console.WriteLine("Database connection successful - table created/verified");
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Could not connect to database: {ex.Message}");
    Console.WriteLine("The application will continue, but database operations will fail until connection is established.");
}

app.MapPost("/payments", async (
    [FromBody] Payment payment) =>
{
    if (payment.CorrelationId == Guid.Empty)
    {
        return Results.BadRequest("CorrelationId is required and must be a valid UUID.");
    }

    if (payment.Amount <= 0)
    {
        return Results.BadRequest("Amount must be greater than zero.");
    }

    var paymentToProcess = new Payment
    {
        CorrelationId = payment.CorrelationId,
        Amount = payment.Amount,
        Requested = DateTime.UtcNow
    };

    var redis = app.Services.GetRequiredService<IConnectionMultiplexer>();
    var database = redis.GetDatabase();
    await database.ListRightPushAsync("payments-processor-queue", JsonSerializer.Serialize(paymentToProcess));

    return Results.Accepted();
});

app.MapGet("/payments/service-health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/payments-summary", async (
    [FromQuery] DateTimeOffset? from,
    [FromQuery] DateTimeOffset? to,
    IPaymentRepository paymentRepository) =>
{
    try
    {
        var fromDate = from ?? DateTimeOffset.MinValue;
        var toDate = to ?? DateTimeOffset.MaxValue;

        var summary = await paymentRepository.GetSummaryAsync(fromDate, toDate);

        var response = new PaymentSummaryResponse
        {
            Default = new ProcessorSummary
            {
                TotalRequests = summary.Default.TotalRequests,
                TotalAmount = summary.Default.TotalAmount
            },
            Fallback = new ProcessorSummary
            {
                TotalRequests = summary.Fallback.TotalRequests,
                TotalAmount = summary.Fallback.TotalAmount
            }
        };

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in /payments-summary: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return Results.Problem($"Internal server error: {ex.Message}", statusCode: 500);
    }
});

app.Run();