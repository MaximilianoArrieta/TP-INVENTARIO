using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventarioApi.Infrastructure.Data;
using InventarioApi.Infrastructure.Extensions;

namespace InventarioApi.Api.Controllers;

// Consulta de la auditoría y del registro de errores: solo administradores.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AuditoriaController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuditoriaController(AppDbContext db) => _db = db;

    // GET /api/auditoria?entidad=Producto&accion=Modificar&usuarioId=1&desde=2026-10-01&hasta=2026-10-31
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? entidad = null, [FromQuery] string? accion = null,
        [FromQuery] int? usuarioId = null, [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null)
    {
        var query = _db.Auditorias.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entidad)) query = query.Where(a => a.Entidad == entidad);
        if (!string.IsNullOrWhiteSpace(accion)) query = query.Where(a => a.Accion == accion);
        if (usuarioId.HasValue) query = query.Where(a => a.UsuarioId == usuarioId);
        if (desde.HasValue) query = query.Where(a => a.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(a => a.Fecha < hasta.Value.Date.AddDays(1));

        var resultado = await query
            .OrderByDescending(a => a.Id)
            .Select(a => new
            {
                a.Id, a.Fecha, a.UsuarioId,
                Usuario = a.Usuario != null ? a.Usuario.NombreUsuario : null,
                a.Accion, a.Entidad, a.EntidadId, a.Detalle
            })
            .ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    [HttpGet("errores")]
    public async Task<IActionResult> Errores([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var resultado = await _db.LogErrores
            .OrderByDescending(l => l.Id)
            .Select(l => new
            {
                l.Id, l.Fecha, l.Ruta, l.Mensaje, l.Detalle, l.UsuarioId,
                Usuario = l.Usuario != null ? l.Usuario.NombreUsuario : null
            })
            .ToPagedResultAsync(pageNumber, pageSize);
        return Ok(resultado);
    }

    // Genera un error a propósito para demostrar que el registro funciona.
    [HttpGet("errores/prueba")]
    public IActionResult ProbarError() =>
        throw new InvalidOperationException("Error de prueba generado a propósito para verificar el registro de errores.");
}
