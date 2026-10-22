
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System.Diagnostics;

public class SyncOrderProcessor
{
    private readonly SyncCustomerRepository _customerRepository;
    private readonly SyncInventoryService _inventoryService;
    private readonly SyncPricingService _pricingService;
    private readonly SyncPaymentGateway _paymentGateway;
    private readonly SyncOrderRepository _orderRepository;
    private readonly SyncEmailService _emailService;
    private readonly JsonFileStore _store;

    public SyncOrderProcessor(
        JsonFileStore store,
        SyncCustomerRepository customerRepository,
        SyncInventoryService inventoryService,
        SyncPricingService pricingService,
        SyncPaymentGateway paymentGateway,
        SyncOrderRepository orderRepository,
        SyncEmailService emailService)
    {
        _store = store;
        _customerRepository = customerRepository;
        _inventoryService = inventoryService;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _orderRepository = orderRepository;
        _emailService = emailService;
    }

    public bool ProcessOrder(
        Order order,
        CancellationToken cancellationToken = default)
    {
        bool stockReserved = false;
        bool orderSavedAsCompleted = false;
        string? transactionId = null;

        Customer? customer = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            customer = MeasureStep(
                "Get customer",
                order.Id,
                () => _customerRepository.GetById(order.CustomerId));

            if (customer is null)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Customer #{order.CustomerId} not found";

                _orderRepository.Save(order);
                return false;
            }

            if (!customer.IsActive)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = "Customer is inactive";

                _orderRepository.Save(order);
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            bool reserved = MeasureStep(
                "Reserve inventory",
                order.Id,
                () => _inventoryService.ReserveStock(order));

            if (!reserved)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Insufficient stock for '{_inventoryService.FailedProductName}'";

                _orderRepository.Save(order);
                return false;
            }

            stockReserved = true;

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                MeasureStep(
                    "Calculate total",
                    order.Id,
                    () => _pricingService.CalculateTotal(order));
            }
            catch (InvalidCouponException ex)
            {
                _inventoryService.ReleaseStock(order);
                stockReserved = false;

                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;

                _orderRepository.Save(order);
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var paymentCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            paymentCts.CancelAfter(TimeSpan.FromSeconds(3));

            PaymentResult paymentResult;

            try
            {
                paymentResult = MeasureStep(
                    "Process payment",
                    order.Id,
                    () => _paymentGateway.ProcessPayment(
                        order.Id,
                        order.TotalAmount,
                        paymentCts.Token));
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                _inventoryService.ReleaseStock(order);
                stockReserved = false;

                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = "Payment gateway timeout (3s)";

                _orderRepository.Save(order);
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!paymentResult.Success)
            {
                _inventoryService.ReleaseStock(order);
                stockReserved = false;

                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = "Card declined by issuer";

                _orderRepository.Save(order);
                return false;
            }

            order.TransactionId = paymentResult.TransactionId;
            transactionId = paymentResult.TransactionId;

            cancellationToken.ThrowIfCancellationRequested();

            order.Status = OrderStatus.Completed;
            order.FailureReason = null;

            try
            {
                _orderRepository.Save(order);
            }
            catch (DatabaseWriteException)
            {
                _paymentGateway.Refund(transactionId!);

                transactionId = null;

                _inventoryService.ReleaseStock(order);
                stockReserved = false;

                order.Status = OrderStatus.Failed;
                order.FailureReason =
                    $"Database save failed after payment; payment {order.TransactionId} refunded";

                _orderRepository.Save(order);
                return false;
            }

            orderSavedAsCompleted = true;

            cancellationToken.ThrowIfCancellationRequested();

            SendConfirmationWithRetry(
                order,
                customer,
                cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            if (orderSavedAsCompleted)
            {
                throw;
            }

            if (transactionId is not null)
            {
                _paymentGateway.Refund(transactionId);
                transactionId = null;
            }

            if (stockReserved)
            {
                _inventoryService.ReleaseStock(order);
                stockReserved = false;
            }

            order.Status = OrderStatus.Pending;
            order.FailureReason = "Cancelled";

            _orderRepository.Save(order);
            throw;
        }
    }

    private void SendConfirmationWithRetry(
        Order order,
        Customer customer,
        CancellationToken cancellationToken)
    {
        int[] retryDelays = { 500, 1000, 2000 };
        string? lastError = null;

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                MeasureStep(
                    $"Send confirmation email (attempt {attempt})",
                    order.Id,
                    () =>
                    {
                        _emailService.SendConfirmation(
                            order,
                            customer,
                            cancellationToken);

                        return true;
                    });

                return;
            }
            catch (SmtpUnavailableException ex)
            {
                lastError = ex.Message;

                AppLogger.Log(
                    $"[SyncOrderProcessor] Email attempt {attempt} failed for order #{order.Id}: {ex.Message}");

                if (attempt < 4)
                {
                    Thread.Sleep(retryDelays[attempt - 1]);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        var retryEmails = _store.Read<List<EmailMessage>>(
            DataFiles.EmailRetry);

        retryEmails.Add(new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} confirmation",
            Body = $"Your order #{order.Id} has been completed. " +
                   $"Total: {order.TotalAmount:F2}",
            SentAt = DateTime.UtcNow,
            Attempts = 4,
            LastError = lastError
        });

        _store.Write(DataFiles.EmailRetry, retryEmails);

        AppLogger.Log(
            $"[SyncOrderProcessor] Email for Order #{order.Id} queued for retry later");
    }

    private T MeasureStep<T>(
        string stepName,
        int orderId,
        Func<T> operation)
    {
        int threadId = Environment.CurrentManagedThreadId;
        var stopwatch = Stopwatch.StartNew();

        string result = "Success";

        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            result = $"Failed ({ex.GetType().Name})";
            throw;
        }
        finally
        {
            stopwatch.Stop();

            AppLogger.Log(
                $"[Order #{orderId}] [Thread ID: {threadId}] " +
                $"[{stepName}] [Result: {result}] " +
                $"[Duration: {stopwatch.ElapsedMilliseconds} ms]");
        }
    }
}
