using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InventarioApi.Web.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace InventarioApi.Web.Services;

public class ApiException : Exception
{
    public HttpStatusCode? Estado { get; }
    public ApiException(string mensaje, HttpStatusCode? estado = null) : base(mensaje) => Estado = estado;
}

// Único punto de contacto con la API: agrega el token a cada pedido
// y convierte las respuestas de error en mensajes legibles.
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthProvider _auth;
    private readonly NavigationManager _nav;

    public ApiClient(HttpClient http, AuthProvider auth, NavigationManager nav)
    {
        _http = http;
        _auth = auth;
        _nav = nav;
    }

    // Las imágenes de productos las sirve la API (wwwroot/uploads)
    public string UrlArchivo(string ruta) => new Uri(_http.BaseAddress!, ruta.TrimStart('/')).ToString();

    public async Task<T?> GetAsync<T>(string url)
    {
        using var resp = await EnviarAsync(HttpMethod.Get, url, null);
        return await LeerAsync<T>(resp);
    }

    // Para descargar archivos binarios (los reportes PDF)
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

    public async Task PostAsync(string url, object cuerpo)
    {
        using var resp = await EnviarAsync(HttpMethod.Post, url, Json(cuerpo));
    }

    public async Task PutAsync(string url, object cuerpo)
    {
        using var resp = await EnviarAsync(HttpMethod.Put, url, Json(cuerpo));
    }

    public async Task DeleteAsync(string url)
    {
        using var resp = await EnviarAsync(HttpMethod.Delete, url, null);
    }

    public async Task SubirArchivoAsync(string url, IBrowserFile archivo, long tamanioMaximo = 5 * 1024 * 1024)
    {
        var contenido = new MultipartFormDataContent();
        var archivoContenido = new StreamContent(archivo.OpenReadStream(tamanioMaximo));
        archivoContenido.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrEmpty(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType);
        contenido.Add(archivoContenido, "archivo", archivo.Name);

        using var resp = await EnviarAsync(HttpMethod.Post, url, contenido);
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
        var token = await _auth.ObtenerTokenAsync();
        using var pedido = new HttpRequestMessage(metodo, url) { Content = contenido };
        if (token != null)
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(pedido);
        }
        catch (HttpRequestException)
        {
            throw new ApiException("No se pudo conectar con el servidor. Revisá que la API esté funcionando.");
        }

        if (resp.IsSuccessStatusCode) return resp;

        using (resp)
        {
            if (resp.StatusCode == HttpStatusCode.Unauthorized && token != null)
            {
                await _auth.CerrarSesionAsync();
                _nav.NavigateTo("login?expirada=1", replace: true);
                throw new ApiException("La sesión venció. Volvé a ingresar.", resp.StatusCode);
            }
            if (resp.StatusCode == HttpStatusCode.Forbidden)
                throw new ApiException("No tenés permiso para realizar esta acción.", resp.StatusCode);

            throw new ApiException(await LeerMensajeErrorAsync(resp), resp.StatusCode);
        }
    }

    // La API devuelve los errores de tres formas: texto plano (BadRequest("...")),
    // { mensaje, codigoError } (errores inesperados) o el formato estándar de validación.
    private static async Task<string> LeerMensajeErrorAsync(HttpResponseMessage resp)
    {
        var texto = (await resp.Content.ReadAsStringAsync()).Trim();
        if (string.IsNullOrEmpty(texto))
            return $"El servidor respondió con un error ({(int)resp.StatusCode}).";

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
                    var lista = errores.EnumerateObject()
                        .SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString()))
                        .Where(s => !string.IsNullOrEmpty(s));
                    var unido = string.Join(" ", lista);
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
