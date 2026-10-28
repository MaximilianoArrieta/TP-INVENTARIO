using System.Globalization;
using InventarioApi.Movil.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace InventarioApi.Movil;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Montos y fechas en formato argentino ($ 1.234,50 · 26/10/2026)
        var argentina = new CultureInfo("es-AR");
        CultureInfo.DefaultThreadCurrentCulture = argentina;
        CultureInfo.DefaultThreadCurrentUICulture = argentina;

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // Servicios de la app (uno solo para toda la app: hay un único usuario por celular)
        builder.Services.AddSingleton<ConfiguracionApp>();
        builder.Services.AddSingleton<SesionUsuario>();
        builder.Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<SesionUsuario>());
        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<CarritoCompras>();
        builder.Services.AddSingleton<AvisosApp>();
        builder.Services.AddSingleton<ArchivosPdf>();
        builder.Services.AddSingleton<FotosProducto>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
