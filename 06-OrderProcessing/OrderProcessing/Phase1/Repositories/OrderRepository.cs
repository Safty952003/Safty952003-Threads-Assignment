
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1.Repositories;

public class OrderRepository
{
    private readonly JsonFileStore _store;

    public OrderRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task SaveAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            $"[OrderRepository] Opening connection to save order #{order.Id}.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            $"[OrderRepository] Beginning transaction for order #{order.Id}.");

        await Task.Delay(100, cancellationToken);

        AppLogger.Log(
            $"[OrderRepository] Inserting or updating order #{order.Id}.");

        await Task.Delay(400, cancellationToken);

        var orders = await _store.ReadAsync<List<Order>>(
            DataFiles.Orders,
            cancellationToken);

        var existingOrder = orders.FirstOrDefault(o => o.Id == order.Id);

        if (existingOrder is null)
        {
            orders.Add(order);
        }
        else
        {
            int index = orders.IndexOf(existingOrder);
            orders[index] = order;
        }

        await _store.WriteAsync(
            DataFiles.Orders,
            orders,
            cancellationToken);

        AppLogger.Log(
            $"[OrderRepository] Committing transaction for order #{order.Id}.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            $"[OrderRepository] Order #{order.Id} saved successfully.");
    }

    public async Task<List<Order>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            "[OrderRepository] Opening connection to get all orders.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            "[OrderRepository] Executing query for all orders.");

        int queryDelay = Random.Shared.Next(300, 601);

        await Task.Delay(queryDelay, cancellationToken);

        var orders = await _store.ReadAsync<List<Order>>(
            DataFiles.Orders,
            cancellationToken);

        AppLogger.Log(
            $"[OrderRepository] Retrieved {orders.Count} orders.");

        return orders;
    }

    public async Task<List<Order>> GetByStatusAsync(
        OrderStatus status,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            $"[OrderRepository] Opening connection to get orders with status {status}.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            $"[OrderRepository] Executing query for status {status}.");

        int queryDelay = Random.Shared.Next(300, 601);

        await Task.Delay(queryDelay, cancellationToken);

        var orders = await _store.ReadAsync<List<Order>>(
            DataFiles.Orders,
            cancellationToken);

        var filteredOrders = orders
            .Where(order => order.Status == status)
            .ToList();

        AppLogger.Log(
            $"[OrderRepository] Retrieved {filteredOrders.Count} orders with status {status}.");

        return filteredOrders;
    }
}
