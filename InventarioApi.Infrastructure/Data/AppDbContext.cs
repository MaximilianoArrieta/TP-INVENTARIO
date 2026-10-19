using InventarioApi.Domain.Entities;
using InventarioApi.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace InventarioApi.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentUser? _currentUser;

    // ICurrentUser es opcional: las herramientas de migraciones crean el contexto sin él.
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<DetalleCompra> DetalleCompras => Set<DetalleCompra>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetalleVentas => Set<DetalleVenta>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<LogError> LogErrores => Set<LogError>();

    // Permite cargar datos masivos (por ejemplo, los de demostración) sin auditar cada fila
    public bool AuditoriaDesactivada { get; set; }

    // ------------------------------------------------------------------
    // Auditoría automática: cada alta, modificación o baja que pasa por
    // SaveChanges queda registrada en la tabla Auditorias, con el usuario.
    // ------------------------------------------------------------------
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (AuditoriaDesactivada)
            return await base.SaveChangesAsync(cancellationToken);

        var cambios = ChangeTracker.Entries()
            .Where(e => e.Entity is not Auditoria && e.Entity is not LogError &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .Select(e => new CambioPendiente
            {
                Entrada = e,
                Estado = e.State,
                // Modificaciones y bajas se describen ANTES de guardar (después se pierden los valores originales)
                Detalle = e.State == EntityState.Added ? null : Describir(e, e.State),
                IdPrevio = e.State == EntityState.Deleted ? ObtenerId(e) : null
            })
            .Where(c => c.Estado != EntityState.Modified || !string.IsNullOrEmpty(c.Detalle))
            .ToList();

        var resultado = await base.SaveChangesAsync(cancellationToken);

        if (cambios.Count > 0)
        {
            var usuarioId = _currentUser?.UsuarioId;
            foreach (var c in cambios)
            {
                Auditorias.Add(new Auditoria
                {
                    Fecha = DateTime.UtcNow,
                    UsuarioId = usuarioId,
                    Accion = c.Estado switch
                    {
                        EntityState.Added => "Crear",
                        EntityState.Modified => "Modificar",
                        _ => "Eliminar"
                    },
                    Entidad = c.Entrada.Metadata.ClrType.Name,
                    // Las altas recién tienen Id después de guardar
                    EntidadId = c.IdPrevio ?? ObtenerId(c.Entrada),
                    Detalle = c.Detalle ?? Describir(c.Entrada, EntityState.Added)
                });
            }
            await base.SaveChangesAsync(cancellationToken);
        }

        return resultado;
    }

    private sealed class CambioPendiente
    {
        public required EntityEntry Entrada { get; init; }
        public EntityState Estado { get; init; }
        public string? Detalle { get; init; }
        public int? IdPrevio { get; init; }
    }

    private static int? ObtenerId(EntityEntry e) =>
        e.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue as int?;

    private static string? Describir(EntityEntry e, EntityState estado)
    {
        var props = e.Properties.Where(p => !p.Metadata.IsPrimaryKey() && p.Metadata.Name != "PasswordHash");

        IEnumerable<string> partes = estado switch
        {
            EntityState.Modified => props
                .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                .Select(p => $"{p.Metadata.Name}: {p.OriginalValue ?? "null"} -> {p.CurrentValue ?? "null"}"),
            EntityState.Added => props.Select(p => $"{p.Metadata.Name}={p.CurrentValue ?? "null"}"),
            EntityState.Deleted => props.Select(p => $"{p.Metadata.Name}={p.OriginalValue ?? "null"}"),
            _ => Enumerable.Empty<string>()
        };

        var texto = string.Join("; ", partes);
        return texto.Length > 1000 ? texto[..1000] + "..." : texto;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Precisión de los importes
        modelBuilder.Entity<Producto>()
            .Property(p => p.Precio).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<DetalleCompra>()
            .Property(d => d.PrecioUnitario).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<DetalleVenta>()
            .Property(d => d.PrecioUnitario).HasColumnType("decimal(18,2)");

        // Roles: tabla propia, nombre único y datos iniciales
        modelBuilder.Entity<Rol>().HasIndex(r => r.Nombre).IsUnique();
        modelBuilder.Entity<Rol>().HasData(
            new Rol { Id = 1, Nombre = "Admin" },
            new Rol { Id = 2, Nombre = "Cajero" });

        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.NombreUsuario).IsUnique();
        modelBuilder.Entity<Usuario>()
            .HasOne(u => u.Rol).WithMany(r => r.Usuarios)
            .HasForeignKey(u => u.RolId)
            .OnDelete(DeleteBehavior.Restrict);

        // Evitar nombres de categoría duplicados
        modelBuilder.Entity<Categoria>()
            .HasIndex(c => c.Nombre).IsUnique();

        // Detalles: se borran con su encabezado, pero un producto con movimientos no se puede borrar
        modelBuilder.Entity<DetalleCompra>()
            .HasOne(d => d.Compra).WithMany(c => c.Detalles)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DetalleCompra>()
            .HasOne(d => d.Producto).WithMany(p => p.DetallesCompra)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DetalleVenta>()
            .HasOne(d => d.Venta).WithMany(v => v.Detalles)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DetalleVenta>()
            .HasOne(d => d.Producto).WithMany(p => p.DetallesVenta)
            .OnDelete(DeleteBehavior.Restrict);

        // Un mismo producto no puede aparecer dos veces en la misma compra/venta
        modelBuilder.Entity<DetalleCompra>()
            .HasIndex(d => new { d.CompraId, d.ProductoId }).IsUnique();
        modelBuilder.Entity<DetalleVenta>()
            .HasIndex(d => new { d.VentaId, d.ProductoId }).IsUnique();

        // Auditoría y errores: si se borra un usuario, el registro queda (sin usuario)
        modelBuilder.Entity<Auditoria>()
            .HasOne(a => a.Usuario).WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<LogError>()
            .HasOne(l => l.Usuario).WithMany()
            .HasForeignKey(l => l.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
