using OrderProcessing.Phase4;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class ShippingQuoteService
{
    private static readonly TimeSpan QuoteBudget =
        TimeSpan.FromMilliseconds(1200);

    private readonly IReadOnlyList<IShippingCarrier> _carriers;

    private readonly ConcurrentDictionary<int, byte>
        _observedThreadIds = new();

    public int ObservedThreadCount =>
        _observedThreadIds.Count;

    public ShippingQuoteService(
        IEnumerable<IShippingCarrier> carriers)
    {
        _carriers = carriers.ToList();

        if (_carriers.Count == 0)
        {
            throw new ArgumentException(
                "At least one shipping carrier is required.",
                nameof(carriers));
        }
    }

    public void ResetObservedThreads()
    {
        _observedThreadIds.Clear();
    }

    private void RecordCurrentThread()
    {
        _observedThreadIds.TryAdd(
            Environment.CurrentManagedThreadId,
            0);
    }

    public async Task<ShippingQuote> GetFastestQuoteAsync(
        Order order,
        CancellationToken ct)
    {
        RecordCurrentThread();

        var stopwatch = Stopwatch.StartNew();

        using var carrierCts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Start all carriers without waiting for any one of them.
        var running = _carriers
            .Select(carrier => new RunningQuote(
                carrier.Name,
                TryGetQuoteAsync(
                    carrier,
                    order,
                    carrierCts.Token)))
            .ToList();

        var pending = new List<RunningQuote>(running);

        var timeoutTask = Task.Delay(QuoteBudget, ct);

        try
        {
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                var nextCompletion = Task.WhenAny(
                    pending.Select(item => item.Task));

                var completed = await Task.WhenAny(
                    nextCompletion,
                    timeoutTask);

                RecordCurrentThread();

                ct.ThrowIfCancellationRequested();

                // No carrier completed successfully within the time budget.
                if (completed == timeoutTask)
                {
                    var cancelledCarriers = pending
                        .Where(item => !item.Task.IsCompleted)
                        .Select(item => item.Name)
                        .ToArray();

                    carrierCts.Cancel();

                    await Task.WhenAll(
                        running.Select(item => item.Task));

                    RecordCurrentThread();

                    stopwatch.Stop();

                    return new ShippingQuote(
                        "Flat rate",
                        25.00m,
                        stopwatch.ElapsedMilliseconds)
                    {
                        CancelledCarriers = cancelledCarriers
                    };
                }

                var completedTask = await nextCompletion;

                RecordCurrentThread();

                var completedCarrier = pending.First(
                    item => item.Task == completedTask);

                pending.Remove(completedCarrier);

                var attempt = await completedTask;

                RecordCurrentThread();

                // A failed carrier does not prevent trying the others.
                if (attempt.Price is not decimal price)
                {
                    continue;
                }

                // A successful quote has arrived.
                var cancelled = running
                    .Where(item =>
                        item.Task != completedTask &&
                        !item.Task.IsCompleted)
                    .Select(item => item.Name)
                    .ToArray();

                carrierCts.Cancel();

                // Observe the remaining tasks after cancellation.
                await Task.WhenAll(
                    running.Select(item => item.Task));

                RecordCurrentThread();

                stopwatch.Stop();

                return new ShippingQuote(
                    completedCarrier.Name,
                    price,
                    stopwatch.ElapsedMilliseconds)
                {
                    CancelledCarriers = cancelled
                };
            }

            // All carriers failed before the timeout.
            throw new ShippingProviderUnavailableException(
                "All carriers failed");
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            carrierCts.Cancel();

            await Task.WhenAll(
                running.Select(item => item.Task));

            RecordCurrentThread();

            throw;
        }
        finally
        {
            RecordCurrentThread();
            stopwatch.Stop();
        }
    }

    private async Task<CarrierAttempt> TryGetQuoteAsync(
        IShippingCarrier carrier,
        Order order,
        CancellationToken ct)
    {
        RecordCurrentThread();

        try
        {
            decimal price = await carrier.GetQuoteAsync(order, ct);

            RecordCurrentThread();

            return new CarrierAttempt(price);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            RecordCurrentThread();

            return new CarrierAttempt(null);
        }
        catch (Exception ex)
        {
            RecordCurrentThread();

            AppLogger.Log(
                $"[{carrier.Name}] quote failed: {ex.Message}");

            return new CarrierAttempt(null);
        }
    }

    private sealed record RunningQuote(
        string Name,
        Task<CarrierAttempt> Task);

    private sealed record CarrierAttempt(decimal? Price);
}