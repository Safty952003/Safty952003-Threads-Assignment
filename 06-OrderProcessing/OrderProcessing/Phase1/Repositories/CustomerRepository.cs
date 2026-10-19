
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1.Repositories;

public class CustomerRepository
{
    private readonly JsonFileStore _store;

    public CustomerRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Customer?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            $"[CustomerRepository] Opening connection to get customer #{id}.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            $"[CustomerRepository] Executing query for customer #{id}.");

        int queryDelay = Random.Shared.Next(300, 601);

        await Task.Delay(queryDelay, cancellationToken);

        var customers = await _store.ReadAsync<List<Customer>>(
            DataFiles.Customers,
            cancellationToken);

        var customer = customers.FirstOrDefault(c => c.Id == id);

        AppLogger.Log(
            customer is null
                ? $"[CustomerRepository] Customer #{id} not found."
                : $"[CustomerRepository] Customer #{id} retrieved successfully.");

        return customer;
    }
}
