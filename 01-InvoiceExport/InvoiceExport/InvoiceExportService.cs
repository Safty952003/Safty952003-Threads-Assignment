
using System.Diagnostics;

namespace InvoiceExport;

public sealed class InvoiceExportService
{
    readonly OrderDatabase _db;
    readonly CustomerFileStore _customers;
    readonly InvoiceFileWriter _invoices;

    public InvoiceExportService(
        OrderDatabase db,
        CustomerFileStore customers,
        InvoiceFileWriter invoices)
    {
        _db = db;
        _customers = customers;
        _invoices = invoices;
    }

    public async Task ExportAllPendingAsync()
    {
        var watch = Stopwatch.StartNew();

        List<Order> orders = await _db.GetPendingOrdersAsync();

        // Start all order-export tasks first.
        List<Task> exportTasks = new();

        foreach (Order order in orders)
        {
            exportTasks.Add(ExportOrderAsync(order));
        }

        // Await each task individually.
        foreach (Task exportTask in exportTasks)
        {
            await exportTask;
        }

        Console.WriteLine(
            $"Export finished in {watch.Elapsed.TotalSeconds:F1} s");
    }

    private async Task ExportOrderAsync(Order order)
    {
        Order fresh = await _db.GetOrderByIdAsync(order.Id)
            ?? throw new InvalidOperationException(
                $"Order {order.Id} missing");

        CustomerProfile customer =
            await _customers.ReadProfileAsync(fresh.CustomerId);

        string path =
            await _invoices.WriteInvoiceAsync(fresh, customer);

        Console.WriteLine($"Wrote {path}");
    }
}
