## Phase 1 — Questions and Answers

### 1. Why are the sync and async times almost the same when orders are awaited one after another?

Both versions process the orders sequentially, one order at a time. In the async version, each order is awaited before starting the next one, so the operations do not overlap. Therefore, the total execution times are almost the same.

### 2. Is the thread ID before an await always the same as after it? Why? Use your own log lines as evidence.

No, the thread ID is not always the same before and after an `await`. When the awaited operation completes, the continuation may run on a different thread.

My application logs show this:

```text
[03:02:17.808] [Thread 12] Before await - Thread ID: 12
[03:02:18.434] [Thread 5] After await - Thread ID: 5
```

The thread ID changed from 12 to 5, demonstrating that the continuation resumed on a different thread after the asynchronous operation.

### 3. What does the thread do while Task.Delay() is waiting? And while Thread.Sleep() is waiting?

With `Task.Delay()`, the asynchronous method yields control while waiting, allowing the thread to perform other work instead of remaining blocked.

With `Thread.Sleep()`, the current thread is blocked for the specified duration and cannot perform other work during that time.

### 4. Why should a repository method return Task<T> instead of T?

A repository method that performs asynchronous I/O should return `Task<T>` so the caller can use `await` without blocking the current thread while waiting for the result. Returning `T` would require a synchronous operation and could block the calling thread during I/O.

### 5. What is the difference between File.ReadAllText and File.ReadAllTextAsync? Does the async version occupy a thread while the disk is reading?

`File.ReadAllText()` reads a file synchronously and blocks the calling thread until the operation finishes.

`File.ReadAllTextAsync()` reads a file asynchronously and returns a `Task<string>` that can be awaited. The calling thread does not need to remain blocked while waiting for the disk I/O to complete. The asynchronous operation does not necessarily occupy a thread for the entire duration of the disk read.