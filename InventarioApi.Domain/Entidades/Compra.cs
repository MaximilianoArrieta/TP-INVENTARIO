using System;
using System.Collections.Generic;
using System.Text;

namespace InventarioApi.Domain.Entities;
    public class Compra
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.UtcNow;
        public int ProveedorId { get; set; }
        public Proveedor? Proveedor { get; set; }
        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }
        public decimal Total { get; set; }
        public ICollection<DetalleCompra> Detalles { get; set; } = new List<DetalleCompra>();
    }

