-- Tabla: comercial.CotizacionDetalle. Ejecutar despues de los archivos anteriores.
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
GO

CREATE INDEX IX_CotizacionDetalle_Producto ON comercial.CotizacionDetalle(ProductoId);
GO
