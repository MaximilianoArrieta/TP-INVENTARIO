namespace InventarioApi.Domain.Entities;

// Pago (simulado) de una venta hecha desde la app. Relación 1 a 1 con Venta.
// No guarda el monto ni la fecha (3FN): salen de la venta. Nunca se guarda el número
// completo de la tarjeta ni el código de seguridad, solo los últimos 4 dígitos.
public class Pago
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public Venta? Venta { get; set; }
    public int MedioPagoId { get; set; }
    public MedioPago? MedioPago { get; set; }
    public int Cuotas { get; set; } = 1;
    public string? UltimosDigitos { get; set; }
    public string CodigoAutorizacion { get; set; } = string.Empty;
}
