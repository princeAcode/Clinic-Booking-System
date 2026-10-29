using System.Net;
using System.Net.Http.Json;

namespace ClinicBooking.Client.Services;

/// <summary>An API call failed. <see cref="Exception.Message"/> is safe to show to the user.</summary>
public sealed class ApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

/// <summary>Shared HTTP plumbing: sends requests and turns problem-details error responses into <see cref="ApiException"/>.</summary>
public abstract class ApiClientBase(HttpClient http)
{
    protected async Task<T> GetAsync<T>(string url, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => http.GetAsync(url, ct));
        return await ReadAsync<T>(response, ct);
    }

    protected async Task<T> PostAsync<T>(string url, object? body, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => http.PostAsJsonAsync(url, body, ct));
        return await ReadAsync<T>(response, ct);
    }

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException)
        {
            throw new ApiException("Could not reach the server. Check your connection and try again.", HttpStatusCode.ServiceUnavailable);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<T>(ct))!;
        }

        throw new ApiException(await GetErrorMessageAsync(response, ct), response.StatusCode);
    }

    private static async Task<string> GetErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ProblemDetailsResponse? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(ct);
        }
        catch (Exception)
        {
            // Not a problem-details body; fall back to a message based on the status code.
        }

        if (!string.IsNullOrWhiteSpace(problem?.Detail))
        {
            return problem.Detail;
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Please sign in to continue.",
            HttpStatusCode.Forbidden => "You do not have permission to do that.",
            HttpStatusCode.NotFound => "The requested item was not found.",
            HttpStatusCode.TooManyRequests => "Too many attempts. Please wait a minute and try again.",
            _ => problem?.Title ?? "Something went wrong. Please try again.",
        };
    }

    private sealed record ProblemDetailsResponse(string? Title, string? Detail, int? Status);
}
