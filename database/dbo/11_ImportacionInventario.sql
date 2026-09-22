-- Tabla: ImportacionInventario. Ejecutar despues de los archivos anteriores.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
CREATE TABLE ImportacionInventario (
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
    CONSTRAINT FK_Importacion_Bodega FOREIGN KEY (BodegaId) REFERENCES Bodega(BodegaId),
    CONSTRAINT FK_Importacion_Usuario FOREIGN KEY (CreadaPorUsuarioId) REFERENCES Usuario(UsuarioId),
    CONSTRAINT CK_Importacion_Archivo CHECK (LEN(LTRIM(RTRIM(NombreArchivo))) > 0),
    CONSTRAINT CK_Importacion_Hash CHECK (LEN(HashArchivo) = 64 AND HashArchivo COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'),
    CONSTRAINT CK_Importacion_Estado CHECK (Estado IN ('Cargada','ConErrores','Validada','Aplicada','Cancelada')),
    CONSTRAINT CK_Importacion_Aplicacion CHECK (
        (Estado = 'Aplicada' AND AplicadaEn IS NOT NULL AND AplicadaEn >= CreadaEn)
        OR (Estado <> 'Aplicada' AND AplicadaEn IS NULL)
    )
);
GO

CREATE INDEX IX_Importacion_Bodega_Fecha ON ImportacionInventario(BodegaId, CreadaEn);
GO

CREATE INDEX IX_Importacion_Hash ON ImportacionInventario(BodegaId, HashArchivo);
GO

CREATE INDEX IX_Importacion_Usuario ON ImportacionInventario(CreadaPorUsuarioId);
GO
