namespace rinha_backend.Model;

public sealed class PaymentSummaryDto
{
    public ProcessorSummary Default { get; set; } = new();
    public ProcessorSummary Fallback { get; set; } = new();
}
