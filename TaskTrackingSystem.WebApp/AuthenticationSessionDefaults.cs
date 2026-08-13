namespace TaskTrackingSystem.WebApp;

public static class AuthenticationSessionDefaults
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);
    public static readonly TimeSpan RememberMeLifetime = TimeSpan.FromDays(7);
}
