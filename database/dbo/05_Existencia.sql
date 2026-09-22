-- Tabla: Existencia. Ejecutar despues de los archivos anteriores.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
CREATE TABLE Existencia (
    BodegaId INT NOT NULL,
    ProductoId INT NOT NULL,
    CantidadFisica DECIMAL(18,3) NOT NULL CONSTRAINT DF_Existencia_Cantidad DEFAULT (0),
    Version ROWVERSION NOT NULL,
    CONSTRAINT PK_Existencia PRIMARY KEY (BodegaId, ProductoId),
    CONSTRAINT FK_Existencia_Bodega FOREIGN KEY (BodegaId) REFERENCES Bodega(BodegaId),
    CONSTRAINT FK_Existencia_Producto FOREIGN KEY (ProductoId) REFERENCES Producto(ProductoId),
    CONSTRAINT CK_Existencia_NoNegativa CHECK (CantidadFisica >= 0)
);
GO

CREATE INDEX IX_Existencia_Producto ON Existencia(ProductoId, BodegaId);
GO
