namespace APITemplate.Domain.Exceptions;

public abstract class AppException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }
    public string Title { get; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    protected AppException(
        string message,
        string errorCode,
        int statusCode,
        string title,
        IReadOnlyDictionary<string, object?>? metadata = null) : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Title = title;
        Metadata = metadata;
    }
}
