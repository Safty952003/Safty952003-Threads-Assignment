using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Shared.Storage

{
    public class DataSeeder
    {
        private readonly JsonFileStore _store;

        public DataSeeder(JsonFileStore store)
        {
            _store = store;
        }

        private static List<Customer> CreateCustomers()
        {
            return new List<Customer>
    {
        new() { Id = 1, Name = "Ahmed Ali", Email = "ahmed.ali@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
        new() { Id = 2, Name = "Sara Mohamed", Email = "sara.mohamed@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver },
        new() { Id = 3, Name = "Omar Hassan", Email = "omar.hassan@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
        new() { Id = 4, Name = "Mona Ibrahim", Email = "mona.ibrahim@example.com", IsActive = false, LoyaltyTier = LoyaltyTier.Silver },
        new() { Id = 5, Name = "Youssef Adel", Email = "youssef.adel@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
        new() { Id = 6, Name = "Nour Khaled", Email = "nour.khaled@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
        new() { Id = 7, Name = "Karim Samir", Email = "karim.samir@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver },
        new() { Id = 8, Name = "Laila Mahmoud", Email = "laila.mahmoud@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
        new() { Id = 9, Name = "Hassan Tarek", Email = "hassan.tarek@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
        new() { Id = 10, Name = "Dina Fathy", Email = "dina.fathy@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver }
    };
        }

        private static List<Product> CreateProducts()
        {
            return new List<Product>
    {
        new() { Id = 1, Name = "Laptop", Price = 1200.00m, StockQuantity = 10, WeightKg = 2.2m },
        new() { Id = 2, Name = "Smartphone", Price = 800.00m, StockQuantity = 15, WeightKg = 0.4m },
        new() { Id = 3, Name = "Wireless Mouse", Price = 25.00m, StockQuantity = 2, WeightKg = 0.1m },
        new() { Id = 4, Name = "Mechanical Keyboard", Price = 90.00m, StockQuantity = 1, WeightKg = 0.9m },
        new() { Id = 5, Name = "USB-C Hub", Price = 45.00m, StockQuantity = 30, WeightKg = 0.2m },
        new() { Id = 6, Name = "Headphones", Price = 150.00m, StockQuantity = 12, WeightKg = 0.3m },
        new() { Id = 7, Name = "27\" Monitor", Price = 350.00m, StockQuantity = 6, WeightKg = 5.0m },
        new() { Id = 8, Name = "Webcam", Price = 70.00m, StockQuantity = 20, WeightKg = 0.2m }
    };
        }

        private static List<Coupon> CreateCoupons()
        {
            DateTime today = DateTime.Today;

            return new List<Coupon>
    {
        new()
        {
            Code = "SAVE10",
            DiscountPercent = 10,
            ExpiryDate = today.AddDays(180)
        },
        new()
        {
            Code = "WELCOME15",
            DiscountPercent = 15,
            ExpiryDate = today.AddDays(90)
        },
        new()
        {
            Code = "FLASH20",
            DiscountPercent = 20,
            ExpiryDate = today.AddDays(7)
        },
        new()
        {
            Code = "OLD20",
            DiscountPercent = 20,
            ExpiryDate = today.AddDays(-30)
        }
    };
        }

        private static List<Order> CreateIncomingOrders()
        {
            static Order CreateOrder(
                int id,
                int customerId,
                string? couponCode,
                params (int ProductId, int Quantity)[] items)
            {
                return new Order
                {
                    Id = id,
                    CustomerId = customerId,

                    Items = items.Select(item => new OrderItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = 0m
                    }).ToList(),

                    CouponCode = couponCode,

                    SubTotal = 0m,
                    Discount = 0m,
                    ShippingCost = 0m,
                    TotalAmount = 0m,

                    Status = OrderStatus.Pending,
                    CreatedAt = DateTime.Now,
                    FailureReason = null,
                    TransactionId = null
                };
            }

            return new List<Order>
    {
        CreateOrder(1, 1, "SAVE10", (1, 1), (3, 1)),
        CreateOrder(2, 2, null, (2, 1)),
        CreateOrder(3, 3, "WELCOME15", (5, 2), (8, 1)),
        CreateOrder(4, 4, null, (6, 1)),
        CreateOrder(5, 5, null, (7, 1)),
        CreateOrder(6, 6, "FLASH20", (5, 1)),
        CreateOrder(7, 7, null, (6, 1), (8, 1)),
        CreateOrder(8, 8, "SAVE10", (1, 1)),
        CreateOrder(9, 9, null, (3, 5)),
        CreateOrder(10, 10, null, (4, 1)),
        CreateOrder(11, 1, null, (4, 1)),
        CreateOrder(12, 2, "OLD20", (2, 1)),
        CreateOrder(13, 3, null, (6, 2)),
        CreateOrder(14, 5, "WELCOME15", (8, 2), (5, 1)),
        CreateOrder(15, 99, null, (2, 1)),
        CreateOrder(16, 6, null, (7, 1), (5, 1)),
        CreateOrder(17, 7, "SAVE10", (2, 1)),
        CreateOrder(18, 8, "FAKE50", (6, 1)),
        CreateOrder(19, 9, null, (8, 1)),
        CreateOrder(20, 10, "FLASH20", (1, 1), (6, 1))
    };
        }

        public async Task ResetAsync(
    CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string dataDirectory = Path.Combine(
                AppContext.BaseDirectory,
                "Data");

            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }

            Directory.CreateDirectory(dataDirectory);

            Directory.CreateDirectory(
                Path.Combine(dataDirectory, DataFiles.InvoicesFolder));

            Directory.CreateDirectory(
                Path.Combine(dataDirectory, DataFiles.ReportsFolder));

            var customers = CreateCustomers();
            var products = CreateProducts();
            var coupons = CreateCoupons();
            var incomingOrders = CreateIncomingOrders();

            await _store.WriteAsync(
                DataFiles.Customers, customers, cancellationToken);
            Console.WriteLine(
                $"[Seeder] {DataFiles.Customers} -> {customers.Count} records.");

            await _store.WriteAsync(
                DataFiles.Products, products, cancellationToken);
            Console.WriteLine(
                $"[Seeder] {DataFiles.Products} -> {products.Count} records.");

            await _store.WriteAsync(
                DataFiles.Coupons, coupons, cancellationToken);
            Console.WriteLine(
                $"[Seeder] {DataFiles.Coupons} -> {coupons.Count} records.");

            await _store.WriteAsync(
                DataFiles.IncomingOrders, incomingOrders, cancellationToken);
            Console.WriteLine(
                $"[Seeder] {DataFiles.IncomingOrders} -> {incomingOrders.Count} records.");

            await _store.WriteAsync(
                DataFiles.Orders, new List<Order>(), cancellationToken);
            Console.WriteLine($"[Seeder] {DataFiles.Orders} -> 0 records.");

            await _store.WriteAsync(
                DataFiles.Payments, new List<PaymentTransaction>(), cancellationToken);
            Console.WriteLine($"[Seeder] {DataFiles.Payments} -> 0 records.");

            await _store.WriteAsync(
                DataFiles.Emails, new List<EmailMessage>(), cancellationToken);
            Console.WriteLine($"[Seeder] {DataFiles.Emails} -> 0 records.");

            await _store.WriteAsync(
                DataFiles.EmailRetry, new List<EmailMessage>(), cancellationToken);
            Console.WriteLine($"[Seeder] {DataFiles.EmailRetry} -> 0 records.");
        }

    }
}
