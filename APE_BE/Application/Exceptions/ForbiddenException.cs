namespace Application.Exceptions;

public sealed class ForbiddenException : Exception
{
    public ForbiddenException()
        : base("Forbidden.")
    {
    }

    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}