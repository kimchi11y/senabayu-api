using Homestay.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Homestay.Api.Infrastructure;

/// <summary>Turns a BookingException into a ProblemDetails response. Anything else stays a generic 500.</summary>
public class BookingExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BookingException bookingException) return false;

        httpContext.Response.StatusCode = bookingException.StatusCode;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = bookingException.StatusCode,
                Title = bookingException.Message,
                Extensions = { ["code"] = bookingException.Code }
            }
        });
    }
}
