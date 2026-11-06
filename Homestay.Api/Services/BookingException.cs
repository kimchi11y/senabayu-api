namespace Homestay.Api.Services;

/// <summary>A broken business rule. BookingExceptionHandler turns it into a ProblemDetails response.</summary>
public class BookingException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    /// <summary>Stable value the SPA can switch on, e.g. "room_unavailable".</summary>
    public string Code { get; } = code;
}
