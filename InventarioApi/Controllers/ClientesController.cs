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
public class ClientesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ClientesController(AppDbContext db) => _db = db;

    public record ClienteDto(string Nombre, string Apellido, string? Email, string? Telefono, string? Direccion);

    // ?buscar= filtra por nombre, apellido o email (lo usa el punto de venta)
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? buscar = null)
    {
        var query = _db.Clientes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
            query = query.Where(c => c.Nombre.Contains(buscar) || c.Apellido.Contains(buscar) ||
                                     (c.Email != null && c.Email.Contains(buscar)));
        var resultado = await query.OrderBy(c => c.Apellido).ThenBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Nombre, c.Apellido, c.Email, c.Telefono, c.Direccion })
            .ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        var cliente = await _db.Clientes.Where(c => c.Id == id)
            .Select(c => new { c.Id, c.Nombre, c.Apellido, c.Email, c.Telefono, c.Direccion })
            .FirstOrDefaultAsync();
        return cliente == null ? NotFound() : Ok(cliente);
    }

    // El cajero también puede dar de alta un cliente desde la caja
    [HttpPost]
    [Authorize(Roles = "Admin,Cajero")]
    public async Task<IActionResult> Crear(ClienteDto dto)
    {
        var cliente = new Cliente
        {
            Nombre = dto.Nombre,
            Apellido = dto.Apellido,
            Email = dto.Email,
            Telefono = dto.Telefono,
            Direccion = dto.Direccion
        };
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObtenerPorId), new { id = cliente.Id },
            new { cliente.Id, cliente.Nombre, cliente.Apellido, cliente.Email, cliente.Telefono, cliente.Direccion });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Actualizar(int id, ClienteDto dto)
    {
        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente == null) return NotFound();

        cliente.Nombre = dto.Nombre;
        cliente.Apellido = dto.Apellido;
        cliente.Email = dto.Email;
        cliente.Telefono = dto.Telefono;
        cliente.Direccion = dto.Direccion;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente == null) return NotFound();

        var tieneVentas = await _db.Ventas.AnyAsync(v => v.ClienteId == id);
        if (tieneVentas)
            return BadRequest("No se puede eliminar: tiene ventas registradas.");

        _db.Clientes.Remove(cliente);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}