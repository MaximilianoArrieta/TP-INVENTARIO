namespace InventarioApi.Domain.Entities;

public class LogError
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Mensaje { get; set; } = string.Empty;
    public string? Detalle { get; set; }   // stack trace
    public string? Ruta { get; set; }      // endpoint donde ocurrió
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}
