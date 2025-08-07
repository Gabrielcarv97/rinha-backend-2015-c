using System.Data;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using rinha_backend.Model;

namespace rinha_backend.Repository;

public class PaymentRepository : IPaymentRepository
{
    private readonly IDbConnection _connection;

    public PaymentRepository(IDbConnection connection)
    {
        _connection = connection;
    }
    
    public async Task BulkInsertAsync(IEnumerable<Payment> payments)
    {
        await using var connection = new NpgsqlConnection(_connection.ConnectionString);
        await connection.OpenAsync();

        using var writer = await connection.BeginBinaryImportAsync(@"
        COPY payment (correlationid, amount, requested, processorused)
        FROM STDIN (FORMAT BINARY)");

        foreach (var payment in payments)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(payment.CorrelationId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(payment.Amount, NpgsqlDbType.Numeric);
            await writer.WriteAsync(payment.Requested, NpgsqlDbType.TimestampTz);
            await writer.WriteAsync(payment.Processorused, NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }

    public void EnsureTableCreated()
    {
        try
        {
            const string sql = @"CREATE TABLE IF NOT EXISTS payment (
                                correlationid UUID PRIMARY KEY,
                                amount NUMERIC NOT NULL,
                                requested TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                                processorused VARCHAR(10) NOT NULL DEFAULT 'default'
                            );
                            
                            -- Índices para performance
                            CREATE INDEX IF NOT EXISTS idx_payment_requested ON payment (requested);";
            _connection.Execute(sql);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create database table: {ex.Message}", ex);
        }
    }


    public async Task<PaymentSummaryDto> GetSummaryAsync(DateTimeOffset from, DateTimeOffset to)
    {
        const string sql = @"
        SELECT processorused,
               COUNT(*) AS TotalRequests,
               COALESCE(SUM(amount), 0) AS TotalAmount
        FROM payment
        WHERE requested BETWEEN @From AND @To
        GROUP BY processorused;";

        var summary = new PaymentSummaryDto();

        await using var connection = new NpgsqlConnection(_connection.ConnectionString);
        await connection.OpenAsync();

        var result = await connection.QueryAsync(sql, new { From = from, To = to }, 
            commandTimeout: 60, 
            commandType: CommandType.Text);

        foreach (var row in result)
        {
            string processor = row.processorused;
            int totalRequests = (int)row.totalrequests;
            decimal totalAmount = row.totalamount;

            if (processor.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                summary.Default.TotalRequests = totalRequests;
                summary.Default.TotalAmount = totalAmount;
            }
            else if (processor.Equals("fallback", StringComparison.OrdinalIgnoreCase))
            {
                summary.Fallback.TotalRequests = totalRequests;
                summary.Fallback.TotalAmount = totalAmount;
            }
        }

        return summary;
    }
}