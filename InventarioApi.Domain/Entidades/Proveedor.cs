using System;
using System.Collections.Generic;
using System.Text;

namespace InventarioApi.Domain.Entities;
    public class Proveedor
    {
        public int Id { get; set; }
        public string RazonSocial { get; set; } = string.Empty;
        public string? Contacto { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Direccion { get; set; }
        public ICollection<Compra> Compras { get; set; } = new List<Compra>();
    }
