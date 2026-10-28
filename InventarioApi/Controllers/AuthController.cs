using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioApi.Infrastructure.Data;
using InventarioApi.Infrastructure.Auth;
using InventarioApi.Domain.Entities;

namespace InventarioApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, TokenService tokenService, IConfiguration config)
    {
        _db = db; _tokenService = tokenService; _config = config;
    }

    public record LoginDto(string NombreUsuario, string Password);
    public record RegistroDto(string NombreUsuario, string Password, string? Email);
    public record RegistroClienteDto(string NombreUsuario, string Password, string Nombre, string Apellido,
        string? Email, string? Telefono, string? Direccion);

    // Registro público: SIEMPRE crea un Cajero.
    // Los administradores se crean desde /api/usuarios (solo Admin).
    [HttpPost("registro")]
    public async Task<IActionResult> Registro(RegistroDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NombreUsuario) || string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest("El usuario es obligatorio y la contraseña debe tener al menos 6 caracteres.");
        if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == dto.NombreUsuario))
            return BadRequest("El usuario ya existe.");

        var rol = await _db.Roles.FirstAsync(r => r.Nombre == "Cajero");
        var usuario = new Usuario
        {
            NombreUsuario = dto.NombreUsuario,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Email = dto.Email,
            RolId = rol.Id
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return Ok(new { usuario.Id, usuario.NombreUsuario, usuario.Email, Rol = rol.Nombre });
    }

    // Registro de compradores de la app móvil: crea su ficha de Cliente y un usuario con rol Cliente.
    [HttpPost("registro-cliente")]
    public async Task<IActionResult> RegistroCliente(RegistroClienteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NombreUsuario) || string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest("El usuario es obligatorio y la contraseña debe tener al menos 6 caracteres.");
        if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Apellido))
            return BadRequest("Completá tu nombre y apellido.");
        if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == dto.NombreUsuario))
            return BadRequest("Ese nombre de usuario ya está en uso.");

        var rol = await _db.Roles.FirstAsync(r => r.Nombre == "Cliente");
        var cliente = new Cliente
        {
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Email = dto.Email?.Trim(),
            Telefono = dto.Telefono?.Trim(),
            Direccion = dto.Direccion?.Trim()
        };
        var usuario = new Usuario
        {
            NombreUsuario = dto.NombreUsuario.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Email = dto.Email?.Trim(),
            RolId = rol.Id,
            Cliente = cliente
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return Ok(new { usuario.Id, usuario.NombreUsuario, usuario.Email, Rol = rol.Nombre, ClienteId = cliente.Id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.NombreUsuario == dto.NombreUsuario && u.Activo);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
        {
            // Los intentos fallidos también quedan auditados
            _db.Auditorias.Add(new Auditoria
            {
                Accion = "LoginFallido",
                Entidad = nameof(Usuario),
                EntidadId = usuario?.Id,
                Detalle = $"Usuario ingresado: {dto.NombreUsuario}"
            });
            await _db.SaveChangesAsync();
            return Unauthorized("Credenciales inválidas.");
        }

        _db.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuario.Id,
            Accion = "Login",
            Entidad = nameof(Usuario),
            EntidadId = usuario.Id
        });
        await _db.SaveChangesAsync();

        var token = _tokenService.GenerarToken(usuario);
        return Ok(new
        {
            token,
            usuario = usuario.NombreUsuario,
            rol = usuario.Rol!.Nombre,
            expira = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiresMinutes"]!))
        });
    }

    // Datos del usuario logueado (lo usa el frontend al iniciar)
    [HttpGet("yo")]
    [Authorize]
    public async Task<IActionResult> Yo()
    {
        var id = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var usuario = await _db.Usuarios
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.NombreUsuario, u.Email, Rol = u.Rol!.Nombre, u.Activo })
            .FirstOrDefaultAsync();
        return usuario == null ? NotFound() : Ok(usuario);
    }
}
