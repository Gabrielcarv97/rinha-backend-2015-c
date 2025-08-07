using System.Text.Json;
using rinha_backend.Model;
using rinha_backend.Repository;
using rinha_backend.Services;
using StackExchange.Redis;

namespace rinha_backend.Jobs;

public class Consumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPaymentProcessorService _paymentProcessorService;
    private readonly ILogger<Consumer> _logger;
    private readonly IDatabase _database;

    private const int MaxBatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(200);

    public Consumer(IServiceProvider serviceProvider,
        IPaymentProcessorService paymentProcessorService,
        ILogger<Consumer> logger,
        IConnectionMultiplexer redis)
    {
        _serviceProvider = serviceProvider;
        _paymentProcessorService = paymentProcessorService;
        _logger = logger;
        _database = redis.GetDatabase();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = new List<Task>();

        for (int i = 0; i < 30; i++)
        {
            int consumerId = i;
            tasks.Add(Task.Run(() => ProcessPaymentsAsync(consumerId, stoppingToken), stoppingToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessPaymentsAsync(int consumerId, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting payment consumer {ConsumerId}", consumerId);

        var buffer = new List<Payment>();
        var lastFlush = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var paymentJson = await _database.ListLeftPopAsync("payments-processor-queue");
                if (paymentJson.HasValue)
                {
                    var payment = JsonSerializer.Deserialize<Payment>(paymentJson!);

                    if (payment != null)
                    {
                        var redisKey = payment.CorrelationId.ToString();
                        if (!await _database.KeyExistsAsync(redisKey))
                        {
                            var respProcess = await _paymentProcessorService.ProcessPaymentAsync(payment);
                            payment.Processorused = respProcess;
                            buffer.Add(payment);

                            await _database.StringSetAsync(redisKey, "1", TimeSpan.FromMinutes(5));
                        }
                        else
                        {
                            _logger.LogWarning("Payment {CorrelationId} already processed", payment.CorrelationId);
                        }
                    }
                }
 
                if (buffer.Count >= MaxBatchSize || (DateTime.UtcNow - lastFlush) >= FlushInterval)
                {
                    if (buffer.Count > 0)
                    {
                        await SavePaymentsAsync(buffer);
                        buffer.Clear();
                        lastFlush = DateTime.UtcNow;
                    }
                }

                await Task.Delay(10, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consumer {ConsumerId} error", consumerId);
                await Task.Delay(100, stoppingToken);
            }
        }
        
        if (buffer.Count > 0)
        {
            await SavePaymentsAsync(buffer);
        }

        _logger.LogInformation("Consumer {ConsumerId} stopped", consumerId);
    }

    private async Task SavePaymentsAsync(List<Payment> payments)
    {
        using var scope = _serviceProvider.CreateScope();
        var paymentRepository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

        try
        {
            await paymentRepository.BulkInsertAsync(payments);
            _logger.LogInformation("Bulk inserted {Count} payments", payments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk insert payments");
        }
    }
}
