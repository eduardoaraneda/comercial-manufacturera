-- Migracion incremental, repetible. Ejecutar sobre ComercialStock con sqlcmd -b.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Venta', 'HuellaSolicitud') IS NULL
    ALTER TABLE dbo.Venta ADD HuellaSolicitud CHAR(64) NULL;
IF COL_LENGTH('dbo.Cotizacion', 'ClaveOperacion') IS NULL
    ALTER TABLE dbo.Cotizacion ADD ClaveOperacion UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.Cotizacion', 'HuellaSolicitud') IS NULL
    ALTER TABLE dbo.Cotizacion ADD HuellaSolicitud CHAR(64) NULL;
IF COL_LENGTH('dbo.Bodega', 'Version') IS NULL
    ALTER TABLE dbo.Bodega ADD Version ROWVERSION NOT NULL;
COMMIT TRANSACTION;
GO
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Cotizacion') AND name='UX_Cotizacion_ClaveOperacion')
    CREATE UNIQUE INDEX UX_Cotizacion_ClaveOperacion ON dbo.Cotizacion(ClaveOperacion) WHERE ClaveOperacion IS NOT NULL;
GO
