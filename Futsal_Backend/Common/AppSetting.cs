namespace Backend.Common;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
}

public class AppOptions
{
    public const string SectionName = "App";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
    public string PublicApiBaseUrl { get; set; } = "http://localhost:5183";
    public string TimeZone { get; set; } = "Asia/Kathmandu";
}

public class BookingOptions
{
    public const string SectionName = "Booking";
    public string ReferencePrefix { get; set; } = "FB";
    public int MaxAdvanceDays { get; set; } = 30;
    public int PlayerCancelCutoffHours { get; set; } = 2;
    public int OnlinePaymentHoldMinutes { get; set; } = 15;
}

public class ReviewOptions
{
    public const string SectionName = "Reviews";
    public bool RequireVerifiedBooking { get; set; } = true;
}

public class EsewaOptions
{
    public const string SectionName = "Esewa";
    public string ProductCode { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string FormUrl { get; set; } = string.Empty;
    public string StatusCheckUrl { get; set; } = string.Empty;
}

public class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";
    public string FullName { get; set; } = "Platform Admin";
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class DatabaseOptions
{
    public const string SectionName = "Database";
    public bool ApplyMigrationsOnStartup { get; set; }
}