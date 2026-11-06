using System.Text.Json.Serialization;
using Homestay.Api.Data;
using Homestay.Api.Domain;
using Homestay.Api.Infrastructure;
using Homestay.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

const string BookingAppCors = "booking-app";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Numbers are only numbers. The web default also accepts "2" as 2, which makes the OpenAPI document
// describe every number as "integer or string" and the generated TypeScript types follow it.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    // Documents enums by name, so the client sends status=Confirmed rather than status=1.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    // Runs when migrations are applied (dotnet ef database update / migrations bundle), not on app start.
    .UseSeeding((context, _) => SeedData.Seed(context, builder.Configuration))
    .UseAsyncSeeding((context, _, cancellationToken) =>
        SeedData.SeedAsync(context, builder.Configuration, cancellationToken)));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: ["ready"]);

// Every error response is ProblemDetails JSON with a stable "code" the SPA can switch on.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status switch
        {
            400 => "validation_error",
            401 => "unauthorized",
            403 => "forbidden",
            404 => "not_found",
            _ => "internal_error"
        }));
builder.Services.AddExceptionHandler<BookingExceptionHandler>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddSingleton<JwtTokens>();
builder.Services.AddSingleton<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => JwtTokens.Configure(options, builder.Configuration));
builder.Services.AddAuthorization();

builder.Services.AddBookingRateLimits();

// Tells browsers which sites may read responses. It doesn't stop scripts; rate limits and rules do that.
builder.Services.AddCors(options => options.AddPolicy(BookingAppCors, policy => policy
    .WithOrigins("https://senabayu-demo.hakimrazak.com", "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Azure's ingress connects to the container on the visitor's behalf and adds their IP to X-Forwarded-For.
// ForwardLimit 1 uses only the value the ingress added, so a visitor can't fake their IP to dodge rate limits.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Configure the HTTP request pipeline.

// Before the rate limiter, which needs the visitor's real IP.
app.UseForwardedHeaders();

// Before everything else that can throw, so it catches their exceptions.
app.UseExceptionHandler();

// Gives empty error responses a ProblemDetails body, like the 401 from a missing token.
app.UseStatusCodePages();

app.UseCors(BookingAppCors);
app.UseRateLimiter();

// Not limited to Development: the live demo link and the SPA's client generation use these.
app.MapOpenApi();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Senabayu API v1"));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// live: the process is up (no dependencies checked). ready: it can also reach the database.
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Lets the integration tests start this app.
public partial class Program;
