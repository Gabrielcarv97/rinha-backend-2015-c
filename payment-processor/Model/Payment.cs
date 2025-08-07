namespace rinha_backend.Model;

public sealed class Payment
{
    public Guid CorrelationId { get; init; }
    public decimal Amount { get; init; }
    public DateTimeOffset Requested { get; init; } 
    public string Processorused { get; set; }
}