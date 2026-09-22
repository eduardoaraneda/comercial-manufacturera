/*
  ComercialStock - estructura inicial para Microsoft SQL Server.
  Ejecutar el archivo completo desde SSMS, Azure Data Studio o sqlcmd.
  GO es un separador del cliente, no una instruccion del motor.

  Script de instalacion inicial: no elimina ni reemplaza tablas existentes.
  Si alguno de los objetos previstos existe, aborta la creacion de tablas.
  CREATE DATABASE requiere permisos y se ejecuta fuera de la transaccion DDL.

  Alcance: una bodega por documento; ventas completas; Excel agrega unidades.
  Las restricciones NO sustituyen las transacciones de reserva, venta e ingreso.
  Fechas UTC. Cantidades en la unidad base de cada producto.
*/
USE [master];
GO

IF DB_ID(N'ComercialStock') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [ComercialStock];');
END;
GO

USE [ComercialStock];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
    IF EXISTS (
        SELECT 1
        FROM (VALUES
            (N'seguridad.Usuario'),
            (N'comercial.Cliente'),
            (N'inventario.Producto'),
            (N'inventario.Bodega'),
            (N'inventario.Existencia'),
            (N'comercial.Cotizacion'),
            (N'comercial.CotizacionDetalle'),
            (N'inventario.ReservaStock'),
            (N'comercial.Venta'),
            (N'comercial.VentaDetalle'),
            (N'inventario.ImportacionInventario'),
            (N'inventario.ImportacionInventarioDetalle'),
            (N'inventario.MovimientoStock')
        ) AS Objetos(Nombre)
        WHERE OBJECT_ID(Objetos.Nombre) IS NOT NULL
    )
    BEGIN
        THROW 50001, N'Ya existen objetos del modelo. Utilice migraciones para actualizar la base existente.', 1;
    END;

    BEGIN TRANSACTION;

    IF SCHEMA_ID(N'seguridad') IS NULL
        EXEC(N'CREATE SCHEMA [seguridad] AUTHORIZATION [dbo];');
    IF SCHEMA_ID(N'comercial') IS NULL
        EXEC(N'CREATE SCHEMA [comercial] AUTHORIZATION [dbo];');
    IF SCHEMA_ID(N'inventario') IS NULL
        EXEC(N'CREATE SCHEMA [inventario] AUTHORIZATION [dbo];');

    -- 1. Perfil de usuario. IdentidadId se vinculara al proveedor de autenticacion.
    -- No hay FK a ASP.NET Identity porque sus tablas aun no forman parte del modelo.
    CREATE TABLE seguridad.Usuario (
        UsuarioId INT IDENTITY(1,1) NOT NULL,
        IdentidadId NVARCHAR(450) NOT NULL,
        Nombre NVARCHAR(150) NOT NULL,
        Activo BIT NOT NULL CONSTRAINT DF_Usuario_Activo DEFAULT (1),
        CreadoEn DATETIME2(3) NOT NULL CONSTRAINT DF_Usuario_CreadoEn DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Usuario PRIMARY KEY (UsuarioId),
        CONSTRAINT UQ_Usuario_IdentidadId UNIQUE (IdentidadId),
        CONSTRAINT CK_Usuario_Nombre CHECK (LEN(LTRIM(RTRIM(Nombre))) > 0),
        CONSTRAINT CK_Usuario_Identidad CHECK (LEN(LTRIM(RTRIM(IdentidadId))) > 0)
    );

    -- 2. Clientes.
    CREATE TABLE comercial.Cliente (
        ClienteId INT IDENTITY(1,1) NOT NULL,
        IdentificadorFiscal NVARCHAR(30) NULL,
        NombreRazonSocial NVARCHAR(200) NOT NULL,
        Email NVARCHAR(254) NULL,
        Telefono NVARCHAR(30) NULL,
        Direccion NVARCHAR(300) NULL,
        Activo BIT NOT NULL CONSTRAINT DF_Cliente_Activo DEFAULT (1),
        CreadoEn DATETIME2(3) NOT NULL CONSTRAINT DF_Cliente_CreadoEn DEFAULT (SYSUTCDATETIME()),
        Version ROWVERSION NOT NULL,
        CONSTRAINT PK_Cliente PRIMARY KEY (ClienteId),
        CONSTRAINT CK_Cliente_Nombre CHECK (LEN(LTRIM(RTRIM(NombreRazonSocial))) > 0)
    );

    -- 3. Productos. PrecioReferencia corresponde a la moneda base de la aplicacion.
    CREATE TABLE inventario.Producto (
        ProductoId INT IDENTITY(1,1) NOT NULL,
        SKU NVARCHAR(50) NOT NULL,
        Nombre NVARCHAR(200) NOT NULL,
        Descripcion NVARCHAR(1000) NULL,
        UnidadMedida VARCHAR(10) NOT NULL,
        PrecioReferencia DECIMAL(19,4) NOT NULL CONSTRAINT DF_Producto_Precio DEFAULT (0),
        Activo BIT NOT NULL CONSTRAINT DF_Producto_Activo DEFAULT (1),
        CreadoEn DATETIME2(3) NOT NULL CONSTRAINT DF_Producto_CreadoEn DEFAULT (SYSUTCDATETIME()),
        Version ROWVERSION NOT NULL,
        CONSTRAINT PK_Producto PRIMARY KEY (ProductoId),
        CONSTRAINT UQ_Producto_SKU UNIQUE (SKU),
        CONSTRAINT CK_Producto_SKU CHECK (LEN(LTRIM(RTRIM(SKU))) > 0),
        CONSTRAINT CK_Producto_Nombre CHECK (LEN(LTRIM(RTRIM(Nombre))) > 0),
        CONSTRAINT CK_Producto_Unidad CHECK (LEN(LTRIM(RTRIM(UnidadMedida))) > 0),
        CONSTRAINT CK_Producto_Precio CHECK (PrecioReferencia >= 0)
    );

    -- 4. Bodegas.
    CREATE TABLE inventario.Bodega (
        BodegaId INT IDENTITY(1,1) NOT NULL,
        Codigo NVARCHAR(20) NOT NULL,
        Nombre NVARCHAR(150) NOT NULL,
        Direccion NVARCHAR(300) NULL,
        Activa BIT NOT NULL CONSTRAINT DF_Bodega_Activa DEFAULT (1),
        CONSTRAINT PK_Bodega PRIMARY KEY (BodegaId),
        CONSTRAINT UQ_Bodega_Codigo UNIQUE (Codigo),
        CONSTRAINT CK_Bodega_Codigo CHECK (LEN(LTRIM(RTRIM(Codigo))) > 0),
        CONSTRAINT CK_Bodega_Nombre CHECK (LEN(LTRIM(RTRIM(Nombre))) > 0)
    );

    -- 5. Saldo fisico por producto y bodega.
    CREATE TABLE inventario.Existencia (
        BodegaId INT NOT NULL,
        ProductoId INT NOT NULL,
        CantidadFisica DECIMAL(18,3) NOT NULL CONSTRAINT DF_Existencia_Cantidad DEFAULT (0),
        Version ROWVERSION NOT NULL,
        CONSTRAINT PK_Existencia PRIMARY KEY (BodegaId, ProductoId),
        CONSTRAINT FK_Existencia_Bodega FOREIGN KEY (BodegaId) REFERENCES inventario.Bodega(BodegaId),
        CONSTRAINT FK_Existencia_Producto FOREIGN KEY (ProductoId) REFERENCES inventario.Producto(ProductoId),
        CONSTRAINT CK_Existencia_NoNegativa CHECK (CantidadFisica >= 0)
    );

    -- 6. Cabecera de cotizacion.
    CREATE TABLE comercial.Cotizacion (
        CotizacionId INT IDENTITY(1,1) NOT NULL,
        Numero NVARCHAR(30) NOT NULL,
        ClienteId INT NOT NULL,
        BodegaId INT NOT NULL,
        Estado VARCHAR(20) NOT NULL CONSTRAINT DF_Cotizacion_Estado DEFAULT ('Borrador'),
        Moneda CHAR(3) NOT NULL CONSTRAINT DF_Cotizacion_Moneda DEFAULT ('CLP'),
        CreadaEn DATETIME2(3) NOT NULL CONSTRAINT DF_Cotizacion_CreadaEn DEFAULT (SYSUTCDATETIME()),
        EmitidaEn DATETIME2(3) NULL,
        VenceEn DATETIME2(3) NULL,
        CreadaPorUsuarioId INT NOT NULL,
        Observaciones NVARCHAR(1000) NULL,
        Version ROWVERSION NOT NULL,
        CONSTRAINT PK_Cotizacion PRIMARY KEY (CotizacionId),
        CONSTRAINT UQ_Cotizacion_Numero UNIQUE (Numero),
        CONSTRAINT FK_Cotizacion_Cliente FOREIGN KEY (ClienteId) REFERENCES comercial.Cliente(ClienteId),
        CONSTRAINT FK_Cotizacion_Bodega FOREIGN KEY (BodegaId) REFERENCES inventario.Bodega(BodegaId),
        CONSTRAINT FK_Cotizacion_Usuario FOREIGN KEY (CreadaPorUsuarioId) REFERENCES seguridad.Usuario(UsuarioId),
        CONSTRAINT CK_Cotizacion_Numero CHECK (LEN(LTRIM(RTRIM(Numero))) > 0),
        CONSTRAINT CK_Cotizacion_Moneda CHECK (Moneda COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%' AND LEN(Moneda) = 3),
        CONSTRAINT CK_Cotizacion_Estado CHECK (Estado IN ('Borrador','Emitida','Vencida','Cancelada','Convertida')),
        CONSTRAINT CK_Cotizacion_Fechas CHECK (
            (EmitidaEn IS NULL AND VenceEn IS NULL AND Estado IN ('Borrador','Cancelada'))
            OR
            (EmitidaEn IS NOT NULL AND VenceEn IS NOT NULL
                AND EmitidaEn >= CreadaEn AND VenceEn > EmitidaEn AND Estado <> 'Borrador')
        )
    );

    -- 7. Lineas de cotizacion. Impuesto como proporcion: 0.19 representa 19%.
    CREATE TABLE comercial.CotizacionDetalle (
        CotizacionDetalleId BIGINT IDENTITY(1,1) NOT NULL,
        CotizacionId INT NOT NULL,
        ProductoId INT NOT NULL,
        DescripcionProducto NVARCHAR(200) NOT NULL,
        Cantidad DECIMAL(18,3) NOT NULL,
        PrecioUnitario DECIMAL(19,4) NOT NULL,
        DescuentoUnitario DECIMAL(19,4) NOT NULL CONSTRAINT DF_CotizacionDetalle_Descuento DEFAULT (0),
        TasaImpuesto DECIMAL(9,6) NOT NULL CONSTRAINT DF_CotizacionDetalle_Impuesto DEFAULT (0),
        CONSTRAINT PK_CotizacionDetalle PRIMARY KEY (CotizacionDetalleId),
        CONSTRAINT UQ_CotizacionDetalle_Producto UNIQUE (CotizacionId, ProductoId),
        CONSTRAINT FK_CotizacionDetalle_Cotizacion FOREIGN KEY (CotizacionId) REFERENCES comercial.Cotizacion(CotizacionId),
        CONSTRAINT FK_CotizacionDetalle_Producto FOREIGN KEY (ProductoId) REFERENCES inventario.Producto(ProductoId),
        CONSTRAINT CK_CotizacionDetalle_Cantidad CHECK (Cantidad > 0),
        CONSTRAINT CK_CotizacionDetalle_Precio CHECK (PrecioUnitario >= 0),
        CONSTRAINT CK_CotizacionDetalle_Descuento CHECK (DescuentoUnitario >= 0 AND DescuentoUnitario <= PrecioUnitario),
        CONSTRAINT CK_CotizacionDetalle_Impuesto CHECK (TasaImpuesto BETWEEN 0 AND 1)
    );

    -- 8. Reservas temporales. No modifican la cantidad fisica.
    CREATE TABLE inventario.ReservaStock (
        ReservaStockId BIGINT IDENTITY(1,1) NOT NULL,
        CotizacionDetalleId BIGINT NOT NULL,
        BodegaId INT NOT NULL,
        ProductoId INT NOT NULL,
        Cantidad DECIMAL(18,3) NOT NULL,
        Estado VARCHAR(20) NOT NULL CONSTRAINT DF_ReservaStock_Estado DEFAULT ('Activa'),
        CreadaEn DATETIME2(3) NOT NULL CONSTRAINT DF_ReservaStock_CreadaEn DEFAULT (SYSUTCDATETIME()),
        VenceEn DATETIME2(3) NOT NULL,
        CerradaEn DATETIME2(3) NULL,
        Version ROWVERSION NOT NULL,
        CONSTRAINT PK_ReservaStock PRIMARY KEY (ReservaStockId),
        CONSTRAINT FK_ReservaStock_CotizacionDetalle FOREIGN KEY (CotizacionDetalleId) REFERENCES comercial.CotizacionDetalle(CotizacionDetalleId),
        CONSTRAINT FK_ReservaStock_Existencia FOREIGN KEY (BodegaId, ProductoId) REFERENCES inventario.Existencia(BodegaId, ProductoId),
        CONSTRAINT CK_ReservaStock_Cantidad CHECK (Cantidad > 0),
        CONSTRAINT CK_ReservaStock_Estado CHECK (Estado IN ('Activa','Consumida','Liberada','Vencida')),
        CONSTRAINT CK_ReservaStock_Vencimiento CHECK (VenceEn > CreadaEn),
        CONSTRAINT CK_ReservaStock_Cierre CHECK (
            (Estado = 'Activa' AND CerradaEn IS NULL)
            OR (Estado <> 'Activa' AND CerradaEn IS NOT NULL AND CerradaEn >= CreadaEn)
        )
    );

    -- 9. Venta confirmada. ClaveOperacion la proporciona quien inicia la solicitud.
    -- No usar NEWID() como DEFAULT: un reintento debe conservar la misma clave.
    CREATE TABLE comercial.Venta (
        VentaId INT IDENTITY(1,1) NOT NULL,
        Numero NVARCHAR(30) NOT NULL,
        ClienteId INT NOT NULL,
        BodegaId INT NOT NULL,
        CotizacionId INT NULL,
        ClaveOperacion UNIQUEIDENTIFIER NOT NULL,
        Estado VARCHAR(20) NOT NULL CONSTRAINT DF_Venta_Estado DEFAULT ('Confirmada'),
        Moneda CHAR(3) NOT NULL CONSTRAINT DF_Venta_Moneda DEFAULT ('CLP'),
        ConfirmadaEn DATETIME2(3) NOT NULL CONSTRAINT DF_Venta_ConfirmadaEn DEFAULT (SYSUTCDATETIME()),
        CreadaPorUsuarioId INT NOT NULL,
        Observaciones NVARCHAR(1000) NULL,
        CONSTRAINT PK_Venta PRIMARY KEY (VentaId),
        CONSTRAINT UQ_Venta_Numero UNIQUE (Numero),
        CONSTRAINT UQ_Venta_ClaveOperacion UNIQUE (ClaveOperacion),
        CONSTRAINT FK_Venta_Cliente FOREIGN KEY (ClienteId) REFERENCES comercial.Cliente(ClienteId),
        CONSTRAINT FK_Venta_Bodega FOREIGN KEY (BodegaId) REFERENCES inventario.Bodega(BodegaId),
        CONSTRAINT FK_Venta_Cotizacion FOREIGN KEY (CotizacionId) REFERENCES comercial.Cotizacion(CotizacionId),
        CONSTRAINT FK_Venta_Usuario FOREIGN KEY (CreadaPorUsuarioId) REFERENCES seguridad.Usuario(UsuarioId),
        CONSTRAINT CK_Venta_Numero CHECK (LEN(LTRIM(RTRIM(Numero))) > 0),
        CONSTRAINT CK_Venta_Moneda CHECK (Moneda COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%' AND LEN(Moneda) = 3),
        -- Anulaciones y devoluciones requeriran movimientos compensatorios y una migracion.
        CONSTRAINT CK_Venta_Estado CHECK (Estado = 'Confirmada')
    );

    -- 10. Lineas historicas de venta.
    CREATE TABLE comercial.VentaDetalle (
        VentaDetalleId BIGINT IDENTITY(1,1) NOT NULL,
        VentaId INT NOT NULL,
        ProductoId INT NOT NULL,
        DescripcionProducto NVARCHAR(200) NOT NULL,
        Cantidad DECIMAL(18,3) NOT NULL,
        PrecioUnitario DECIMAL(19,4) NOT NULL,
        DescuentoUnitario DECIMAL(19,4) NOT NULL CONSTRAINT DF_VentaDetalle_Descuento DEFAULT (0),
        TasaImpuesto DECIMAL(9,6) NOT NULL CONSTRAINT DF_VentaDetalle_Impuesto DEFAULT (0),
        CONSTRAINT PK_VentaDetalle PRIMARY KEY (VentaDetalleId),
        CONSTRAINT UQ_VentaDetalle_Producto UNIQUE (VentaId, ProductoId),
        CONSTRAINT FK_VentaDetalle_Venta FOREIGN KEY (VentaId) REFERENCES comercial.Venta(VentaId),
        CONSTRAINT FK_VentaDetalle_Producto FOREIGN KEY (ProductoId) REFERENCES inventario.Producto(ProductoId),
        CONSTRAINT CK_VentaDetalle_Cantidad CHECK (Cantidad > 0),
        CONSTRAINT CK_VentaDetalle_Precio CHECK (PrecioUnitario >= 0),
        CONSTRAINT CK_VentaDetalle_Descuento CHECK (DescuentoUnitario >= 0 AND DescuentoUnitario <= PrecioUnitario),
        CONSTRAINT CK_VentaDetalle_Impuesto CHECK (TasaImpuesto BETWEEN 0 AND 1)
    );

    -- 11. Cabecera de importacion: un archivo para una bodega.
    CREATE TABLE inventario.ImportacionInventario (
        ImportacionInventarioId BIGINT IDENTITY(1,1) NOT NULL,
        BodegaId INT NOT NULL,
        NombreArchivo NVARCHAR(255) NOT NULL,
        HashArchivo CHAR(64) NOT NULL,
        ClaveOperacion UNIQUEIDENTIFIER NOT NULL,
        Estado VARCHAR(20) NOT NULL CONSTRAINT DF_Importacion_Estado DEFAULT ('Cargada'),
        ReferenciaRecepcion NVARCHAR(100) NULL,
        CreadaEn DATETIME2(3) NOT NULL CONSTRAINT DF_Importacion_CreadaEn DEFAULT (SYSUTCDATETIME()),
        AplicadaEn DATETIME2(3) NULL,
        CreadaPorUsuarioId INT NOT NULL,
        Version ROWVERSION NOT NULL,
        CONSTRAINT PK_ImportacionInventario PRIMARY KEY (ImportacionInventarioId),
        CONSTRAINT UQ_Importacion_ClaveOperacion UNIQUE (ClaveOperacion),
        CONSTRAINT FK_Importacion_Bodega FOREIGN KEY (BodegaId) REFERENCES inventario.Bodega(BodegaId),
        CONSTRAINT FK_Importacion_Usuario FOREIGN KEY (CreadaPorUsuarioId) REFERENCES seguridad.Usuario(UsuarioId),
        CONSTRAINT CK_Importacion_Archivo CHECK (LEN(LTRIM(RTRIM(NombreArchivo))) > 0),
        CONSTRAINT CK_Importacion_Hash CHECK (LEN(HashArchivo) = 64 AND HashArchivo COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'),
        CONSTRAINT CK_Importacion_Estado CHECK (Estado IN ('Cargada','ConErrores','Validada','Aplicada','Cancelada')),
        CONSTRAINT CK_Importacion_Aplicacion CHECK (
            (Estado = 'Aplicada' AND AplicadaEn IS NOT NULL AND AplicadaEn >= CreadaEn)
            OR (Estado <> 'Aplicada' AND AplicadaEn IS NULL)
        )
    );

    -- 12. Filas originales y resultado de validacion del Excel.
    -- Cantidad puede ser invalida en esta tabla de preparacion; aplicar exige > 0.
    CREATE TABLE inventario.ImportacionInventarioDetalle (
        ImportacionInventarioDetalleId BIGINT IDENTITY(1,1) NOT NULL,
        ImportacionInventarioId BIGINT NOT NULL,
        NumeroFila INT NOT NULL,
        SKUOriginal NVARCHAR(100) NULL,
        CantidadOriginal NVARCHAR(100) NULL,
        ProductoId INT NULL,
        Cantidad DECIMAL(18,3) NULL,
        ErrorValidacion NVARCHAR(1000) NULL,
        CONSTRAINT PK_ImportacionInventarioDetalle PRIMARY KEY (ImportacionInventarioDetalleId),
        CONSTRAINT UQ_ImportacionDetalle_Fila UNIQUE (ImportacionInventarioId, NumeroFila),
        CONSTRAINT FK_ImportacionDetalle_Importacion FOREIGN KEY (ImportacionInventarioId) REFERENCES inventario.ImportacionInventario(ImportacionInventarioId),
        CONSTRAINT FK_ImportacionDetalle_Producto FOREIGN KEY (ProductoId) REFERENCES inventario.Producto(ProductoId),
        CONSTRAINT CK_ImportacionDetalle_Fila CHECK (NumeroFila > 0)
    );

    -- 13. Historial fisico. Las reservas no generan movimientos fisicos.
    CREATE TABLE inventario.MovimientoStock (
        MovimientoStockId BIGINT IDENTITY(1,1) NOT NULL,
        BodegaId INT NOT NULL,
        ProductoId INT NOT NULL,
        Tipo VARCHAR(30) NOT NULL,
        Cantidad DECIMAL(18,3) NOT NULL,
        VentaDetalleId BIGINT NULL,
        ImportacionInventarioDetalleId BIGINT NULL,
        CreadoEn DATETIME2(3) NOT NULL CONSTRAINT DF_MovimientoStock_CreadoEn DEFAULT (SYSUTCDATETIME()),
        CreadoPorUsuarioId INT NOT NULL,
        CONSTRAINT PK_MovimientoStock PRIMARY KEY (MovimientoStockId),
        CONSTRAINT FK_MovimientoStock_Existencia FOREIGN KEY (BodegaId, ProductoId) REFERENCES inventario.Existencia(BodegaId, ProductoId),
        CONSTRAINT FK_MovimientoStock_VentaDetalle FOREIGN KEY (VentaDetalleId) REFERENCES comercial.VentaDetalle(VentaDetalleId),
        CONSTRAINT FK_MovimientoStock_ImportacionDetalle FOREIGN KEY (ImportacionInventarioDetalleId) REFERENCES inventario.ImportacionInventarioDetalle(ImportacionInventarioDetalleId),
        CONSTRAINT FK_MovimientoStock_Usuario FOREIGN KEY (CreadoPorUsuarioId) REFERENCES seguridad.Usuario(UsuarioId),
        CONSTRAINT CK_MovimientoStock_Origen CHECK (
            (Tipo = 'EntradaExcel' AND Cantidad > 0 AND ImportacionInventarioDetalleId IS NOT NULL AND VentaDetalleId IS NULL)
            OR
            (Tipo = 'SalidaVenta' AND Cantidad < 0 AND VentaDetalleId IS NOT NULL AND ImportacionInventarioDetalleId IS NULL)
        )
    );

    -- Indices unicos filtrados para relaciones opcionales y operaciones activas.
    CREATE UNIQUE INDEX UX_ReservaStock_DetalleActiva
        ON inventario.ReservaStock(CotizacionDetalleId) WHERE Estado = 'Activa';

    CREATE UNIQUE INDEX UX_Venta_Cotizacion
        ON comercial.Venta(CotizacionId) WHERE CotizacionId IS NOT NULL;

    CREATE UNIQUE INDEX UX_MovimientoStock_VentaDetalle
        ON inventario.MovimientoStock(VentaDetalleId) WHERE VentaDetalleId IS NOT NULL;

    CREATE UNIQUE INDEX UX_MovimientoStock_ImportacionDetalle
        ON inventario.MovimientoStock(ImportacionInventarioDetalleId)
        WHERE ImportacionInventarioDetalleId IS NOT NULL;

    -- Accesos por FK y consultas principales.
    CREATE INDEX IX_Existencia_Producto ON inventario.Existencia(ProductoId, BodegaId);
    CREATE INDEX IX_Cotizacion_Cliente_Fecha ON comercial.Cotizacion(ClienteId, CreadaEn);
    CREATE INDEX IX_Cotizacion_Bodega ON comercial.Cotizacion(BodegaId);
    CREATE INDEX IX_Cotizacion_Usuario ON comercial.Cotizacion(CreadaPorUsuarioId);
    CREATE INDEX IX_Cotizacion_Estado_Vencimiento ON comercial.Cotizacion(Estado, VenceEn);
    CREATE INDEX IX_CotizacionDetalle_Producto ON comercial.CotizacionDetalle(ProductoId);
    CREATE INDEX IX_ReservaStock_Detalle ON inventario.ReservaStock(CotizacionDetalleId);
    CREATE INDEX IX_ReservaStock_Disponibilidad
        ON inventario.ReservaStock(BodegaId, ProductoId, Estado, VenceEn) INCLUDE (Cantidad);
    CREATE INDEX IX_ReservaStock_PorVencer
        ON inventario.ReservaStock(VenceEn) INCLUDE (CotizacionDetalleId, BodegaId, ProductoId)
        WHERE Estado = 'Activa';
    CREATE INDEX IX_Venta_Cliente_Fecha ON comercial.Venta(ClienteId, ConfirmadaEn);
    CREATE INDEX IX_Venta_Bodega ON comercial.Venta(BodegaId);
    CREATE INDEX IX_Venta_Usuario ON comercial.Venta(CreadaPorUsuarioId);
    CREATE INDEX IX_VentaDetalle_Producto ON comercial.VentaDetalle(ProductoId);
    CREATE INDEX IX_Importacion_Bodega_Fecha ON inventario.ImportacionInventario(BodegaId, CreadaEn);
    CREATE INDEX IX_Importacion_Hash ON inventario.ImportacionInventario(BodegaId, HashArchivo);
    CREATE INDEX IX_Importacion_Usuario ON inventario.ImportacionInventario(CreadaPorUsuarioId);
    CREATE INDEX IX_ImportacionDetalle_Producto ON inventario.ImportacionInventarioDetalle(ProductoId);
    CREATE INDEX IX_MovimientoStock_Historial
        ON inventario.MovimientoStock(BodegaId, ProductoId, CreadoEn, MovimientoStockId)
        INCLUDE (Tipo, Cantidad);
    CREATE INDEX IX_MovimientoStock_Usuario ON inventario.MovimientoStock(CreadoPorUsuarioId);

    COMMIT TRANSACTION;
    PRINT N'ComercialStock: 13 tablas creadas con PK, FK, restricciones e indices.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
