# Comercial Manufacturera LTDA.

Sistema de gestión comercial para una empresa manufacturera: cotizaciones con
reserva temporal de stock, ventas e inventario por bodega. Proyecto de portafolio
con datos ficticios.

**[Ver demostración en línea](https://portafolio.somee.com)** ·
**[Explorar el código](https://github.com/eduardoaraneda/comercial-manufacturera)**

La demostración está alojada en Somee y requiere una cuenta autorizada.
Las credenciales de administración no se publican en este repositorio.

## Funcionalidades y decisiones de diseño

- Cotizaciones con reservas de stock que vencen y conversión a venta.
- Ventas e inventario actualizados dentro de transacciones, con controles de
  concurrencia e idempotencia para evitar sobreventas y operaciones duplicadas.
- Ingreso de inventario desde Excel con revisión previa y movimientos trazables.
- Catálogos de productos, clientes y bodegas, más reportes de la operación.
- Autenticación con ASP.NET Core Identity, permisos por rol y sesiones con vencimiento.
- Arquitectura por capas: Domain, Application, Infrastructure y presentación MVC.

## Tecnologías

Creada a partir de la organizacion de TheLine2: cuatro proyectos, ASP.NET Core MVC,
vistas Razor y frontend TypeScript compilado con Vite. Usa .NET 10, EF Core,
SQL Server, ASP.NET Core Identity y Tailwind CSS.

## Abrir y ejecutar

1. Abrir `ComercialStock.slnx` con Visual Studio compatible con .NET 10.
2. Establecer `Web` (carpeta `ComercialStockWeb`) como proyecto de inicio.
3. Seleccionar el perfil **https** y ejecutar con F5.
4. Abrir **https://localhost:7143/Login**.
5. Elegir **Crea tu cuenta de administrador**, ingresar nombre, correo y una
   contrasena propia. Luego iniciar sesion con esa cuenta.

La configuracion inicial solo esta disponible en Development, desde una conexion
local (loopback), con `Bootstrap:AllowLocalSetup=true` y mientras no exista ningun
usuario de Identity. No hay cuenta predeterminada ni contrasena incluida en el codigo.
El registro y la asignacion del perfil/rol se realizan dentro de una transaccion.
Un bloqueo de SQL Server impide crear dos administradores iniciales simultaneos.

Si el navegador no reconoce el certificado de desarrollo, ejecutar personalmente:

```powershell
dotnet dev-certs https --trust
```

Aceptar la solicitud del sistema para confiar en el certificado de desarrollo de
ASP.NET Core, y volver a abrir la pagina. El login requiere HTTPS.

### Desde una terminal

Desde la carpeta `ComercialStock`:

```powershell
dotnet build ComercialStock.slnx
dotnet run --project ComercialStockWeb/Web.csproj --launch-profile https
```

Los recursos compilados de la interfaz estan en `ComercialStockWeb/wwwroot/dist`.
Al modificar TypeScript, Tailwind o las clases de las vistas, recompilarlos:

```powershell
cd ComercialStockWeb
npm ci
npm run build
```

Para recompilar automaticamente durante el desarrollo: `npm run watch` en una
terminal y la aplicacion .NET en otra. No hace falta ejecutar un servidor Vite aparte.

## Conexion a SQL Server

En desarrollo, `ComercialStockWeb/appsettings.Development.json` apunta a:

```text
Server=localhost;Database=ComercialStock;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10
```

Se usa la cuenta Windows que ejecuta Visual Studio/dotnet. La instancia local
predeterminada identificada durante la configuracion es GUARRIN. Las tablas
comerciales existentes estan en `dbo`, y el mapeo respeta ese esquema.

Para otro servidor, sobrescribir `ConnectionStrings:DefaultConnection` mediante
User Secrets, variables de entorno (`ConnectionStrings__DefaultConnection`) o
`appsettings.Local.json`, excluido de Git y de publicacion. Las variables de entorno
tienen prioridad. No guardar credenciales SQL en archivos versionados.

En produccion, configurar el servidor y un certificado SQL valido: el valor base
usa `TrustServerCertificate=False`. Configurar tambien `AllowedHosts` y HTTPS del
sitio. No habilitar la configuracion inicial local en un despliegue publico.

## Base de datos

En el equipo actual ya existian las 13 tablas comerciales en `dbo`. Se agregaron
solamente las siete tablas `AspNet*` mediante `database/014_identity.sql`.
La aplicacion no crea, migra, elimina ni cambia tablas al arrancar.

Para una instalacion nueva:

1. Crear la base `ComercialStock` en SQL Server.
2. Seleccionarla y ejecutar los 13 archivos de `database/dbo` en orden numerico.
3. Ejecutar `database/014_identity.sql` **una sola vez**. Con sqlcmd usar `-b`.
4. Ejecutar `database/015_operaciones.sql` para las operaciones comerciales.
5. Iniciar la aplicacion en Development y crear el primer administrador local.

Los scripts anteriores de `database/por_tabla` usan esquemas separados y se
conservan como antecedente. **No combinarlos con `database/dbo`**: esta aplicacion
usa las tablas sin prefijos que ya creaste (esquema `dbo`).

`Usuario.IdentidadId` guarda el Id de `AspNetUsers`. Usuario es el perfil de negocio
usado para auditoria; Identity almacena los hashes de contrasena, roles, bloqueos
y sellos de seguridad. No se modifico la estructura existente de Usuario ni se
agrego una FK nueva a identidades previamente existentes.

## Estructura y responsabilidades

```text
ComercialStock.slnx
Domain/
  Entidades/               # Perfil Usuario, sin dependencia de EF o MVC
Application/
  DTO/                     # Resultados del acceso y resumen
  Interfaces/              # Contratos de los casos de uso
Infrastructure/
  Persistence/             # ApplicationDBContext y mapeo dbo
  Servicios/               # Identity, autenticacion, creacion inicial
  Repositorios/            # Consultas de SQL Server
ComercialStockWeb/
  Controllers/             # HTTP, validacion, redirecciones
  Models/                  # Modelos de formulario
  Views/                   # Razor, layouts y formularios
  src/                     # TypeScript y Tailwind
  wwwroot/dist/            # Assets compilados por Vite
Tests/ComercialStock.Tests/ # Integracion sobre SQL Server real
database/                  # Scripts versionados
docs/                      # Decisiones de arquitectura
```

Dependencias: Application -> Domain; Infrastructure -> Application/Domain;
Web -> Application/Infrastructure. Web registra Infrastructure en el arranque,
mientras los controladores operan mediante los contratos de Application.

## Funcionalidad entregada

- Login y cierre de sesion mediante POST con antiforgery.
- Cookies HttpOnly/Secure y HTTPS obligatorio.
- Ticket de acceso con vencimiento absoluto configurable (30 minutos inicialmente),
  sin renovacion automatica, y retorno al login desde paginas abiertas al expirar.
- Hash de contrasenas con ASP.NET Core Identity.
- Bloqueo de 15 minutos despues de cinco intentos incorrectos.
- Limitacion de 20 solicitudes de login/configuracion por IP cada cinco minutos
  (en memoria por instancia de la aplicacion).
- Rutas autenticadas por defecto y rechazo de redirecciones externas.
- Revocacion de sesion al desactivar `Usuario.Activo`.
- Primer administrador con rol Administrador y perfil Usuario en una transaccion.
- Inicio con conteos reales de productos, bodegas, cotizaciones y ventas.
- Interfaz adaptable y mensajes de fallo sin exponer detalles de SQL al usuario.

- Catalogos de productos, clientes y bodegas, con permisos por rol.
- Cotizaciones en borrador, emision con reserva temporal, cancelacion y conversion a venta.
- Ventas directas con validacion de disponibilidad y descuento transaccional de stock.
- Importacion XLSX por bodega con revision previa y confirmacion del ingreso.
- Consulta de existencias fisicas, reservadas y disponibles.
- Reportes de ventas y movimientos, exportacion Excel e impresion de documentos.

Los documentos son internos: no incluyen integracion tributaria con el SII.
Todavia no se implementan administracion web de usuarios adicionales, recuperacion
por correo ni MFA. La herramienta opcional `Tools/SeedDemo` crea datos ficticios y
cuentas de prueba con contrasenas generadas en cada instalacion nueva.

### Primer recorrido

Crear una bodega y un cliente activos, revisar los productos y entrar a **Ingresos
Excel > Nueva carga**. Descargar la plantilla, completar `SKU` y `Cantidad`, elegir
la bodega, cargar y validar, y **Confirmar ingreso**. La carga suma unidades.
Luego crear una cotizacion, guardar el borrador y **Emitir y reservar** (24 horas
por defecto). **Convertir en venta** consume la reserva y descuenta el stock fisico.
Tambien se pueden crear ventas directamente. Consultar el resultado en Inventario
y Reportes. La base de datos y sus registros no se incluyen en el repositorio.

## Pruebas

```powershell
dotnet test ComercialStock.slnx
```

Requieren SQL Server y una cuenta de desarrollo con permiso para crear y eliminar
bases de prueba. Cada prueba crea `ComercialStock_Test_<guid>` y la elimina al
terminar; no utiliza la base ComercialStock. Para otra instancia definir
`COMERCIALSTOCK_TEST_SQL` con la conexion de pruebas. No usar credenciales de produccion.

Cubren registro inicial, persistencia del hash y perfil, cookies, login/logout,
redirecciones, antiforgery, bloqueo, perfil inactivo y configuracion concurrente.
Tambien verifican vencimiento con actividad y con/sin Recordar mediante un reloj
controlado. Las pruebas del observador de sesion se ejecutan con `npm test` en
ComercialStockWeb (Node.js 24).

La configuracion del tiempo y el flujo del ticket de autenticacion estan explicados
en [Sesion y vencimiento](docs/ADR-002-sesion-y-vencimiento.md).

## Publicacion

Compilar primero el frontend y luego ejecutar:

```powershell
dotnet publish ComercialStockWeb/Web.csproj -c Release -o artifacts/publish
```

En un despliegue con varias instancias se deben compartir las claves de Data
Protection y revisar la limitacion de solicitudes distribuida. La preparacion de
cuentas de produccion debe hacerse mediante un proceso administrativo controlado;
la pantalla de primer ingreso entregada es exclusivamente para desarrollo local.
