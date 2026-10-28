# Caja y Stock — App móvil (tienda online para Android)

Trabajo práctico de **Programación Móvil (TP6)**: frontend **.NET MAUI Blazor Hybrid** para **Android**, vinculado a la **API REST en ASP.NET Core (.NET 10)** del TP5. Es la tienda online del negocio: los clientes ven el catálogo con la disponibilidad de cada producto, arman el carrito y pagan (pago simulado). El administrador registra el ingreso de mercadería, consulta el stock, genera reportes PDF y revisa la auditoría desde el celular.

La app usa **la misma API y la misma base de datos** que el TP6 de Programación Web ([README.md](README.md)). Las ventas de la tienda y las de la caja web descuentan el mismo stock y aparecen juntas en los reportes, diferenciadas por canal (**App** o **Caja**).

## Cómo probarla

| | |
|---|---|
| **API publicada** (la que usa la app por defecto) | https://inventarioapi.runasp.net/swagger |
| **Proyecto** | `InventarioApi.Movil` dentro de la solución `InventarioApi/InventarioApi.slnx` |
| **Plataforma** | Android 7.0 o superior (probada en emulador Pixel 9 · Android 16) |

### Usuarios de prueba

| Usuario | Contraseña | Rol | Qué ve en la app |
|---|---|---|---|
| *(crear desde la app)* | | Cliente | Catálogo, carrito, pago y Mis compras |
| `admin` | `Admin123!` | Administrador | Stock, ingreso de mercadería, reportes, ventas, auditoría y usuarios |
| `cajero1` | `Cajero123!` | Cajero | Catálogo con stock y sus ventas |

Sin iniciar sesión se puede recorrer el catálogo y armar el carrito; para pagar la app pide ingresar o crear una cuenta de cliente.

### Tarjetas de prueba (pago simulado)

| Tarjeta | Resultado |
|---|---|
| `4509 9535 6623 3704` | Aprobada |
| `4000 0000 0000 0002` | Rechazada por fondos insuficientes |
| Cualquier compra de más de $ 500.000 con tarjeta | Rechazada por límite |

Vencimiento futuro (MM/AA) y cualquier código de 3 dígitos. La pantalla de pago tiene un botón que completa estos datos. Billetera virtual y transferencia siempre se aprueban.

## Funcionalidades

**Catálogo (estilo tienda online)**
- Grilla de productos con foto, precio y cuotas sin interés.
- Búsqueda por nombre y filtro por categoría.
- Disponibilidad visible en cada producto: *Disponible*, *¡Últimas 3 unidades!*, *¡Último disponible!* o *Sin stock* (el producto agotado se muestra atenuado y no se puede comprar).
- Detalle del producto con descripción, stock disponible y selector de cantidad limitado al stock.

**Carrito y compra (Cliente)**
- El carrito se guarda en el celular aunque se cierre la app.
- Antes de pagar se vuelven a consultar precio y stock de cada producto: si algo cambió o se agotó, se avisa y se ajusta.
- Pago simulado con tarjeta de crédito (1, 3, 6 o 12 cuotas), tarjeta de débito, billetera virtual o transferencia. Vista previa de la tarjeta mientras se completan los datos.
- La API valida todo de nuevo dentro de una transacción: bloquea el stock, autoriza el pago y recién entonces registra la venta. Si el pago se rechaza, **no se registra la venta ni se toca el stock**, y el rechazo queda auditado.
- Comprobante con código de autorización y sección **Mis compras** con el historial.

**Administración (Admin)**
- **Stock:** el catálogo muestra el número de unidades y resalta el stock bajo.
- **Fotos de productos:** desde el detalle se puede **sacar una foto con la cámara** o elegirla de la galería; se achica en el celular y se sube a la API.
- **Ingreso de mercadería:** proveedor (o alta de uno nuevo), productos, cantidades y costo; muestra el stock resultante antes de confirmar.
- **Reportes PDF:** stock, ventas (salidas, con totales por canal) e ingresos (entradas) por período. Se **ven dentro de la app**, se **comparten** (WhatsApp, Gmail, Drive…) o se **envían por correo** desde la API.
- **Ventas:** las de la caja web y las de la app, con su canal.
- **Auditoría y errores:** movimientos filtrables por acción y entidad, registro de errores con detalle técnico y botón para generar un error de prueba.
- **Usuarios y roles:** cambio de rol y activación/desactivación.

**Configuración**
- Pantalla **Servidor** para elegir a qué API conectarse: la publicada (por defecto), la API corriendo en la PC (para el emulador) u otra dirección, con prueba de conexión.

## Arquitectura

```
InventarioApi.Movil/
├── MauiProgram.cs            → registro de servicios, cultura es-AR
├── MainPage.xaml             → BlazorWebView que aloja la interfaz Blazor
├── Components/
│   ├── Routes.razor          → rutas con AuthorizeRouteView (control de acceso por rol)
│   ├── Layout/               → MainLayout con barra de pestañas según el rol
│   ├── Pages/                → Catálogo, Producto, Carrito, Pagar, Mis compras, Ingreso, Reportes, Ventas, Auditoría, Usuarios, Cuenta, Servidor…
│   └── Shared/               → componentes reutilizables (tarjeta de producto, precio, disponibilidad, íconos…)
├── Services/
│   ├── ApiClient.cs          → todas las llamadas HTTP; agrega el token y traduce los errores
│   ├── SesionUsuario.cs      → AuthenticationStateProvider; token JWT en SecureStorage
│   ├── CarritoCompras.cs     → carrito persistido en Preferences
│   ├── ConfiguracionApp.cs   → servidor elegido
│   ├── ArchivosPdf.cs        → guardar, ver, abrir y compartir PDF
│   └── FotosProducto.cs      → cámara / galería y reducción de la imagen
├── Platforms/Android/
│   ├── LectorPdf.cs          → muestra el PDF con PdfRenderer (lector nativo de Android)
│   ├── AndroidManifest.xml   → permisos (internet, cámara)
│   └── Resources/xml/network_security_config.xml → solo HTTPS, salvo la API local del emulador
└── wwwroot/                  → index.html y estilos (diseño propio, sin Bootstrap)
```

**Blazor Hybrid:** las pantallas son componentes Razor como en la web, pero se ejecutan de forma nativa dentro de la app (.NET corre en el celular) y se dibujan en un WebView. Por eso pueden usar funciones del dispositivo que una página web no tiene: almacenamiento seguro, cámara, compartir archivos y el lector de PDF de Android.

### Cambios en la API (aditivos, sin afectar la web)

| Cambio | Para qué |
|---|---|
| Rol `Cliente` y `POST api/auth/registro-cliente` | Registro de compradores desde la app (crea la ficha de cliente y su usuario) |
| `TiendaController`: `GET api/tienda/medios-pago`, `POST api/tienda/checkout`, `GET api/tienda/mis-compras` | Pago simulado y compras del cliente |
| `VentaService` | Registra la venta con bloqueo de stock y autorización del pago dentro de la misma transacción |
| `SimuladorPagos` | Valida la tarjeta (algoritmo de Luhn, vencimiento, código) y simula la respuesta del banco; guarda solo los últimos 4 dígitos |
| Columna `Ventas.Canal` y campo `Canal` en `GET api/ventas` | Distinguir ventas de la caja y de la app; el reporte de ventas muestra los totales por canal |
| Auditoría `PagoRechazado` | Los pagos rechazados quedan registrados aunque no se cree la venta |

## Base de datos

Misma base SQL Server del TP web, **normalizada hasta tercera forma normal**, con 14 tablas. Diagrama entidad-relación (editable en `diagrama-er.drawio`):

![Diagrama entidad-relación](diagrama-er.png)

Tablas y columnas que agrega la tienda (migración `TiendaMovil`):

- **`MediosPago`** (`Id`, `Nombre` único, `RequiereTarjeta`, `PermiteCuotas`): catálogo de medios de pago, cargado como dato inicial.
- **`Pagos`** (`Id`, `VentaId` único, `MedioPagoId`, `Cuotas`, `UltimosDigitos`, `CodigoAutorizacion`): un pago por venta de la app.
- **`Ventas.Canal`**: `Caja` o `App` (las ventas existentes quedan como `Caja`).
- **`Usuarios.ClienteId`** (único, admite NULL): vincula el usuario con rol Cliente con su ficha en `Clientes`.

Por qué respeta la 3FN:

- **El pago no guarda el importe ni la fecha:** se obtienen de la venta (fecha) y de sus detalles (total). Guardarlos en `Pagos` sería un dato derivado que podría quedar desincronizado.
- **Los medios de pago están en su propia tabla:** el nombre y sus reglas (si requiere tarjeta, si permite cuotas) dependen del medio, no de cada pago.
- **`Pagos` es una tabla aparte de `Ventas`:** las ventas de la caja no tienen pago con tarjeta; así no quedan columnas vacías en cada venta.
- **El cliente no se duplica en `Usuarios`:** nombre, apellido, email y dirección están solo en `Clientes`; el usuario guarda la referencia `ClienteId`.
- **Por seguridad no se guarda el número de tarjeta ni el código**, solo los últimos 4 dígitos.

## Seguridad y control de acceso

- **JWT:** el token se guarda en el **almacenamiento seguro** del celular (`SecureStorage`, cifrado por Android) y se envía en cada pedido. Si vence, la app cierra la sesión y vuelve al login.
- **Roles en la API** con `[Authorize(Roles = "...")]`: solo un Cliente puede pagar; solo el Admin ingresa mercadería, ve reportes, auditoría y usuarios.
- **Roles en la app:** `AuthorizeRouteView` protege cada pantalla y la barra de pestañas cambia según el rol. Es solo comodidad: la API vuelve a controlar todo.
- **El registro desde la app solo crea clientes.** No se puede crear un administrador ni un cajero desde la tienda.
- **El precio lo pone la API** desde la base de datos; la app solo envía producto y cantidad.
- **Solo HTTPS:** Android bloquea conexiones sin cifrar; la única excepción configurada es la API local vista desde el emulador (`10.0.2.2`).

## Auditoría y registro de errores

Se reutiliza el mecanismo de la API: cada alta, modificación y baja se audita automáticamente (incluidas las ventas de la app, sus pagos y el descuento de stock), además de los ingresos al sistema, los pagos rechazados y los reportes generados o enviados. Los errores no controlados se guardan en `LogErrores` y la app muestra el número de error. Desde **Más → Auditoría y errores** se pueden consultar y generar un error de prueba.

## Cómo ejecutarla

1. Visual Studio 2026 con la carga de trabajo **Desarrollo de .NET Multi-platform App UI** (instala el SDK y el emulador de Android).
2. Abrir `InventarioApi/InventarioApi.slnx`.
3. **Con la API publicada:** click derecho en `InventarioApi.Movil` → *Establecer como proyecto de inicio*, elegir el emulador o el celular y **F5**.
4. **Con la API en la PC:**
   - Preparar la base como indica el [README del TP web](README.md#cómo-ejecutarlo-localmente).
   - Iniciar la API con click derecho en `InventarioApi` → *Depurar → Iniciar sin depurar*.
   - Ejecutar la app con **F5** y, en la app, *Ingresar → Servidor → API en la PC (solo emulador)*.
5. **En un celular:** activar las *Opciones de desarrollador* y la *Depuración USB*, conectarlo por cable, aceptar el aviso en el teléfono, elegirlo en Visual Studio (*Dispositivos locales de Android*) y **F5**. La app queda instalada.

## Tecnologías

.NET 10 · .NET MAUI · Blazor Hybrid · ASP.NET Core Web API · Entity Framework Core · SQL Server · JWT · BCrypt · QuestPDF · MailKit · Android PdfRenderer

## Autores

Trabajo práctico desarrollado por **Arrieta Maximiliano** y **Marco Gonzáles** — 2026.
