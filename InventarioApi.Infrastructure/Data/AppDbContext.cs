using System;
using System.Collections.Generic;
using System.Text;

namespace InventarioApi.Infrastructure.Data
{
    using InventarioApi.Domain.Entities;
    using Microsoft.EntityFrameworkCore;

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Categoria> Categorias => Set<Categoria>();
        public DbSet<Producto> Productos => Set<Producto>();
        public DbSet<Proveedor> Proveedores => Set<Proveedor>();
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Compra> Compras => Set<Compra>();
        public DbSet<DetalleCompra> DetalleCompras => Set<DetalleCompra>();
        public DbSet<Venta> Ventas => Set<Venta>();
        public DbSet<DetalleVenta> DetalleVentas => Set<DetalleVenta>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Producto>()
                .Property(p => p.Precio).HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Compra>()
                .Property(c => c.Total).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DetalleCompra>()
                .Property(c => c.PrecioUnitario).HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Venta>()
                .Property(v => v.Total).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DetalleVenta>()
                .Property(v => v.PrecioUnitario).HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.NombreUsuario).IsUnique();

            // Evitar múltiples cascadas conflictivas en SQL Server
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
        }
    }
}
