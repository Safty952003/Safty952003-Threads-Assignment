
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1.Repositories;

public class SyncProductRepository
{
    private readonly JsonFileStore _store;

    public SyncProductRepository(JsonFileStore store)
    {
        _store = store;
    }

    public Product? GetById(int id)
    {
        AppLogger.Log(
            $"[SyncProductRepository] Opening connection to get product #{id}.");

        Thread.Sleep(200);

        AppLogger.Log(
            $"[SyncProductRepository] Executing query for product #{id}.");

        Thread.Sleep(Random.Shared.Next(300, 601));

        var products = _store.Read<List<Product>>(DataFiles.Products);

        return products.FirstOrDefault(p => p.Id == id);
    }

    private static readonly SemaphoreSlim StockLock = new(1, 1);

    public bool ReserveStock(int productId, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        StockLock.Wait();

        try
        {
            AppLogger.Log(
                $"[SyncProductRepository] Reserving stock for product #{productId}.");

            Thread.Sleep(200);
            Thread.Sleep(100);

            var products = _store.Read<List<Product>>(DataFiles.Products);

            var product = products.FirstOrDefault(p => p.Id == productId);

            if (product is null || product.StockQuantity < quantity)
            {
                AppLogger.Log(
                    $"[SyncProductRepository] Reservation failed for product #{productId}.");

                return false;
            }

            Thread.Sleep(400);

            product.StockQuantity -= quantity;

            _store.Write(DataFiles.Products, products);

            AppLogger.Log(
                $"[SyncProductRepository] Reserved {quantity} unit(s) of product #{productId}.");

            return true;
        }
        finally
        {
            StockLock.Release();
        }
    }

    public void ReleaseStock(int productId, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        StockLock.Wait();

        try
        {
            var products = _store.Read<List<Product>>(DataFiles.Products);

            var product = products.FirstOrDefault(p => p.Id == productId);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{productId} was not found.");
            }

            Thread.Sleep(200);

            product.StockQuantity += quantity;

            _store.Write(DataFiles.Products, products);

            AppLogger.Log(
                $"[SyncProductRepository] Released {quantity} unit(s) of product #{productId}.");
        }
        finally
        {
            StockLock.Release();
        }
    }

}
