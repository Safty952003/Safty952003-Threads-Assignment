
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

public class SyncPaymentGateway
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _timeoutOrderIds;
    private readonly HashSet<int> _declineOrderIds;
    private readonly object _paymentsLock = new();

    public SyncPaymentGateway(
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

    public PaymentResult ProcessPayment(
        int orderId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (_timeoutOrderIds.Contains(orderId))
        {
            Thread.Sleep(5000);
        }
        else
        {
            Thread.Sleep(200);
            Thread.Sleep(300);
            Thread.Sleep(200);
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_paymentsLock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payments = _store.Read<List<PaymentTransaction>>(
                DataFiles.Payments);

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

            _store.Write(DataFiles.Payments, payments);

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
    }

    public void Refund(string transactionId)
    {
        lock (_paymentsLock)
        {
            var payments = _store.Read<List<PaymentTransaction>>(
                DataFiles.Payments);

            var transaction = payments.FirstOrDefault(
                payment => payment.TransactionId == transactionId);

            if (transaction is null)
            {
                throw new InvalidOperationException(
                    $"Transaction '{transactionId}' was not found.");
            }

            transaction.Status = PaymentStatus.Refunded;

            _store.Write(DataFiles.Payments, payments);
        }
    }
}
