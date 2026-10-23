using OrderProcessing.Phase3;
using OrderProcessing.Phase3.Email;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace OrderProcessing.Phase4;

public sealed class EmailResendService
{
    private readonly IEmailService _emailService;
    private readonly JsonFileStore _store;

    public EmailResendService(
        IEmailService emailService,
        JsonFileStore store)
    {
        _emailService = emailService;
        _store = store;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("===== Task 10.4 B: Email Re-send =====");

        var customer = new Customer
        {
            Id = 1,
            Email = "phase4-test@example.com"
        };

        var orders = Enumerable.Range(1001, 30)
            .Select(id => new Order
            {
                Id = id,
                TotalAmount = 100m
            })
            .ToArray();

        int activeSends = 0;
        int maxConcurrentSends = 0;
        int sentCount = 0;

        var threadIds = new ConcurrentDictionary<int, byte>();

        int threadPoolBefore = ThreadPool.ThreadCount;
        var stopwatch = Stopwatch.StartNew();

        await Parallel.ForEachAsync(
            orders,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 4
            },
            async (order, ct) =>
            {
                int current = Interlocked.Increment(ref activeSends);

                UpdateMaximum(ref maxConcurrentSends, current);

                threadIds.TryAdd(
                    Environment.CurrentManagedThreadId, 0);

                try
                {
                    await _emailService.SendConfirmationAsync(
                        order,
                        customer,
                        ct);

                    Interlocked.Increment(ref sentCount);
                }
                finally
                {
                    Interlocked.Decrement(ref activeSends);
                }
            });

        stopwatch.Stop();

        int threadPoolAfter = ThreadPool.ThreadCount;

        var storedEmails =
            await _store.ReadAsync<List<EmailMessage>>(
                DataFiles.Emails,
                cancellationToken);

        var testEmails = storedEmails
            .Where(email => email.OrderId >= 1001 &&
                            email.OrderId <= 1030)
            .ToList();

        bool exactlyOnce = Enumerable.Range(1001, 30)
            .All(orderId =>
                testEmails.Count(email => email.OrderId == orderId) == 1);

        Console.WriteLine($"Emails sent: {sentCount}");
        Console.WriteLine($"Test emails in emails.json: {testEmails.Count}");
        Console.WriteLine($"Each email stored exactly once: {exactlyOnce}");
        Console.WriteLine($"Highest concurrent sends: {maxConcurrentSends}");
        Console.WriteLine($"Total time: {stopwatch.Elapsed.TotalSeconds:F2} s");
        Console.WriteLine($"Distinct threads used: {threadIds.Count}");
        Console.WriteLine(
            $"ThreadPool.ThreadCount: {threadPoolBefore} -> {threadPoolAfter}");
    }

    private static void UpdateMaximum(ref int maximum, int value)
    {
        int current;

        while ((current = Volatile.Read(ref maximum)) < value &&
               Interlocked.CompareExchange(
                   ref maximum, value, current) != current)
        {
        }
    }
}