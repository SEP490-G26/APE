namespace Application.Models;

public enum CodeExecutionErrorCategory
{
    Unknown = 0,
    Validation = 1,
    Timeout = 2,
    Network = 3,
    RateLimit = 4,
    Provider = 5,
    Authentication = 6,
    Authorization = 7,
    NotFound = 8
}
