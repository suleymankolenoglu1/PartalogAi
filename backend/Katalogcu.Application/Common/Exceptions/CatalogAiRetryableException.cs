namespace Katalogcu.Application.Common.Exceptions;

public sealed class CatalogAiRetryableException : Exception
{
    public CatalogAiRetryableException(string operation, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
    }

    public string Operation { get; }
}
