using System;
using System.Collections.Generic;
using System.Text;

namespace InventarioApi.Domain.Entities;
    public class Venta
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.UtcNow;
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }
        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }
        public decimal Total { get; set; }
        public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
    }
