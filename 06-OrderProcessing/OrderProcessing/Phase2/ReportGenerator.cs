using System.Text.Json;
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class ReportGenerator
{
    private readonly OrderRepository _orderRepository;
    private readonly ProductRepository _productRepository;

    public ReportGenerator(
        OrderRepository orderRepository,
        ProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    public async Task<Report> GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log("[ReportGenerator] Loading orders and products.");

        var orders = await _orderRepository.GetAllAsync(
            cancellationToken);

        var products = await _productRepository.GetAllAsync(
            cancellationToken);

        var completedOrders = orders
            .Where(o => o.Status == OrderStatus.Completed)
            .ToList();

        decimal totalRevenue = completedOrders.Sum(
            o => o.TotalAmount);

        int completedCount = completedOrders.Count;

        var productQuantities = completedOrders
            .SelectMany(o => o.Items)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .OrderByDescending(item => item.Quantity)
            .ThenBy(item => item.ProductId)
            .FirstOrDefault();

        var topProduct = productQuantities is null
            ? null
            : products.FirstOrDefault(
                p => p.Id == productQuantities.ProductId);

        var report = new Report
        {
            TotalOrders = orders.Count,

            CompletedCount = orders.Count(
                o => o.Status == OrderStatus.Completed),

            RejectedCount = orders.Count(
                o => o.Status == OrderStatus.Rejected),

            FailedCount = orders.Count(
                o => o.Status == OrderStatus.Failed),

            PaymentFailedCount = orders.Count(
                o => o.Status == OrderStatus.PaymentFailed),

            TotalRevenue = Math.Round(
                totalRevenue,
                2,
                MidpointRounding.AwayFromZero),

            AverageOrderValue = completedCount == 0
                ? 0m
                : Math.Round(
                    totalRevenue / completedCount,
                    2,
                    MidpointRounding.AwayFromZero),

            TopSellingProduct = topProduct?.Name,

            TopSellingQuantity = productQuantities?.Quantity ?? 0,

            GeneratedAt = DateTime.Now
        };

        string reportsDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            DataFiles.ReportsFolder);

        Directory.CreateDirectory(reportsDirectory);

        string fileName =
            $"report_{report.GeneratedAt:yyyyMMdd_HHmmss}.json";

        string filePath = Path.Combine(
            reportsDirectory,
            fileName);

        string json = JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        await File.WriteAllTextAsync(
            filePath,
            json,
            cancellationToken);

        AppLogger.Success(
            $"[ReportGenerator] Report saved to {fileName}.");

        return report;
    }
}