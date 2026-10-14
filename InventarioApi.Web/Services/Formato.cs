using System.Globalization;

namespace InventarioApi.Web.Services;

public static class Formato
{
    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("es-AR");

    public static string Moneda(decimal valor) => valor.ToString("C2", Ar);

    // La API guarda las fechas en UTC; acá se muestran en la hora local del navegador
    public static string Fecha(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", Ar);
}
