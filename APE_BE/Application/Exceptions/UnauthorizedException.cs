namespace Application.Exceptions;

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException()
        : base("Unauthorized.")
    {
    }

    public UnauthorizedException(string message)
        : base(message)
    {
    }

    public UnauthorizedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}