-- Tabla: ImportacionInventarioDetalle. Ejecutar despues de los archivos anteriores.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
CREATE TABLE ImportacionInventarioDetalle (
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
    CONSTRAINT FK_ImportacionDetalle_Importacion FOREIGN KEY (ImportacionInventarioId) REFERENCES ImportacionInventario(ImportacionInventarioId),
    CONSTRAINT FK_ImportacionDetalle_Producto FOREIGN KEY (ProductoId) REFERENCES Producto(ProductoId),
    CONSTRAINT CK_ImportacionDetalle_Fila CHECK (NumeroFila > 0)
);
GO

CREATE INDEX IX_ImportacionDetalle_Producto ON ImportacionInventarioDetalle(ProductoId);
GO
