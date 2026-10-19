
namespace OrderProcessing.Shared.Logging;

public static class AppLogger
{
    private static readonly object LockObject = new();

    public static void Log(string message)
    {
        Write(message, ConsoleColor.Gray);
    }

    public static void Success(string message)
    {
        Write(message, ConsoleColor.Green);
    }

    public static void Error(string message)
    {
        Write(message, ConsoleColor.Red);
    }

    private static void Write(string message, ConsoleColor color)
    {
        lock (LockObject)
        {
            ConsoleColor originalColor = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = color;

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] " +
                    $"[Thread {Environment.CurrentManagedThreadId}] " +
                    message);
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }
}
