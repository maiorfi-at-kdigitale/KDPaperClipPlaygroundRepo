namespace Majordomo.SharedKernel;

/// <summary>Input sintatticamente corretto ma semanticamente non valido. Mappata a 422.</summary>
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception("La richiesta contiene parametri non validi.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public RequestValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }
}

/// <summary>Precondizione HTTP (If-Match) non soddisfatta. Mappata a 412.</summary>
public sealed class PreconditionFailedException(string message) : Exception(message);
