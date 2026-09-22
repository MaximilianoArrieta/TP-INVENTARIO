using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioApi.Infrastructure.Data;
using InventarioApi.Infrastructure.Extensions;
using InventarioApi.Domain.Entities;

namespace InventarioApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProveedoresController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProveedoresController(AppDbContext db) => _db = db;

    public record ProveedorDto(string RazonSocial, string? Contacto, string? Telefono, string? Email, string? Direccion);

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Proveedores.OrderBy(p => p.RazonSocial);
        var resultado = await query.ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        var proveedor = await _db.Proveedores.FindAsync(id);
        return proveedor == null ? NotFound() : Ok(proveedor);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Crear(ProveedorDto dto)
    {
        var proveedor = new Proveedor
        {
            RazonSocial = dto.RazonSocial,
            Contacto = dto.Contacto,
            Telefono = dto.Telefono,
            Email = dto.Email,
            Direccion = dto.Direccion
        };
        _db.Proveedores.Add(proveedor);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObtenerPorId), new { id = proveedor.Id }, proveedor);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Actualizar(int id, ProveedorDto dto)
    {
        var proveedor = await _db.Proveedores.FindAsync(id);
        if (proveedor == null) return NotFound();

        proveedor.RazonSocial = dto.RazonSocial;
        proveedor.Contacto = dto.Contacto;
        proveedor.Telefono = dto.Telefono;
        proveedor.Email = dto.Email;
        proveedor.Direccion = dto.Direccion;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var proveedor = await _db.Proveedores.FindAsync(id);
        if (proveedor == null) return NotFound();

        var tieneCompras = await _db.Compras.AnyAsync(c => c.ProveedorId == id);
        if (tieneCompras)
            return BadRequest("No se puede eliminar: tiene compras registradas.");

        _db.Proveedores.Remove(proveedor);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}