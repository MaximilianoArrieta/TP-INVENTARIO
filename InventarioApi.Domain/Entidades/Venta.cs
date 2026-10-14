namespace InventarioApi.Domain.Entities;

// El total NO se guarda (3FN): se calcula sumando Cantidad x PrecioUnitario de los detalles.
public class Venta
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
}
