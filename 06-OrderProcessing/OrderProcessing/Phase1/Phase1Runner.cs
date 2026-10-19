
using System.Diagnostics;
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class Phase1Runner
{
    private readonly DataSeeder _seeder;
    private readonly IncomingOrderRepository _incomingOrderRepository;
    private readonly OrderRepository _orderRepository;

    private readonly SyncOrderValidator _syncValidator;
    private readonly OrderValidator _asyncValidator;

    public Phase1Runner(
        DataSeeder seeder,
        IncomingOrderRepository incomingOrderRepository,
        OrderRepository orderRepository,
        SyncOrderValidator syncValidator,
        OrderValidator asyncValidator)
    {
        _seeder = seeder;
        _incomingOrderRepository = incomingOrderRepository;
        _orderRepository = orderRepository;
        _syncValidator = syncValidator;
        _asyncValidator = asyncValidator;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log("========== PHASE 1 ==========");

        // Reset data before the comparison.
        await _seeder.ResetAsync(cancellationToken);

        // Load the incoming orders.
        var incomingOrders = await _incomingOrderRepository
            .GetAllAsync(cancellationToken);

        AppLogger.Log(
            $"Loaded {incomingOrders.Count} incoming orders.");

        // Synchronous validation.
        AppLogger.Log(
            "Starting synchronous validation...");

        var syncStopwatch = Stopwatch.StartNew();

        foreach (var order in incomingOrders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _syncValidator.ValidateOrder(order);
        }

        syncStopwatch.Stop();

        AppLogger.Success(
            $"Synchronous validation finished in {syncStopwatch.Elapsed.TotalSeconds:F2} seconds.");

        // Reset data before the asynchronous run.
        await _seeder.ResetAsync(cancellationToken);

        // Asynchronous validation and saving.
        AppLogger.Log(
            "Starting asynchronous validation...");


        var asyncStopwatch = new Stopwatch();

        foreach (var order in incomingOrders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Measure validation time only.
            asyncStopwatch.Start();

            await _asyncValidator.ValidateOrderAsync(
                order, cancellationToken);

            asyncStopwatch.Stop();

            // Save the validated order outside the measurement.
            await _orderRepository.SaveAsync(
                order, cancellationToken);
        }


        asyncStopwatch.Stop();

        AppLogger.Success(
            $"Asynchronous validation finished in {asyncStopwatch.Elapsed.TotalSeconds:F2} seconds.");


        // Read the saved results from orders.json.
        var savedOrders = await _orderRepository.GetAllAsync(
            cancellationToken);

        var validatedOrders = await _orderRepository.GetByStatusAsync(
            OrderStatus.Validated, cancellationToken);

        var rejectedOrders = await _orderRepository.GetByStatusAsync(
            OrderStatus.Rejected, cancellationToken);

        Console.WriteLine();
        Console.WriteLine("===== Validation Summary =====");
        Console.WriteLine($"Total Orders: {savedOrders.Count}");
        Console.WriteLine($"Validated: {validatedOrders.Count}");
        Console.WriteLine($"Rejected: {rejectedOrders.Count}");

        foreach (var order in rejectedOrders.OrderBy(o => o.Id))
        {
            Console.WriteLine(
                $" - Order #{order.Id}: {order.FailureReason}");
        }

        Console.WriteLine(
            $"Sync Time: {syncStopwatch.Elapsed.TotalSeconds:F1} seconds");

        Console.WriteLine(
            $"Async Time: {asyncStopwatch.Elapsed.TotalSeconds:F1} seconds");

        Console.WriteLine(
            $"orders.json now contains {savedOrders.Count} records.");

    }
}
