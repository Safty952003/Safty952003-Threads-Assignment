
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3.Payment;

public interface IPaymentGateway
{
    Task<PaymentResult> ProcessPaymentAsync(
        int orderId,
        decimal amount,
        CancellationToken cancellationToken);

    Task RefundAsync(string transactionId);
}
