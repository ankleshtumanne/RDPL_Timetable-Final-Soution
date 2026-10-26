namespace Rdpl.Timetable.Api.Infrastructure;

/// <summary>Invalid client input. Mapped to 400 ValidationProblemDetails by <see cref="SafeExceptionHandler"/>.</summary>
public sealed class RequestValidationException : Exception
{
    public RequestValidationException(IDictionary<string, string[]> errors)
        : base("One or more request values are invalid.")
    {
        Errors = new Dictionary<string, string[]>(errors, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>The requested resource does not exist (yet). Mapped to 404 with <see cref="Exception.Message"/> as detail.</summary>
public sealed class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>A generation is already running (only one solve at a time). Mapped to 409.</summary>
public sealed class GenerationInProgressException : Exception
{
    public GenerationInProgressException()
        : base("A timetable generation is already running. Poll its job or retry later.")
    {
    }
}
