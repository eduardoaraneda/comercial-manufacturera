-- Tabla: VentaDetalle. Ejecutar despues de los archivos anteriores.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
CREATE TABLE VentaDetalle (
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
    CONSTRAINT FK_VentaDetalle_Venta FOREIGN KEY (VentaId) REFERENCES Venta(VentaId),
    CONSTRAINT FK_VentaDetalle_Producto FOREIGN KEY (ProductoId) REFERENCES Producto(ProductoId),
    CONSTRAINT CK_VentaDetalle_Cantidad CHECK (Cantidad > 0),
    CONSTRAINT CK_VentaDetalle_Precio CHECK (PrecioUnitario >= 0),
    CONSTRAINT CK_VentaDetalle_Descuento CHECK (DescuentoUnitario >= 0 AND DescuentoUnitario <= PrecioUnitario),
    CONSTRAINT CK_VentaDetalle_Impuesto CHECK (TasaImpuesto BETWEEN 0 AND 1)
);
GO

CREATE INDEX IX_VentaDetalle_Producto ON VentaDetalle(ProductoId);
GO
