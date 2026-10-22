
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3.Email;
using OrderProcessing.Phase3.Payment;
using OrderProcessing.Phase3.Services;
using OrderProcessing.Shared;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System.Diagnostics;

namespace OrderProcessing.Phase3;

public class OrderProcessor
{
    private static int _stepIndex = 0;
    private readonly CustomerService _customerService;
    private readonly InventoryService _inventoryService;
    private readonly PricingService _pricingService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly OrderRepository _orderRepository;
    private readonly IEmailService _emailService;
    private readonly JsonFileStore _store;

    public OrderProcessor(JsonFileStore store,
        CustomerService customerService,
        InventoryService inventoryService,
        PricingService pricingService,
        IPaymentGateway paymentGateway,
        OrderRepository orderRepository,
        IEmailService emailService)
    {
        _customerService = customerService;
        _inventoryService = inventoryService;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _orderRepository = orderRepository;
        _emailService = emailService;
        _store = store;
    }




    public async Task<bool> ProcessOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        bool stockReserved = false;
        bool orderSavedAsCompleted = false;

        string? capturedTransactionId = null;
        Customer? customer = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                customer = await MeasureStepAsync<Customer>(
                    "Get customer",
                    order.Id,
                    () => _customerService.GetCustomerAsync(
                        order.CustomerId,
                        cancellationToken));
            }
            catch (CustomerNotFoundException ex)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;

                await _orderRepository.SaveAsync(
                    order,
                    cancellationToken);

                return false;
            }

            if (!customer.IsActive)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = "Customer is inactive";

                await _orderRepository.SaveAsync(
                    order,
                    cancellationToken);

                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            bool reserved = await MeasureStepAsync<bool>(
                "Reserve inventory",
                order.Id,
                () => _inventoryService.ReserveStockAsync(
                    order,
                    cancellationToken));

            if (!reserved)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Insufficient stock for '{_inventoryService.FailedProductName}'";

                await _orderRepository.SaveAsync(
                    order,
                    cancellationToken);

                return false;
            }

            stockReserved = true;

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await MeasureStepAsync<decimal>(
                    "Calculate total",
                    order.Id,
                    () => _pricingService.CalculateTotalAsync(
                        order,
                        cancellationToken));
            }
            catch (InvalidCouponException ex)
            {
                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                stockReserved = false;

                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;

                await _orderRepository.SaveAsync(
                    order,
                    cancellationToken);

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
                paymentResult = await MeasureStepAsync<PaymentResult>(
                    "Process payment",
                    order.Id,
                    () => _paymentGateway.ProcessPaymentAsync(
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
                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                stockReserved = false;

                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = "Payment gateway timeout (3s)";

                await _orderRepository.SaveAsync(
                    order,
                    CancellationToken.None);

                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!paymentResult.Success)
            {
                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                stockReserved = false;

                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = "Card declined by issuer";

                await _orderRepository.SaveAsync(
                    order,
                    CancellationToken.None);

                return false;
            }

            order.TransactionId = paymentResult.TransactionId;
            capturedTransactionId = paymentResult.TransactionId;

            cancellationToken.ThrowIfCancellationRequested();

            order.Status = OrderStatus.Completed;
            order.FailureReason = null;

            try
            {
                await _orderRepository.SaveAsync(
                    order,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DatabaseWriteException)
            {
                await _paymentGateway.RefundAsync(
                    capturedTransactionId!);

                capturedTransactionId = null;

                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                stockReserved = false;

                order.Status = OrderStatus.Failed;
                order.FailureReason =
                    $"Database save failed after payment; payment {order.TransactionId} refunded";

                await _orderRepository.SaveAsync(
                    order,
                    CancellationToken.None);

                return false;
            }
            orderSavedAsCompleted = true;

            cancellationToken.ThrowIfCancellationRequested();

            await SendConfirmationWithRetryAsync(
                order,
                customer,
                cancellationToken);

            totalStopwatch.Stop();

            AppLogger.Log(
                $"[Order #{order.Id}] Total processing time: {totalStopwatch.ElapsedMilliseconds} ms.");

            return order.Status == OrderStatus.Completed;
        }
        catch (OperationCanceledException)
    when (cancellationToken.IsCancellationRequested)
        {
            // لو الطلب اتحفظ Completed بالفعل،
            // ما نرجعش الفلوس أو المخزون وما نغيّرش حالته.
            if (orderSavedAsCompleted)
            {
                throw;
            }

            if (capturedTransactionId != null)
            {
                await _paymentGateway.RefundAsync(
                    capturedTransactionId);

                capturedTransactionId = null;
            }

            if (stockReserved)
            {
                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                stockReserved = false;
            }

            order.Status = OrderStatus.Pending;
            order.FailureReason = "Cancelled";

            await _orderRepository.SaveAsync(
                order,
                CancellationToken.None);

            throw;
        }
    }

    private async Task SendConfirmationWithRetryAsync(
        Order order,
        Customer customer,
        CancellationToken cancellationToken)
    {
        int[] retryDelays = { 500, 1000, 2000 };
        string? lastError = null;

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            try
            {

                await MeasureStepAsync(
                    $"Send confirmation email (attempt {attempt})",
                    order.Id,
                    async () =>
                    {
                        await _emailService.SendConfirmationAsync(
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
                    $"[OrderProcessor] Email attempt {attempt} failed for order #{order.Id}: {ex.Message}");

                if (attempt < 4)
                {
                    await Task.Delay(
                        retryDelays[attempt - 1],
                        cancellationToken);
                }
            }
        }

        var retryEmails = await ReadRetryEmailsAsync(cancellationToken);

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

        await WriteRetryEmailsAsync(retryEmails, cancellationToken);

        AppLogger.Log(
            $"[OrderProcessor] Email for Order #{order.Id} queued for retry later");
    }

    private async Task<List<EmailMessage>> ReadRetryEmailsAsync(
        CancellationToken cancellationToken)
    {
        return await _store.ReadAsync<List<EmailMessage>>(
            DataFiles.EmailRetry,
            cancellationToken);
    }

    private async Task WriteRetryEmailsAsync(
        List<EmailMessage> emails,
        CancellationToken cancellationToken)
    {
        await _store.WriteAsync(
            DataFiles.EmailRetry,
            emails,
            cancellationToken);
    }


    private async Task<T> MeasureStepAsync<T>(
        string stepName,
        int orderId,
        Func<Task<T>> operation)
    {
        int stepIndex = Interlocked.Increment(ref _stepIndex);
        int threadId = Environment.CurrentManagedThreadId;

        DateTime startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        string result = "Success";

        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            result = "Cancelled";
            throw;
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
                $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC] " +
                $"[Order #{orderId}] " +
                $"[Step #{stepIndex}] " +
                $"[Thread ID: {threadId}] " +
                $"[{stepName}] " +
                $"[Result: {result}] " +
                $"[Duration: {stopwatch.ElapsedMilliseconds} ms] " +
                $"[Started: {startTime:yyyy-MM-dd HH:mm:ss.fff} UTC]");
        }
    }


    public async Task ProcessOrderAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order);

        Console.WriteLine("[Main] Starting email operation...");

        await _emailService.SendConfirmationAsync(order, customer);

        Console.WriteLine("[Main] Email operation completed.");
        Console.WriteLine("Order processing finished");
    }


}
