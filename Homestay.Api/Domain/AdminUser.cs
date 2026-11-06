namespace Homestay.Api.Domain;

public class AdminUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Admin;
}

public static class Roles
{
    public const string Admin = "Admin";
}
