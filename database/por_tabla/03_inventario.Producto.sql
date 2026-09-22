-- Tabla: inventario.Producto. Ejecutar despues de los archivos anteriores.
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
GO
