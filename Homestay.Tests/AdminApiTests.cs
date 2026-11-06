using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Homestay.Api.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Homestay.Tests;

public class AdminApiTests : IClassFixture<ApiFixture>, IDisposable
{
    private static readonly DateOnly CheckIn = new(2032, 2, 1);
    private static readonly DateOnly CheckOut = new(2032, 2, 3);

    private readonly ApiFixture _api;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    /// <summary>xUnit creates this class per test, so each test has its own rate-limit counters.</summary>
    public AdminApiTests(ApiFixture fixture)
    {
        _api = fixture;
        _factory = fixture.CreateFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Admin_endpoints_reject_requests_without_a_token()
    {
        var response = await _client.GetAsync("/api/v1/admin/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Admin_endpoints_reject_a_token_signed_with_another_key()
    {
        var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "senabayu-api",
            Audience = "senabayu-admin",
            Expires = DateTime.UtcNow.AddHours(1),
            Claims = new Dictionary<string, object> { ["role"] = "Admin" },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("an-attackers-key-that-is-also-long-enough")),
                SecurityAlgorithms.HmacSha256)
        });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        var response = await _client.PostAsync("/api/v1/admin/demo/reset", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(ApiFixture.AdminEmail, "wrong-password")]
    [InlineData("nobody@example.com", ApiFixture.AdminPassword)]
    public async Task Wrong_email_and_wrong_password_look_the_same(string email, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_credentials", await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Bookings_follow_the_allowed_status_changes()
    {
        await LogInAsync();
        var code = await _api.AddRoomTypeAsync(roomCount: 1);
        var booking = await BookAndFindAsync(code);

        Assert.Equal("Confirmed", await ChangeStatusAsync(booking.Id, "confirm", HttpStatusCode.OK));

        // Already confirmed.
        Assert.Equal("invalid_transition", await ChangeStatusAsync(booking.Id, "confirm", HttpStatusCode.Conflict));

        Assert.Equal("Cancelled", await ChangeStatusAsync(booking.Id, "cancel", HttpStatusCode.OK));

        // Cancelled is final.
        Assert.Equal("invalid_transition", await ChangeStatusAsync(booking.Id, "cancel", HttpStatusCode.Conflict));
        Assert.Equal("invalid_transition", await ChangeStatusAsync(booking.Id, "confirm", HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Changing_a_booking_that_does_not_exist_is_not_found()
    {
        await LogInAsync();

        Assert.Equal("booking_not_found", await ChangeStatusAsync(0, "confirm", HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Cancelling_frees_the_room_for_another_guest()
    {
        await LogInAsync();
        var code = await _api.AddRoomTypeAsync(roomCount: 1);
        var booking = await BookAndFindAsync(code);

        Assert.Equal(HttpStatusCode.Conflict, (await CreateBookingAsync(code)).StatusCode);

        await ChangeStatusAsync(booking.Id, "cancel", HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.Created, (await CreateBookingAsync(code)).StatusCode);
    }

    [Fact]
    public async Task Bookings_can_be_filtered_by_status()
    {
        await LogInAsync();
        var code = await _api.AddRoomTypeAsync(roomCount: 2);
        var first = await BookAndFindAsync(code);
        await CreateBookingAsync(code);
        await ChangeStatusAsync(first.Id, "confirm", HttpStatusCode.OK);

        var page = await _client.GetFromJsonAsync<AdminBookingPageDto>("/api/v1/admin/bookings?status=Confirmed");

        Assert.Contains(page!.Items, b => b.Id == first.Id);
        Assert.All(page.Items, b => Assert.Equal("Confirmed", b.Status));
    }

    [Fact]
    public async Task A_burst_of_logins_gets_rate_limited()
    {
        var client = _client;

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/admin/auth/login", new LoginRequest(ApiFixture.AdminEmail, "wrong-password"));
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.All(statuses[..5], status => Assert.Equal(HttpStatusCode.Unauthorized, status));
    }

    private async Task LogInAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/auth/login", new LoginRequest(ApiFixture.AdminEmail, ApiFixture.AdminPassword));
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private Task<HttpResponseMessage> CreateBookingAsync(string code) =>
        _client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            code, CheckIn, CheckOut, 2, "Admin Test", "admin-test@example.com", "0123456789"));

    private async Task<AdminBookingDto> BookAndFindAsync(string code)
    {
        var created = await CreateBookingAsync(code);
        created.EnsureSuccessStatusCode();
        var reference = (await created.Content.ReadFromJsonAsync<BookingDto>())!.Reference;

        var page = await _client.GetFromJsonAsync<AdminBookingPageDto>("/api/v1/admin/bookings");
        return page!.Items.Single(b => b.Reference == reference);
    }

    /// <summary>Returns the new status on success, otherwise the error's code.</summary>
    private async Task<string> ChangeStatusAsync(int id, string action, HttpStatusCode expected)
    {
        var response = await _client.PostAsync($"/api/v1/admin/bookings/{id}/{action}", null);
        Assert.Equal(expected, response.StatusCode);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<AdminBookingDto>())!.Status
            : await ReadCodeAsync(response);
    }

    private static async Task<string> ReadCodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("code").GetString()!;
    }
}
