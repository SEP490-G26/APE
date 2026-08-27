namespace Application.Exceptions;

public sealed class ConflictException : Exception
{
    public ConflictException()
        : base("Resource conflict.")
    {
    }

    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}