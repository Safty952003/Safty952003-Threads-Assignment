
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public class OrderValidator
{
    private readonly CustomerRepository _customerRepository;
    private readonly ProductRepository _productRepository;
    private readonly CouponRepository _couponRepository;

    public OrderValidator(
        CustomerRepository customerRepository,
        ProductRepository productRepository,
        CouponRepository couponRepository
        )
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _couponRepository = couponRepository;
    }

    public async Task ValidateOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log($"Validating Order #{order.Id}.");

        string? failureReason = null;

        var customer = await _customerRepository.GetByIdAsync(
            order.CustomerId, cancellationToken);

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
            foreach (var item in order.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var product = await _productRepository.GetByIdAsync(
                    item.ProductId, cancellationToken);

                if (product is null)
                {
                    failureReason = $"Product #{item.ProductId} not found";
                    break;
                }

                if (product.StockQuantity < item.Quantity)
                {
                    failureReason =
                        $"Insufficient stock for '{product.Name}'";
                    break;
                }
            }
        }

        if (failureReason is null && order.CouponCode is not null)
        {
            var coupon = await _couponRepository.GetByCodeAsync(
                order.CouponCode, cancellationToken);

            if (coupon is null)
            {
                failureReason =
                    $"Coupon '{order.CouponCode}' does not exist";
            }
            else if (coupon.ExpiryDate.Date < DateTime.Today)
            {
                failureReason =
                    $"Coupon '{order.CouponCode}' has expired";
            }
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
