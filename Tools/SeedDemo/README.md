# Datos de ejemplo para desarrollo local

Desde la raiz ComercialStock ejecutar:

```powershell
dotnet run --project Tools/SeedDemo/SeedDemo.csproj
```

La herramienta apunta exclusivamente a localhost / ComercialStock con autenticacion
Windows. Agrega 12 productos de mobiliario con precios ficticios netos en CLP por
unidad y tres cuentas: admin, ventas y bodega en el dominio reservado
comercialstock.test. Genera una contrasena aleatoria distinta por cuenta y muestra
las credenciales al terminar; las almacena en SQL Server mediante el hasher de Identity.

La carga se confirma en una transaccion. Si se repite, no duplica los SKU ni las
cuentas existentes y no cambia sus precios, contrasenas o roles. No carga stock.

Los roles quedan asignados en Identity; las restricciones por modulo se implementaran
cuando se desarrollen esos modulos. Actualmente las tres cuentas pueden acceder al inicio.

Al existir usuarios de Identity, la pantalla de configuracion del primer administrador
queda cerrada, igual que cuando se completa manualmente.
