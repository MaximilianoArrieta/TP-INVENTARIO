using InventarioApi.Domain.Entities;

namespace InventarioApi.Api.Services;

public record DatosPago(int MedioPagoId, string? NumeroTarjeta, string? Titular, string? Vencimiento, string? Codigo, int Cuotas = 1);

// Pasarela de pagos SIMULADA (no se conecta a ningún banco).
// Reglas para probar:
//   - Cualquier tarjeta válida (16 dígitos que pasen el algoritmo de Luhn, vencimiento futuro, código de 3-4 dígitos) se aprueba.
//   - Tarjetas terminadas en 0002: rechazadas por "fondos insuficientes".
//   - Compras de más de $ 500.000 con tarjeta: rechazadas por "supera el límite disponible".
//   - Billetera virtual y transferencia: siempre se aprueban.
public static class SimuladorPagos
{
    public const decimal LimiteTarjeta = 500_000m;
    private static readonly int[] CuotasPermitidas = { 1, 3, 6, 12 };

    // Validación de los datos (errores del usuario al completar el formulario)
    public static string? Validar(MedioPago medio, DatosPago datos)
    {
        if (!medio.PermiteCuotas && datos.Cuotas != 1)
            return $"{medio.Nombre} no permite pagar en cuotas.";
        if (medio.PermiteCuotas && !CuotasPermitidas.Contains(datos.Cuotas))
            return "Las cuotas pueden ser 1, 3, 6 o 12.";
        if (!medio.RequiereTarjeta) return null;

        var numero = SoloDigitos(datos.NumeroTarjeta);
        if (numero.Length < 13 || numero.Length > 19 || !PasaLuhn(numero))
            return "El número de tarjeta no es válido.";
        if (string.IsNullOrWhiteSpace(datos.Titular))
            return "Completá el nombre del titular como figura en la tarjeta.";
        if (!VencimientoValido(datos.Vencimiento))
            return "La fecha de vencimiento no es válida o la tarjeta está vencida (formato MM/AA).";
        var codigo = SoloDigitos(datos.Codigo);
        if (codigo.Length is < 3 or > 4)
            return "El código de seguridad debe tener 3 o 4 dígitos.";
        return null;
    }

    // Autorización (lo que en la vida real respondería el banco)
    public static ResultadoPago Autorizar(MedioPago medio, DatosPago datos, decimal total)
    {
        string? ultimos = null;
        if (medio.RequiereTarjeta)
        {
            var numero = SoloDigitos(datos.NumeroTarjeta);
            ultimos = numero[^4..];
            if (ultimos == "0002")
                return new(false, "Pago rechazado: fondos insuficientes.", null);
            if (total > LimiteTarjeta)
                return new(false, "Pago rechazado: el importe supera el límite disponible de la tarjeta.", null);
        }

        return new(true, null, new Pago
        {
            MedioPagoId = medio.Id,
            Cuotas = datos.Cuotas,
            UltimosDigitos = ultimos,
            CodigoAutorizacion = Random.Shared.Next(100000, 999999).ToString()
        });
    }

    public static string SoloDigitos(string? texto) =>
        new string((texto ?? "").Where(char.IsDigit).ToArray());

    private static bool PasaLuhn(string numero)
    {
        var suma = 0;
        var duplicar = false;
        for (var i = numero.Length - 1; i >= 0; i--)
        {
            var d = numero[i] - '0';
            if (duplicar)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            suma += d;
            duplicar = !duplicar;
        }
        return suma % 10 == 0;
    }

    private static bool VencimientoValido(string? texto)
    {
        var partes = (texto ?? "").Split('/');
        if (partes.Length != 2 || !int.TryParse(partes[0], out var mes) || !int.TryParse(partes[1], out var anio))
            return false;
        if (mes is < 1 or > 12) return false;
        if (anio < 100) anio += 2000;
        if (anio is < 2000 or > 2100) return false;
        var ultimoDia = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes));
        return ultimoDia >= DateTime.UtcNow.Date;
    }
}
