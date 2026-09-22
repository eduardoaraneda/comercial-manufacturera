-- Tabla: MovimientoStock. Ejecutar despues de los archivos anteriores.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
CREATE TABLE MovimientoStock (
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
    CONSTRAINT FK_MovimientoStock_Existencia FOREIGN KEY (BodegaId, ProductoId) REFERENCES Existencia(BodegaId, ProductoId),
    CONSTRAINT FK_MovimientoStock_VentaDetalle FOREIGN KEY (VentaDetalleId) REFERENCES VentaDetalle(VentaDetalleId),
    CONSTRAINT FK_MovimientoStock_ImportacionDetalle FOREIGN KEY (ImportacionInventarioDetalleId) REFERENCES ImportacionInventarioDetalle(ImportacionInventarioDetalleId),
    CONSTRAINT FK_MovimientoStock_Usuario FOREIGN KEY (CreadoPorUsuarioId) REFERENCES Usuario(UsuarioId),
    CONSTRAINT CK_MovimientoStock_Origen CHECK (
        (Tipo = 'EntradaExcel' AND Cantidad > 0 AND ImportacionInventarioDetalleId IS NOT NULL AND VentaDetalleId IS NULL)
        OR
        (Tipo = 'SalidaVenta' AND Cantidad < 0 AND VentaDetalleId IS NOT NULL AND ImportacionInventarioDetalleId IS NULL)
    )
);
GO

CREATE UNIQUE INDEX UX_MovimientoStock_VentaDetalle
    ON MovimientoStock(VentaDetalleId) WHERE VentaDetalleId IS NOT NULL;
GO

CREATE UNIQUE INDEX UX_MovimientoStock_ImportacionDetalle
    ON MovimientoStock(ImportacionInventarioDetalleId)
    WHERE ImportacionInventarioDetalleId IS NOT NULL;
GO

CREATE INDEX IX_MovimientoStock_Historial
    ON MovimientoStock(BodegaId, ProductoId, CreadoEn, MovimientoStockId)
    INCLUDE (Tipo, Cantidad);
GO

CREATE INDEX IX_MovimientoStock_Usuario ON MovimientoStock(CreadoPorUsuarioId);
GO
