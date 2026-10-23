using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class SyncOrderRepository
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failSaveForOrderIds;

    private static readonly SemaphoreSlim _saveLock = new(1, 1);

    public SyncOrderRepository(
    JsonFileStore store,
    IEnumerable<int>? failSaveForOrderIds = null,
    bool enableFailureScenarios = true)
    {
        _store = store;

        _failSaveForOrderIds = enableFailureScenarios
            ? new HashSet<int>(
                failSaveForOrderIds ?? Enumerable.Empty<int>())
            : new HashSet<int>();
    }

    public void Save(Order order)
    {
        _saveLock.Wait();

        try
        {
            AppLogger.Log(
                $"[SyncOrderRepository] Opening connection to save order #{order.Id}.");

            Thread.Sleep(200);

            AppLogger.Log(
                $"[SyncOrderRepository] Beginning transaction for order #{order.Id}.");

            Thread.Sleep(100);

            AppLogger.Log(
                $"[SyncOrderRepository] Inserting or updating order #{order.Id}.");

            Thread.Sleep(400);

            var orders = _store.Read<List<Order>>(DataFiles.Orders);

            if (order.Id == 13 &&
                order.Status == OrderStatus.Completed &&
                _failSaveForOrderIds.Contains(order.Id))
            {
                AppLogger.Log(
                    "[SyncOrderRepository] Simulated database commit failure for order #13.");

                throw new DatabaseWriteException(
                    "Connection lost during commit");
            }

            var existingOrder = orders.FirstOrDefault(
                o => o.Id == order.Id);

            if (existingOrder is null)
            {
                orders.Add(order);
            }
            else
            {
                int index = orders.IndexOf(existingOrder);
                orders[index] = order;
            }

            _store.Write(DataFiles.Orders, orders);

            AppLogger.Log(
                $"[SyncOrderRepository] Committing transaction for order #{order.Id}.");

            Thread.Sleep(200);

            AppLogger.Log(
                $"[SyncOrderRepository] Order #{order.Id} saved successfully.");
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public List<Order> GetAll()
    {
        AppLogger.Log(
            "[SyncOrderRepository] Opening connection to get all orders.");

        Thread.Sleep(200);

        AppLogger.Log(
            "[SyncOrderRepository] Executing query for all orders.");

        Thread.Sleep(Random.Shared.Next(300, 601));

        var orders = _store.Read<List<Order>>(DataFiles.Orders);

        AppLogger.Log(
            $"[SyncOrderRepository] Retrieved {orders.Count} orders.");

        return orders;
    }

    public List<Order> GetByStatus(OrderStatus status)
    {
        AppLogger.Log(
            $"[SyncOrderRepository] Opening connection to get orders with status {status}.");

        Thread.Sleep(200);

        AppLogger.Log(
            $"[SyncOrderRepository] Executing query for status {status}.");

        Thread.Sleep(Random.Shared.Next(300, 601));

        var orders = _store.Read<List<Order>>(DataFiles.Orders);

        var filteredOrders = orders
            .Where(order => order.Status == status)
            .ToList();

        AppLogger.Log(
            $"[SyncOrderRepository] Retrieved {filteredOrders.Count} orders with status {status}.");

        return filteredOrders;
    }
}
