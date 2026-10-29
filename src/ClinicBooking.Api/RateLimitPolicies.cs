namespace ClinicBooking.Api;

public static class RateLimitPolicies
{
    /// <summary>Limits sign-in and registration attempts per IP to slow down password guessing.</summary>
    public const string Auth = "auth";
}
