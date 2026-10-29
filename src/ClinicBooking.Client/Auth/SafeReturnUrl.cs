using Microsoft.AspNetCore.Components;

namespace ClinicBooking.Client.Auth;

/// <summary>Only allows redirects back into this app after sign-in, preventing open-redirect attacks via ?returnUrl=.</summary>
public static class SafeReturnUrl
{
    public static string Resolve(string? returnUrl, NavigationManager navigation, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return fallback;
        }

        var absolute = navigation.ToAbsoluteUri(returnUrl);
        var baseUri = new Uri(navigation.BaseUri);
        return absolute.Scheme == baseUri.Scheme && absolute.Authority == baseUri.Authority
            ? navigation.ToBaseRelativePath(absolute.ToString())
            : fallback;
    }
}
