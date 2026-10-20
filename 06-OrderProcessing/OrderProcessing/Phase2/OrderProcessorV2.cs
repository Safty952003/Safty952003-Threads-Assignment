
using System.Collections.Concurrent;
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public class OrderProcessorV2
{
    private readonly ProductRepository _productRepository;
    private readonly OrderRepository _orderRepository;
    private readonly OrderPricer _orderPricer;
    private readonly InvoiceGenerator _invoiceGenerator;

    private readonly ConcurrentDictionary<int, List<OrderItem>>
        _reservedItemsByOrder = new();

    public OrderProcessorV2(
        ProductRepository productRepository,
        OrderRepository orderRepository,
        OrderPricer orderPricer,
        InvoiceGenerator invoiceGenerator)
    {
        _productRepository = productRepository;
        _orderRepository = orderRepository;
        _orderPricer = orderPricer;
        _invoiceGenerator = invoiceGenerator;
    }

    public async Task<bool> ProcessValidatedOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        var reservedItems = new List<OrderItem>();

        _reservedItemsByOrder[order.Id] = reservedItems;

        foreach (var item in order.Items)
        {
            bool reserved = await _productRepository.ReserveStockAsync(
                item.ProductId,
                item.Quantity,
                cancellationToken);

            if (!reserved)
            {
                await ReleaseReservedStockAsync(
                    order.Id,
                    cancellationToken);

                var product = await _productRepository.GetByIdAsync(
                    item.ProductId,
                    cancellationToken);

                string productName = product?.Name
                    ?? $"Product #{item.ProductId}";

                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Insufficient stock for '{productName}'";

                await _orderRepository.SaveAsync(
                    order,
                    cancellationToken);

                AppLogger.Error(
                    $"[OrderProcessorV2] Order #{order.Id} rejected: " +
                    order.FailureReason);

                return false;
            }

            reservedItems.Add(item);
        }

        await _orderPricer.PriceOrderAsync(
            order,
            cancellationToken);

        await _invoiceGenerator.GenerateAsync(
            order,
            cancellationToken);

        order.Status = OrderStatus.Completed;
        order.FailureReason = null;

        await _orderRepository.SaveAsync(
            order,
            cancellationToken);

        _reservedItemsByOrder.TryRemove(order.Id, out _);

        AppLogger.Success(
            $"[OrderProcessorV2] Order #{order.Id} completed successfully.");

        return true;
    }

    public async Task ReleaseReservedStockAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        if (!_reservedItemsByOrder.TryRemove(
                orderId,
                out var reservedItems))
        {
            return;
        }

        foreach (var item in reservedItems)
        {
            await _productRepository.ReleaseStockAsync(
                item.ProductId,
                item.Quantity,
                cancellationToken);
        }

        AppLogger.Log(
            $"[OrderProcessorV2] Released reserved stock for order #{orderId}.");
    }
}
