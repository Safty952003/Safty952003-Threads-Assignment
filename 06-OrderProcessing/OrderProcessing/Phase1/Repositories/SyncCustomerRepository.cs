using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1.Repositories;

public class SyncCustomerRepository
{
    private readonly JsonFileStore _store;

    public SyncCustomerRepository(JsonFileStore store)
    {
        _store = store;
    }

    public Customer? GetById(int id)
    {
        AppLogger.Log(
            $"[SyncCustomerRepository] Opening connection to get customer #{id}.");

        Thread.Sleep(200);

        AppLogger.Log(
            $"[SyncCustomerRepository] Executing query for customer #{id}.");

        Thread.Sleep(Random.Shared.Next(300, 601));

        var customers = _store.Read<List<Customer>>(DataFiles.Customers);

        return customers.FirstOrDefault(c => c.Id == id);
    }
}