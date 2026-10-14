using System.Globalization;
using InventarioApi.Web;
using InventarioApi.Web.Auth;
using InventarioApi.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Dirección de la API: sale de wwwroot/appsettings.json (o appsettings.Production.json al publicar)
var apiBase = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
if (!apiBase.EndsWith('/')) apiBase += "/";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });

// Autenticación con el JWT que devuelve la API
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthProvider>());
builder.Services.AddScoped<ApiClient>();

// Moneda y fechas en formato argentino
var cultura = new CultureInfo("es-AR");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

await builder.Build().RunAsync();
