using System.Security.Claims;
using InventarioApi.Api.Services;
using InventarioApi.Domain.Entities;
using InventarioApi.Infrastructure.Data;
using InventarioApi.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventarioApi.Api.Controllers;

// Tienda online de la app móvil: carrito, pago simulado y compras del cliente.
// El catálogo usa los endpoints públicos que ya existen: GET /api/productos y GET /api/categorias.
[ApiController]
[Route("api/[controller]")]
public class TiendaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly VentaService _ventas;

    public TiendaController(AppDbContext db, VentaService ventas)
    {
        _db = db;
        _ventas = ventas;
    }

    public record ItemCarritoDto(int ProductoId, int Cantidad);
    public record CheckoutDto(List<ItemCarritoDto> Items, DatosPago Pago);

    public class LineaCompraVista
    {
        public int ProductoId { get; set; }
        public string Producto { get; set; } = "";
        public string? ImagenUrl { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class CompraClienteVista
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public string? MedioPago { get; set; }
        public int Cuotas { get; set; }
        public string? UltimosDigitos { get; set; }
        public string? CodigoAutorizacion { get; set; }
        public List<LineaCompraVista> Detalles { get; set; } = new();
    }

    private int UsuarioId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("medios-pago")]
    [AllowAnonymous]
    public async Task<IActionResult> MediosPago() =>
        Ok(await _db.MediosPago.OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.Nombre, m.RequiereTarjeta, m.PermiteCuotas })
            .ToListAsync());

    // Confirmar la compra del carrito: valida stock, simula el pago y registra la venta.
    [HttpPost("checkout")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> Checkout(CheckoutDto dto)
    {
        var usuarioId = UsuarioId;
        var clienteId = await _db.Usuarios.Where(u => u.Id == usuarioId).Select(u => u.ClienteId).FirstOrDefaultAsync();
        if (clienteId == null)
            return BadRequest("Tu usuario no tiene una ficha de cliente asociada.");
        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest("El carrito está vacío.");
        if (dto.Pago == null)
            return BadRequest("Elegí un medio de pago.");

        var medio = await _db.MediosPago.FindAsync(dto.Pago.MedioPagoId);
        if (medio == null) return BadRequest("El medio de pago no existe.");

        var errorDatos = SimuladorPagos.Validar(medio, dto.Pago);
        if (errorDatos != null) return BadRequest(errorDatos);

        var resultado = await _ventas.RegistrarAsync(
            clienteId.Value, usuarioId,
            dto.Items.Select(i => new LineaPedido(i.ProductoId, i.Cantidad)),
            Venta.CanalApp,
            total => SimuladorPagos.Autorizar(medio, dto.Pago, total));

        if (resultado.PagoRechazado)
        {
            // El rechazo también queda auditado (la venta no se registró y el stock no cambió)
            var digitos = SimuladorPagos.SoloDigitos(dto.Pago.NumeroTarjeta);
            _db.Auditorias.Add(new Auditoria
            {
                UsuarioId = usuarioId,
                Accion = "PagoRechazado",
                Entidad = nameof(Pago),
                Detalle = $"{medio.Nombre}{(digitos.Length >= 4 ? $" terminada en {digitos[^4..]}" : "")}: {resultado.Error}"
            });
            await _db.SaveChangesAsync();
            return StatusCode(StatusCodes.Status402PaymentRequired, resultado.Error);
        }
        if (resultado.Venta == null)
            return BadRequest(resultado.Error);

        var compra = await Proyectar(_db.Ventas.Where(v => v.Id == resultado.Venta.Id)).FirstAsync();
        return CreatedAtAction(nameof(MiCompra), new { id = compra.Id }, compra);
    }

    [HttpGet("mis-compras")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> MisCompras([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var clienteId = await ClienteActualAsync();
        var resultado = await Proyectar(_db.Ventas.Where(v => v.ClienteId == clienteId).OrderByDescending(v => v.Fecha))
            .ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    [HttpGet("mis-compras/{id}")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> MiCompra(int id)
    {
        var clienteId = await ClienteActualAsync();
        var compra = await Proyectar(_db.Ventas.Where(v => v.Id == id && v.ClienteId == clienteId)).FirstOrDefaultAsync();
        return compra == null ? NotFound() : Ok(compra);
    }

    private async Task<int> ClienteActualAsync()
    {
        var usuarioId = UsuarioId;
        return await _db.Usuarios.Where(u => u.Id == usuarioId).Select(u => u.ClienteId ?? 0).FirstOrDefaultAsync();
    }

    private static IQueryable<CompraClienteVista> Proyectar(IQueryable<Venta> query) =>
        query.Select(v => new CompraClienteVista
        {
            Id = v.Id,
            Fecha = v.Fecha,
            Total = v.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario),
            MedioPago = v.Pago != null ? v.Pago.MedioPago!.Nombre : null,
            Cuotas = v.Pago != null ? v.Pago.Cuotas : 1,
            UltimosDigitos = v.Pago != null ? v.Pago.UltimosDigitos : null,
            CodigoAutorizacion = v.Pago != null ? v.Pago.CodigoAutorizacion : null,
            Detalles = v.Detalles.Select(d => new LineaCompraVista
            {
                ProductoId = d.ProductoId,
                Producto = d.Producto!.Nombre,
                ImagenUrl = d.Producto.ImagenUrl,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                Subtotal = d.Cantidad * d.PrecioUnitario
            }).ToList()
        });
}
