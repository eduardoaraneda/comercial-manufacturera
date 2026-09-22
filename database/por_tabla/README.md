# Scripts por tabla

Ejecutar como consultas de SQL Server en el orden indicado. Cada archivo contiene los campos, PK, FK, DEFAULT, CHECK, UNIQUE e indices correspondientes a su tabla.

Esta carpeta es una alternativa al script completo, no una migracion. Si ya creaste las tablas con el script anterior, no vuelvas a crearlas. No hay DROP ni borrados automaticos.

El archivo 00 usa CREATE DATABASE y CREATE SCHEMA directos. Si la base o alguno de los esquemas ya existe, omite exclusivamente su bloque CREATE correspondiente. Cada CREATE SCHEMA esta separado por GO.

Los archivos no usan TRY/CATCH, THROW ni SQL dinamico. Son scripts de ejecucion, no archivos listos para compilar como un proyecto SQL declarativo (.sqlproj).

| Orden | Archivo |
|---|---|
| 00 | [Base y esquemas](00_base_y_esquemas.sql) |
| 01 | [seguridad.Usuario](01_seguridad.Usuario.sql) |
| 02 | [comercial.Cliente](02_comercial.Cliente.sql) |
| 03 | [inventario.Producto](03_inventario.Producto.sql) |
| 04 | [inventario.Bodega](04_inventario.Bodega.sql) |
| 05 | [inventario.Existencia](05_inventario.Existencia.sql) |
| 06 | [comercial.Cotizacion](06_comercial.Cotizacion.sql) |
| 07 | [comercial.CotizacionDetalle](07_comercial.CotizacionDetalle.sql) |
| 08 | [inventario.ReservaStock](08_inventario.ReservaStock.sql) |
| 09 | [comercial.Venta](09_comercial.Venta.sql) |
| 10 | [comercial.VentaDetalle](10_comercial.VentaDetalle.sql) |
| 11 | [inventario.ImportacionInventario](11_inventario.ImportacionInventario.sql) |
| 12 | [inventario.ImportacionInventarioDetalle](12_inventario.ImportacionInventarioDetalle.sql) |
| 13 | [inventario.MovimientoStock](13_inventario.MovimientoStock.sql) |

## Reglas que requieren procedimientos o servicios

Las FK comprueban existencia de registros; no implementan reservas, ventas ni importaciones. La disponibilidad, las transiciones de estado, la coincidencia de bodega/producto/cantidad con el documento origen y la actualizacion conjunta de saldo y movimientos se implementaran en transacciones.

IdentidadId es un identificador externo de autenticacion, no una FK a una tabla incluida aqui. Usuario no almacena contrasenas.

La duracion de la reserva se proporciona en VenceEn, sin fijar aun 24 horas. El Excel agrega unidades. Las cantidades usan decimal(18,3); las tasas de impuesto son proporciones entre 0 y 1.

Los CHECK validan valores y las FK usan NO ACTION; no hay borrados en cascada. Los indices unicos filtrados restringen una reserva activa por linea, una venta por cotizacion y un movimiento por linea de origen.

Vease [el contexto completo del modelo](../README.md) para las reglas pendientes y el alcance.
