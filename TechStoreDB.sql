-- ============================================================
-- TechStoreDB.sql
-- Script de creación, restricciones y carga de datos de prueba.
--
-- Re-ejecutar recrea TechStoreDB y su carga de prueba.
-- ============================================================

IF DB_ID('TechStoreDB') IS NULL
BEGIN
    CREATE DATABASE TechStoreDB;
END
GO

USE TechStoreDB;
GO

SET NOCOUNT ON;
GO

-- ============================================================
-- Limpieza (para poder re-ejecutar el script desde cero)
-- ============================================================
IF OBJECT_ID('dbo.DetalleVenta', 'U') IS NOT NULL DROP TABLE dbo.DetalleVenta;
IF OBJECT_ID('dbo.Venta', 'U')        IS NOT NULL DROP TABLE dbo.Venta;
IF OBJECT_ID('dbo.Producto', 'U')     IS NOT NULL DROP TABLE dbo.Producto;
IF OBJECT_ID('dbo.Categoria', 'U')    IS NOT NULL DROP TABLE dbo.Categoria;
IF OBJECT_ID('dbo.Cliente', 'U')      IS NOT NULL DROP TABLE dbo.Cliente;
GO

-- ============================================================
-- Tablas (Parte A - ver Figura 2 del enunciado)
-- ============================================================
CREATE TABLE dbo.Categoria (
    IdCategoria INT IDENTITY(1,1) PRIMARY KEY,
    Nombre      NVARCHAR(50) NOT NULL,
    CONSTRAINT UQ_Categoria_Nombre UNIQUE (Nombre)
);

CREATE TABLE dbo.Producto (
    IdProducto  INT IDENTITY(1,1) PRIMARY KEY,
    Codigo      NVARCHAR(20) NOT NULL,
    Nombre      NVARCHAR(150) NOT NULL,
    Marca       NVARCHAR(80) NOT NULL CONSTRAINT DF_Producto_Marca DEFAULT (N'Generica'),
    Precio      DECIMAL(10,2) NOT NULL,
    Stock       INT NOT NULL,
    IdCategoria INT NOT NULL,
    CONSTRAINT UQ_Producto_Codigo UNIQUE (Codigo),
    CONSTRAINT FK_Producto_Categoria FOREIGN KEY (IdCategoria)
        REFERENCES dbo.Categoria (IdCategoria),
    CONSTRAINT CK_Producto_Precio CHECK (Precio > 0),
    CONSTRAINT CK_Producto_Stock  CHECK (Stock >= 0)
);

CREATE TABLE dbo.Cliente (
    IdCliente INT IDENTITY(1,1) PRIMARY KEY,
    Cedula    NVARCHAR(15) NOT NULL,
    Nombre    NVARCHAR(150) NOT NULL,
    Email     NVARCHAR(150) NOT NULL,
    CONSTRAINT UQ_Cliente_Cedula UNIQUE (Cedula),
    CONSTRAINT UQ_Cliente_Email  UNIQUE (Email)
);

CREATE TABLE dbo.Venta (
    IdVenta   INT IDENTITY(1,1) PRIMARY KEY,
    IdCliente INT NOT NULL,
    Fecha     DATETIME NOT NULL CONSTRAINT DF_Venta_Fecha DEFAULT (GETDATE()),
    Total     DECIMAL(12,2) NOT NULL,
    CONSTRAINT FK_Venta_Cliente FOREIGN KEY (IdCliente)
        REFERENCES dbo.Cliente (IdCliente)
);

CREATE TABLE dbo.DetalleVenta (
    IdDetalle      INT IDENTITY(1,1) PRIMARY KEY,
    IdVenta        INT NOT NULL,
    IdProducto     INT NOT NULL,
    Cantidad       INT NOT NULL,
    PrecioUnitario DECIMAL(10,2) NOT NULL,
    CONSTRAINT FK_DetalleVenta_Venta FOREIGN KEY (IdVenta)
        REFERENCES dbo.Venta (IdVenta),
    CONSTRAINT FK_DetalleVenta_Producto FOREIGN KEY (IdProducto)
        REFERENCES dbo.Producto (IdProducto),
    CONSTRAINT CK_DetalleVenta_Cantidad CHECK (Cantidad > 0)
);
GO

-- ============================================================
-- Categorías (5)
-- ============================================================
INSERT INTO dbo.Categoria (Nombre) VALUES
    (N'Laptops'), (N'Periféricos'), (N'Monitores'), (N'Almacenamiento'), (N'Componentes');
GO

-- ============================================================
-- Tabla de números auxiliar, generada en bloque (sin WHILE) para
-- poder insertar 50,000+ productos rápido. Técnica sugerida en la
-- pista del enunciado: CTEs en cascada + CROSS JOIN.
-- ============================================================
IF OBJECT_ID('tempdb..#Numeros') IS NOT NULL DROP TABLE #Numeros;

;WITH L0 AS (SELECT 1 AS c UNION ALL SELECT 1),                    -- 2
     L1 AS (SELECT 1 AS c FROM L0 A CROSS JOIN L0 B),              -- 4
     L2 AS (SELECT 1 AS c FROM L1 A CROSS JOIN L1 B),              -- 16
     L3 AS (SELECT 1 AS c FROM L2 A CROSS JOIN L2 B),              -- 256
     L4 AS (SELECT 1 AS c FROM L3 A CROSS JOIN L3 B)               -- 65,536
SELECT ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
INTO #Numeros
FROM L4;
GO

-- ============================================================
-- 50,000 productos (carga en bloque con INSERT...SELECT)
-- ============================================================
INSERT INTO dbo.Producto (Codigo, Nombre, Marca, Precio, Stock, IdCategoria)
SELECT
    'PRD-' + RIGHT('000000' + CAST(n AS VARCHAR(10)), 6),
    'Producto ' + CAST(n AS VARCHAR(10)),
    CASE (n % 5)
        WHEN 0 THEN N'Lenovo'
        WHEN 1 THEN N'Logitech'
        WHEN 2 THEN N'Samsung'
        WHEN 3 THEN N'Kingston'
        ELSE N'ASUS'
    END,
    CAST(100 + (ABS(CHECKSUM(NEWID())) % 50000) / 100.0 AS DECIMAL(10,2)),
    ABS(CHECKSUM(NEWID())) % 500,
    1 + ABS(CHECKSUM(NEWID())) % 5
FROM #Numeros
WHERE n <= 50000;
GO

-- ============================================================
-- 100 clientes
-- ============================================================
INSERT INTO dbo.Cliente (Cedula, Nombre, Email)
SELECT
    RIGHT('00000000000' + CAST(40000000 + n AS VARCHAR(11)), 11),
    'Cliente ' + CAST(n AS VARCHAR(10)),
    'cliente' + CAST(n AS VARCHAR(10)) + '@correo.com'
FROM #Numeros
WHERE n <= 100;
GO

-- ============================================================
-- 1,000 ventas con 1 a 3 detalles cada una.
-- Aquí sí se usa un WHILE: son solo 1,000 iteraciones (no 50,000),
-- así que el rendimiento no es un problema real.
-- ============================================================
DECLARE @i INT = 1;
DECLARE @idCliente INT;
DECLARE @idVenta INT;
DECLARE @cantidadDetalles INT;
DECLARE @j INT;
DECLARE @idProducto INT;
DECLARE @precio DECIMAL(10,2);
DECLARE @cantidad INT;
DECLARE @total DECIMAL(12,2);

WHILE @i <= 1000
BEGIN
    SET @idCliente = 1 + ABS(CHECKSUM(NEWID())) % 100;
    SET @total = 0;

    INSERT INTO dbo.Venta (IdCliente, Fecha, Total)
    VALUES (@idCliente, DATEADD(DAY, -(ABS(CHECKSUM(NEWID())) % 365), GETDATE()), 0);

    SET @idVenta = CAST(SCOPE_IDENTITY() AS INT);
    SET @cantidadDetalles = 1 + ABS(CHECKSUM(NEWID())) % 3;
    SET @j = 1;

    WHILE @j <= @cantidadDetalles
    BEGIN
        SET @idProducto = 1 + ABS(CHECKSUM(NEWID())) % 50000;
        SELECT @precio = Precio FROM dbo.Producto WHERE IdProducto = @idProducto;
        SET @cantidad = 1 + ABS(CHECKSUM(NEWID())) % 5;

        INSERT INTO dbo.DetalleVenta (IdVenta, IdProducto, Cantidad, PrecioUnitario)
        VALUES (@idVenta, @idProducto, @cantidad, @precio);

        SET @total += @precio * @cantidad;
        SET @j += 1;
    END;

    UPDATE dbo.Venta SET Total = @total WHERE IdVenta = @idVenta;

    SET @i += 1;
END;
GO

DROP TABLE IF EXISTS #Numeros;
GO

-- ============================================================
-- Verificación rápida de la carga
-- ============================================================
SELECT 'Categoria'    AS Tabla, COUNT(*) AS Filas FROM dbo.Categoria
UNION ALL SELECT 'Producto',      COUNT(*) FROM dbo.Producto
UNION ALL SELECT 'Cliente',       COUNT(*) FROM dbo.Cliente
UNION ALL SELECT 'Venta',         COUNT(*) FROM dbo.Venta
UNION ALL SELECT 'DetalleVenta',  COUNT(*) FROM dbo.DetalleVenta;
GO

-- La base para EF Core se crea vacía. El esquema lo crea la migración
-- Code First y los datos se copian con SeedTechStoreDbEf.sql.
IF DB_ID('TechStoreDB_EF') IS NULL
BEGIN
    CREATE DATABASE TechStoreDB_EF;
END
GO
