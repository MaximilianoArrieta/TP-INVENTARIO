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
public class VentasController : ControllerBase
{
    private readonly AppDbContext _db;
    public VentasController(AppDbContext db) => _db = db;

    public record DetalleVentaDto(int ProductoId, int Cantidad, decimal PrecioUnitario);
    public record VentaDto(int ClienteId, List<DetalleVentaDto> Detalles);

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Ventas.Include(v => v.Cliente).Include(v => v.Detalles)
            .OrderByDescending(v => v.Fecha);
        var resultado = await query.ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    [HttpPost]
    public async Task<IActionResult> RegistrarSalida(VentaDto dto)
    {
        if (dto.Detalles == null || dto.Detalles.Count == 0)
            return BadRequest("Debe incluir al menos un producto.");

        var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        using var transaccion = await _db.Database.BeginTransactionAsync();
        try
        {
            var productosCache = new Dictionary<int, Producto>();
            foreach (var d in dto.Detalles)
            {
                var producto = await _db.Productos.FindAsync(d.ProductoId);
                if (producto == null)
                    return BadRequest($"Producto {d.ProductoId} no existe.");
                if (producto.Stock < d.Cantidad)
                    return BadRequest($"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.Stock}.");

                productosCache[d.ProductoId] = producto;
            }

            var venta = new Venta
            {
                ClienteId = dto.ClienteId,
                UsuarioId = usuarioId,
                Fecha = DateTime.UtcNow
            };

            decimal total = 0;
            foreach (var d in dto.Detalles)
            {
                var producto = productosCache[d.ProductoId];
                producto.Stock -= d.Cantidad; // SALIDA: resta stock
                total += d.Cantidad * d.PrecioUnitario;

                venta.Detalles.Add(new DetalleVenta
                {
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario
                });
            }
            venta.Total = total;

            _db.Ventas.Add(venta);
            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();

            return CreatedAtAction(nameof(Listar), new { id = venta.Id }, venta);
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }
}