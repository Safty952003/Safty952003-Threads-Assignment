
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public class SyncOrderValidator
{
    private readonly SyncCustomerRepository _customerRepository;
    private readonly SyncProductRepository _productRepository;
    private readonly SyncCouponRepository _couponRepository;

    public SyncOrderValidator(
        SyncCustomerRepository customerRepository,
        SyncProductRepository productRepository,
        SyncCouponRepository couponRepository)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _couponRepository = couponRepository;
    }

    public void ValidateOrder(Order order)
    {
        AppLogger.Log($"Validating Order #{order.Id} synchronously.");

        string? failureReason = null;

        var customer = _customerRepository.GetById(order.CustomerId);

        if (customer is null)
        {
            failureReason = $"Customer #{order.CustomerId} not found";
        }
        else if (!customer.IsActive)
        {
            failureReason = "Customer is inactive";
        }
        else
        {
            var products = new List<Product>();

            foreach (var item in order.Items)
            {
                var product = _productRepository.GetById(item.ProductId);

                if (product is null)
                {
                    failureReason = $"Product #{item.ProductId} not found";
                    break;
                }

                products.Add(product);
            }

            if (failureReason is null)
            {
                for (int i = 0; i < order.Items.Count; i++)
                {
                    if (products[i].StockQuantity < order.Items[i].Quantity)
                    {
                        failureReason =
                            $"Insufficient stock for '{products[i].Name}'";
                        break;
                    }
                }
            }
        }

        if (failureReason is null && order.CouponCode is not null)
        {
            var coupon = _couponRepository.GetByCode(order.CouponCode);

            if (coupon is null)
                failureReason = $"Coupon '{order.CouponCode}' does not exist";
            else if (coupon.ExpiryDate.Date < DateTime.Today)
                failureReason = $"Coupon '{order.CouponCode}' has expired";
        }

        if (failureReason is null)
        {
            order.Status = OrderStatus.Validated;
            order.FailureReason = null;

            AppLogger.Success($"Order #{order.Id} validated.");
        }
        else
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = failureReason;

            AppLogger.Error(
                $"Order #{order.Id} rejected: {failureReason}");
        }
    }
}
