using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace InventarioApi.Web.Auth;

// Guarda el token JWT en el navegador (localStorage) y le dice a Blazor
// quién está logueado y con qué rol, leyendo los datos que vienen dentro del token.
public class AuthProvider : AuthenticationStateProvider
{
    private const string Clave = "inventario.token";
    private static readonly AuthenticationState Anonimo = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly IJSRuntime _js;
    private string? _token;
    private bool _cargado;

    public AuthProvider(IJSRuntime js) => _js = js;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await ObtenerTokenAsync();
        return token == null ? Anonimo : new AuthenticationState(CrearUsuario(token));
    }

    public async Task<string?> ObtenerTokenAsync()
    {
        if (!_cargado)
        {
            try { _token = await _js.InvokeAsync<string?>("localStorage.getItem", Clave); }
            catch { _token = null; }
            _cargado = true;
        }

        // Un token vencido es lo mismo que no tener sesión
        if (_token != null && Vencido(_token))
            await CerrarSesionAsync(notificar: false);

        return _token;
    }

    public async Task IniciarSesionAsync(string token)
    {
        _token = token;
        _cargado = true;
        await _js.InvokeVoidAsync("localStorage.setItem", Clave, token);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CrearUsuario(token))));
    }

    public async Task CerrarSesionAsync(bool notificar = true)
    {
        _token = null;
        _cargado = true;
        try { await _js.InvokeVoidAsync("localStorage.removeItem", Clave); } catch { }
        if (notificar) NotifyAuthenticationStateChanged(Task.FromResult(Anonimo));
    }

    // ---- Lectura del token ----

    private static Dictionary<string, JsonElement>? LeerDatos(string token)
    {
        var partes = token.Split('.');
        if (partes.Length < 2) return null;

        // La parte del medio es JSON en Base64 "url-safe"
        var b64 = partes[1].Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static bool Vencido(string token)
    {
        var datos = LeerDatos(token);
        if (datos == null || !datos.TryGetValue("exp", out var exp) || !exp.TryGetInt64(out var segundos))
            return true;
        return DateTimeOffset.FromUnixTimeSeconds(segundos) <= DateTimeOffset.UtcNow;
    }

    private static ClaimsPrincipal CrearUsuario(string token)
    {
        var datos = LeerDatos(token);
        if (datos == null) return Anonimo.User;

        var claims = new List<Claim>();
        foreach (var (clave, valor) in datos)
        {
            // La API escribe los claims con nombres cortos (role, unique_name, nameid)
            var tipo = clave switch
            {
                "role" or "roles" => ClaimTypes.Role,
                "unique_name" or "name" => ClaimTypes.Name,
                "nameid" or "sub" => ClaimTypes.NameIdentifier,
                _ => clave
            };

            if (valor.ValueKind == JsonValueKind.Array)
                foreach (var v in valor.EnumerateArray()) claims.Add(new Claim(tipo, v.ToString()));
            else
                claims.Add(new Claim(tipo, valor.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role));
    }
}
