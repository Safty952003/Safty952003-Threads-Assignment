
using OrderProcessing.Phase1;
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System.Diagnostics;

namespace OrderProcessing.Phase2;

public class Phase2Runner
{
    private readonly DataSeeder _dataSeeder;
    private readonly IncomingOrderRepository _incomingOrderRepository;
    private readonly OrderRepository _orderRepository;
    private readonly OrderValidator _orderValidator;
    private readonly ProductRepository _productRepository;
    private readonly PricingService _pricingService;
    private readonly OrderPricer _orderPricer;
    private readonly RaceConditionDemo _raceConditionDemo;
    private readonly OrderProcessorV2 _orderProcessor;
    private readonly ReportGenerator _reportGenerator;

    public Phase2Runner(
        DataSeeder dataSeeder,
        IncomingOrderRepository incomingOrderRepository,
        OrderRepository orderRepository,
        OrderValidator orderValidator,
        ProductRepository productRepository,
        PricingService pricingService,
        OrderPricer orderPricer,
        RaceConditionDemo raceConditionDemo,
        OrderProcessorV2 orderProcessor,
        ReportGenerator reportGenerator)
    {
        _dataSeeder = dataSeeder;
        _incomingOrderRepository = incomingOrderRepository;
        _orderRepository = orderRepository;
        _orderValidator = orderValidator;
        _productRepository = productRepository;
        _pricingService = pricingService;
        _orderPricer = orderPricer;
        _raceConditionDemo = raceConditionDemo;
        _orderProcessor = orderProcessor;
        _reportGenerator = reportGenerator;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n========== PHASE 2 ==========\n");

        await _dataSeeder.ResetAsync(cancellationToken);

        await _raceConditionDemo.RunAsync(cancellationToken);

        await _dataSeeder.ResetAsync(cancellationToken);

        var incomingOrders = await _incomingOrderRepository.GetAllAsync(
            cancellationToken);

        var originalOrder = incomingOrders.First(
            order => order.Id == 1);

        var parallelOrder = CloneOrder(originalOrder);
        var sequentialOrder = CloneOrder(originalOrder);

        var parallelStopwatch = Stopwatch.StartNew();

        await _orderPricer.PriceOrderAsync(
            parallelOrder,
            cancellationToken);

        parallelStopwatch.Stop();

        var sequentialStopwatch = Stopwatch.StartNew();

        await _orderPricer.PriceOrderSequentialAsync(
            sequentialOrder,
            cancellationToken);

        sequentialStopwatch.Stop();

        Console.WriteLine("\n===== Pricing Comparison =====");

        Console.WriteLine(
            $"Concurrent pricing: {parallelStopwatch.Elapsed.TotalSeconds:F2} seconds");

        Console.WriteLine(
            $"Sequential pricing: {sequentialStopwatch.Elapsed.TotalSeconds:F2} seconds");

        Console.WriteLine(
            $"Order #1 SubTotal: {parallelOrder.SubTotal:F2}");

        Console.WriteLine(
            $"Order #1 Discount: {parallelOrder.Discount:F2}");

        Console.WriteLine(
            $"Order #1 Shipping: {parallelOrder.ShippingCost:F2}");

        Console.WriteLine(
            $"Order #1 Total: {parallelOrder.TotalAmount:F2}");

        Console.WriteLine("\n===== Concurrent Validation =====");

        var validationTasks = incomingOrders
            .Select(async order =>
            {
                await _orderValidator.ValidateOrderAsync(
                    order,
                    cancellationToken);

                return order;
            })
            .ToList();

        foreach (var validationTask in validationTasks)
        {
            var order = await validationTask;

            await _orderRepository.SaveAsync(
                order,
                cancellationToken);
        }

        var validatedOrders = incomingOrders
            .Where(order => order.Status == OrderStatus.Validated)
            .ToList();

        Console.WriteLine(
            $"Validated orders ready for processing: {validatedOrders.Count}");

        Console.WriteLine("\n===== Concurrent Order Processing =====");

        // Measure only the concurrent order-processing stage.
        int threadPoolBefore = ThreadPool.ThreadCount;

        var processingStopwatch = Stopwatch.StartNew();

        var processingTasks = validatedOrders
            .Select(order => (
                Order: order,
                Task: _orderProcessor.ProcessValidatedOrderAsync(
                    order,
                    cancellationToken)))
            .ToList();

        if (processingTasks.Count > 0)
        {
            Console.WriteLine(
                $"First task status immediately after creation: " +
                $"{processingTasks[0].Task.Status}");
        }

        await Task.Delay(1000, cancellationToken);

        Console.WriteLine("\nTask statuses after 1 second:");

        foreach (var entry in processingTasks)
        {
            Console.WriteLine(
                $"Order #{entry.Order.Id}: {entry.Task.Status}");
        }

        foreach (var entry in processingTasks)
        {
            try
            {
                await entry.Task;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var order = entry.Order;

                AppLogger.Error(
                    $"Order #{order.Id} failed: {ex.Message}");

                await _orderProcessor.ReleaseReservedStockAsync(
                    order.Id,
                    cancellationToken);

                order.Status = OrderStatus.Failed;
                order.FailureReason = ex.Message;

                await _orderRepository.SaveAsync(
                    order,
                    cancellationToken);
            }
        }

        processingStopwatch.Stop();

        int threadPoolAfter = ThreadPool.ThreadCount;

        Console.WriteLine("\n===== Processing Performance =====");

        Console.WriteLine(
            $"Order processing time: {processingStopwatch.Elapsed.TotalSeconds:F2} seconds");

        Console.WriteLine(
            $"ThreadPool.ThreadCount: {threadPoolBefore} -> {threadPoolAfter}");

        Console.WriteLine("\nFinal task statuses:");

        foreach (var entry in processingTasks)
        {
            Console.WriteLine(
                $"Order #{entry.Order.Id}: {entry.Task.Status}");
        }

        var savedOrders = await _orderRepository.GetAllAsync(
            cancellationToken);

        Console.WriteLine(
            $"\nTotal records in orders.json: {savedOrders.Count}");

        var report = await _reportGenerator.GenerateAsync(
            cancellationToken);

        Console.WriteLine("\n===== Phase 2 Report =====");

        Console.WriteLine($"Total orders: {report.TotalOrders}");
        Console.WriteLine($"Completed: {report.CompletedCount}");
        Console.WriteLine($"Rejected: {report.RejectedCount}");
        Console.WriteLine($"Failed: {report.FailedCount}");
        Console.WriteLine($"Payment failed: {report.PaymentFailedCount}");
        Console.WriteLine($"Total revenue: {report.TotalRevenue:F2}");
        Console.WriteLine($"Average order value: {report.AverageOrderValue:F2}");

        Console.WriteLine(
            $"Top-selling product: {report.TopSellingProduct ?? "None"}");

        Console.WriteLine(
            $"Top-selling quantity: {report.TopSellingQuantity}");

        Console.WriteLine("\n========== PHASE 2 COMPLETE ==========\n");
    }

    private static Order CloneOrder(Order source)
    {
        return new Order
        {
            Id = source.Id,
            CustomerId = source.CustomerId,

            Items = source.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList(),

            CouponCode = source.CouponCode,
            SubTotal = source.SubTotal,
            Discount = source.Discount,
            ShippingCost = source.ShippingCost,
            TotalAmount = source.TotalAmount,
            Status = source.Status,
            CreatedAt = source.CreatedAt,
            FailureReason = source.FailureReason,
            TransactionId = source.TransactionId
        };
    }
}