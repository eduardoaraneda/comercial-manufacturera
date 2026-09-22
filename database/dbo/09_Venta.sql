-- Tabla: Venta. Ejecutar despues de los archivos anteriores.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
CREATE TABLE Venta (
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
    CONSTRAINT FK_Venta_Cliente FOREIGN KEY (ClienteId) REFERENCES Cliente(ClienteId),
    CONSTRAINT FK_Venta_Bodega FOREIGN KEY (BodegaId) REFERENCES Bodega(BodegaId),
    CONSTRAINT FK_Venta_Cotizacion FOREIGN KEY (CotizacionId) REFERENCES Cotizacion(CotizacionId),
    CONSTRAINT FK_Venta_Usuario FOREIGN KEY (CreadaPorUsuarioId) REFERENCES Usuario(UsuarioId),
    CONSTRAINT CK_Venta_Numero CHECK (LEN(LTRIM(RTRIM(Numero))) > 0),
    CONSTRAINT CK_Venta_Moneda CHECK (Moneda COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%' AND LEN(Moneda) = 3),
    -- Anulaciones y devoluciones requeriran movimientos compensatorios y una migracion.
    CONSTRAINT CK_Venta_Estado CHECK (Estado = 'Confirmada')
);
GO

CREATE UNIQUE INDEX UX_Venta_Cotizacion
    ON Venta(CotizacionId) WHERE CotizacionId IS NOT NULL;
GO

CREATE INDEX IX_Venta_Cliente_Fecha ON Venta(ClienteId, ConfirmadaEn);
GO

CREATE INDEX IX_Venta_Bodega ON Venta(BodegaId);
GO

CREATE INDEX IX_Venta_Usuario ON Venta(CreadaPorUsuarioId);
GO
