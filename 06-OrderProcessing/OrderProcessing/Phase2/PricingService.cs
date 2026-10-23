using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class PricingService
{
    private readonly JsonFileStore _store;
    private readonly CustomerRepository _customerRepository;
    private readonly CouponRepository _couponRepository;
    private readonly IEnumerable<int> _failShippingForOrderIds;

    public PricingService(
    JsonFileStore store,
    CustomerRepository customerRepository,
    CouponRepository couponRepository,
    IEnumerable<int> failShippingForOrderIds,
    bool enableFailureScenarios = true)
    {
        _store = store;
        _customerRepository = customerRepository;
        _couponRepository = couponRepository;

        _failShippingForOrderIds = enableFailureScenarios
            ? failShippingForOrderIds
            : Array.Empty<int>();
    }

    public async Task<decimal> CalculateSubTotalAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            $"[PricingService] Opening database connection for order #{order.Id}.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            $"[PricingService] Executing subtotal query for order #{order.Id}.");

        await Task.Delay(
            Random.Shared.Next(300, 601),
            cancellationToken);

        var products = await _store.ReadAsync<List<Product>>(
            DataFiles.Products,
            cancellationToken);

        decimal subTotal = 0;

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(
                p => p.Id == item.ProductId);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} was not found.");
            }

            item.UnitPrice = product.Price;

            subTotal += item.UnitPrice * item.Quantity;
        }

        order.SubTotal = Math.Round(
            subTotal,
            2,
            MidpointRounding.AwayFromZero);

        return order.SubTotal;
    }

    public async Task<decimal> GetDiscountAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            $"[PricingService] Requesting discount for order #{order.Id}.");

        await Task.Delay(200, cancellationToken);

        var customer = await _customerRepository.GetByIdAsync(
            order.CustomerId,
            cancellationToken);

        if (customer is null)
        {
            throw new InvalidOperationException(
                $"Customer #{order.CustomerId} was not found.");
        }

        decimal loyaltyDiscount = customer.LoyaltyTier switch
        {
            LoyaltyTier.Bronze => 0m,
            LoyaltyTier.Silver => 5m,
            LoyaltyTier.Gold => 10m,
            _ => 0m
        };

        decimal couponDiscount = 0m;

        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupon = await _couponRepository.GetByCodeAsync(
                order.CouponCode,
                cancellationToken);


            if (coupon is null)
            {
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' does not exist");
            }

            if (coupon.ExpiryDate.Date < DateTime.Today)
            {
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' has expired");
            }

            couponDiscount = coupon.DiscountPercent;

        }

        decimal discountPercent = Math.Min(
            loyaltyDiscount + couponDiscount,
            30m);

        decimal discount = order.SubTotal * discountPercent / 100m;

        return Math.Round(
            discount,
            2,
            MidpointRounding.AwayFromZero);
    }

    public async Task<decimal> CalculateShippingCostAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            $"[PricingService] Connecting to shipping provider for order #{order.Id}.");

        await Task.Delay(200, cancellationToken);

        if (_failShippingForOrderIds.Contains(order.Id))
        {
            throw new ShippingProviderUnavailableException(
                "Shipping provider unavailable");
        }

        AppLogger.Log(
            $"[PricingService] Calculating shipping for order #{order.Id}.");

        await Task.Delay(
            Random.Shared.Next(300, 601),
            cancellationToken);

        if (order.SubTotal >= 1000m)
        {
            return 0m;
        }

        var products = await _store.ReadAsync<List<Product>>(
            DataFiles.Products,
            cancellationToken);

        int totalQuantity = 0;
        decimal totalWeight = 0m;

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(
                p => p.Id == item.ProductId);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} was not found.");
            }

            totalQuantity += item.Quantity;
            totalWeight += product.WeightKg * item.Quantity;
        }

        decimal shippingCost =
            5.00m +
            (1.50m * totalQuantity) +
            (2.00m * totalWeight);

        return Math.Round(
            shippingCost,
            2,
            MidpointRounding.AwayFromZero);
    }



    public async Task<decimal> CalculateTotalAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        await CalculateSubTotalAsync(order, cancellationToken);

        var discountTask = GetDiscountAsync(order, cancellationToken);
        var shippingTask = CalculateShippingCostAsync(
            order,
            cancellationToken);

        await discountTask;
        await shippingTask;

        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        return order.TotalAmount;
    }

}