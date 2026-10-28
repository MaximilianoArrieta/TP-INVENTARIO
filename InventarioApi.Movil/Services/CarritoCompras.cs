using System.Text.Json;
using InventarioApi.Movil.Models;

namespace InventarioApi.Movil.Services;

// El carrito vive en el celular (Preferences) hasta que se paga: recién ahí la API
// valida el stock y registra la venta. Se conserva aunque se cierre la app.
public class CarritoCompras
{
    private const string Clave = "carrito";
    private readonly List<ItemCarrito> _items;

    public event Action? Cambio;

    public CarritoCompras()
    {
        try
        {
            _items = JsonSerializer.Deserialize<List<ItemCarrito>>(Preferences.Default.Get(Clave, "[]")) ?? new();
        }
        catch
        {
            _items = new();
        }
    }

    public IReadOnlyList<ItemCarrito> Items => _items;
    public int Unidades => _items.Sum(i => i.Cantidad);
    public decimal Total => _items.Sum(i => i.Subtotal);

    public int CantidadDe(int productoId) =>
        _items.FirstOrDefault(i => i.ProductoId == productoId)?.Cantidad ?? 0;

    // Devuelve un mensaje si no se puede agregar (sin stock suficiente)
    public string? Agregar(ProductoItem producto, int cantidad)
    {
        if (producto.Stock <= 0) return "Este producto no tiene stock.";

        var item = _items.FirstOrDefault(i => i.ProductoId == producto.Id);
        var nueva = (item?.Cantidad ?? 0) + cantidad;
        if (nueva > producto.Stock)
            return item == null
                ? $"Solo hay {producto.Stock} disponibles."
                : $"Solo hay {producto.Stock} disponibles y ya tenés {item.Cantidad} en el carrito.";

        if (item == null)
        {
            item = new ItemCarrito { ProductoId = producto.Id };
            _items.Add(item);
        }
        item.Nombre = producto.Nombre;
        item.Categoria = producto.Categoria;
        item.ImagenUrl = producto.ImagenUrl;
        item.Precio = producto.Precio;
        item.Stock = producto.Stock;
        item.Cantidad = nueva;
        Guardar();
        return null;
    }

    public void CambiarCantidad(int productoId, int cantidad)
    {
        var item = _items.FirstOrDefault(i => i.ProductoId == productoId);
        if (item == null) return;
        item.Cantidad = Math.Clamp(cantidad, 1, Math.Max(1, item.Stock));
        Guardar();
    }

    public void Quitar(int productoId)
    {
        _items.RemoveAll(i => i.ProductoId == productoId);
        Guardar();
    }

    public void Vaciar()
    {
        _items.Clear();
        Guardar();
    }

    // Pone al día precio y stock con lo que dice la API. Devuelve un aviso si algo cambió.
    public string? Actualizar(int productoId, ProductoItem? actual)
    {
        var item = _items.FirstOrDefault(i => i.ProductoId == productoId);
        if (item == null) return null;

        string? aviso = null;
        if (actual == null || !actual.Activo || actual.Stock <= 0)
        {
            _items.Remove(item);
            aviso = $"«{item.Nombre}» se quedó sin stock y se quitó del carrito.";
        }
        else
        {
            if (actual.Precio != item.Precio)
                aviso = $"«{actual.Nombre}» cambió de precio: ahora cuesta {Formato.Moneda(actual.Precio)}.";
            if (item.Cantidad > actual.Stock)
            {
                aviso = $"De «{actual.Nombre}» solo quedan {actual.Stock}: se ajustó la cantidad.";
                item.Cantidad = actual.Stock;
            }
            item.Nombre = actual.Nombre;
            item.ImagenUrl = actual.ImagenUrl;
            item.Precio = actual.Precio;
            item.Stock = actual.Stock;
        }
        Guardar();
        return aviso;
    }

    private void Guardar()
    {
        Preferences.Default.Set(Clave, JsonSerializer.Serialize(_items));
        Cambio?.Invoke();
    }
}
