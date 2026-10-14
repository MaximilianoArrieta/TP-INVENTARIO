namespace InventarioApi.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Email { get; set; }   // para enviar reportes por correo
    public bool Activo { get; set; } = true;

    public int RolId { get; set; }
    public Rol? Rol { get; set; }
}
