using System;
using System.Collections.Generic;
using System.Text;

namespace InventarioApi.Domain.Entities;
    public class Cliente
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
    }
