
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System.Collections.Concurrent;

public class SyncEmailService
{
    private readonly JsonFileStore _store;
    private readonly Dictionary<int, int> _failAttempts;
    private readonly ConcurrentDictionary<int, int> _attemptCounts = new();
    private readonly object _emailsLock = new();

    public SyncEmailService(
        JsonFileStore store,
        IDictionary<int, int>? failAttempts = null,
        bool enableFailureScenarios = true)
    {
        _store = store;

        _failAttempts = enableFailureScenarios && failAttempts is not null
            ? new Dictionary<int, int>(failAttempts)
            : new Dictionary<int, int>();
    }

    public void SendConfirmation(
        Order order,
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Thread.Sleep(300);

        cancellationToken.ThrowIfCancellationRequested();

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

        lock (_emailsLock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var emails = _store.Read<List<EmailMessage>>(
                DataFiles.Emails);

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

            _store.Write(DataFiles.Emails, emails);
        }
    }
}
