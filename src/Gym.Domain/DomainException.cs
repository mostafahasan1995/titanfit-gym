namespace Gym.Domain;

/// <summary>
/// A business rule was broken. <see cref="Code"/> is a stable, machine-readable key
/// (for example <c>insufficient_stock</c>) that the apps can translate; <see cref="Exception.Message"/> is English.
/// </summary>
public class DomainException(string code, string message, IReadOnlyDictionary<string, object?>? details = null)
    : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>Optional extra data for the caller, e.g. the available quantity.</summary>
    public IReadOnlyDictionary<string, object?>? Details { get; } = details;
}

/// <summary>The requested record does not exist (HTTP 404).</summary>
public sealed class NotFoundException(string code, string message) : DomainException(code, message);

/// <summary>The request conflicts with the current data, e.g. a duplicate national number (HTTP 409).</summary>
public sealed class ConflictException(string code, string message, IReadOnlyDictionary<string, object?>? details = null)
    : DomainException(code, message, details);

/// <summary>The logged-in user may not do this to this record, e.g. close another cashier's shift (HTTP 403).</summary>
public sealed class ForbiddenException(string code, string message) : DomainException(code, message);
