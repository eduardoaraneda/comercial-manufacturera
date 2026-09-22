-- Ejecutar una sola vez en una instancia de desarrollo.
-- Si ComercialStock ya existe, omitir el bloque CREATE DATABASE.
USE [master];
GO
CREATE DATABASE [ComercialStock];
GO
USE [ComercialStock];
GO
-- Ejecutar solamente los CREATE SCHEMA de esquemas que aun no existan.
CREATE SCHEMA [seguridad] AUTHORIZATION [dbo];
GO
CREATE SCHEMA [comercial] AUTHORIZATION [dbo];
GO
CREATE SCHEMA [inventario] AUTHORIZATION [dbo];
GO
