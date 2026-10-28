namespace InventarioApi.Domain.Entities;

// Medios de pago disponibles en la tienda (tabla propia, como Roles: evita textos repetidos)
public class MedioPago
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool RequiereTarjeta { get; set; }
    public bool PermiteCuotas { get; set; }
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}
