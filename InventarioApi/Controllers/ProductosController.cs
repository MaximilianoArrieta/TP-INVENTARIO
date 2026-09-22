using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioApi.Infrastructure.Data;
using InventarioApi.Infrastructure.Extensions;
using InventarioApi.Domain.Entities;
using InventarioApi.Domain.Common;

namespace InventarioApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductosController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductosController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<object>>> Listar(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
        [FromQuery] int? categoriaId = null, [FromQuery] string? nombre = null)
    {
        var query = _db.Productos.Include(p => p.Categoria).Where(p => p.Activo).AsQueryable();

        if (categoriaId.HasValue)
            query = query.Where(p => p.CategoriaId == categoriaId);
        if (!string.IsNullOrWhiteSpace(nombre))
            query = query.Where(p => p.Nombre.Contains(nombre));

        var proyectado = query.OrderBy(p => p.Nombre).Select(p => new
        {
            p.Id,
            p.Nombre,
            p.Descripcion,
            p.Precio,
            p.Stock,
            p.ImagenUrl,
            Categoria = p.Categoria!.Nombre
        });

        var resultado = await proyectado.ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        var producto = await _db.Productos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id);
        return producto == null ? NotFound() : Ok(producto);
    }

    public record ProductoDto(string Nombre, string? Descripcion, decimal Precio, int CategoriaId);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Crear(ProductoDto dto)
    {
        var producto = new Producto
        {
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Precio = dto.Precio,
            CategoriaId = dto.CategoriaId,
            Stock = 0
        };
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObtenerPorId), new { id = producto.Id }, producto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Actualizar(int id, ProductoDto dto)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto == null) return NotFound();

        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.Precio = dto.Precio;
        producto.CategoriaId = dto.CategoriaId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto == null) return NotFound();
        producto.Activo = false; // baja lógica
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/imagen")]
    [Authorize]
    public async Task<IActionResult> SubirImagen(int id, IFormFile archivo)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto == null) return NotFound();

        if (archivo == null || archivo.Length == 0) return BadRequest("Archivo inválido.");

        var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!extensionesPermitidas.Contains(ext))
            return BadRequest("Formato de imagen no soportado.");

        var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(carpeta);

        var nombreArchivo = $"{Guid.NewGuid()}{ext}";
        var rutaCompleta = Path.Combine(carpeta, nombreArchivo);

        using (var stream = new FileStream(rutaCompleta, FileMode.Create))
        {
            await archivo.CopyToAsync(stream);
        }

        producto.ImagenUrl = $"/uploads/{nombreArchivo}";
        await _db.SaveChangesAsync();

        return Ok(new { producto.Id, producto.ImagenUrl });
    }
}