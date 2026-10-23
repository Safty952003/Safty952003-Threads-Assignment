

using OrderProcessing.Phase1;
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Phase3.Email;
using OrderProcessing.Phase3.Payment;
using OrderProcessing.Phase3.Services;
using OrderProcessing.Phase4;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

internal class Program
{
    static async Task Main(string[] args)
    {

        using var cancellationTokenSource = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();

            Console.WriteLine();
            Console.WriteLine("Cancellation requested. Stopping the run...");
        };

        try
        {

            var store = new JsonFileStore();
            var seeder = new DataSeeder(store);

            var customerRepository = new CustomerRepository(store);
            var productRepository = new ProductRepository(store);
            var couponRepository = new CouponRepository(store);
            var orderRepository = new OrderRepository(store);
            var incomingOrderRepository = new IncomingOrderRepository(store);

            var syncCustomerRepository = new SyncCustomerRepository(store);
            var syncProductRepository = new SyncProductRepository(store);
            var syncCouponRepository = new SyncCouponRepository(store);

            var syncValidator = new SyncOrderValidator(
                syncCustomerRepository,
                syncProductRepository,
                syncCouponRepository);

            var asyncValidator = new OrderValidator(
                customerRepository,
                productRepository,
                couponRepository);

            var phase1Runner = new Phase1Runner(
                seeder,
                incomingOrderRepository,
                orderRepository,
                syncValidator,
                asyncValidator);

            var pricingService = new PricingService(
                store,
                customerRepository,
                couponRepository,
                new[] { 7 });

            var orderPricer = new OrderPricer(pricingService);

            var raceConditionDemo = new RaceConditionDemo(
                store,
                productRepository,
                seeder);

            var invoiceGenerator = new InvoiceGenerator();

            var orderProcessor = new OrderProcessorV2(
                productRepository,
                orderRepository,
                orderPricer,
                invoiceGenerator);

            var reportGenerator = new ReportGenerator(
                orderRepository,
                productRepository);

            var phase2Runner = new Phase2Runner(
                seeder,
                incomingOrderRepository,
                orderRepository,
                asyncValidator,
                productRepository,
                pricingService,
                orderPricer,
                raceConditionDemo,
                orderProcessor,
                reportGenerator);


            var phase3OrderRepository = new OrderRepository(
                store,
                failSaveForOrderIds: new[] { 13 });

            var phase3CustomerService = new CustomerService(
                customerRepository);

            var phase3InventoryService = new InventoryService(
                productRepository);

            var phase3PricingService = new PricingService(
                store,
                customerRepository,
                couponRepository,
                Array.Empty<int>());

            IPaymentGateway paymentGateway = new PaymentGateway(
                store,
                timeoutOrderIds: new[] { 5 },
                declineOrderIds: new[] { 8 });

            IEmailService emailService = new EmailService(
                store,
                failAttempts: new Dictionary<int, int>
                {
                    [3] = 2,
                    [17] = int.MaxValue
                });

            var phase3OrderProcessor = new OrderProcessing.Phase3.OrderProcessor(
                store,
                phase3CustomerService,
                phase3InventoryService,
                phase3PricingService,
                paymentGateway,
                phase3OrderRepository,
                emailService);

            var phase3Runner = new OrderProcessing.Phase3.Phase3Runner(
      seeder,
      incomingOrderRepository,
      phase3OrderProcessor,
      orderRepository);

            var customerDashboardService = new CustomerDashboardService(
    store,
    customerRepository,
    phase3OrderRepository,
    failPaymentsForCustomerIds: new[] { 99 });
            var shippingQuoteService = new ShippingQuoteService(
    new IShippingCarrier[]
    {
        new FastShip(failForOrderIds: new[] { 6 }),
        new CairoExpress(failForOrderIds: new[] { 6 }),
        new NileCourier(failForOrderIds: new[] { 6 })
    });
            var fraudScoringService = new FraudScoringService();
            var emailResendService = new EmailResendService(
    new EmailService(
        store,
        enableFailureScenarios: false,
        sendDelay: TimeSpan.FromSeconds(1)),
    store);

            var phase4Runner = new Phase4Runner(
    seeder,
    incomingOrderRepository,
    phase3OrderProcessor,
    phase3OrderRepository,
    customerDashboardService,
    shippingQuoteService,
    fraudScoringService,
    emailResendService);


            string mode = "";
            int? cancelAfterMs = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(
                    "--cancel-after",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length ||
                        !int.TryParse(args[i + 1], out int milliseconds) ||
                        milliseconds <= 0)
                    {
                        Console.WriteLine(
                            "Invalid value for --cancel-after. " +
                            "Provide a positive number of milliseconds.");

                        return;
                    }

                    cancelAfterMs = milliseconds;
                    i++;
                }
                else if (mode.Length == 0)
                {
                    mode = args[i].Trim().ToLowerInvariant();
                }
                else
                {
                    Console.WriteLine($"Unknown argument: {args[i]}");
                    return;
                }
            }

            if (cancelAfterMs.HasValue)
            {
                cancellationTokenSource.CancelAfter(cancelAfterMs.Value);
            }

            if (mode.Length == 0)
            {
                Console.WriteLine("===== Order Processing =====");
                Console.WriteLine("1. Phase 1");
                Console.WriteLine("2. Phase 2");
                Console.WriteLine("3. Phase 3");
                Console.WriteLine("4. Phase 4");
                Console.WriteLine("compare. Compare Sync vs Async");
                Console.WriteLine("fire-and-forget");


                Console.WriteLine("all. Run all phases");

                Console.Write("Choose phase: ");

                mode = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "";
            }


            switch (mode)
            {
                case "1":
                case "phase1":
                    await phase1Runner.RunAsync(
                        cancellationTokenSource.Token);
                    break;

                case "2":
                case "phase2":
                    await phase2Runner.RunAsync(
                        cancellationTokenSource.Token);
                    break;
                case "3":
                case "phase3":
                    await phase3Runner.RunAsync(
                        cancellationTokenSource.Token);
                    break;

                case "4":
                case "phase4":
                    {
                        var ct = cancellationTokenSource.Token;

                        await phase4Runner.RunLiveCheckoutProgressAsync(ct);

                        await phase4Runner.RunCustomerDashboardAsync(3, ct);

                        await phase4Runner.RunCustomerDashboardAsync(99, ct);

                        Console.WriteLine(
                            "===== Reached Task 10.3 =====");

                        await phase4Runner.RunShippingQuotesAsync(ct);
                        phase4Runner.RunFraudScoring(cancellationTokenSource.Token);
                        await phase4Runner.RunEmailResendAsync(
    cancellationTokenSource.Token);
                        break;
                    }
                case "fire-and-forget":
                    {
                        var order = new Order
                        {
                            Id = 9001,
                            CustomerId = 1,
                            TotalAmount = 100,
                            CreatedAt = DateTime.UtcNow
                        };

                        var customer = new Customer
                        {
                            Id = 1,
                            Name = "Test Customer",
                            Email = "test@example.com",
                            IsActive = true
                        };

                        await phase3OrderProcessor.ProcessOrderAsync(order, customer);
                        break;
                    }
                case "compare":
                    await RunComparisonAsync(
                        store,
                        cancellationTokenSource.Token);
                    break;

                case "all":
                    await phase1Runner.RunAsync(
                        cancellationTokenSource.Token);

                    await phase2Runner.RunAsync(
                        cancellationTokenSource.Token);
                    break;
               

                default:
                    Console.WriteLine(
    "Invalid option. Use phase1, phase2, phase3,phase4, compare, or all.");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            AppLogger.Log("Operation cancelled.");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Application error: {ex}");
        }
    }

    private static void PrepareComparisonData(
        JsonFileStore sourceStore,
        string targetDirectory)
    {
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }

        Directory.CreateDirectory(targetDirectory);

        string[] inputFiles =
        {
        DataFiles.Customers,
        DataFiles.Products,
        DataFiles.Coupons,
        DataFiles.IncomingOrders
    };

        foreach (string fileName in inputFiles)
        {
            string sourcePath = Path.Combine(
                sourceStore.DataDirectory,
                fileName);

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    $"Required comparison file was not found: {sourcePath}");
            }

            File.Copy(
                sourcePath,
                Path.Combine(targetDirectory, fileName));
        }

        string[] resultFiles =
        {
        DataFiles.Orders,
        DataFiles.Payments,
        DataFiles.Emails,
        DataFiles.EmailRetry
    };

        foreach (string fileName in resultFiles)
        {
            File.WriteAllText(
                Path.Combine(targetDirectory, fileName),
                "[]");
        }

        Directory.CreateDirectory(
            Path.Combine(targetDirectory, DataFiles.InvoicesFolder));

        Directory.CreateDirectory(
            Path.Combine(targetDirectory, DataFiles.ReportsFolder));
    }

    private static (string SyncPath, string AsyncPath) PrepareComparisonDirectories(
        JsonFileStore sourceStore)
    {
        string comparisonRoot = Path.Combine(
            AppContext.BaseDirectory,
            "CompareData");

        string syncPath = Path.Combine(comparisonRoot, "Sync");
        string asyncPath = Path.Combine(comparisonRoot, "Async");

        PrepareComparisonData(sourceStore, syncPath);
        PrepareComparisonData(sourceStore, asyncPath);

        return (syncPath, asyncPath);
    }

    private static async Task RunSyncComparisonAsync(
        string dataDirectory,
        CancellationToken cancellationToken)
    {
        var store = new JsonFileStore(dataDirectory);
        var seeder = new DataSeeder(store);

        var customerRepository = new SyncCustomerRepository(store);
        var productRepository = new SyncProductRepository(store);
        var couponRepository = new SyncCouponRepository(store);
        var orderRepository = new SyncOrderRepository(
            store,
            enableFailureScenarios: false);

        var inventoryService = new SyncInventoryService(
            productRepository);

        var pricingService = new SyncPricingService(
    store,
    customerRepository,
    couponRepository);

        var paymentGateway = new SyncPaymentGateway(
            store,
            enableFailureScenarios: false);

        var emailService = new SyncEmailService(
            store,
            enableFailureScenarios: false);

        var processor = new SyncOrderProcessor(
            store,
            customerRepository,
            inventoryService,
            pricingService,
            paymentGateway,
            orderRepository,
            emailService);

        var runner = new SyncPhase3Runner(
            seeder,
            store,
            processor,
            orderRepository);

        await runner.RunAsync(cancellationToken);
    }

    private static async Task RunAsyncComparisonPipelineAsync(
        string dataDirectory,
        CancellationToken cancellationToken)
    {
        var store = new JsonFileStore(dataDirectory);
        var seeder = new DataSeeder(store);

        var customerRepository = new CustomerRepository(store);
        var productRepository = new ProductRepository(store);
        var couponRepository = new CouponRepository(store);
        var incomingOrderRepository = new IncomingOrderRepository(store);

        var orderRepository = new OrderRepository(
            store,
            enableFailureScenarios: false);

        var customerService = new CustomerService(customerRepository);
        var inventoryService = new InventoryService(productRepository);

        var pricingService = new PricingService(
            store,
            customerRepository,
            couponRepository,
            Array.Empty<int>(),
            enableFailureScenarios: false);

        IPaymentGateway paymentGateway = new PaymentGateway(
            store,
            enableFailureScenarios: false);

        IEmailService emailService = new EmailService(
            store,
            enableFailureScenarios: false);

        var processor = new OrderProcessing.Phase3.OrderProcessor(
            store,
            customerService,
            inventoryService,
            pricingService,
            paymentGateway,
            orderRepository,
            emailService);

        var runner = new OrderProcessing.Phase3.Phase3Runner(
            seeder,
            incomingOrderRepository,
            processor,
            orderRepository);

        await runner.RunAsync(cancellationToken, maxOrders: 10);
    }

    private static async Task RunComparisonAsync(
        JsonFileStore sourceStore,
        CancellationToken cancellationToken)
    {
        var directories = PrepareComparisonDirectories(sourceStore);

        var syncTimer = System.Diagnostics.Stopwatch.StartNew();

        await RunSyncComparisonAsync(
            directories.SyncPath,
            cancellationToken);

        syncTimer.Stop();

        cancellationToken.ThrowIfCancellationRequested();

        var asyncTimer = System.Diagnostics.Stopwatch.StartNew();

        await RunAsyncComparisonPipelineAsync(
            directories.AsyncPath,
            cancellationToken);

        asyncTimer.Stop();

        Console.WriteLine();
        Console.WriteLine("===== Sync vs Async Comparison =====");
        Console.WriteLine($"Sync elapsed:  {syncTimer.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Async elapsed: {asyncTimer.Elapsed.TotalSeconds:F2} seconds");

        if (syncTimer.Elapsed < asyncTimer.Elapsed)
        {
            Console.WriteLine("Sync was faster in this run.");
        }
        else if (asyncTimer.Elapsed < syncTimer.Elapsed)
        {
            Console.WriteLine("Async was faster in this run.");
        }
        else
        {
            Console.WriteLine("Both pipelines took the same time.");
        }
    }

   

}
