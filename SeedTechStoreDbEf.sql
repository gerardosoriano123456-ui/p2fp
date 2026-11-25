USE TechStoreDB_EF;
GO

SET XACT_ABORT ON;
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM dbo.Producto) OR EXISTS (SELECT 1 FROM dbo.Categoria)
    THROW 50001, 'TechStoreDB_EF ya contiene datos; no se duplicó la carga.', 1;

BEGIN TRANSACTION;

SET IDENTITY_INSERT dbo.Categoria ON;
INSERT INTO dbo.Categoria (IdCategoria, Nombre)
SELECT IdCategoria, Nombre FROM TechStoreDB.dbo.Categoria;
SET IDENTITY_INSERT dbo.Categoria OFF;

SET IDENTITY_INSERT dbo.Producto ON;
INSERT INTO dbo.Producto (IdProducto, Codigo, Nombre, Marca, Precio, Stock, IdCategoria)
SELECT IdProducto, Codigo, Nombre, Marca, Precio, Stock, IdCategoria
FROM TechStoreDB.dbo.Producto;
SET IDENTITY_INSERT dbo.Producto OFF;

SET IDENTITY_INSERT dbo.Cliente ON;
INSERT INTO dbo.Cliente (IdCliente, Cedula, Nombre, Email)
SELECT IdCliente, Cedula, Nombre, Email FROM TechStoreDB.dbo.Cliente;
SET IDENTITY_INSERT dbo.Cliente OFF;

SET IDENTITY_INSERT dbo.Venta ON;
INSERT INTO dbo.Venta (IdVenta, IdCliente, Fecha, Total)
SELECT IdVenta, IdCliente, Fecha, Total FROM TechStoreDB.dbo.Venta;
SET IDENTITY_INSERT dbo.Venta OFF;

SET IDENTITY_INSERT dbo.DetalleVenta ON;
INSERT INTO dbo.DetalleVenta (IdDetalle, IdVenta, IdProducto, Cantidad, PrecioUnitario)
SELECT IdDetalle, IdVenta, IdProducto, Cantidad, PrecioUnitario
FROM TechStoreDB.dbo.DetalleVenta;
SET IDENTITY_INSERT dbo.DetalleVenta OFF;

COMMIT TRANSACTION;
GO

SELECT 'Categoria' AS Tabla, COUNT(*) AS Filas FROM dbo.Categoria
UNION ALL SELECT 'Producto', COUNT(*) FROM dbo.Producto
UNION ALL SELECT 'Cliente', COUNT(*) FROM dbo.Cliente
UNION ALL SELECT 'Venta', COUNT(*) FROM dbo.Venta
UNION ALL SELECT 'DetalleVenta', COUNT(*) FROM dbo.DetalleVenta;
GO
