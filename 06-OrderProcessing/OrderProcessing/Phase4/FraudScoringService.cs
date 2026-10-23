using System.Collections.Concurrent;
using System.Diagnostics;

namespace OrderProcessing.Phase4;

public sealed class FraudScoringService
{
    private const int OrderCount = 10_000;

    public void Run(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("===== Task 10.4 A: Fraud Scoring =====");

        // 1. Sequential execution
        var sequentialFlagged = new List<int>();
        var sequentialThreads = new HashSet<int>();

        var sequentialStopwatch = Stopwatch.StartNew();

        for (int orderId = 1; orderId <= OrderCount; orderId++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            sequentialThreads.Add(
                Environment.CurrentManagedThreadId);

            int score = Score(orderId);

            if (score >= 90)
            {
                sequentialFlagged.Add(orderId);
            }
        }

        sequentialStopwatch.Stop();

        // 2. Parallel execution
        var parallelFlagged = new ConcurrentBag<int>();

        var parallelThreads = new ConcurrentDictionary<int, byte>();

        int threadPoolCountBefore = ThreadPool.ThreadCount;

        var parallelStopwatch = Stopwatch.StartNew();

        Parallel.ForEach(
            Enumerable.Range(1, OrderCount),
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            orderId =>
            {
                parallelThreads.TryAdd(
                    Environment.CurrentManagedThreadId, 0);

                int score = Score(orderId);

                if (score >= 90)
                {
                    parallelFlagged.Add(orderId);
                }
            });

        parallelStopwatch.Stop();

        int threadPoolCountAfter = ThreadPool.ThreadCount;

        // Sort parallel results for comparison
        var sortedParallelFlagged = parallelFlagged
            .OrderBy(id => id)
            .ToArray();

        var expectedFirstFive = new[] { 4, 8, 12, 29, 33 };

        bool sameResults = sequentialFlagged
            .SequenceEqual(sortedParallelFlagged);

        bool requirementMet =
            sequentialFlagged.Count == 1005 &&
            sortedParallelFlagged.SequenceEqual(sequentialFlagged) &&
            sequentialFlagged.Take(5).SequenceEqual(expectedFirstFive);

        double speedUp =
            parallelStopwatch.Elapsed.TotalMilliseconds > 0
                ? sequentialStopwatch.Elapsed.TotalMilliseconds /
                  parallelStopwatch.Elapsed.TotalMilliseconds
                : 0;

        // 3. Print results
        Console.WriteLine(
            $"Sequential flagged: {sequentialFlagged.Count}");

        Console.WriteLine(
            $"Parallel flagged:   {sortedParallelFlagged.Length}");

        Console.WriteLine(
            $"First 5 flagged IDs: {string.Join(", ", sequentialFlagged.Take(5))}");

        Console.WriteLine(
            $"Results match: {sameResults}");

        Console.WriteLine(
            $"Requirements passed: {requirementMet}");

        Console.WriteLine(
            $"Sequential time: {sequentialStopwatch.Elapsed.TotalSeconds:F2} s");

        Console.WriteLine(
            $"Parallel time:   {parallelStopwatch.Elapsed.TotalSeconds:F2} s");

        Console.WriteLine(
            $"Speed-up: {speedUp:F2}x");

        Console.WriteLine(
            $"Sequential distinct threads: {sequentialThreads.Count}");

        Console.WriteLine(
            $"Parallel distinct threads: {parallelThreads.Count}");

        Console.WriteLine(
            $"Processor count: {Environment.ProcessorCount}");

        Console.WriteLine(
            $"ThreadPool.ThreadCount: {threadPoolCountBefore} -> {threadPoolCountAfter}");
    }

    private static int Score(int orderId)
    {
        long h = orderId;

        for (int i = 0; i < 100_000; i++)
        {
            h = (h * 31 + i) % 1_000_000_007;
        }

        return (int)(h % 100);
    }
}