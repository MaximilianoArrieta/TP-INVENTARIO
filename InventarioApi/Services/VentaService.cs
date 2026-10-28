using InventarioApi.Domain.Entities;
using InventarioApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventarioApi.Api.Services;

public record LineaPedido(int ProductoId, int Cantidad);

public record ResultadoVenta(Venta? Venta, string? Error, bool PagoRechazado = false);

// Resultado de autorizar un pago: si se aprueba trae el Pago a guardar; si no, el motivo.
public record ResultadoPago(bool Aprobado, string? Motivo, Pago? Pago);

// Registra una venta controlando el stock dentro de una transacción.
// Lo usa la tienda de la app móvil. (La caja web usa VentasController, que aplica
// la misma lógica y se dejó sin cambios para no afectar al TP web.)
public class VentaService
{
    private readonly AppDbContext _db;
    public VentaService(AppDbContext db) => _db = db;

    public async Task<ResultadoVenta> RegistrarAsync(
        int clienteId, int usuarioId, IEnumerable<LineaPedido> pedido, string canal,
        Func<decimal, ResultadoPago>? autorizarPago = null)
    {
        var lineas = pedido
            .GroupBy(l => l.ProductoId)
            .Select(g => new LineaPedido(g.Key, g.Sum(x => x.Cantidad)))
            .OrderBy(l => l.ProductoId) // siempre el mismo orden de bloqueo: evita deadlocks
            .ToList();

        if (lineas.Count == 0) return new(null, "El carrito está vacío.");
        if (lineas.Any(l => l.Cantidad <= 0)) return new(null, "Las cantidades deben ser mayores a cero.");

        using var transaccion = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1) Bloquear y validar el stock de cada producto
            var productos = new Dictionary<int, Producto>();
            foreach (var l in lineas)
            {
                var producto = await _db.Productos
                    .FromSqlInterpolated($"SELECT * FROM Productos WITH (UPDLOCK, ROWLOCK) WHERE Id = {l.ProductoId}")
                    .FirstOrDefaultAsync();

                if (producto == null || !producto.Activo)
                    return new(null, $"El producto {l.ProductoId} ya no está disponible.");
                if (producto.Stock < l.Cantidad)
                    return new(null, $"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.Stock}.");

                productos[l.ProductoId] = producto;
            }

            // 2) Con el stock reservado, se autoriza el pago (si corresponde)
            var total = lineas.Sum(l => l.Cantidad * productos[l.ProductoId].Precio);
            Pago? pago = null;
            if (autorizarPago != null)
            {
                var resultado = autorizarPago(total);
                if (!resultado.Aprobado)
                    return new(null, resultado.Motivo, PagoRechazado: true); // se deshace todo: no se toca el stock
                pago = resultado.Pago;
            }

            // 3) Registrar la venta y descontar el stock
            var venta = new Venta
            {
                ClienteId = clienteId,
                UsuarioId = usuarioId,
                Fecha = DateTime.UtcNow,
                Canal = canal,
                Pago = pago
            };
            foreach (var l in lineas)
            {
                var producto = productos[l.ProductoId];
                producto.Stock -= l.Cantidad;
                venta.Detalles.Add(new DetalleVenta
                {
                    ProductoId = l.ProductoId,
                    Cantidad = l.Cantidad,
                    PrecioUnitario = producto.Precio
                });
            }

            _db.Ventas.Add(venta);
            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();
            return new(venta, null);
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }
}
