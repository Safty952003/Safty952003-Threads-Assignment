
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1.Repositories;

public class IncomingOrderRepository
{
    private readonly JsonFileStore _store;

    public IncomingOrderRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<List<Order>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            "[IncomingOrderRepository] Opening connection to get incoming orders.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            "[IncomingOrderRepository] Executing query for incoming orders.");

        int queryDelay = Random.Shared.Next(300, 601);

        await Task.Delay(queryDelay, cancellationToken);

        var orders = await _store.ReadAsync<List<Order>>(
            DataFiles.IncomingOrders,
            cancellationToken);

        AppLogger.Log(
            $"[IncomingOrderRepository] Retrieved {orders.Count} incoming orders.");

        return orders;
    }
}
