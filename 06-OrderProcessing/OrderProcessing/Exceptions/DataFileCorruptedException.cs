
namespace OrderProcessing.Exceptions;

public class DataFileCorruptedException : Exception
{
    public DataFileCorruptedException(string fileName, Exception innerException)
        : base($"The data file '{fileName}' is corrupted.", innerException)
    {
    }
}
