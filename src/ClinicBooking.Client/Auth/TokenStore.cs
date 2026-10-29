using Microsoft.JSInterop;

namespace ClinicBooking.Client.Auth;

/// <summary>Keeps the JWT in browser localStorage so the user stays signed in across page reloads.</summary>
public sealed class TokenStore(IJSRuntime js)
{
    private const string StorageKey = "clinicBooking.token";
    private string? _cachedToken;
    private bool _loaded;

    public async ValueTask<string?> GetTokenAsync()
    {
        if (!_loaded)
        {
            _cachedToken = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            _loaded = true;
        }

        return _cachedToken;
    }

    public async ValueTask SetTokenAsync(string token)
    {
        _cachedToken = token;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, token);
    }

    public async ValueTask ClearAsync()
    {
        _cachedToken = null;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }
}
