
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3.Services;

public class InventoryService
{
    private readonly ProductRepository _productRepository;
    public string? FailedProductName { get; private set; }

    public InventoryService(ProductRepository productRepository)
    {
        _productRepository = productRepository;
    }


    public async Task<bool> ReserveStockAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        FailedProductName = null;

        var reservedItems = new List<OrderItem>();

        try
        {
            foreach (var item in order.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool reserved = await _productRepository.ReserveStockAsync(
                    item.ProductId,
                    item.Quantity,
                    cancellationToken);

                if (!reserved)
                {
                    var failedProduct = await _productRepository.GetByIdAsync(
                        item.ProductId,
                        cancellationToken);

                    FailedProductName = failedProduct?.Name
                        ?? $"Product #{item.ProductId}";

                    foreach (var reservedItem in reservedItems)
                    {
                        await _productRepository.ReleaseStockAsync(
                            reservedItem.ProductId,
                            reservedItem.Quantity,
                            CancellationToken.None);
                    }

                    return false;
                }

                reservedItems.Add(item);
            }

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            foreach (var reservedItem in reservedItems)
            {
                await _productRepository.ReleaseStockAsync(
                    reservedItem.ProductId,
                    reservedItem.Quantity,
                    CancellationToken.None);
            }

            throw;
        }
    }

    public async Task ReleaseStockAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in order.Items)
        {
            await _productRepository.ReleaseStockAsync(
                item.ProductId,
                item.Quantity,
                cancellationToken);
        }
    }
}
