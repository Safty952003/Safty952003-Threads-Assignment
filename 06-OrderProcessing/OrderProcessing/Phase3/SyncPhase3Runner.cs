using OrderProcessing.Phase3;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;


using System.Collections.Concurrent;
using System.Diagnostics;

internal class SyncPhase3Runner
{
    private readonly DataSeeder _seeder;
    private readonly JsonFileStore _store;
    private readonly SyncOrderProcessor _orderProcessor;
    private readonly SyncOrderRepository _orderRepository;

    public SyncPhase3Runner(
        DataSeeder seeder,
        JsonFileStore store,
        SyncOrderProcessor orderProcessor,
        SyncOrderRepository orderRepository)
    {
        _seeder = seeder;
        _store = store;
        _orderProcessor = orderProcessor;
        _orderRepository = orderRepository;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine("===== Phase 3: Synchronous Order Processing =====");

        await _seeder.ResetAsync(cancellationToken);

        var allOrders = _store.Read<List<Order>>(
            DataFiles.IncomingOrders);

        // Task 9.6 compares the first 10 orders only.
        var orders = allOrders.Take(10).ToList();

        var usedThreadIds = new ConcurrentDictionary<int, byte>();
        var errors = new ConcurrentQueue<Exception>();

        int processedOrders = 0;
        int cancelledOrders = 0;

        int threadPoolBefore = ThreadPool.ThreadCount;
        var stopwatch = Stopwatch.StartNew();

        var threads = new List<Thread>();

        foreach (Order order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var thread = new Thread(() =>
            {
                usedThreadIds.TryAdd(
                    Environment.CurrentManagedThreadId, 0);

                try
                {
                    _orderProcessor.ProcessOrder(
                        order,
                        cancellationToken);

                    Interlocked.Increment(ref processedOrders);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    Interlocked.Increment(ref cancelledOrders);
                }
                catch (Exception exception)
                {
                    errors.Enqueue(exception);
                }
            })
            {
                Name = $"Order-{order.Id}"
            };

            threads.Add(thread);
        }

        // Start all 10 threads before waiting for any of them.
        foreach (Thread thread in threads)
        {
            thread.Start();
        }

        // Wait for every started thread to finish.
        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        stopwatch.Stop();

        int threadPoolAfter = ThreadPool.ThreadCount;

        Console.WriteLine();
        Console.WriteLine("===== Sync Comparison Finished =====");
        Console.WriteLine($"Orders selected: {orders.Count}");
        Console.WriteLine($"Threads started: {threads.Count}");
        Console.WriteLine($"Orders processed: {processedOrders}");
        Console.WriteLine($"Orders cancelled: {cancelledOrders}");
        Console.WriteLine(
            $"Processing time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Distinct threads used: {usedThreadIds.Count}");
        Console.WriteLine(
            $"ThreadPool.ThreadCount: {threadPoolBefore} -> {threadPoolAfter}");

        if (!errors.IsEmpty)
        {
            Console.WriteLine();
            Console.WriteLine("===== Processing Errors =====");

            foreach (Exception error in errors)
            {
                Console.WriteLine(error.Message);
            }
        }

        var finalOrders = _orderRepository.GetAll();

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