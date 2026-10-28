namespace InventarioApi.Domain.Entities;

// El total NO se guarda (3FN): se calcula sumando Cantidad x PrecioUnitario de los detalles.
public class Venta
{
    public const string CanalCaja = "Caja";
    public const string CanalApp = "App";

    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    // Desde dónde se hizo la venta: la caja web o la app móvil
    public string Canal { get; set; } = CanalCaja;

    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();

    // Solo las ventas de la app tienen pago registrado
    public Pago? Pago { get; set; }
}
