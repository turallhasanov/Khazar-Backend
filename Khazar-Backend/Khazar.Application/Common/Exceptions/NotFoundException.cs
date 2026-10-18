namespace Khazar.Application.Common.Exceptions;

/// <summary>Thrown when a resource referenced by a request does not exist.</summary>
public sealed class NotFoundException(string message) : Exception(message)
{
    public NotFoundException(string resourceName, object key)
        : this($"{resourceName} '{key}' was not found.")
    {
    }
}
