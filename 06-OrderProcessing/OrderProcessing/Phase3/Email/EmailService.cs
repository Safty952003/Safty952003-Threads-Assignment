
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System.Collections.Concurrent;

namespace OrderProcessing.Phase3.Email;

public class EmailService : IEmailService
{
    private readonly JsonFileStore _store;
    private readonly Dictionary<int, int> _failAttempts;
    private readonly ConcurrentDictionary<int, int> _attemptCounts = new();
    private static readonly SemaphoreSlim _emailFileLock = new(1, 1);
    private readonly TimeSpan _sendDelay;

    public EmailService(
    JsonFileStore store,
    IDictionary<int, int>? failAttempts = null,
    bool enableFailureScenarios = true,
    TimeSpan? sendDelay = null)
    {
        _store = store;

        _sendDelay = sendDelay ?? TimeSpan.FromMilliseconds(300);

        _failAttempts = enableFailureScenarios && failAttempts is not null
            ? new Dictionary<int, int>(failAttempts)
            : new Dictionary<int, int>();
    }


    public async Task SendConfirmationAsync(
        Order order,
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(_sendDelay, cancellationToken);
        
        int attempt = _attemptCounts.AddOrUpdate(
    order.Id,
    1,
    (_, current) => current + 1);

        if (_failAttempts.TryGetValue(order.Id, out int failures)
            && attempt <= failures)
        {
            throw new SmtpUnavailableException(
                "SMTP server unavailable");
        }

        await _emailFileLock.WaitAsync(cancellationToken);

        try
        {
            var emails = await _store.ReadAsync<List<EmailMessage>>(
                DataFiles.Emails,
                cancellationToken);

            emails.Add(new EmailMessage
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                To = customer.Email,
                Subject = $"Order #{order.Id} confirmation",
                Body = $"Your order #{order.Id} has been completed. " +
                       $"Total: {order.TotalAmount:F2}",
                SentAt = DateTime.UtcNow,
                Attempts = attempt,
                LastError = null
            });

            await _store.WriteAsync(
                DataFiles.Emails,
                emails,
                cancellationToken);
        }
        finally
        {
            _emailFileLock.Release();
        }
    }

}
