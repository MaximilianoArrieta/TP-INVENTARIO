namespace InventarioApi.Domain.Entities;

// El total NO se guarda (3FN): se calcula sumando Cantidad x PrecioUnitario de los detalles.
public class Compra
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public ICollection<DetalleCompra> Detalles { get; set; } = new List<DetalleCompra>();
}
