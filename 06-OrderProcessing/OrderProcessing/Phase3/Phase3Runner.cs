
using OrderProcessing.Phase1.Repositories;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrderProcessing.Phase3;

    internal class Phase3Runner
    {
        private readonly DataSeeder _seeder;
        private readonly IncomingOrderRepository _incomingOrderRepository;
        private readonly OrderProcessor _orderProcessor;
    private readonly OrderRepository _orderRepository;

    public Phase3Runner(
     DataSeeder seeder,
     IncomingOrderRepository incomingOrderRepository,
     OrderProcessor orderProcessor,
     OrderRepository orderRepository)
    {
        _seeder = seeder;
        _incomingOrderRepository = incomingOrderRepository;
        _orderProcessor = orderProcessor;
        _orderRepository = orderRepository;
    }



    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine("===== Phase 3: Order Processing =====");

        await _seeder.ResetAsync(cancellationToken);

        var orders = await _incomingOrderRepository.GetAllAsync(
            cancellationToken);

        var tasks = new List<Task<bool>>();

        foreach (Order order in orders)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            tasks.Add(
                _orderProcessor.ProcessOrderAsync(
                    order,
                    cancellationToken));
        }

        int completedTasks = 0;
        int cancelledTasks = 0;

        foreach (Task<bool> task in tasks)
        {
            try
            {
                await task;
                completedTasks++;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                cancelledTasks++;
            }
        }

        Console.WriteLine();
        Console.WriteLine("===== Phase 3: Processing Finished =====");
        Console.WriteLine($"Tasks started: {tasks.Count}");
        Console.WriteLine($"Tasks finished: {completedTasks}");
        Console.WriteLine($"Tasks cancelled: {cancelledTasks}");

        if (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Run was cancelled.");
        }

        var finalOrders = await _orderRepository.GetAllAsync(
    CancellationToken.None);

        Console.WriteLine();
        Console.WriteLine("===== Final Order Statuses =====");
        Console.WriteLine(
            $"{"Order ID",-10} {"Status",-15} {"Failure Reason"}");

        foreach (Order order in finalOrders.OrderBy(order => order.Id))
        {
            Console.WriteLine(
                $"{order.Id,-10} {order.Status,-15} " +
                $"{order.FailureReason ?? "-"}");
        }
    }


}


