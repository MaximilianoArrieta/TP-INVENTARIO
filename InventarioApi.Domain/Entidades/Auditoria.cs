namespace InventarioApi.Domain.Entities;

public class Auditoria
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Accion { get; set; } = string.Empty;   // Crear, Modificar, Eliminar, Login
    public string Entidad { get; set; } = string.Empty;  // Producto, Venta, Compra...
    public int? EntidadId { get; set; }
    public string? Detalle { get; set; }
}
