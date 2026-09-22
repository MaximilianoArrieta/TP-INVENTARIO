# Inventario API — API REST de Gestión de Inventario

API REST desarrollada en **.NET 10** con controladores, conectada a **SQL Server** mediante Entity Framework Core. Permite gestionar productos, categorías, proveedores, clientes, usuarios, ingresos de stock (compras) y salidas de stock (ventas), con autenticación JWT y paginado en todos los listados extensos.

## Tecnologías utilizadas

- **.NET 10** / ASP.NET Core Web API (controladores)
- **Entity Framework Core** + **SQL Server**
- **JWT (JSON Web Tokens)** para autenticación y autorización basada en roles
- **BCrypt.Net** para hash de contraseñas
- **Swagger / OpenAPI** para documentación interactiva de la API
- **CORS** configurado en modo permisivo

## Estructura del proyecto

```
InventarioApi/
├── InventarioApi.Api/            → Controladores, Program.cs, appsettings, wwwroot/uploads
├── InventarioApi.Domain/         → Entidades y clases de paginado (PagedResult, PaginationParams)
└── InventarioApi.Infrastructure/ → AppDbContext, migraciones, TokenService, extensiones de paginado
```

## Modelo de datos

El esquema relacional incluye 9 tablas: `Categorias`, `Productos`, `Proveedores`, `Clientes`, `Usuarios`, `Compras`, `DetalleCompras`, `Ventas`, `DetalleVentas`.

Los ingresos (compras a proveedores) y las salidas (ventas a clientes) se modelan como **encabezado + detalle**, permitiendo registrar varios productos en una misma operación. Cada operación guarda fecha, proveedor/cliente y usuario que la registró, y **afecta automáticamente el stock** del producto (las compras lo suman, las ventas lo restan). No se permite registrar una venta si no hay stock suficiente.


## Endpoints principales

| Recurso | Rutas |
|---|---|
| Autenticación | `POST /api/auth/registro`, `POST /api/auth/login` |
| Categorías | `GET/POST/PUT/DELETE /api/categorias` (con paginado) |
| Productos | `GET/POST/PUT/DELETE /api/productos`, `POST /api/productos/{id}/imagen` |
| Proveedores | `GET/POST/PUT/DELETE /api/proveedores` |
| Clientes | `GET/POST/PUT/DELETE /api/clientes` |
| Compras (ingreso de stock) | `GET/POST /api/compras` |
| Ventas (salida de stock) | `GET/POST /api/ventas` |

Todos los listados (`GET`) aceptan `?pageNumber=1&pageSize=10` para paginado. La documentación interactiva completa está disponible en `/swagger` una vez que la API está corriendo.

## Seguridad

- Autenticación mediante **JWT**: se obtiene un token con `POST /api/auth/login` y se envía en el header `Authorization: Bearer {token}` en los endpoints protegidos.
- Autorización por roles (`Admin` / `Vendedor`) usando `[Authorize(Roles = "...")]`.
- **CORS** configurado en modo permisivo (`AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`).

## Cómo correrlo localmente

1. Abrir `InventarioApi.sln` en Visual Studio 2026 (con la carga de trabajo "Desarrollo de ASP.NET y web").
2. Configurar la cadena de conexión a tu SQL Server local en `InventarioApi.Api/appsettings.json` (`ConnectionStrings:DefaultConnection`).
3. Aplicar las migraciones desde la Consola del Administrador de paquetes (proyecto predeterminado: `InventarioApi.Infrastructure`):
   ```
   Update-Database
   ```
4. Ejecutar con **F5**. Se abre automáticamente en `https://localhost:{puerto}/swagger`.

## API publicada

- **URL pública:** `http://inventarioapi.runasp.net/swagger/index.html`
- **Usuario de prueba:** `admin`
- **Contraseña de prueba:** `Admin123!`

> Recordar loguearse primero con `/api/auth/login`, copiar el `token` de la respuesta, y pegarlo en el botón **Authorize** 
## Autor

Trabajo práctico desarrollado por Arrieta Maximiliano 2026.
