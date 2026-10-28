namespace InventarioApi.Movil.Services;

// A qué API se conecta la app. Por defecto, la publicada (funciona en el emulador y en el celular).
// Para desarrollar con la API corriendo en la PC se puede elegir el servidor local del emulador.
public class ConfiguracionApp
{
    public const string ServidorPublicado = "https://inventarioapi.runasp.net/";

    // Dentro del emulador de Android, 10.0.2.2 es la PC donde corre Visual Studio
    public const string ServidorLocalEmulador = "http://10.0.2.2:5136/";

    private const string Clave = "config.servidor";

    public string UrlServidor { get; private set; }

    public ConfiguracionApp()
    {
        UrlServidor = Normalizar(Preferences.Default.Get(Clave, ServidorPublicado));
    }

    public void CambiarServidor(string url)
    {
        UrlServidor = Normalizar(url);
        Preferences.Default.Set(Clave, UrlServidor);
    }

    public static bool EsValida(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static string Normalizar(string url)
    {
        url = url.Trim();
        return url.EndsWith('/') ? url : url + "/";
    }
}
