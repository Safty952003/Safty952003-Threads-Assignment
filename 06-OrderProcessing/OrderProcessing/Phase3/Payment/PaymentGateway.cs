
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3.Payment;

public class PaymentGateway : IPaymentGateway
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _timeoutOrderIds;
    private readonly HashSet<int> _declineOrderIds;
    private readonly SemaphoreSlim _paymentsLock = new(1, 1);


    public PaymentGateway(
        JsonFileStore store,
        IEnumerable<int>? timeoutOrderIds = null,
        IEnumerable<int>? declineOrderIds = null,
        bool enableFailureScenarios = true)
    {
        _store = store;

        _timeoutOrderIds = enableFailureScenarios
            ? new HashSet<int>(timeoutOrderIds ?? [])
            : new HashSet<int>();

        _declineOrderIds = enableFailureScenarios
            ? new HashSet<int>(declineOrderIds ?? [])
            : new HashSet<int>();
    }



    public async Task<PaymentResult> ProcessPaymentAsync(
        int orderId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (_timeoutOrderIds.Contains(orderId))
        {
            await Task.Delay(5000, cancellationToken);
        }
        else
        {
            await Task.Delay(200, cancellationToken);
            await Task.Delay(300, cancellationToken);
            await Task.Delay(200, cancellationToken);
        }

        await _paymentsLock.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payments = await _store.ReadAsync<List<PaymentTransaction>>(
                DataFiles.Payments,
                cancellationToken);

            var transactionId =
                "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

            var declined = _declineOrderIds.Contains(orderId);

            var transaction = new PaymentTransaction
            {
                OrderId = orderId,
                Amount = amount,
                Status = declined
                    ? PaymentStatus.Declined
                    : PaymentStatus.Captured,
                TransactionId = transactionId,
                CreatedAt = DateTime.UtcNow
            };

            payments.Add(transaction);

            await _store.WriteAsync(
                DataFiles.Payments,
                payments,
                cancellationToken);

            return new PaymentResult
            {
                Success = !declined,
                TransactionId = transactionId,
                Status = transaction.Status,
                Message = declined
                    ? "Card declined by issuer"
                    : "Payment captured successfully"
            };
        }
        finally
        {
            _paymentsLock.Release();
        }
    }

    public async Task RefundAsync(string transactionId)
    {
        await _paymentsLock.WaitAsync();

        try
        {
            var payments = await _store.ReadAsync<List<PaymentTransaction>>(
                DataFiles.Payments);

            var transaction = payments.FirstOrDefault(
                payment => payment.TransactionId == transactionId);

            if (transaction is null)
            {
                throw new InvalidOperationException(
                    $"Transaction '{transactionId}' was not found.");
            }

            transaction.Status = PaymentStatus.Refunded;

            await _store.WriteAsync(
                DataFiles.Payments,
                payments);
        }
        finally
        {
            _paymentsLock.Release();
        }
    }

}
