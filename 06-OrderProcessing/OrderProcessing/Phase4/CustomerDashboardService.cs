using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Phase4;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class CustomerDashboardService
{
    private readonly JsonFileStore _store;
    private readonly CustomerRepository _customerRepository;
    private readonly OrderRepository _orderRepository;
    private readonly HashSet<int> _failPaymentsForCustomerIds;

    private int _lastObservedThreadCount;

    public int LastObservedThreadCount =>
        Volatile.Read(ref _lastObservedThreadCount);

    public CustomerDashboardService(
        JsonFileStore store,
        CustomerRepository customerRepository,
        OrderRepository orderRepository,
        IEnumerable<int>? failPaymentsForCustomerIds = null)
    {
        _store = store;
        _customerRepository = customerRepository;
        _orderRepository = orderRepository;

        _failPaymentsForCustomerIds = new HashSet<int>(
            failPaymentsForCustomerIds ?? Enumerable.Empty<int>());
    }

    public async Task<CustomerDashboard> LoadAsync(
        int customerId,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        var observedThreadIds =
            new ConcurrentDictionary<int, byte>();

        RecordCurrentThread(observedThreadIds);

        try
        {
            // Start the four loads without waiting for each one.
            Task<Customer> customerTask =
                LoadCustomerAsync(
                    customerId,
                    ct,
                    observedThreadIds);

            Task<List<Order>> ordersTask =
                LoadOrdersAsync(
                    customerId,
                    ct,
                    observedThreadIds);

            Task<List<PaymentTransaction>> paymentsTask =
                LoadPaymentsAsync(
                    customerId,
                    ct,
                    observedThreadIds);

            Task<List<EmailMessage>> emailsTask =
                LoadEmailsAsync(
                    ct,
                    observedThreadIds);

            // Wait for all four operations.
            var allLoadsTask = Task.WhenAll(
                customerTask,
                ordersTask,
                paymentsTask,
                emailsTask);

            try
            {
                await allLoadsTask;

                RecordCurrentThread(observedThreadIds);
            }
            catch
            {
                RecordCurrentThread(observedThreadIds);

                var failures = allLoadsTask.Exception?
                    .Flatten()
                    .InnerExceptions;

                if (failures is { Count: > 0 })
                {
                    throw new AggregateException(
                        "Customer dashboard loading failed.",
                        failures);
                }

                throw;
            }

            // These tasks have already completed successfully.
            var customer = await customerTask;
            RecordCurrentThread(observedThreadIds);

            var orders = await ordersTask;
            RecordCurrentThread(observedThreadIds);

            var payments = await paymentsTask;
            RecordCurrentThread(observedThreadIds);

            var emails = await emailsTask;
            RecordCurrentThread(observedThreadIds);

            var orderIds = orders
                .Select(order => order.Id)
                .ToHashSet();

            var dashboard = new CustomerDashboard
            {
                Customer = customer,
                Orders = orders,
                Payments = payments
                    .Where(payment =>
                        orderIds.Contains(payment.OrderId))
                    .ToList(),
                Emails = emails
                    .Where(email =>
                        orderIds.Contains(email.OrderId))
                    .ToList()
            };

            stopwatch.Stop();

            AppLogger.Log(
                $"[CustomerDashboardService] Customer #{customerId} " +
                $"dashboard loaded in {stopwatch.ElapsedMilliseconds} ms.");

            return dashboard;
        }
        finally
        {
            RecordCurrentThread(observedThreadIds);

            stopwatch.Stop();

            Volatile.Write(
                ref _lastObservedThreadCount,
                observedThreadIds.Count);
        }
    }

    private async Task<Customer> LoadCustomerAsync(
        int customerId,
        CancellationToken ct,
        ConcurrentDictionary<int, byte> observedThreadIds)
    {
        RecordCurrentThread(observedThreadIds);

        var customer = await _customerRepository.GetByIdAsync(
            customerId,
            ct);

        RecordCurrentThread(observedThreadIds);

        if (customer is null)
        {
            throw new CustomerNotFoundException(
                $"Customer #{customerId} not found");
        }

        return customer;
    }

    private async Task<List<Order>> LoadOrdersAsync(
        int customerId,
        CancellationToken ct,
        ConcurrentDictionary<int, byte> observedThreadIds)
    {
        RecordCurrentThread(observedThreadIds);

        var orders = await _orderRepository.GetAllAsync(ct);

        RecordCurrentThread(observedThreadIds);

        return orders
            .Where(order => order.CustomerId == customerId)
            .ToList();
    }

    private async Task<List<PaymentTransaction>> LoadPaymentsAsync(
        int customerId,
        CancellationToken ct,
        ConcurrentDictionary<int, byte> observedThreadIds)
    {
        RecordCurrentThread(observedThreadIds);

        AppLogger.Log(
            $"[CustomerDashboardService] Opening payments query " +
            $"for customer #{customerId}.");

        await Task.Delay(200, ct);

        RecordCurrentThread(observedThreadIds);

        AppLogger.Log(
            $"[CustomerDashboardService] Executing payments query " +
            $"for customer #{customerId}.");

        await Task.Delay(Random.Shared.Next(300, 601), ct);

        RecordCurrentThread(observedThreadIds);

        if (_failPaymentsForCustomerIds.Contains(customerId))
        {
            throw new TimeoutException("Payments store timeout");
        }

        var payments =
            await _store.ReadAsync<List<PaymentTransaction>>(
                DataFiles.Payments,
                ct);

        RecordCurrentThread(observedThreadIds);

        return payments;
    }

    private async Task<List<EmailMessage>> LoadEmailsAsync(
        CancellationToken ct,
        ConcurrentDictionary<int, byte> observedThreadIds)
    {
        RecordCurrentThread(observedThreadIds);

        AppLogger.Log(
            "[CustomerDashboardService] Opening emails query.");

        await Task.Delay(200, ct);

        RecordCurrentThread(observedThreadIds);

        AppLogger.Log(
            "[CustomerDashboardService] Executing emails query.");

        await Task.Delay(Random.Shared.Next(300, 601), ct);

        RecordCurrentThread(observedThreadIds);

        var emails = await _store.ReadAsync<List<EmailMessage>>(
            DataFiles.Emails,
            ct);

        RecordCurrentThread(observedThreadIds);

        return emails;
    }

    private static void RecordCurrentThread(
        ConcurrentDictionary<int, byte> observedThreadIds)
    {
        observedThreadIds.TryAdd(
            Environment.CurrentManagedThreadId,
            0);
    }
}