using ClinicBooking.Client;
using ClinicBooking.Client.Auth;
using ClinicBooking.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
});

// Authentication: the JWT lives in localStorage and is attached to API calls by AuthHeaderHandler.
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddTransient<AuthHeaderHandler>();
builder.Services.AddScoped<BookingConfirmationStore>();

// The API is served from the same origin as this app.
var apiBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
builder.Services.AddHttpClient<IClinicApi, ClinicApi>(c => c.BaseAddress = apiBaseAddress).AddHttpMessageHandler<AuthHeaderHandler>();
builder.Services.AddHttpClient<IAppointmentApi, AppointmentApi>(c => c.BaseAddress = apiBaseAddress).AddHttpMessageHandler<AuthHeaderHandler>();
builder.Services.AddHttpClient<IAuthApi, AuthApi>(c => c.BaseAddress = apiBaseAddress);

await builder.Build().RunAsync();
