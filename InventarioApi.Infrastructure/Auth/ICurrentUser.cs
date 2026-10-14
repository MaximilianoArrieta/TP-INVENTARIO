namespace InventarioApi.Infrastructure.Auth;

// Permite que la capa de datos sepa qué usuario está haciendo el pedido
// (para la auditoría) sin depender de ASP.NET Core.
public interface ICurrentUser
{
    int? UsuarioId { get; }
}
