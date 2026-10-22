
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

public class SyncPricingService
{
    private readonly JsonFileStore _store;
    private readonly SyncCustomerRepository _customerRepository;
    private readonly SyncCouponRepository _couponRepository;

    public SyncPricingService(
        JsonFileStore store,
        SyncCustomerRepository customerRepository,
        SyncCouponRepository couponRepository)
    {
        _store = store;
        _customerRepository = customerRepository;
        _couponRepository = couponRepository;
    }

    public decimal CalculateTotal(Order order)
    {
        AppLogger.Log(
            $"[SyncPricingService] Calculating total for order #{order.Id}.");

        var products = _store.Read<List<Product>>(DataFiles.Products);

        decimal subTotal = 0m;

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
            subTotal, 2, MidpointRounding.AwayFromZero);

        var customer = _customerRepository.GetById(order.CustomerId);

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
            var coupon = _couponRepository.GetByCode(order.CouponCode);

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
            loyaltyDiscount + couponDiscount, 30m);

        order.Discount = Math.Round(
            order.SubTotal * discountPercent / 100m,
            2,
            MidpointRounding.AwayFromZero);

        decimal shippingCost = 0m;

        if (order.SubTotal < 1000m)
        {
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

            shippingCost = Math.Round(
                5.00m + (1.50m * totalQuantity) + (2.00m * totalWeight),
                2,
                MidpointRounding.AwayFromZero);
        }

        order.ShippingCost = shippingCost;

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        return order.TotalAmount;
    }
}
