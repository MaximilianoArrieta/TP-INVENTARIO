using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace InventarioApi.Movil.Services;

// Guarda el token JWT en el almacenamiento seguro del celular (SecureStorage, cifrado por Android)
// y le dice a Blazor quién está logueado y con qué rol, leyendo los datos del token.
public class SesionUsuario : AuthenticationStateProvider
{
    private const string Clave = "sesion.token";
    private static readonly AuthenticationState Anonimo = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private string? _token;
    private bool _cargado;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await ObtenerTokenAsync();
        return token == null ? Anonimo : new AuthenticationState(CrearUsuario(token));
    }

    public async Task<string?> ObtenerTokenAsync()
    {
        if (!_cargado)
        {
            try
            {
                _token = await SecureStorage.Default.GetAsync(Clave);
            }
            catch
            {
                // Puede fallar si se reinstaló la app (cambió la clave de cifrado): se empieza sin sesión
                _token = null;
                SecureStorage.Default.Remove(Clave);
            }
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
        try { await SecureStorage.Default.SetAsync(Clave, token); }
        catch { /* si no se puede guardar, la sesión dura hasta cerrar la app */ }
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CrearUsuario(token))));
    }

    public Task CerrarSesionAsync(bool notificar = true)
    {
        _token = null;
        _cargado = true;
        SecureStorage.Default.Remove(Clave);
        if (notificar) NotifyAuthenticationStateChanged(Task.FromResult(Anonimo));
        return Task.CompletedTask;
    }

    // ---- Lectura del token (la parte del medio es JSON en Base64) ----

    private static Dictionary<string, JsonElement>? LeerDatos(string token)
    {
        var partes = token.Split('.');
        if (partes.Length < 2) return null;

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
