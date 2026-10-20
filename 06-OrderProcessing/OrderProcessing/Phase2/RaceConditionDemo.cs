using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class RaceConditionDemo
{
    private readonly JsonFileStore _store;
    private readonly ProductRepository _productRepository;
    private readonly DataSeeder _dataSeeder;

    public RaceConditionDemo(
        JsonFileStore store,
        ProductRepository productRepository,
        DataSeeder dataSeeder)
    {
        _store = store;
        _productRepository = productRepository;
        _dataSeeder = dataSeeder;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        await _dataSeeder.ResetAsync(cancellationToken);

        var products = await _store.ReadAsync<List<Product>>(
            DataFiles.Products,
            cancellationToken);

        var keyboard = products.First(p => p.Id == 4);
        keyboard.StockQuantity = 1;

        await _store.WriteAsync(
            DataFiles.Products,
            products,
            cancellationToken);

        AppLogger.Log("Starting unsafe stock reservation demo...");

        var order10Task = _productRepository.ReserveStockUnsafeAsync(
            4, 1, cancellationToken);

        var order11Task = _productRepository.ReserveStockUnsafeAsync(
            4, 1, cancellationToken);

        var results = await Task.WhenAll(order10Task, order11Task);

        products = await _store.ReadAsync<List<Product>>(
            DataFiles.Products,
            cancellationToken);

        keyboard = products.First(p => p.Id == 4);

        Console.WriteLine("\n===== Unsafe Reservation Results =====");
        Console.WriteLine($"Order #10 reserved keyboard: {results[0]}");
        Console.WriteLine($"Order #11 reserved keyboard: {results[1]}");
        Console.WriteLine($"Keyboard stock remaining: {keyboard.StockQuantity}");

        products = await _store.ReadAsync<List<Product>>(
            DataFiles.Products,
            cancellationToken);

        keyboard = products.First(p => p.Id == 4);
        keyboard.StockQuantity = 1;

        await _store.WriteAsync(
            DataFiles.Products,
            products,
            cancellationToken);

        AppLogger.Log("Starting safe stock reservation demo...");

        var safeOrder10Task = _productRepository.ReserveStockAsync(
            4, 1, cancellationToken);

        var safeOrder11Task = _productRepository.ReserveStockAsync(
            4, 1, cancellationToken);

        var safeResults = await Task.WhenAll(
            safeOrder10Task,
            safeOrder11Task);

        products = await _store.ReadAsync<List<Product>>(
            DataFiles.Products,
            cancellationToken);

        keyboard = products.First(p => p.Id == 4);

        Console.WriteLine("\n===== Safe Reservation Results =====");
        Console.WriteLine($"Order #10 reserved keyboard: {safeResults[0]}");
        Console.WriteLine($"Order #11 reserved keyboard: {safeResults[1]}");
        Console.WriteLine($"Keyboard stock remaining: {keyboard.StockQuantity}");
    }
}