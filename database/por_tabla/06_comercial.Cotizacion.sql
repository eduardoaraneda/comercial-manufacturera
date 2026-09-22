-- Tabla: comercial.Cotizacion. Ejecutar despues de los archivos anteriores.
USE [ComercialStock];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
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
GO

CREATE INDEX IX_Cotizacion_Cliente_Fecha ON comercial.Cotizacion(ClienteId, CreadaEn);
GO

CREATE INDEX IX_Cotizacion_Bodega ON comercial.Cotizacion(BodegaId);
GO

CREATE INDEX IX_Cotizacion_Usuario ON comercial.Cotizacion(CreadaPorUsuarioId);
GO

CREATE INDEX IX_Cotizacion_Estado_Vencimiento ON comercial.Cotizacion(Estado, VenceEn);
GO
