using rinha_backend.Model;
using System;
using System.Threading.Tasks;

namespace rinha_backend.Repository;

public interface IPaymentRepository
{
    Task BulkInsertAsync(IEnumerable<Payment> payments); 
    void EnsureTableCreated();
    Task<PaymentSummaryDto> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to); 
}
