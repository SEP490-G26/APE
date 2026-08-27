namespace Application.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException()
        : base("Resource not found.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string resource, string id)
        : base($"{resource} '{id}' was not found.")
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}