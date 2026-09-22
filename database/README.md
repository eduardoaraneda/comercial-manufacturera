# ComercialStock: base de datos inicial

## Ejecucion

1. Abrir `001_crear_base_y_tablas.sql` en SQL Server Management Studio (SSMS).
2. Conectarse a una instancia de desarrollo de Microsoft SQL Server.
3. Ejecutar el archivo completo con una cuenta que pueda crear bases y tablas.

Tambien se puede ejecutar con `sqlcmd -S <servidor> -E -b -i 001_crear_base_y_tablas.sql`.
`-b` devuelve un error al proceso si falla el script. No incluir contrasenas en comandos.

La base se crea solo si no existe. La estructura se crea en una transaccion:
si falla alguna tabla o indice, se revierte la estructura creada en esa transaccion.
La creacion de la base queda fuera de esa transaccion. Una segunda ejecucion aborta
si encuentra alguno de los 13 objetos previstos; no borra datos ni sustituye tablas.

## Alcance y decisiones

- Una bodega por cotizacion, venta o importacion.
- Una cotizacion se convierte en una sola venta completa.
- El Excel registra recepciones que **agregan** unidades; no reemplaza el saldo.
- La reserva reduce disponibilidad, pero no cantidad fisica.
- Disponibilidad = saldo fisico menos reservas con estado Activa y VenceEn > ahora UTC.
- Las reservas vencidas deben cerrarse antes de generar otra reserva activa para la misma linea.
- PrecioReferencia usa la moneda base de la aplicacion; cualquier conversion se decide antes de guardar los precios del documento.
- Las tasas se guardan como proporcion (por ejemplo, 0.19); el script no determina tasas aplicables.
- Los importes totales se calculan desde los detalles. Falta definir redondeo antes de implementar ventas.
- Numero es un identificador comercial unico proporcionado por la aplicacion; no es un folio tributario.
- Las FK utilizan NO ACTION por defecto. No hay eliminaciones en cascada.
- Usuario es el perfil de auditoria: no contiene contrasenas ni reemplaza las tablas del proveedor de autenticacion.
- No se incluyen devoluciones, anulaciones de ventas, traslados ni ajustes fisicos todavia.

## Garantias pendientes de implementar en operaciones transaccionales

Este archivo crea el esquema; **no implementa las operaciones de negocio**.
En particular, una FK verifica que el registro relacionado exista, pero no compara
automaticamente producto, cantidad o bodega de toda la cadena documental.

Los procedimientos o servicios deben:

1. Serializar reserva/venta/liberacion por fila de Existencia, con una estrategia de
   bloqueo compartida y un orden consistente para documentos de varios productos.
2. Comprobar stock disponible dentro de esa transaccion y rechazar una reserva excesiva.
3. Verificar que reserva y linea de cotizacion coincidan en producto, cantidad, bodega
   y vencimiento. Una cotizacion emitida no se edita sin gestionar antes sus reservas.
4. Al convertir una cotizacion, respetar sus reservas vigentes sin descontarlas dos veces,
   o volver a comprobar disponibilidad si vencieron. Guardar documento, detalles,
   consumo de reservas, saldo y movimientos en la misma transaccion.
5. Verificar que cliente, bodega, moneda y lineas de la venta correspondan a su cotizacion.
6. Al importar, exigir al menos una fila, productos validos, cantidades positivas,
   ausencia de errores y revision de duplicados. Congelar filas validadas o revalidarlas
   dentro de la transaccion de aplicacion, y actualizar saldo, movimientos y estado juntos.
7. Verificar que producto, bodega y cantidad de cada movimiento coincidan con su origen.
8. Reutilizar ClaveOperacion en reintentos; recuperar el resultado anterior y rechazar
   el uso de la misma clave con datos diferentes. UNIQUE es una barrera, no todo el flujo.
9. Restringir escrituras directas y proteger documentos confirmados y movimientos
   historicos mediante permisos y operaciones controladas. Las PK/FK no los hacen inmutables.

No hay triggers ni SP en esta entrega. No ejecutar ventas reales hasta implementar
y probar estos flujos, incluida concurrencia, rollback e idempotencia.
