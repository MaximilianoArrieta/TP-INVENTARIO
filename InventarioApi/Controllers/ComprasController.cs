using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioApi.Infrastructure.Data;
using InventarioApi.Infrastructure.Extensions;
using InventarioApi.Domain.Entities;

namespace InventarioApi.Api.Controllers;

// Ingreso de mercadería: SOLO administradores.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ComprasController : ControllerBase
{
    private readonly AppDbContext _db;
    public ComprasController(AppDbContext db) => _db = db;

    // En las compras sí se informa el precio: es el costo que cobra el proveedor.
    public record DetalleCompraDto(int ProductoId, int Cantidad, decimal PrecioUnitario);
    public record CompraDto(int ProveedorId, List<DetalleCompraDto> Detalles);

    public class LineaCompraVista
    {
        public int ProductoId { get; set; }
        public string Producto { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class CompraVista
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public int ProveedorId { get; set; }
        public string Proveedor { get; set; } = "";
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = "";
        public decimal Total { get; set; }
        public List<LineaCompraVista> Detalles { get; set; } = new();
    }

    private static IQueryable<CompraVista> Proyectar(IQueryable<Compra> query) =>
        query.Select(c => new CompraVista
        {
            Id = c.Id,
            Fecha = c.Fecha,
            ProveedorId = c.ProveedorId,
            Proveedor = c.Proveedor!.RazonSocial,
            UsuarioId = c.UsuarioId,
            Usuario = c.Usuario!.NombreUsuario,
            Total = c.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario),
            Detalles = c.Detalles.Select(d => new LineaCompraVista
            {
                ProductoId = d.ProductoId,
                Producto = d.Producto!.Nombre,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                Subtotal = d.Cantidad * d.PrecioUnitario
            }).ToList()
        });

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null)
    {
        var query = _db.Compras.AsQueryable();
        if (desde.HasValue) query = query.Where(c => c.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(c => c.Fecha < hasta.Value.Date.AddDays(1));

        var resultado = await Proyectar(query.OrderByDescending(c => c.Fecha))
            .ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        var compra = await Proyectar(_db.Compras.Where(c => c.Id == id)).FirstOrDefaultAsync();
        return compra == null ? NotFound() : Ok(compra);
    }

    [HttpPost]
    public async Task<IActionResult> RegistrarIngreso(CompraDto dto)
    {
        if (dto.Detalles == null || dto.Detalles.Count == 0)
            return BadRequest("Debe incluir al menos un producto.");
        if (dto.Detalles.Any(d => d.Cantidad <= 0 || d.PrecioUnitario < 0))
            return BadRequest("Las cantidades deben ser mayores a cero y los precios no pueden ser negativos.");
        if (!await _db.Proveedores.AnyAsync(p => p.Id == dto.ProveedorId))
            return BadRequest($"Proveedor {dto.ProveedorId} no existe.");

        var lineas = dto.Detalles
            .GroupBy(d => d.ProductoId)
            .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(x => x.Cantidad), PrecioUnitario = g.First().PrecioUnitario })
            .OrderBy(l => l.ProductoId)
            .ToList();

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

            foreach (var l in lineas)
            {
                // Bloqueo de fila: evita que dos operaciones simultáneas pisen el stock
                var producto = await _db.Productos
                    .FromSqlInterpolated($"SELECT * FROM Productos WITH (UPDLOCK, ROWLOCK) WHERE Id = {l.ProductoId}")
                    .FirstOrDefaultAsync();
                if (producto == null) return BadRequest($"Producto {l.ProductoId} no existe.");

                producto.Stock += l.Cantidad; // INGRESO: suma stock

                compra.Detalles.Add(new DetalleCompra
                {
                    ProductoId = l.ProductoId,
                    Cantidad = l.Cantidad,
                    PrecioUnitario = l.PrecioUnitario
                });
            }

            _db.Compras.Add(compra);
            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();

            var resultado = await Proyectar(_db.Compras.Where(c => c.Id == compra.Id)).FirstAsync();
            return CreatedAtAction(nameof(ObtenerPorId), new { id = compra.Id }, resultado);
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }
}
