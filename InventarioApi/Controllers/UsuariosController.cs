using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioApi.Infrastructure.Data;
using InventarioApi.Infrastructure.Extensions;
using InventarioApi.Domain.Entities;

namespace InventarioApi.Api.Controllers;

// Gestión de usuarios y roles: solo administradores.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsuariosController(AppDbContext db) => _db = db;

    public record UsuarioCrearDto(string NombreUsuario, string Password, string? Email, int RolId);
    public record CambiarRolDto(int RolId);
    public record CambiarEstadoDto(bool Activo);

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Usuarios
            .OrderBy(u => u.NombreUsuario)
            .Select(u => new { u.Id, u.NombreUsuario, u.Email, u.RolId, Rol = u.Rol!.Nombre, u.Activo });
        return Ok(await query.ToPagedResultAsync(pageNumber, pageSize));
    }

    [HttpGet("roles")]
    public async Task<IActionResult> Roles() =>
        Ok(await _db.Roles.OrderBy(r => r.Id).Select(r => new { r.Id, r.Nombre }).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Crear(UsuarioCrearDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NombreUsuario) || string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest("El usuario es obligatorio y la contraseña debe tener al menos 6 caracteres.");
        if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == dto.NombreUsuario))
            return BadRequest("El usuario ya existe.");
        var rol = await _db.Roles.FindAsync(dto.RolId);
        if (rol == null) return BadRequest($"El rol {dto.RolId} no existe.");

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

    [HttpPut("{id}/rol")]
    public async Task<IActionResult> CambiarRol(int id, CambiarRolDto dto)
    {
        var usuario = await _db.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == id);
        if (usuario == null) return NotFound();
        var rol = await _db.Roles.FindAsync(dto.RolId);
        if (rol == null) return BadRequest($"El rol {dto.RolId} no existe.");

        if (usuario.Rol!.Nombre == "Admin" && rol.Nombre != "Admin" && await QuedaUnSoloAdminAsync())
            return BadRequest("No se puede quitar el rol al último administrador activo.");

        usuario.RolId = rol.Id;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoDto dto)
    {
        var usuario = await _db.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == id);
        if (usuario == null) return NotFound();

        var miId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (!dto.Activo && usuario.Id == miId)
            return BadRequest("No podés desactivar tu propio usuario.");
        if (!dto.Activo && usuario.Rol!.Nombre == "Admin" && await QuedaUnSoloAdminAsync())
            return BadRequest("No se puede desactivar al último administrador activo.");

        usuario.Activo = dto.Activo;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> QuedaUnSoloAdminAsync() =>
        await _db.Usuarios.CountAsync(u => u.Rol!.Nombre == "Admin" && u.Activo) <= 1;
}
