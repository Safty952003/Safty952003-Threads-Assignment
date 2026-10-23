using System.Text;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class InvoiceGenerator
{
    public async Task GenerateAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                long result = 0;

                for (int i = 0; i < 20_000_000; i++)
                {
                    result += i % 10;
                }

                cancellationToken.ThrowIfCancellationRequested();
            },
            cancellationToken);

        string invoicesDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            DataFiles.InvoicesFolder);

        Directory.CreateDirectory(invoicesDirectory);

        string filePath = Path.Combine(
            invoicesDirectory,
            $"invoice_{order.Id}.txt");

        string content = $"""
            Invoice #{order.Id}
            Customer ID: {order.CustomerId}
            SubTotal: {order.SubTotal:F2}
            Discount: {order.Discount:F2}
            Shipping Cost: {order.ShippingCost:F2}
            Total Amount: {order.TotalAmount:F2}
            Status: {order.Status}
            """;

        await File.WriteAllTextAsync(
            filePath,
            content,
            Encoding.UTF8,
            cancellationToken);
    }
}