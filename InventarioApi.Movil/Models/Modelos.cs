namespace InventarioApi.Movil.Models;

// Clases que reflejan el JSON que devuelve la API

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class MensajeRespuesta
{
    public string Mensaje { get; set; } = "";
}

public class LoginRespuesta
{
    public string Token { get; set; } = "";
    public string Usuario { get; set; } = "";
    public string Rol { get; set; } = "";
    public DateTime Expira { get; set; }
}

public class UsuarioActual
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string? Email { get; set; }
    public string Rol { get; set; } = "";
    public bool Activo { get; set; }
}

public class ProductoItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public string? ImagenUrl { get; set; }
    public int CategoriaId { get; set; }
    public string? Categoria { get; set; }
    public bool Activo { get; set; } = true;
}

public class CategoriaItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

public class ProveedorItem
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = "";
    public string? Telefono { get; set; }
}

public class MedioPagoItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool RequiereTarjeta { get; set; }
    public bool PermiteCuotas { get; set; }
}

public class LineaVista
{
    public int ProductoId { get; set; }
    public string Producto { get; set; } = "";
    public string? ImagenUrl { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

// Una compra hecha por el cliente desde la app (api/tienda/mis-compras)
public class CompraClienteVista
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string? MedioPago { get; set; }
    public int Cuotas { get; set; } = 1;
    public string? UltimosDigitos { get; set; }
    public string? CodigoAutorizacion { get; set; }
    public List<LineaVista> Detalles { get; set; } = new();
}

public class VentaVista
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Cliente { get; set; } = "";
    public string Usuario { get; set; } = "";
    public string? Canal { get; set; }
    public decimal Total { get; set; }
    public List<LineaVista> Detalles { get; set; } = new();
}

public class IngresoVista
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Proveedor { get; set; } = "";
    public decimal Total { get; set; }
    public List<LineaVista> Detalles { get; set; } = new();
}

public class AuditoriaItem
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string? Usuario { get; set; }
    public string Accion { get; set; } = "";
    public string Entidad { get; set; } = "";
    public int? EntidadId { get; set; }
    public string? Detalle { get; set; }
}

public class ErrorItem
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string? Ruta { get; set; }
    public string Mensaje { get; set; } = "";
    public string? Detalle { get; set; }
    public string? Usuario { get; set; }
}

public class UsuarioItem
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string? Email { get; set; }
    public int RolId { get; set; }
    public string Rol { get; set; } = "";
    public bool Activo { get; set; }
}

public class RolItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

// Lo que se guarda en el carrito (en el celular, no en la base)
public class ItemCarrito
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Categoria { get; set; }
    public string? ImagenUrl { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int Cantidad { get; set; }
    public decimal Subtotal => Precio * Cantidad;
}
