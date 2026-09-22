using System;
using System.Collections.Generic;
using System.Text;

namespace InventarioApi.Domain.Entities;
    public class Usuario
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Rol { get; set; } = "Vendedor"; // Admin | Vendedor
        public bool Activo { get; set; } = true;
    }