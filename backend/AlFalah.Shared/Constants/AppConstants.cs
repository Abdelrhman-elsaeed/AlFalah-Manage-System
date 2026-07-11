namespace AlFalah.Shared.Constants;

public static class AppConstants
{
    public const string DefaultLanguage = "ar";
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int JwtAccessTokenExpiryMinutes = 60;
    public const int JwtRefreshTokenExpiryDays = 30;
    public const string ApiVersion = "v1";
    public const string ApiRoutePrefix = $"api/{ApiVersion}";
}
