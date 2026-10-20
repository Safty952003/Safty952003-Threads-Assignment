
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1.Repositories;

public class ProductRepository
{
    private readonly JsonFileStore _store;

    public ProductRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Product?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            $"[ProductRepository] Opening connection to get product #{id}.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            $"[ProductRepository] Executing query for product #{id}.");

        int queryDelay = Random.Shared.Next(300, 601);

        await Task.Delay(queryDelay, cancellationToken);

        var products = await _store.ReadAsync<List<Product>>(
            DataFiles.Products,
            cancellationToken);

        var product = products.FirstOrDefault(p => p.Id == id);

        AppLogger.Log(
            product is null
                ? $"[ProductRepository] Product #{id} not found."
                : $"[ProductRepository] Product #{id} retrieved successfully.");

        return product;
    }

    private static readonly SemaphoreSlim StockLock = new(1, 1);

    public async Task<bool> ReserveStockAsync(
        int productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        await StockLock.WaitAsync(cancellationToken);

        try
        {
            AppLogger.Log(
                $"[ProductRepository] Opening connection to reserve stock for product #{productId}.");

            await Task.Delay(200, cancellationToken);

            AppLogger.Log(
                $"[ProductRepository] Beginning stock reservation transaction for product #{productId}.");

            await Task.Delay(100, cancellationToken);

            AppLogger.Log(
                $"[ProductRepository] Updating stock for product #{productId}.");

            await Task.Delay(400, cancellationToken);

            var products = await _store.ReadAsync<List<Product>>(
                DataFiles.Products,
                cancellationToken);

            var product = products.FirstOrDefault(p => p.Id == productId);

            if (product is null || product.StockQuantity < quantity)
            {
                AppLogger.Log(
                    $"[ProductRepository] Stock reservation failed for product #{productId}.");

                return false;
            }

            product.StockQuantity -= quantity;

            await _store.WriteAsync(
                DataFiles.Products,
                products,
                cancellationToken);

            AppLogger.Log(
                $"[ProductRepository] Committing stock reservation for product #{productId}.");

            await Task.Delay(200, cancellationToken);

            AppLogger.Log(
                $"[ProductRepository] Reserved {quantity} unit(s) of product #{productId}.");

            return true;
        }
        finally
        {
            StockLock.Release();
        }
    }

    public async Task<bool> ReserveStockUnsafeAsync(
    int productId,
    int quantity,
    CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        AppLogger.Log(
            $"[ProductRepository] Opening connection for unsafe reservation of product #{productId}.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            $"[ProductRepository] Beginning transaction for product #{productId}.");

        await Task.Delay(100, cancellationToken);

        var products = await _store.ReadAsync<List<Product>>(
            DataFiles.Products,
            cancellationToken);

        var product = products.FirstOrDefault(p => p.Id == productId);

        if (product is null || product.StockQuantity < quantity)
        {
            return false;
        }

        await Task.Delay(400, cancellationToken);

        product.StockQuantity -= quantity;

        await _store.WriteAsync(
            DataFiles.Products,
            products,
            cancellationToken);

        AppLogger.Log(
            $"[ProductRepository] Unsafe reservation completed for product #{productId}.");

        return true;
    }

    public async Task ReleaseStockAsync(
    int productId,
    int quantity,
    CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        await StockLock.WaitAsync(cancellationToken);

        try
        {
            AppLogger.Log(
                $"[ProductRepository] Releasing {quantity} unit(s) of product #{productId}.");

            await Task.Delay(200, cancellationToken);

            var products = await _store.ReadAsync<List<Product>>(
                DataFiles.Products,
                cancellationToken);

            var product = products.FirstOrDefault(
                p => p.Id == productId);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{productId} was not found.");
            }

            product.StockQuantity += quantity;

            await _store.WriteAsync(
                DataFiles.Products,
                products,
                cancellationToken);

            AppLogger.Success(
                $"[ProductRepository] Released {quantity} unit(s) of product #{productId}.");
        }
        finally
        {
            StockLock.Release();
        }
    }

    public async Task<List<Product>> GetAllAsync(
    CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            "[ProductRepository] Opening connection to get all products.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            "[ProductRepository] Executing query for all products.");

        await Task.Delay(
            Random.Shared.Next(300, 601),
            cancellationToken);

        var products = await _store.ReadAsync<List<Product>>(
            DataFiles.Products,
            cancellationToken);

        AppLogger.Log(
            $"[ProductRepository] Retrieved {products.Count} products.");

        return products;
    }

}
