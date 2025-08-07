namespace rinha_backend.Model;

public sealed class PaymentSummaryDto
{
    public DefaultSummary Default { get; set; } = new();
    public FallbackSummary Fallback { get; set; } = new();
}

public sealed class DefaultSummary
{
    public int TotalRequests { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class FallbackSummary
{
    public int TotalRequests { get; set; }
    public decimal TotalAmount { get; set; }
}
