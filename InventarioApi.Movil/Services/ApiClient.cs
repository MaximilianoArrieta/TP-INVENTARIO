using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InventarioApi.Movil.Services;

public class ApiException : Exception
{
    public HttpStatusCode? Estado { get; }
    public ApiException(string mensaje, HttpStatusCode? estado = null) : base(mensaje) => Estado = estado;
}

// Único punto de contacto con la API: arma la dirección según el servidor elegido,
// agrega el token a cada pedido y convierte las respuestas de error en mensajes legibles.
public class ApiClient
{
    // El hosting gratuito puede tardar en "despertar" la primera vez
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly ConfiguracionApp _config;
    private readonly SesionUsuario _sesion;

    public ApiClient(ConfiguracionApp config, SesionUsuario sesion)
    {
        _config = config;
        _sesion = sesion;
    }

    public string Servidor => new Uri(_config.UrlServidor).Authority;

    // Las imágenes de productos las sirve la API (wwwroot/uploads)
    public string? UrlArchivo(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return null;
        if (ruta.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return ruta;
        return new Uri(new Uri(_config.UrlServidor), ruta.TrimStart('/')).ToString();
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        using var resp = await EnviarAsync(HttpMethod.Get, url, null);
        return await LeerAsync<T>(resp);
    }

    // Archivos binarios (los reportes PDF)
    public async Task<byte[]> GetArchivoAsync(string url)
    {
        using var resp = await EnviarAsync(HttpMethod.Get, url, null);
        return await resp.Content.ReadAsByteArrayAsync();
    }

    public async Task<T?> PostAsync<T>(string url, object cuerpo)
    {
        using var resp = await EnviarAsync(HttpMethod.Post, url, Json(cuerpo));
        return await LeerAsync<T>(resp);
    }

    public async Task PutAsync(string url, object cuerpo)
    {
        using var resp = await EnviarAsync(HttpMethod.Put, url, Json(cuerpo));
    }

    // Sube la foto de un producto (multipart/form-data, campo "archivo", como en la web)
    public async Task SubirImagenAsync(string url, Stream contenido, string nombreArchivo)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new StreamContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        formulario.Add(archivo, "archivo", nombreArchivo);

        using var resp = await EnviarAsync(HttpMethod.Post, url, formulario);
    }

    // Prueba un servidor antes de guardarlo (pantalla Servidor)
    public async Task<(bool Ok, string Mensaje)> ProbarServidorAsync(string urlBase)
    {
        try
        {
            var reloj = Stopwatch.StartNew();
            using var resp = await _http.GetAsync(new Uri(new Uri(ConfiguracionApp.Normalizar(urlBase)), "api/tienda/medios-pago"));
            reloj.Stop();

            if (resp.IsSuccessStatusCode)
                return (true, $"Conectado correctamente ({reloj.ElapsedMilliseconds} ms).");
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return (false, "El servidor responde, pero no tiene la tienda de la app (falta publicar la versión nueva de la API).");
            return (false, $"El servidor respondió con un error ({(int)resp.StatusCode}).");
        }
        catch (TaskCanceledException)
        {
            return (false, "El servidor tardó demasiado en responder.");
        }
        catch (Exception)
        {
            return (false, "No se pudo conectar. Revisá la dirección y que la API esté funcionando.");
        }
    }

    private static HttpContent Json(object cuerpo) => JsonContent.Create(cuerpo, cuerpo.GetType());

    private static async Task<T?> LeerAsync<T>(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.NoContent || resp.Content.Headers.ContentLength == 0)
            return default;
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string url, HttpContent? contenido)
    {
        var token = await _sesion.ObtenerTokenAsync();
        using var pedido = new HttpRequestMessage(metodo, new Uri(new Uri(_config.UrlServidor), url)) { Content = contenido };
        if (token != null)
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(pedido);
        }
        catch (TaskCanceledException)
        {
            throw new ApiException("El servidor tardó demasiado en responder. Probá de nuevo en unos segundos.");
        }
        catch (Exception)
        {
            throw new ApiException($"No se pudo conectar con el servidor ({Servidor}). Revisá tu conexión a internet.");
        }

        if (resp.IsSuccessStatusCode) return resp;

        using (resp)
        {
            if (resp.StatusCode == HttpStatusCode.Unauthorized && token != null)
            {
                // Token vencido o inválido: se cierra la sesión y las pantallas protegidas mandan al login
                await _sesion.CerrarSesionAsync();
                throw new ApiException("Tu sesión venció. Volvé a ingresar.", resp.StatusCode);
            }
            if (resp.StatusCode == HttpStatusCode.Forbidden)
                throw new ApiException("Tu usuario no tiene permiso para realizar esta acción.", resp.StatusCode);

            throw new ApiException(await LeerMensajeErrorAsync(resp), resp.StatusCode);
        }
    }

    // La API devuelve los errores de tres formas: texto (BadRequest("...")),
    // { mensaje, codigoError } (errores inesperados) o el formato estándar de validación.
    private static async Task<string> LeerMensajeErrorAsync(HttpResponseMessage resp)
    {
        var texto = (await resp.Content.ReadAsStringAsync()).Trim();
        if (string.IsNullOrEmpty(texto))
            return resp.StatusCode == HttpStatusCode.NotFound
                ? "No se encontró lo que buscabas."
                : $"El servidor respondió con un error ({(int)resp.StatusCode}).";

        try
        {
            using var doc = JsonDocument.Parse(texto);
            var raiz = doc.RootElement;

            if (raiz.ValueKind == JsonValueKind.String)
                return raiz.GetString() ?? texto;

            if (raiz.ValueKind == JsonValueKind.Object)
            {
                if (raiz.TryGetProperty("mensaje", out var mensaje))
                {
                    var resultado = mensaje.GetString() ?? "Ocurrió un error inesperado.";
                    if (raiz.TryGetProperty("codigoError", out var codigo) && codigo.ValueKind == JsonValueKind.Number)
                        resultado += $" (código de error {codigo.GetInt32()})";
                    return resultado;
                }

                if (raiz.TryGetProperty("errors", out var errores) && errores.ValueKind == JsonValueKind.Object)
                {
                    var unido = string.Join(" ", errores.EnumerateObject()
                        .SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString()))
                        .Where(s => !string.IsNullOrEmpty(s)));
                    if (unido.Length > 0) return unido;
                }

                if (raiz.TryGetProperty("title", out var titulo))
                    return titulo.GetString() ?? texto;
            }
        }
        catch (JsonException)
        {
            // No era JSON: es el texto del BadRequest tal cual
        }

        return texto.Length > 300 ? texto[..300] : texto;
    }
}
