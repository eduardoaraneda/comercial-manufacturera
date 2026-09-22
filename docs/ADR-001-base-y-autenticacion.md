# ADR 001: estructura por capas y autenticacion

Estado: aceptado para la primera entrega.

## Contexto

El sistema de portafolio parte de una base ComercialStock existente con 13 tablas
dbo. Se solicita conservar la organizacion de TheLine2 (Domain, Application,
Infrastructure, Web) y entregar login y conexion funcionales.

## Decision

Mantener una aplicacion ASP.NET Core MVC desplegable y una base SQL Server.
Usar bibliotecas de clases para Domain/Application/Infrastructure y el SDK Web
solamente para el proyecto de presentacion. Mantener Views y src con Vite como en
el proyecto de referencia; TypeScript mejora formularios Razor y Tailwind genera
los estilos. No se requiere React para este primer flujo de formularios.

ASP.NET Core Identity controla las credenciales y el estado de autenticacion.
La tabla Usuario conserva nombre, estado y el identificador Identity para auditoria.
Las siete tablas Identity se agregan por un script explicito; no hay migraciones
automaticas al arrancar ni recreacion de las tablas comerciales existentes.

La primera cuenta se crea en desarrollo, desde loopback, antes de que exista otra.
El caso de uso obtiene un bloqueo transaccional antes de volver a verificar que
no haya cuentas. Crear la identidad, asignar Administrador y guardar el perfil
de negocio forman una sola transaccion.

## Alternativas consideradas

- Copiar la comparacion directa de contrasenas del proyecto original: descartada
  porque no aporta almacenamiento seguro de credenciales ni bloqueo por intentos.
- Crear un sistema propio de credenciales: descartado por duplicar mecanismos
  existentes de hashing, cookies, sellos de seguridad y bloqueo.
- Separar un servicio de identidad remoto: costo de despliegue e integracion
  innecesario para el alcance local inicial.

## Consecuencias

Se agregan tablas y dependencias de Identity. Application define interfaces sin
referencias a EF; Infrastructure implementa autenticacion y acceso a SQL. El login
depende de la disponibilidad de SQL Server. Se consulta el perfil activo al validar
la cookie para revocar el acceso cuando se desactiva el usuario.

Las pruebas usan SQL Server real para comprobar transacciones y concurrencia de
la primera cuenta. A futuro se necesitaran flujos administrativos de altas,
recuperacion y MFA, y migraciones incrementales para cambios del modelo.
