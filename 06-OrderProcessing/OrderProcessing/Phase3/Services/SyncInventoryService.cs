
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

public class SyncInventoryService
{
    private readonly SyncProductRepository _productRepository;

    public string? FailedProductName { get; private set; }

    public SyncInventoryService(
        SyncProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public bool ReserveStock(Order order)
    {
        FailedProductName = null;

        var reservedItems = new List<OrderItem>();

        foreach (var item in order.Items)
        {
            bool reserved = _productRepository.ReserveStock(
                item.ProductId,
                item.Quantity);

            if (!reserved)
            {
                var product = _productRepository.GetById(
                    item.ProductId);

                FailedProductName = product?.Name
                    ?? $"Product #{item.ProductId}";

                foreach (var reservedItem in reservedItems)
                {
                    _productRepository.ReleaseStock(
                        reservedItem.ProductId,
                        reservedItem.Quantity);
                }

                return false;
            }

            reservedItems.Add(item);
        }

        return true;
    }

    public void ReleaseStock(Order order)
    {
        foreach (var item in order.Items)
        {
            _productRepository.ReleaseStock(
                item.ProductId,
                item.Quantity);
        }
    }
}
