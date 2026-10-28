using System.Globalization;

namespace InventarioApi.Movil.Services;

public static class Formato
{
    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("es-AR");

    public static string Moneda(decimal valor) => valor.ToString("C2", Ar);

    // Para mostrar precios grandes: "$ 1.234" y los centavos aparte
    public static string Entero(decimal valor) => Math.Truncate(valor).ToString("N0", Ar);
    public static string Centavos(decimal valor) =>
        ((int)Math.Round((valor - Math.Truncate(valor)) * 100)).ToString("00");

    // La API guarda las fechas en UTC; se muestran en la hora del celular
    public static string Fecha(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", Ar);

    public static string FechaCorta(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("d 'de' MMMM", Ar);

    public static string Unidades(int n) => n == 1 ? "1 unidad" : $"{n} unidades";
}
