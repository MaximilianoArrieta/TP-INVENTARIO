using System.Globalization;
using InventarioApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventarioApi.Api.Services;

public record ReporteGenerado(byte[] Pdf, string NombreArchivo, string Titulo);

// Arma los reportes en PDF (stock, ventas e ingresos) con QuestPDF.
public class ReportesService
{
    public static readonly string[] Tipos = { "stock", "ventas", "ingresos" };

    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("es-AR");
    private const string Tinta = "#182028";
    private const string TintaSuave = "#5B6772";
    private const string Linea = "#D3D9DE";
    private const string Fondo = "#F2F4F6";
    private const string Aviso = "#8A5A00";
    private const string Peligro = "#B42318";

    private readonly AppDbContext _db;
    private readonly int _diferenciaHoras;
    private readonly string _negocio;

    public ReportesService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        // Las fechas se guardan en UTC; Argentina es UTC-3
        _diferenciaHoras = config.GetValue("Reportes:DiferenciaHorariaHoras", -3);
        _negocio = config["Reportes:NombreNegocio"] ?? "Caja y Stock";
    }

    public string Negocio => _negocio;

    // Si no se indica período: desde el primer día del mes hasta hoy (hora local)
    public (DateTime Desde, DateTime Hasta) NormalizarPeriodo(DateTime? desde, DateTime? hasta)
    {
        var hoy = ALocal(DateTime.UtcNow).Date;
        return ((desde ?? new DateTime(hoy.Year, hoy.Month, 1)).Date, (hasta ?? hoy).Date);
    }

    public Task<ReporteGenerado> GenerarAsync(string tipo, DateTime desde, DateTime hasta, string emitidoPor) => tipo switch
    {
        "stock" => StockAsync(emitidoPor),
        "ventas" => VentasAsync(desde, hasta, emitidoPor),
        "ingresos" => IngresosAsync(desde, hasta, emitidoPor),
        _ => throw new ArgumentException($"Reporte desconocido: {tipo}")
    };

    // ------------------------------------------------------------------ Stock

    private async Task<ReporteGenerado> StockAsync(string emitidoPor)
    {
        var productos = await _db.Productos
            .Where(p => p.Activo)
            .OrderBy(p => p.Categoria!.Nombre).ThenBy(p => p.Nombre)
            .Select(p => new { p.Nombre, Categoria = p.Categoria!.Nombre, p.Precio, p.Stock })
            .ToListAsync();

        var unidades = productos.Sum(p => p.Stock);
        var valor = productos.Sum(p => p.Precio * p.Stock);
        var bajos = productos.Count(p => p.Stock <= 5);

        var pdf = Documento("Reporte de stock", "Existencias al momento de la emisión", emitidoPor, col =>
        {
            col.Item().Element(c => Resumen(c,
                ("Productos activos", productos.Count.ToString()),
                ("Unidades en stock", unidades.ToString("N0", Ar)),
                ("Valor a precio de venta", Moneda(valor)),
                ("Con stock bajo (5 o menos)", bajos.ToString())));

            if (productos.Count == 0)
            {
                col.Item().PaddingTop(14).Text("No hay productos activos.").FontColor(TintaSuave);
                return;
            }

            col.Item().PaddingTop(16).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4);
                    c.RelativeColumn(2.4f);
                    c.RelativeColumn(2);
                    c.RelativeColumn(1.3f);
                    c.RelativeColumn(2.2f);
                });
                t.Header(h =>
                {
                    Encabezado(h.Cell(), "Producto");
                    Encabezado(h.Cell(), "Categoría");
                    Encabezado(h.Cell(), "Precio", derecha: true);
                    Encabezado(h.Cell(), "Stock", derecha: true);
                    Encabezado(h.Cell(), "Valorizado", derecha: true);
                });
                foreach (var p in productos)
                {
                    var color = p.Stock == 0 ? Peligro : p.Stock <= 5 ? Aviso : Tinta;
                    Celda(t.Cell(), p.Nombre);
                    Celda(t.Cell(), p.Categoria);
                    Celda(t.Cell(), Moneda(p.Precio), derecha: true);
                    Celda(t.Cell(), p.Stock.ToString(), derecha: true, color: color, negrita: p.Stock <= 5);
                    Celda(t.Cell(), Moneda(p.Precio * p.Stock), derecha: true);
                }
            });

            col.Item().PaddingTop(8).Text("En rojo: sin stock. En ámbar: stock bajo (5 unidades o menos).")
                .FontSize(8).FontColor(TintaSuave);
        });

        return new ReporteGenerado(pdf, $"stock_{ALocal(DateTime.UtcNow):yyyyMMdd_HHmm}.pdf", "Reporte de stock");
    }

    // ----------------------------------------------------------------- Ventas

    private async Task<ReporteGenerado> VentasAsync(DateTime desde, DateTime hasta, string emitidoPor)
    {
        var desdeUtc = AUtc(desde);
        var hastaUtc = AUtc(hasta.AddDays(1));

        var ventas = await _db.Ventas
            .Where(v => v.Fecha >= desdeUtc && v.Fecha < hastaUtc)
            .OrderBy(v => v.Fecha)
            .Select(v => new
            {
                v.Id,
                v.Fecha,
                Cliente = v.Cliente!.Nombre + " " + v.Cliente.Apellido,
                Cajero = v.Usuario!.NombreUsuario,
                v.Canal,
                Unidades = v.Detalles.Sum(d => d.Cantidad),
                Total = v.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario)
            })
            .ToListAsync();

        var masVendidos = await _db.DetalleVentas
            .Where(d => d.Venta!.Fecha >= desdeUtc && d.Venta.Fecha < hastaUtc)
            .GroupBy(d => d.Producto!.Nombre)
            .Select(g => new
            {
                Producto = g.Key,
                Unidades = g.Sum(d => d.Cantidad),
                Importe = g.Sum(d => d.Cantidad * d.PrecioUnitario)
            })
            .OrderByDescending(x => x.Unidades)
            .Take(10)
            .ToListAsync();

        var total = ventas.Sum(v => v.Total);
        var promedio = ventas.Count > 0 ? total / ventas.Count : 0;

        var pdf = Documento("Reporte de ventas", Periodo(desde, hasta), emitidoPor, col =>
        {
            col.Item().Element(c => Resumen(c,
                ("Ventas", ventas.Count.ToString()),
                ("Unidades vendidas", ventas.Sum(v => v.Unidades).ToString("N0", Ar)),
                ("Total facturado", Moneda(total)),
                ("Ticket promedio", Moneda(promedio))));

            if (ventas.Count == 0)
            {
                col.Item().PaddingTop(14).Text("No hay ventas en el período.").FontColor(TintaSuave);
                return;
            }

            Subtitulo(col, "Ventas por canal");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(1.5f); c.RelativeColumn(2.2f); });
                t.Header(h =>
                {
                    Encabezado(h.Cell(), "Canal");
                    Encabezado(h.Cell(), "Ventas", derecha: true);
                    Encabezado(h.Cell(), "Total", derecha: true);
                });
                foreach (var g in ventas.GroupBy(v => v.Canal).OrderBy(g => g.Key))
                {
                    Celda(t.Cell(), g.Key == "App" ? "App móvil (tienda online)" : "Caja (punto de venta)");
                    Celda(t.Cell(), g.Count().ToString(), derecha: true);
                    Celda(t.Cell(), Moneda(g.Sum(v => v.Total)), derecha: true);
                }
            });

            Subtitulo(col, "Productos más vendidos");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(1.5f); c.RelativeColumn(2.2f); });
                t.Header(h =>
                {
                    Encabezado(h.Cell(), "Producto");
                    Encabezado(h.Cell(), "Unidades", derecha: true);
                    Encabezado(h.Cell(), "Importe", derecha: true);
                });
                foreach (var p in masVendidos)
                {
                    Celda(t.Cell(), p.Producto);
                    Celda(t.Cell(), p.Unidades.ToString("N0", Ar), derecha: true);
                    Celda(t.Cell(), Moneda(p.Importe), derecha: true);
                }
            });

            Subtitulo(col, "Detalle de ventas");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(44);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(2.2f);
                });
                t.Header(h =>
                {
                    Encabezado(h.Cell(), "N.º");
                    Encabezado(h.Cell(), "Fecha");
                    Encabezado(h.Cell(), "Cliente");
                    Encabezado(h.Cell(), "Usuario");
                    Encabezado(h.Cell(), "Canal");
                    Encabezado(h.Cell(), "Unid.", derecha: true);
                    Encabezado(h.Cell(), "Total", derecha: true);
                });
                foreach (var v in ventas)
                {
                    Celda(t.Cell(), v.Id.ToString());
                    Celda(t.Cell(), ALocal(v.Fecha).ToString("dd/MM/yyyy HH:mm", Ar));
                    Celda(t.Cell(), v.Cliente);
                    Celda(t.Cell(), v.Cajero);
                    Celda(t.Cell(), v.Canal);
                    Celda(t.Cell(), v.Unidades.ToString(), derecha: true);
                    Celda(t.Cell(), Moneda(v.Total), derecha: true);
                }
                FilaTotal(t, 7, Moneda(total));
            });
        });

        return new ReporteGenerado(pdf, $"ventas_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.pdf", "Reporte de ventas");
    }

    // --------------------------------------------------------------- Ingresos

    private async Task<ReporteGenerado> IngresosAsync(DateTime desde, DateTime hasta, string emitidoPor)
    {
        var desdeUtc = AUtc(desde);
        var hastaUtc = AUtc(hasta.AddDays(1));

        var compras = await _db.Compras
            .Where(c => c.Fecha >= desdeUtc && c.Fecha < hastaUtc)
            .OrderBy(c => c.Fecha)
            .Select(c => new
            {
                c.Id,
                c.Fecha,
                Proveedor = c.Proveedor!.RazonSocial,
                Registro = c.Usuario!.NombreUsuario,
                Unidades = c.Detalles.Sum(d => d.Cantidad),
                Total = c.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario)
            })
            .ToListAsync();

        var porProducto = await _db.DetalleCompras
            .Where(d => d.Compra!.Fecha >= desdeUtc && d.Compra.Fecha < hastaUtc)
            .GroupBy(d => d.Producto!.Nombre)
            .Select(g => new
            {
                Producto = g.Key,
                Unidades = g.Sum(d => d.Cantidad),
                Costo = g.Sum(d => d.Cantidad * d.PrecioUnitario)
            })
            .OrderBy(x => x.Producto)
            .ToListAsync();

        var total = compras.Sum(c => c.Total);

        var pdf = Documento("Reporte de ingresos de mercadería", Periodo(desde, hasta), emitidoPor, col =>
        {
            col.Item().Element(c => Resumen(c,
                ("Ingresos registrados", compras.Count.ToString()),
                ("Unidades ingresadas", compras.Sum(x => x.Unidades).ToString("N0", Ar)),
                ("Total invertido", Moneda(total)),
                ("Proveedores", compras.Select(x => x.Proveedor).Distinct().Count().ToString())));

            if (compras.Count == 0)
            {
                col.Item().PaddingTop(14).Text("No hay ingresos en el período.").FontColor(TintaSuave);
                return;
            }

            Subtitulo(col, "Detalle de ingresos");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(44);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(3.2f);
                    c.RelativeColumn(1.8f);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(2.2f);
                });
                t.Header(h =>
                {
                    Encabezado(h.Cell(), "N.º");
                    Encabezado(h.Cell(), "Fecha");
                    Encabezado(h.Cell(), "Proveedor");
                    Encabezado(h.Cell(), "Registró");
                    Encabezado(h.Cell(), "Unid.", derecha: true);
                    Encabezado(h.Cell(), "Total", derecha: true);
                });
                foreach (var c in compras)
                {
                    Celda(t.Cell(), c.Id.ToString());
                    Celda(t.Cell(), ALocal(c.Fecha).ToString("dd/MM/yyyy HH:mm", Ar));
                    Celda(t.Cell(), c.Proveedor);
                    Celda(t.Cell(), c.Registro);
                    Celda(t.Cell(), c.Unidades.ToString(), derecha: true);
                    Celda(t.Cell(), Moneda(c.Total), derecha: true);
                }
                FilaTotal(t, 6, Moneda(total));
            });

            Subtitulo(col, "Unidades ingresadas por producto");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(1.5f); c.RelativeColumn(2.2f); });
                t.Header(h =>
                {
                    Encabezado(h.Cell(), "Producto");
                    Encabezado(h.Cell(), "Unidades", derecha: true);
                    Encabezado(h.Cell(), "Costo total", derecha: true);
                });
                foreach (var p in porProducto)
                {
                    Celda(t.Cell(), p.Producto);
                    Celda(t.Cell(), p.Unidades.ToString("N0", Ar), derecha: true);
                    Celda(t.Cell(), Moneda(p.Costo), derecha: true);
                }
            });
        });

        return new ReporteGenerado(pdf, $"ingresos_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.pdf", "Reporte de ingresos de mercadería");
    }

    // --------------------------------------------------------- Piezas comunes

    private byte[] Documento(string titulo, string subtitulo, string emitidoPor, Action<ColumnDescriptor> contenido)
    {
        var emision = ALocal(DateTime.UtcNow);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Tinta));

                page.Header().PaddingBottom(14).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(_negocio).FontSize(9).FontColor(TintaSuave);
                            c.Item().Text(titulo).FontSize(18).Bold();
                            c.Item().Text(subtitulo).FontSize(10).FontColor(TintaSuave);
                        });
                        row.ConstantItem(170).Column(c =>
                        {
                            c.Item().AlignRight().Text($"Emitido: {emision:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(TintaSuave);
                            c.Item().AlignRight().Text($"Por: {emitidoPor}").FontSize(9).FontColor(TintaSuave);
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Tinta);
                });

                page.Content().Column(contenido);

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor(TintaSuave));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void Resumen(IContainer contenedor, params (string Etiqueta, string Valor)[] datos)
    {
        contenedor.Row(row =>
        {
            row.Spacing(8);
            foreach (var (etiqueta, valor) in datos)
            {
                row.RelativeItem().Border(1).BorderColor(Linea).Padding(8).Column(c =>
                {
                    c.Item().Text(etiqueta).FontSize(8.5f).FontColor(TintaSuave);
                    c.Item().Text(valor).FontSize(14).Bold();
                });
            }
        });
    }

    private static void Subtitulo(ColumnDescriptor col, string texto) =>
        col.Item().PaddingTop(18).PaddingBottom(6).Text(texto).FontSize(12).Bold();

    private static void Encabezado(IContainer contenedor, string texto, bool derecha = false)
    {
        var celda = contenedor.Background(Fondo).BorderBottom(1).BorderColor(Linea).PaddingVertical(5).PaddingHorizontal(4);
        if (derecha) celda = celda.AlignRight();
        celda.Text(texto).FontSize(8.5f).SemiBold().FontColor(TintaSuave);
    }

    private static void Celda(IContainer contenedor, string texto, bool derecha = false, string? color = null, bool negrita = false)
    {
        var celda = contenedor.BorderBottom(0.5f).BorderColor(Linea).PaddingVertical(4).PaddingHorizontal(4);
        if (derecha) celda = celda.AlignRight();
        var texto2 = celda.Text(texto).FontSize(9.5f).FontColor(color ?? Tinta);
        if (negrita) texto2.Bold();
    }

    private static void FilaTotal(TableDescriptor tabla, int columnas, string total)
    {
        tabla.Cell().ColumnSpan((uint)(columnas - 1)).PaddingVertical(6).PaddingHorizontal(4)
            .AlignRight().Text("Total").Bold();
        tabla.Cell().PaddingVertical(6).PaddingHorizontal(4).AlignRight().Text(total).Bold();
    }

    private static string Periodo(DateTime desde, DateTime hasta) =>
        $"Período: del {desde.ToString("dd/MM/yyyy", Ar)} al {hasta.ToString("dd/MM/yyyy", Ar)}";

    private static string Moneda(decimal valor) => valor.ToString("C2", Ar);
    private DateTime ALocal(DateTime utc) => utc.AddHours(_diferenciaHoras);
    private DateTime AUtc(DateTime local) => local.AddHours(-_diferenciaHoras);
}
