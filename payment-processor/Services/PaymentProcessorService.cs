using System.Net;
using System.Text;
using System.Text.Json;
using rinha_backend.Model;

namespace rinha_backend.Services;

public interface IPaymentProcessorService
{
    Task<string> ProcessPaymentAsync(Payment payment);
}

public class PaymentProcessorService : IPaymentProcessorService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentProcessorService> _logger;
    private readonly string _defaultUrl;
    private readonly string _fallbackUrl;

    public PaymentProcessorService(HttpClient httpClient, ILogger<PaymentProcessorService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;

        _defaultUrl = configuration["PaymentProcessors:Default"] ?? "http://payment-processor-default:8080";
        _fallbackUrl = configuration["PaymentProcessors:Fallback"] ?? "http://payment-processor-fallback:8080";

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string> ProcessPaymentAsync(Payment payment)
    {
        const int maxRetries = 3;

        var request = new PaymentProcessorRequest
        {
            CorrelationId = payment.CorrelationId,
            Amount = payment.Amount,
            RequestedAt = payment.Requested,
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_defaultUrl}/payments", content);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Payment {CorrelationId} processed by default on attempt {Attempt}", payment.CorrelationId, attempt);
                    return "default";
                }
                _logger.LogWarning("Attempt {Attempt} failed on default with status {StatusCode}", attempt, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} exception on default processor", attempt);
            }
            await Task.Delay(200);
        }

        try
        {
            var response = await _httpClient.PostAsync($"{_fallbackUrl}/payments", content);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Payment {CorrelationId} processed by fallback", payment.CorrelationId);
                return "fallback";
            }
            _logger.LogWarning("Fallback processor failed with status {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception on fallback processor");
        }

        _logger.LogError("Payment {CorrelationId} failed on both default and fallback", payment.CorrelationId);
        throw new Exception("Payment processing failed on both default and fallback.");
    }
}
