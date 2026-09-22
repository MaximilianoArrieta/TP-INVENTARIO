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
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriasController(AppDbContext db) => _db = db;

    public record CategoriaDto(string Nombre, string? Descripcion);

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Categorias.OrderBy(c => c.Nombre);
        var resultado = await query.ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        var categoria = await _db.Categorias.FindAsync(id);
        return categoria == null ? NotFound() : Ok(categoria);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Crear(CategoriaDto dto)
    {
        var categoria = new Categoria { Nombre = dto.Nombre, Descripcion = dto.Descripcion };
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObtenerPorId), new { id = categoria.Id }, categoria);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Actualizar(int id, CategoriaDto dto)
    {
        var categoria = await _db.Categorias.FindAsync(id);
        if (categoria == null) return NotFound();

        categoria.Nombre = dto.Nombre;
        categoria.Descripcion = dto.Descripcion;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var categoria = await _db.Categorias.FindAsync(id);
        if (categoria == null) return NotFound();

        var tieneProductos = await _db.Productos.AnyAsync(p => p.CategoriaId == id);
        if (tieneProductos)
            return BadRequest("No se puede eliminar: hay productos asociados a esta categoría.");

        _db.Categorias.Remove(categoria);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}