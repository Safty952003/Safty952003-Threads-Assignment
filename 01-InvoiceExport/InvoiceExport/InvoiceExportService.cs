using System.Diagnostics;

namespace InvoiceExport;

public sealed class InvoiceExportService
{
    readonly OrderDatabase _db;
    readonly CustomerFileStore _customers;
    readonly InvoiceFileWriter _invoices;

    public InvoiceExportService(OrderDatabase db, CustomerFileStore customers, InvoiceFileWriter invoices)
    {
        _db = db;
        _customers = customers;
        _invoices = invoices;
    }


    public async Task ExportAllPendingAsync()
    {
        var watch = Stopwatch.StartNew();

        List<Order> orders = await _db.GetPendingOrdersAsync();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 8
        };

        await Parallel.ForEachAsync(
            orders,
            options,
            async (order, cancellationToken) =>
            {
                Order fresh = await _db.GetOrderByIdAsync(order.Id)
                    ?? throw new InvalidOperationException(
                        $"Order {order.Id} missing");

                CustomerProfile customer =
                    await _customers.ReadProfileAsync(fresh.CustomerId);

                string path =
                    await _invoices.WriteInvoiceAsync(fresh, customer);

                Console.WriteLine($"Wrote {path}");
            });

        Console.WriteLine(
            $"Export finished in {watch.Elapsed.TotalSeconds:F1} s");
    }
}
