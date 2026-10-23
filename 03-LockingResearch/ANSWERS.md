# Assignment 03 — Answers

Student name: Mahmoud Mohamed Elsafty  
Group / ID:G1

1. `System.Threading.Monitor` is used to synchronize threads when they access shared resources. It ensures that only one thread at a time can execute a protected critical section using the same monitor.

2. For an ordinary reference-type lock object, the C# `lock` statement uses `Monitor.Enter` and `Monitor.Exit`, with a `try/finally` structure to ensure the lock is released.
 With `System.Threading.Lock` in C# 13+, it uses `EnterScope()` instead.

3. 
   - `lock` is simpler and automatically releases the lock, while direct `Monitor` calls require a `try/finally` structure to release it safely.
   - `Monitor` provides additional methods such as `TryEnter`, `Wait`, and `Pulse` for advanced synchronization scenarios.



4. 
   - `Monitor.TryEnter` is useful when a thread should try to acquire a lock without waiting indefinitely, optionally with a timeout.
   - `Monitor.Wait` allows a thread to wait for a condition while temporarily releasing the monitor.
   - `Monitor.Pulse` wakes one thread waiting on the same monitor so it can try to continue.

5. **Using `lock`:**

   ```csharp
   using System.Threading;

   public class Counter
   {
       private readonly Lock _lockObject = new();
       private int _counter;

       public void Increment()
       {
           lock (_lockObject)
           {
               _counter++;
           }
       }
   }
   ```

   **Using `Monitor.Enter` / `Monitor.Exit`:**

   ```csharp
   using System.Threading;

   public class Counter
   {
       private readonly object _lockObject = new();
       private int _counter;

       public void Increment()
       {
           bool lockTaken = false;

           try
           {
               Monitor.Enter(_lockObject, ref lockTaken);
               _counter++;
           }
           finally
           {
               if (lockTaken)
               {
                   Monitor.Exit(_lockObject);
               }
           }
       }
   }
   ```

Links:

- https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor
- https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock