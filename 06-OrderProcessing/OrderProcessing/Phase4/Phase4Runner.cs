using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Phase3;
using OrderProcessing.Phase4;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class Phase4Runner
{
    private readonly DataSeeder _seeder;
    private readonly IncomingOrderRepository _incomingOrderRepository;
    private readonly OrderProcessor _orderProcessor;
    private readonly OrderRepository _orderRepository;
    private readonly CustomerDashboardService _customerDashboardService;
    private readonly ShippingQuoteService _shippingQuoteService;
    private readonly FraudScoringService _fraudScoringService;
    private readonly EmailResendService _emailResendService;

    public Phase4Runner(
        DataSeeder seeder,
        IncomingOrderRepository incomingOrderRepository,
        OrderProcessor orderProcessor,
        OrderRepository orderRepository,
        CustomerDashboardService customerDashboardService,
        ShippingQuoteService shippingQuoteService,
        FraudScoringService fraudScoringService,
        EmailResendService emailResendService)
    {
        _seeder = seeder;
        _incomingOrderRepository = incomingOrderRepository;
        _orderProcessor = orderProcessor;
        _orderRepository = orderRepository;
        _customerDashboardService = customerDashboardService;
        _shippingQuoteService = shippingQuoteService;
        _fraudScoringService = fraudScoringService;
        _emailResendService = emailResendService;
    }

    public async Task RunLiveCheckoutProgressAsync(
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            "===== Task 10.1: Live Checkout Progress =====");

        await _seeder.ResetAsync();

        var orders = await _incomingOrderRepository.GetAllAsync(
            cancellationToken);

        var batchTimer = Stopwatch.StartNew();

        var tasks = orders
            .Select(order => ProcessOrderAndMeasureAsync(
                order,
                cancellationToken))
            .ToArray();

        int completedCount = 0;

        await foreach (var task in Task.WhenEach(tasks))
        {
            var result = await task;
            completedCount++;

            string reason = string.IsNullOrWhiteSpace(
                result.Order.FailureReason)
                ? ""
                : $" ({result.Order.FailureReason})";

            Console.WriteLine(
                $"[{completedCount,2}/{orders.Count}] " +
                $"Order #{result.Order.Id} -> " +
                $"{result.Order.Status}{reason} " +
                $"after {result.Elapsed.TotalSeconds:F1} s");
        }

        batchTimer.Stop();

        var savedOrders = await _orderRepository.GetAllAsync(
            cancellationToken);

        Console.WriteLine();
        Console.WriteLine(
            $"All {completedCount} orders finished in " +
            $"{batchTimer.Elapsed.TotalSeconds:F2} s.");

        Console.WriteLine();
        Console.WriteLine("===== Status Summary =====");

        foreach (var group in savedOrders
                     .GroupBy(order => order.Status)
                     .OrderBy(group => group.Key))
        {
            Console.WriteLine(
                $"{group.Key}: {group.Count()}");
        }
    }

    private async Task<ProcessedOrderResult> ProcessOrderAndMeasureAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();

        await _orderProcessor.ProcessOrderAsync(
            order,
            cancellationToken);

        timer.Stop();

        return new ProcessedOrderResult(
            order,
            timer.Elapsed);
    }

    private sealed record ProcessedOrderResult(
        Order Order,
        TimeSpan Elapsed);

    public async Task RunCustomerDashboardAsync(
        int customerId,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"===== Task 10.2: Customer Dashboard #{customerId} =====");

        int threadPoolBefore = ThreadPool.ThreadCount;
        var taskTimer = Stopwatch.StartNew();

        try
        {
            var dashboard = await _customerDashboardService.LoadAsync(
                customerId,
                cancellationToken);

            var orders = string.Join(
                ", ",
                dashboard.Orders
                    .OrderBy(order => order.Id)
                    .Select(order => $"#{order.Id} {order.Status}"));

            var payments = string.Join(
                ", ",
                dashboard.Payments
                    .OrderBy(payment => payment.OrderId)
                    .Select(payment =>
                        $"#{payment.OrderId} {payment.Status}"));

            var emails = string.Join(
                ", ",
                dashboard.Emails
                    .OrderBy(email => email.OrderId)
                    .Select(email => $"#{email.OrderId}"));

            Console.WriteLine(
                $"{dashboard.Customer.Name} — " +
                $"{dashboard.Orders.Count} orders ({orders}), " +
                $"{dashboard.Payments.Count} payments ({payments}), " +
                $"{dashboard.Emails.Count} email(s) ({emails})");
        }
        catch (AggregateException ex)
        {
            foreach (var failure in ex.Flatten().InnerExceptions)
            {
                Console.WriteLine($"Load failed: {failure.Message}");
            }
        }
        finally
        {
            taskTimer.Stop();

            Console.WriteLine(
                $"Dashboard call elapsed: " +
                $"{taskTimer.ElapsedMilliseconds} ms");

            Console.WriteLine(
                "Distinct thread IDs observed: " +
                $"{_customerDashboardService.LastObservedThreadCount}");

            Console.WriteLine(
                $"ThreadPool.ThreadCount: {threadPoolBefore} -> " +
                $"{ThreadPool.ThreadCount}");
        }
    }

    public async Task RunShippingQuotesAsync(
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            "===== Task 10.3: Fastest Shipping Quote =====");

        var orders = await _orderRepository.GetAllAsync(
            cancellationToken);
        foreach (var order in orders.Where(o => o.Id >= 1 && o.Id <= 5))
        {
            Console.WriteLine(
                $"Order #{order.Id} | ShippingCost: {order.ShippingCost}");
        }

        _shippingQuoteService.ResetObservedThreads();

        int threadPoolBefore = ThreadPool.ThreadCount;
        var quotesTimer = Stopwatch.StartNew();

        try
        {
            foreach (var order in orders
                         .Where(order => order.Id >= 1 && order.Id <= 5)
                         .OrderBy(order => order.Id))
            {
                var quote =
                    await _shippingQuoteService.GetFastestQuoteAsync(
                        order,
                        cancellationToken);

                string cancelled = quote.CancelledCarriers.Count == 0
                    ? "None"
                    : string.Join(", ", quote.CancelledCarriers);

                Console.WriteLine(
                    $"Order #{order.Id} -> {quote.Carrier} | " +
                    $"Price: {quote.Price:F2} | " +
                    $"Time: {quote.ElapsedMs} ms | " +
                    $"Cancelled: {cancelled}");
            }

            Console.WriteLine();
            Console.WriteLine(
                "===== All Carriers Failure Test: Order #6 =====");

            var order6 = orders.First(order => order.Id == 6);

            try
            {
                var quote =
                    await _shippingQuoteService.GetFastestQuoteAsync(
                        order6,
                        cancellationToken);

                Console.WriteLine(
                    $"Order #6 -> {quote.Carrier} | " +
                    $"Price: {quote.Price:F2}");
            }
            catch (ShippingProviderUnavailableException ex)
            {
                Console.WriteLine(
                    $"Order #6 -> Shipping failed: {ex.Message}");
            }
        }
        finally
        {
            quotesTimer.Stop();

            Console.WriteLine();
            Console.WriteLine(
                $"Shipping quotes total time: " +
                $"{quotesTimer.Elapsed.TotalSeconds:F2} s");

            Console.WriteLine(
                $"Distinct thread IDs observed: " +
                $"{_shippingQuoteService.ObservedThreadCount}");

            Console.WriteLine(
                $"ThreadPool.ThreadCount: {threadPoolBefore} -> " +
                $"{ThreadPool.ThreadCount}");
        }
    }

    public void RunFraudScoring(
        CancellationToken cancellationToken)
    {
        _fraudScoringService.Run(cancellationToken);
    }

    public Task RunEmailResendAsync(
        CancellationToken cancellationToken)
    {
        return _emailResendService.RunAsync(cancellationToken);
    }
}