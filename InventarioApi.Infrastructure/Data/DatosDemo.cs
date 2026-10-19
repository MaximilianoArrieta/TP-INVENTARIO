using InventarioApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventarioApi.Infrastructure.Data;

// Datos de demostración para mostrar el sistema funcionando: categorías, productos,
// proveedores, clientes, dos cajeros, compras y unas 40 ventas repartidas en las
// últimas 3 semanas (así los reportes por período tienen contenido).
// Solo se cargan si la base no tiene productos y "DatosDemo:Cargar" está en true.
public static class DatosDemo
{
    private record ProductoDemo(string Nombre, string Descripcion, decimal Precio, decimal Costo,
        int Categoria, int Proveedor, int CompraInicial, int Reposicion);

    // Cajero: 0 = cajero1, 1 = cajero2, 2 = admin. Cliente: -1 = Consumidor Final.
    private record VentaDemo(int DiasAtras, int Hora, int Minuto, int Cajero, int Cliente, (int Producto, int Cantidad)[] Items);

    private static readonly (string Nombre, string Descripcion)[] Categorias =
    {
        ("Bebidas", "Gaseosas, aguas, jugos, cervezas y vinos"),
        ("Almacén", "Productos secos y de despensa"),
        ("Lácteos", "Leches, yogures y derivados"),
        ("Limpieza", "Artículos de limpieza del hogar"),
        ("Perfumería", "Higiene y cuidado personal")
    };

    private static readonly (string RazonSocial, string Contacto, string Telefono, string Email, string Direccion)[] Proveedores =
    {
        ("Distribuidora del Sur S.A.", "Juan Pérez", "2954 123456", "ventas@delsur.example", "Ruta 5 km 3"),
        ("Lácteos La Pampa S.R.L.", "Sofía Martínez", "2954 654321", "pedidos@lacteoslapampa.example", "Av. Luro 1450"),
        ("Higiene Total S.A.", "Diego Romero", "11 4567-8900", "comercial@higienetotal.example", "Av. Corrientes 2300")
    };

    private static readonly (string Nombre, string Apellido, string Email, string Telefono)[] Clientes =
    {
        ("María", "Gómez", "maria.gomez@example.com", "2954 111222"),
        ("Juan", "Pérez", "juan.perez@example.com", "2954 333444"),
        ("Laura", "Fernández", "laura.fernandez@example.com", "2954 555666"),
        ("Carlos", "Ruiz", "carlos.ruiz@example.com", "2954 777888"),
        ("Ana", "Torres", "ana.torres@example.com", "2954 999000")
    };

    private static readonly ProductoDemo[] Productos =
    {
        new("Gaseosa cola 2,25 L", "Botella retornable", 2500m, 1800m, 0, 0, 36, 24),
        new("Agua mineral 2 L", "Sin gas", 1200m, 800m, 0, 0, 40, 24),
        new("Jugo de naranja 1 L", "Exprimido, refrigerado", 1800m, 1300m, 0, 0, 18, 0),
        new("Cerveza rubia lata 473 ml", "Lata", 1600m, 1100m, 0, 0, 48, 0),
        new("Vino tinto Malbec 750 ml", "Mendoza", 5200m, 3600m, 0, 0, 12, 0),
        new("Arroz largo fino 1 kg", "Paquete", 1500m, 1050m, 1, 0, 30, 0),
        new("Fideos tirabuzón 500 g", "Paquete", 1100m, 750m, 1, 0, 40, 0),
        new("Aceite de girasol 1,5 L", "Botella", 3200m, 2400m, 1, 0, 20, 0),
        new("Yerba mate 1 kg", "Con palo", 4800m, 3500m, 1, 0, 24, 12),
        new("Azúcar 1 kg", "Común tipo A", 1300m, 900m, 1, 0, 25, 0),
        new("Harina 000 1 kg", "Paquete", 950m, 650m, 1, 0, 20, 0),
        new("Galletitas de agua 300 g", "Paquete x3", 1400m, 950m, 1, 0, 24, 0),
        new("Leche entera 1 L", "Sachet", 1350m, 950m, 2, 1, 40, 30),
        new("Yogur bebible frutilla 1 L", "Botella", 2100m, 1500m, 2, 1, 15, 0),
        new("Manteca 200 g", "Pan", 2300m, 1650m, 2, 1, 12, 0),
        new("Dulce de leche 400 g", "Clásico", 2600m, 1850m, 2, 1, 14, 0),
        new("Detergente 750 ml", "Aroma limón", 1900m, 1350m, 3, 2, 18, 0),
        new("Lavandina 2 L", "Concentrada", 1400m, 950m, 3, 2, 10, 0),
        new("Jabón en polvo 800 g", "Para lavarropas", 3900m, 2800m, 3, 2, 10, 0),
        new("Papel higiénico 4 rollos", "Hoja simple 30 m", 2800m, 2000m, 3, 2, 16, 0),
        new("Shampoo 400 ml", "Uso diario", 3600m, 2500m, 4, 2, 8, 0),
        new("Jabón de tocador x3", "Pack", 2200m, 1500m, 4, 2, 4, 0),
        new("Pasta dental 90 g", "Con flúor", 1700m, 1150m, 4, 2, 8, 0)
    };

    private static readonly VentaDemo[] Ventas =
    {
        new(19, 11, 25, 0, -1, new[] { (3, 1), (6, 1), (9, 1) }),
        new(19, 12, 5, 2, 3, new[] { (3, 1) }),
        new(18, 9, 36, 2, 3, new[] { (1, 2) }),
        new(18, 11, 18, 1, -1, new[] { (5, 2), (9, 3) }),
        new(18, 12, 23, 0, -1, new[] { (6, 2), (13, 1), (16, 1) }),
        new(17, 12, 50, 0, 0, new[] { (19, 2) }),
        new(17, 14, 46, 1, 1, new[] { (3, 2), (12, 1), (16, 2) }),
        new(16, 19, 4, 2, 2, new[] { (16, 1), (19, 2) }),
        new(15, 13, 30, 0, -1, new[] { (14, 1), (16, 1), (20, 2), (21, 1) }),
        new(14, 18, 7, 1, -1, new[] { (3, 2), (4, 3) }),
        new(14, 10, 10, 1, 3, new[] { (8, 3), (12, 2) }),
        new(13, 12, 9, 0, -1, new[] { (6, 1), (9, 1) }),
        new(13, 13, 18, 0, -1, new[] { (9, 2), (12, 3), (19, 3) }),
        new(13, 19, 47, 0, 4, new[] { (1, 2), (6, 2), (12, 2) }),
        new(12, 10, 21, 2, -1, new[] { (12, 2) }),
        new(12, 10, 23, 2, -1, new[] { (19, 2) }),
        new(11, 19, 16, 1, 2, new[] { (1, 1), (5, 1), (16, 2) }),
        new(11, 13, 30, 0, -1, new[] { (16, 1), (19, 1) }),
        new(10, 13, 41, 0, 1, new[] { (10, 3), (12, 1), (17, 2) }),
        new(10, 12, 39, 0, 0, new[] { (0, 1), (7, 1), (8, 3) }),
        new(10, 16, 16, 0, 2, new[] { (1, 1), (8, 1), (11, 1) }),
        new(9, 18, 57, 2, -1, new[] { (0, 1), (11, 3), (22, 1) }),
        new(9, 16, 56, 0, 3, new[] { (0, 1), (3, 3), (5, 1), (12, 3) }),
        new(9, 18, 57, 1, -1, new[] { (0, 1), (4, 2), (15, 3) }),
        new(8, 17, 47, 0, 3, new[] { (5, 2), (6, 1) }),
        new(7, 18, 20, 1, 3, new[] { (1, 1), (21, 2) }),
        new(7, 17, 26, 2, -1, new[] { (5, 2), (12, 1), (19, 1) }),
        new(6, 11, 30, 2, -1, new[] { (0, 2), (1, 1), (16, 1) }),
        new(6, 12, 17, 0, -1, new[] { (0, 1), (6, 3), (9, 2) }),
        new(5, 16, 32, 2, 4, new[] { (6, 2), (8, 2), (22, 2) }),
        new(5, 11, 26, 0, 3, new[] { (0, 1), (1, 1), (6, 3) }),
        new(4, 11, 16, 0, 4, new[] { (3, 2), (5, 3) }),
        new(4, 12, 10, 1, 3, new[] { (8, 1) }),
        new(3, 14, 35, 1, 4, new[] { (3, 1), (8, 2), (12, 1), (16, 1) }),
        new(2, 13, 2, 0, 1, new[] { (19, 2), (21, 1) }),
        new(2, 18, 31, 1, -1, new[] { (1, 1) }),
        new(1, 9, 40, 0, 1, new[] { (7, 1) }),
        new(1, 13, 55, 0, 4, new[] { (19, 2) }),
        new(0, 18, 8, 0, 0, new[] { (8, 1) }),
        new(0, 11, 12, 1, 1, new[] { (0, 1), (9, 2), (12, 1) })
    };

    public static async Task<bool> CargarAsync(AppDbContext db, int diferenciaHoras)
    {
        if (await db.Productos.AnyAsync()) return false;

        var admin = await db.Usuarios.FirstOrDefaultAsync(u => u.Rol!.Nombre == "Admin");
        var rolCajero = await db.Roles.FirstOrDefaultAsync(r => r.Nombre == "Cajero");
        if (admin == null || rolCajero == null) return false;

        var consumidorFinalId = await DatosIniciales.ObtenerConsumidorFinalIdAsync(db);
        var ahora = DateTime.UtcNow;

        // Los movimientos de demostración no se auditan uno por uno: se deja un solo registro al final
        db.AuditoriaDesactivada = true;
        try
        {
            var cajeros = new[]
            {
                await CajeroAsync(db, "cajero1", "cajero1@example.com", rolCajero.Id),
                await CajeroAsync(db, "cajero2", "cajero2@example.com", rolCajero.Id),
                admin
            };

            var categorias = Categorias.Select(c => new Categoria { Nombre = c.Nombre, Descripcion = c.Descripcion }).ToList();
            var proveedores = Proveedores.Select(p => new Proveedor
            {
                RazonSocial = p.RazonSocial, Contacto = p.Contacto, Telefono = p.Telefono, Email = p.Email, Direccion = p.Direccion
            }).ToList();
            var clientes = Clientes.Select(c => new Cliente
            {
                Nombre = c.Nombre, Apellido = c.Apellido, Email = c.Email, Telefono = c.Telefono
            }).ToList();
            var productos = Productos.Select(p => new Producto
            {
                Nombre = p.Nombre, Descripcion = p.Descripcion, Precio = p.Precio, Categoria = categorias[p.Categoria]
            }).ToList();

            db.Categorias.AddRange(categorias);
            db.Proveedores.AddRange(proveedores);
            db.Clientes.AddRange(clientes);
            db.Productos.AddRange(productos);

            // Compras: stock inicial hace 20 días y una reposición hace 8 días
            AgregarCompras(db, productos, proveedores, admin, p => p.CompraInicial, Fecha(20, 8, 0, diferenciaHoras, ahora));
            AgregarCompras(db, productos, proveedores, admin, p => p.Reposicion, Fecha(8, 8, 30, diferenciaHoras, ahora));

            // Ventas
            foreach (var v in Ventas)
            {
                var venta = new Venta
                {
                    Fecha = Fecha(v.DiasAtras, v.Hora, v.Minuto, diferenciaHoras, ahora),
                    Usuario = cajeros[v.Cajero]
                };
                if (v.Cliente >= 0) venta.Cliente = clientes[v.Cliente];
                else venta.ClienteId = consumidorFinalId;

                foreach (var (indice, cantidad) in v.Items)
                {
                    var producto = productos[indice];
                    venta.Detalles.Add(new DetalleVenta { Producto = producto, Cantidad = cantidad, PrecioUnitario = producto.Precio });
                    producto.Stock -= cantidad;
                }
                db.Ventas.Add(venta);
            }

            await db.SaveChangesAsync();
        }
        finally
        {
            db.AuditoriaDesactivada = false;
        }

        db.Auditorias.Add(new Auditoria
        {
            Accion = "CargarDatosDemo",
            Entidad = "Sistema",
            Detalle = $"Datos de demostración: {Categorias.Length} categorías, {Productos.Length} productos, " +
                      $"{Proveedores.Length} proveedores, {Clientes.Length} clientes, 2 cajeros y {Ventas.Length} ventas."
        });
        await db.SaveChangesAsync();
        return true;
    }

    private static void AgregarCompras(AppDbContext db, List<Producto> productos, List<Proveedor> proveedores,
        Usuario admin, Func<ProductoDemo, int> cantidad, DateTime fecha)
    {
        for (var prov = 0; prov < proveedores.Count; prov++)
        {
            var compra = new Compra { Fecha = fecha, Proveedor = proveedores[prov], Usuario = admin };
            for (var i = 0; i < Productos.Length; i++)
            {
                var datos = Productos[i];
                var unidades = cantidad(datos);
                if (datos.Proveedor != prov || unidades <= 0) continue;

                compra.Detalles.Add(new DetalleCompra { Producto = productos[i], Cantidad = unidades, PrecioUnitario = datos.Costo });
                productos[i].Stock += unidades;
            }
            if (compra.Detalles.Count > 0) db.Compras.Add(compra);
        }
    }

    private static async Task<Usuario> CajeroAsync(AppDbContext db, string nombre, string email, int rolId)
    {
        var existente = await db.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombre);
        if (existente != null) return existente;

        var usuario = new Usuario
        {
            NombreUsuario = nombre,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Cajero123!"),
            Email = email,
            RolId = rolId
        };
        db.Usuarios.Add(usuario);
        return usuario;
    }

    // Fecha en UTC a partir de "hace N días a tal hora" en hora local; nunca en el futuro
    private static DateTime Fecha(int diasAtras, int hora, int minuto, int diferenciaHoras, DateTime ahoraUtc)
    {
        var local = ahoraUtc.AddHours(diferenciaHoras).Date.AddDays(-diasAtras).AddHours(hora).AddMinutes(minuto);
        var utc = local.AddHours(-diferenciaHoras);
        return utc > ahoraUtc ? ahoraUtc.AddMinutes(-(60 - minuto)) : utc;
    }
}
