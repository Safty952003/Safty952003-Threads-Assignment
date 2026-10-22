using OrderProcessing.Phase3;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

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

        var orders = _store.Read<List<Order>>(
            DataFiles.IncomingOrders);

        int completedTasks = 0;
        int cancelledTasks = 0;

        foreach (Order order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _orderProcessor.ProcessOrder(
                    order,
                    cancellationToken);

                completedTasks++;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                cancelledTasks++;
                break;
            }
        }

        Console.WriteLine();
        Console.WriteLine("===== Sync Processing Finished =====");
        Console.WriteLine($"Orders loaded: {orders.Count}");
        Console.WriteLine($"Orders processed: {completedTasks}");
        Console.WriteLine($"Orders cancelled: {cancelledTasks}");

        if (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Run was cancelled.");
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
