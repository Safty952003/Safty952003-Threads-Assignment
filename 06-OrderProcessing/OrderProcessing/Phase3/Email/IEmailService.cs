
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3.Email;

public interface IEmailService
{
    Task SendConfirmationAsync(
        Order order,
        Customer customer,
        CancellationToken cancellationToken = default);
}
