namespace rinha_backend.Model;

public sealed class PaymentProcessorRequest
{
    public Guid CorrelationId { get; init; }
    public decimal Amount { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
}

public sealed class PaymentProcessorResponse
{
    public string Message { get; init; } = string.Empty;
}

public sealed class HealthCheckResponse
{
    public bool Failing { get; init; }
    public int MinResponseTime { get; init; }
}

public sealed class PaymentSummaryResponse
{
    public ProcessorSummary Default { get; set; } = new();
    public ProcessorSummary Fallback { get; set; } = new();
}

public sealed class ProcessorSummary
{
    public int TotalRequests { get; set; }
    public decimal TotalAmount { get; set; }
}
