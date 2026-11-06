using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Homestay.Api.Infrastructure;

/// <summary>Per-IP limits on the public endpoints that write data or could be used to guess bookings.</summary>
public static class RateLimits
{
    public const string BookingWrites = "booking-writes";
    public const string BookingLookups = "booking-lookups";

    public static IServiceCollection AddBookingRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) =>
                context.HttpContext.RequestServices
                    .GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails =
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many requests. Please try again in a minute.",
                            Extensions = { ["code"] = "rate_limited" }
                        }
                    });

            AddPerIpPolicy(options, BookingWrites, permitsPerMinute: 5);
            AddPerIpPolicy(options, BookingLookups, permitsPerMinute: 20);
        });

    private static void AddPerIpPolicy(RateLimiterOptions options, string name, int permitsPerMinute) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1)
            }));
}
