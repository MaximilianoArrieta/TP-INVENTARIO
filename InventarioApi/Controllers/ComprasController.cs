using System.Security.Claims;
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
public class ComprasController : ControllerBase
{
    private readonly AppDbContext _db;
    public ComprasController(AppDbContext db) => _db = db;

    public record DetalleCompraDto(int ProductoId, int Cantidad, decimal PrecioUnitario);
    public record CompraDto(int ProveedorId, List<DetalleCompraDto> Detalles);

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Compras.Include(c => c.Proveedor).Include(c => c.Detalles)
            .OrderByDescending(c => c.Fecha);
        var resultado = await query.ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    [HttpPost]
    public async Task<IActionResult> RegistrarIngreso(CompraDto dto)
    {
        if (dto.Detalles == null || dto.Detalles.Count == 0)
            return BadRequest("Debe incluir al menos un producto.");

        var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        using var transaccion = await _db.Database.BeginTransactionAsync();
        try
        {
            var compra = new Compra
            {
                ProveedorId = dto.ProveedorId,
                UsuarioId = usuarioId,
                Fecha = DateTime.UtcNow
            };

            decimal total = 0;
            foreach (var d in dto.Detalles)
            {
                var producto = await _db.Productos.FindAsync(d.ProductoId);
                if (producto == null) return BadRequest($"Producto {d.ProductoId} no existe.");

                producto.Stock += d.Cantidad; // INGRESO: suma stock
                total += d.Cantidad * d.PrecioUnitario;

                compra.Detalles.Add(new DetalleCompra
                {
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario
                });
            }
            compra.Total = total;

            _db.Compras.Add(compra);
            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();

            return CreatedAtAction(nameof(Listar), new { id = compra.Id }, compra);
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }
}