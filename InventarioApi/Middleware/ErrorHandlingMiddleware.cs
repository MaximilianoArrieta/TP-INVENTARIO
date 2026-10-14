using System.Security.Claims;
using InventarioApi.Domain.Entities;
using InventarioApi.Infrastructure.Data;

namespace InventarioApi.Api.Middleware;

// Atrapa cualquier error no controlado, lo guarda en la tabla LogErrores
// y le devuelve al cliente un mensaje prolijo con el número de error.
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var ruta = $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}";
            _logger.LogError(ex, "Error no controlado en {Ruta}", ruta);

            int? codigo = null;
            try
            {
                // Contexto nuevo: el del pedido puede haber quedado en un estado inválido
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var idTexto = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var log = new LogError
                {
                    Fecha = DateTime.UtcNow,
                    Mensaje = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message,
                    Detalle = ex.ToString(),
                    Ruta = ruta,
                    UsuarioId = int.TryParse(idTexto, out var id) ? id : null
                };
                db.LogErrores.Add(log);
                await db.SaveChangesAsync();
                codigo = log.Id;
            }
            catch (Exception exLog)
            {
                _logger.LogError(exLog, "No se pudo guardar el error en la base de datos");
            }

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                mensaje = "Ocurrió un error inesperado. Quedó registrado para su revisión.",
                codigoError = codigo
            });
        }
    }
}
