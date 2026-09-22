# ADR 002: ticket de acceso y vencimiento absoluto

La aplicacion MVC usa ASP.NET Core Identity con un ticket de autenticacion
cifrado y autenticado mediante Data Protection dentro de la cookie
`__Host-ComercialStock.Auth`. Es una credencial de acceso, pero no es un JWT.
No se guarda en localStorage ni se expone a JavaScript. La cookie es HttpOnly,
Secure y SameSite=Lax. Los formularios mantienen su proteccion antiforgery.

## Flujo

1. `ServicioAcceso.IniciarSesionAsync` valida usuario, perfil activo y contrasena
   con `SignInManager.PasswordSignInAsync`. Identity aplica bloqueo por intentos.
2. El middleware emite un ticket con identidad/roles, IssuedUtc y ExpiresUtc.
3. `Autenticacion:DuracionMinutos` en appsettings.json determina su duracion
   (30 minutos inicialmente; valores permitidos entre 1 y 1440).
4. `SlidingExpiration=false` impide prolongar la sesion al navegar. Tambien se
   desactiva la renovacion solicitada por el validador de sellos de Identity,
   conservando sus comprobaciones de revocacion. Para actualizar roles se vuelve
   a iniciar sesion. Se sigue verificando Usuario.Activo en cada peticion.
5. Al vencer, el servidor rechaza el ticket. Una navegacion protegida redirige a
   Login con ReturnUrl local y el aviso de sesion vencida/inactiva. AJAX y rutas
   /api reciben 401; no se devuelven paginas de login como si fueran datos.
6. `SesionController.Estado` devuelve 401 o solo las fechas de vencimiento y hora
   del servidor. No devuelve el token ni datos personales. Usa Cache-Control no-store.
7. `src/site/sesion.ts`, incluido en el layout autenticado, consulta el estado al
   cargar, cada 30 segundos como maximo, al recuperar foco/visibilidad y al vencer
   el plazo. Si recibe 401, reemplaza la pagina por el login. Verifica nuevamente
   al vencer para respetar un nuevo login en otra pestana. Si hay un fallo de red
   reintenta: no lo confunde con credenciales invalidas.

La proteccion real reside en el servidor y funciona aunque JavaScript este
desactivado. La redireccion de una pagina inactiva requiere JavaScript y conexion;
un navegador suspendido la comprobara al volver. No se reproducen automaticamente
formularios POST ni operaciones pendientes despues del nuevo login.

## Configuracion

```json
"Autenticacion": {
  "DuracionMinutos": 30
}
```

Modificar `ComercialStockWeb/appsettings.json` y reiniciar la aplicacion. Tambien
se admite `Autenticacion__DuracionMinutos` como variable de entorno.
Para una prueba manual usar 1, reiniciar y volver a iniciar sesion. Dejar el inicio
abierto y comprobar el retorno al login aproximadamente un minuto despues.
Los tickets anteriores con duracion mayor al nuevo limite se rechazan en su
proxima peticion; aumentar el limite no alarga tickets ya emitidos.

Mantener mi sesion solo conserva la cookie al cerrar el navegador; NO prolonga
su vencimiento. No hay refresh token porque se requiere volver a autenticar al
cumplir el plazo. Para futuras APIs externas se evaluara OAuth/OIDC y access tokens
segun el tipo de cliente, sin mezclar innecesariamente ese flujo con MVC.

## Verificacion

Pruebas de integracion con reloj controlado verifican duracion configurable,
vencimiento aunque haya actividad, comportamiento con/sin Recordar, 401 para
consultas de estado/AJAX y redireccion de navegaciones. No necesitan esperar
minutos reales. Las pruebas de frontend comprueban temporizador, redireccion y
recuperacion tras fallo de red.

Referencia: https://learn.microsoft.com/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0
