namespace DriverPlatform.Application.Common;

/// <summary>The request is well-formed but breaks a business rule. The API answers 400 with the errors per field.</summary>
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public RequestValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }
}

public sealed class NotFoundException(string message) : Exception(message);

/// <summary>The API answers 409; <see cref="Payload"/> is returned to the client (e.g. the new price).</summary>
public sealed class ConflictException(string message, object? payload = null) : Exception(message)
{
    public object? Payload { get; } = payload;
}
