using System.Security.Claims;
using InventarioApi.Infrastructure.Auth;

namespace InventarioApi.Api.Services;

// Lee el id del usuario desde el token JWT del pedido actual.
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public int? UsuarioId
    {
        get
        {
            var valor = _accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(valor, out var id) ? id : null;
        }
    }
}
