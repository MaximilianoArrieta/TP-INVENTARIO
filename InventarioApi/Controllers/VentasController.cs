using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioApi.Infrastructure.Data;
using InventarioApi.Infrastructure.Extensions;
using InventarioApi.Domain.Entities;

namespace InventarioApi.Api.Controllers;

// Punto de venta: siempre con usuario autenticado (Admin o Cajero).
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Cajero")]
public class VentasController : ControllerBase
{
    private readonly AppDbContext _db;
    public VentasController(AppDbContext db) => _db = db;

    // El precio NO lo manda el cliente: se toma del producto en la base.
    public record DetalleVentaDto(int ProductoId, int Cantidad);
    // ClienteId es opcional: si no se indica, la venta va a "Consumidor Final".
    public record VentaDto(int? ClienteId, List<DetalleVentaDto> Detalles);

    public class LineaVentaVista
    {
        public int ProductoId { get; set; }
        public string Producto { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class VentaVista
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public int ClienteId { get; set; }
        public string Cliente { get; set; } = "";
        public int UsuarioId { get; set; }
        public string Usuario { get; set; } = "";
        public decimal Total { get; set; }
        public List<LineaVentaVista> Detalles { get; set; } = new();
    }

    private int UsuarioActualId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    // El admin ve todas las ventas; un cajero, solo las suyas.
    private IQueryable<Venta> VentasVisibles()
    {
        var query = _db.Ventas.AsQueryable();
        if (!User.IsInRole("Admin"))
        {
            var id = UsuarioActualId;
            query = query.Where(v => v.UsuarioId == id);
        }
        return query;
    }

    // Proyección plana (sin ciclos) con el total calculado en la consulta.
    private static IQueryable<VentaVista> Proyectar(IQueryable<Venta> query) =>
        query.Select(v => new VentaVista
        {
            Id = v.Id,
            Fecha = v.Fecha,
            ClienteId = v.ClienteId,
            Cliente = v.Cliente!.Nombre + " " + v.Cliente.Apellido,
            UsuarioId = v.UsuarioId,
            Usuario = v.Usuario!.NombreUsuario,
            Total = v.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario),
            Detalles = v.Detalles.Select(d => new LineaVentaVista
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
        var query = VentasVisibles();
        if (desde.HasValue) query = query.Where(v => v.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(v => v.Fecha < hasta.Value.Date.AddDays(1));

        var resultado = await Proyectar(query.OrderByDescending(v => v.Fecha))
            .ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        var venta = await Proyectar(VentasVisibles().Where(v => v.Id == id)).FirstOrDefaultAsync();
        return venta == null ? NotFound() : Ok(venta);
    }

    [HttpPost]
    public async Task<IActionResult> RegistrarSalida(VentaDto dto)
    {
        if (dto.Detalles == null || dto.Detalles.Count == 0)
            return BadRequest("Debe incluir al menos un producto.");
        if (dto.Detalles.Any(d => d.Cantidad <= 0))
            return BadRequest("Las cantidades deben ser mayores a cero.");

        int clienteId;
        if (dto.ClienteId is int idCliente)
        {
            if (!await _db.Clientes.AnyAsync(c => c.Id == idCliente))
                return BadRequest($"Cliente {idCliente} no existe.");
            clienteId = idCliente;
        }
        else
        {
            clienteId = await DatosIniciales.ObtenerConsumidorFinalIdAsync(_db);
        }

        // Si el mismo producto viene en varias líneas, se suma en una sola.
        // Se ordenan por Id para bloquear siempre en el mismo orden (evita deadlocks entre cajas).
        var lineas = dto.Detalles
            .GroupBy(d => d.ProductoId)
            .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .OrderBy(l => l.ProductoId)
            .ToList();

        using var transaccion = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1) Leer cada producto BLOQUEANDO su fila hasta terminar la transacción (UPDLOCK).
            //    Así dos cajas no pueden vender a la vez la última unidad.
            var productos = new Dictionary<int, Producto>();
            foreach (var l in lineas)
            {
                var producto = await _db.Productos
                    .FromSqlInterpolated($"SELECT * FROM Productos WITH (UPDLOCK, ROWLOCK) WHERE Id = {l.ProductoId}")
                    .FirstOrDefaultAsync();

                if (producto == null || !producto.Activo)
                    return BadRequest($"Producto {l.ProductoId} no existe.");
                if (producto.Stock < l.Cantidad)
                    return BadRequest($"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.Stock}.");

                productos[l.ProductoId] = producto;
            }

            // 2) Recién con todo validado se descuenta el stock y se arma la venta
            var venta = new Venta
            {
                ClienteId = clienteId,
                UsuarioId = UsuarioActualId,
                Fecha = DateTime.UtcNow
            };

            foreach (var l in lineas)
            {
                var producto = productos[l.ProductoId];
                producto.Stock -= l.Cantidad; // SALIDA: resta stock

                venta.Detalles.Add(new DetalleVenta
                {
                    ProductoId = l.ProductoId,
                    Cantidad = l.Cantidad,
                    PrecioUnitario = producto.Precio // precio vigente al momento de la venta
                });
            }

            _db.Ventas.Add(venta);
            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();

            // Devuelve el "ticket" completo
            var ticket = await Proyectar(_db.Ventas.Where(v => v.Id == venta.Id)).FirstAsync();
            return CreatedAtAction(nameof(ObtenerPorId), new { id = venta.Id }, ticket);
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }
}
