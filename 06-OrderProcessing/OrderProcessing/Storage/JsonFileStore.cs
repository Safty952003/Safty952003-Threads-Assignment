
using System.Collections.Concurrent;
using System.Text.Json;
using OrderProcessing.Exceptions;

namespace OrderProcessing.Storage;

public class JsonFileStore
{
    private static readonly string DataDirectory =
        Path.Combine(AppContext.BaseDirectory, "Data");

    private static readonly ConcurrentDictionary<string, SemaphoreSlim>
        FileLocks = new();

    private static string GetFilePath(string fileName)
    {
        Directory.CreateDirectory(DataDirectory);

        return Path.Combine(DataDirectory, fileName);
    }

    private static T CreateDefaultValue<T>()
    {
        if (typeof(T).IsGenericType &&
            typeof(T).GetGenericTypeDefinition() == typeof(List<>))
        {
            return (T)Activator.CreateInstance(typeof(T))!;
        }

        return Activator.CreateInstance<T>();
    }


    public async Task<T> ReadAsync<T>(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        string filePath = GetFilePath(fileName);

        var fileLock = FileLocks.GetOrAdd(
            filePath,
            _ => new SemaphoreSlim(1, 1));

        await fileLock.WaitAsync(cancellationToken);

        try
        {

            if (!File.Exists(filePath))
            {
                return CreateDefaultValue<T>();
            }


            string json = await File.ReadAllTextAsync(
                filePath,
                cancellationToken);


            try
            {
                return JsonSerializer.Deserialize<T>(
                    json,
                    JsonSerializerConfig.Options)
                 ?? CreateDefaultValue<T>();
            }
            catch (JsonException ex)
            {
                throw new DataFileCorruptedException(fileName, ex);
            }

        }
        finally
        {
            fileLock.Release();
        }
    }


    public async Task WriteAsync<T>(
        string fileName,
        T data,
        CancellationToken cancellationToken = default)
    {
        string filePath = GetFilePath(fileName);

        var fileLock = FileLocks.GetOrAdd(
            filePath,
            _ => new SemaphoreSlim(1, 1));

        await fileLock.WaitAsync(cancellationToken);

        try
        {
            string json = JsonSerializer.Serialize(
                data,
                JsonSerializerConfig.Options);


            string tempFilePath = filePath + $".{Guid.NewGuid():N}.tmp";

            try
            {
                await File.WriteAllTextAsync(
                    tempFilePath,
                    json,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                File.Move(tempFilePath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }

        }
        finally
        {
            fileLock.Release();
        }
    }


}
