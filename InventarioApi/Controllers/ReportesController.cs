using System.Security.Claims;
using InventarioApi.Api.Services;
using InventarioApi.Domain.Entities;
using InventarioApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventarioApi.Api.Controllers;

// Reportes en PDF: solo administradores.
//   GET  /api/reportes/stock
//   GET  /api/reportes/ventas?desde=2026-10-01&hasta=2026-10-31
//   GET  /api/reportes/ingresos?desde=...&hasta=...
//   POST /api/reportes/{tipo}/enviar   { "email": "...", "desde": "...", "hasta": "..." }
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ReportesController : ControllerBase
{
    private readonly ReportesService _reportes;
    private readonly CorreoService _correo;
    private readonly AppDbContext _db;

    public ReportesController(ReportesService reportes, CorreoService correo, AppDbContext db)
    {
        _reportes = reportes;
        _correo = correo;
        _db = db;
    }

    public record EnviarReporteDto(string? Email, DateTime? Desde, DateTime? Hasta);

    private int UsuarioId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string NombreUsuario => User.Identity?.Name ?? "—";

    [HttpGet("{tipo}")]
    public async Task<IActionResult> Generar(string tipo, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        tipo = tipo.ToLowerInvariant();
        if (!ReportesService.Tipos.Contains(tipo))
            return NotFound("Ese reporte no existe. Opciones: stock, ventas, ingresos.");

        var (d, h) = _reportes.NormalizarPeriodo(desde, hasta);
        if (d > h) return BadRequest("La fecha 'desde' no puede ser posterior a 'hasta'.");

        var reporte = await _reportes.GenerarAsync(tipo, d, h, NombreUsuario);
        await AuditarAsync("GenerarReporte", DescribirReporte(tipo, reporte.Titulo, d, h));

        return File(reporte.Pdf, "application/pdf", reporte.NombreArchivo);
    }

    [HttpPost("{tipo}/enviar")]
    public async Task<IActionResult> Enviar(string tipo, EnviarReporteDto dto)
    {
        tipo = tipo.ToLowerInvariant();
        if (!ReportesService.Tipos.Contains(tipo))
            return NotFound("Ese reporte no existe. Opciones: stock, ventas, ingresos.");
        if (!_correo.Configurado)
            return BadRequest("El envío de correo no está configurado en el servidor (falta completar la sección \"Correo\").");

        // Si no se indica destinatario, se usa el email del usuario logueado
        var email = dto.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            var id = UsuarioId;
            email = await _db.Usuarios.Where(u => u.Id == id).Select(u => u.Email).FirstOrDefaultAsync();
        }
        if (!CorreoService.EsEmailValido(email))
            return BadRequest("Indicá un email válido para enviar el reporte.");

        var (d, h) = _reportes.NormalizarPeriodo(dto.Desde, dto.Hasta);
        if (d > h) return BadRequest("La fecha 'desde' no puede ser posterior a 'hasta'.");

        var reporte = await _reportes.GenerarAsync(tipo, d, h, NombreUsuario);
        var detalle = DescribirReporte(tipo, reporte.Titulo, d, h);
        var cuerpo = $"""
            <p>Hola,</p>
            <p>Te enviamos el <strong>{reporte.Titulo.ToLowerInvariant()}</strong> generado desde {_reportes.Negocio}.</p>
            <p>{(tipo == "stock" ? "Refleja las existencias al momento de la emisión." : detalle)}</p>
            <p>Lo encontrás adjunto en PDF.</p>
            """;

        try
        {
            await _correo.EnviarAsync(email!, $"{reporte.Titulo} - {_reportes.Negocio}", cuerpo, reporte.Pdf, reporte.NombreArchivo);
        }
        catch (Exception ex)
        {
            // Se vuelve a lanzar con un mensaje claro: el middleware lo guarda en LogErrores
            throw new InvalidOperationException($"No se pudo enviar el correo a {email}: {ex.Message}", ex);
        }

        await AuditarAsync("EnviarReporte", $"{detalle} enviado a {email}");
        return Ok(new { mensaje = $"Reporte enviado a {email}." });
    }

    private static string DescribirReporte(string tipo, string titulo, DateTime desde, DateTime hasta) =>
        tipo == "stock" ? titulo : $"{titulo} del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";

    private async Task AuditarAsync(string accion, string detalle)
    {
        _db.Auditorias.Add(new Auditoria
        {
            UsuarioId = UsuarioId,
            Accion = accion,
            Entidad = "Reporte",
            Detalle = detalle
        });
        await _db.SaveChangesAsync();
    }
}
