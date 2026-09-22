-- Tabla: ReservaStock. Ejecutar despues de los archivos anteriores.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
CREATE TABLE ReservaStock (
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
    CONSTRAINT FK_ReservaStock_CotizacionDetalle FOREIGN KEY (CotizacionDetalleId) REFERENCES CotizacionDetalle(CotizacionDetalleId),
    CONSTRAINT FK_ReservaStock_Existencia FOREIGN KEY (BodegaId, ProductoId) REFERENCES Existencia(BodegaId, ProductoId),
    CONSTRAINT CK_ReservaStock_Cantidad CHECK (Cantidad > 0),
    CONSTRAINT CK_ReservaStock_Estado CHECK (Estado IN ('Activa','Consumida','Liberada','Vencida')),
    CONSTRAINT CK_ReservaStock_Vencimiento CHECK (VenceEn > CreadaEn),
    CONSTRAINT CK_ReservaStock_Cierre CHECK (
        (Estado = 'Activa' AND CerradaEn IS NULL)
        OR (Estado <> 'Activa' AND CerradaEn IS NOT NULL AND CerradaEn >= CreadaEn)
    )
);
GO

CREATE UNIQUE INDEX UX_ReservaStock_DetalleActiva
    ON ReservaStock(CotizacionDetalleId) WHERE Estado = 'Activa';
GO

CREATE INDEX IX_ReservaStock_Detalle ON ReservaStock(CotizacionDetalleId);
GO

CREATE INDEX IX_ReservaStock_Disponibilidad
    ON ReservaStock(BodegaId, ProductoId, Estado, VenceEn) INCLUDE (Cantidad);
GO

CREATE INDEX IX_ReservaStock_PorVencer
    ON ReservaStock(VenceEn) INCLUDE (CotizacionDetalleId, BodegaId, ProductoId)
    WHERE Estado = 'Activa';
GO
