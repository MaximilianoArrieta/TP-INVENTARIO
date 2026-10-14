using InventarioApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InventarioApi.Infrastructure.Data;

// Datos mínimos para que el sistema funcione desde el primer arranque.
public static class DatosIniciales
{
    public const string ClienteGenericoNombre = "Consumidor";
    public const string ClienteGenericoApellido = "Final";

    public static async Task CargarAsync(AppDbContext db, IConfiguration config)
    {
        await ObtenerConsumidorFinalIdAsync(db);
        await CrearAdminInicialAsync(db, config);
    }

    // Cliente genérico para las ventas de caja sin cliente identificado.
    public static async Task<int> ObtenerConsumidorFinalIdAsync(AppDbContext db)
    {
        var id = await db.Clientes
            .Where(c => c.Nombre == ClienteGenericoNombre && c.Apellido == ClienteGenericoApellido)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();
        if (id != 0) return id;

        var cliente = new Cliente { Nombre = ClienteGenericoNombre, Apellido = ClienteGenericoApellido };
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return cliente.Id;
    }

    // Como el registro público solo crea cajeros, el primer administrador
    // se crea solo, con los datos de la sección "AdminInicial" de appsettings.
    private static async Task CrearAdminInicialAsync(AppDbContext db, IConfiguration config)
    {
        if (await db.Usuarios.AnyAsync(u => u.Rol!.Nombre == "Admin")) return;

        var nombre = config["AdminInicial:Usuario"];
        var password = config["AdminInicial:Password"];
        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(password)) return;
        if (await db.Usuarios.AnyAsync(u => u.NombreUsuario == nombre)) return;

        var rolAdmin = await db.Roles.FirstAsync(r => r.Nombre == "Admin");
        db.Usuarios.Add(new Usuario
        {
            NombreUsuario = nombre,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Email = config["AdminInicial:Email"],
            RolId = rolAdmin.Id
        });
        await db.SaveChangesAsync();
    }
}
