
using System.Diagnostics;
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

internal class Phase3Runner
{
    private readonly DataSeeder _seeder;
    private readonly IncomingOrderRepository _incomingOrderRepository;
    private readonly OrderProcessor _orderProcessor;
    private readonly OrderRepository _orderRepository;

    public Phase3Runner(
        DataSeeder seeder,
        IncomingOrderRepository incomingOrderRepository,
        OrderProcessor orderProcessor,
        OrderRepository orderRepository)
    {
        _seeder = seeder;
        _incomingOrderRepository = incomingOrderRepository;
        _orderProcessor = orderProcessor;
        _orderRepository = orderRepository;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default,
        int? maxOrders = null)
    {
        Console.WriteLine();
        Console.WriteLine("===== Phase 3: Order Processing =====");

        await _seeder.ResetAsync(cancellationToken);

        var allOrders = await _incomingOrderRepository.GetAllAsync(
            cancellationToken);

        var orders = maxOrders.HasValue
            ? allOrders.Take(maxOrders.Value).ToList()
            : allOrders;

        var tasks = new List<Task<bool>>();

        _orderProcessor.ResetObservedThreads();

        int threadPoolBefore = ThreadPool.ThreadCount;
        var processingStopwatch = Stopwatch.StartNew();

        foreach (Order order in orders)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            tasks.Add(
                _orderProcessor.ProcessOrderAsync(
                    order,
                    cancellationToken));
        }

        int completedTasks = 0;
        int cancelledTasks = 0;

        foreach (Task<bool> task in tasks)
        {
            try
            {
                await task;
                completedTasks++;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                cancelledTasks++;
            }
        }

        processingStopwatch.Stop();

        int threadPoolAfter = ThreadPool.ThreadCount;

        Console.WriteLine();
        Console.WriteLine("===== Phase 3: Processing Finished =====");
        Console.WriteLine($"Orders selected: {orders.Count}");
        Console.WriteLine($"Tasks started: {tasks.Count}");
        Console.WriteLine($"Tasks finished: {completedTasks}");
        Console.WriteLine($"Tasks cancelled: {cancelledTasks}");

        Console.WriteLine(
            $"Processing time: {processingStopwatch.Elapsed.TotalSeconds:F2} seconds");

        Console.WriteLine(
            $"Distinct thread IDs observed: {_orderProcessor.ObservedThreadCount}");

        Console.WriteLine(
            $"ThreadPool.ThreadCount: {threadPoolBefore} -> {threadPoolAfter}");

        if (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Run was cancelled.");
        }

        var finalOrders = await _orderRepository.GetAllAsync(
            CancellationToken.None);

        Console.WriteLine();
        Console.WriteLine("===== Final Order Statuses =====");
        Console.WriteLine(
            $"{"Order ID",-10} {"Status",-15} {"Failure Reason"}");

        foreach (Order order in finalOrders.OrderBy(order => order.Id))
        {
            Console.WriteLine(
                $"{order.Id,-10} {order.Status,-15} " +
                $"{order.FailureReason ?? "-"}");
        }
    }
}