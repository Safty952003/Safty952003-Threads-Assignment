
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
}
