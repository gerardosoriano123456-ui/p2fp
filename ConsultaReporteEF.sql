DECLARE @__desde_0 datetime2 = '2024-11-24T00:00:00.0000000-04:00';
DECLARE @__hasta_1 datetime2 = '2026-11-25T00:00:00.0000000-04:00';

SELECT [c].[Nombre] AS [Categoria], COALESCE(SUM([d].[Cantidad]), 0) AS [UnidadesVendidas], COALESCE(SUM(CAST([d].[Cantidad] AS decimal(10,2)) * [d].[PrecioUnitario]), 0.0) AS [TotalVendido]
FROM [DetalleVenta] AS [d]
INNER JOIN [Venta] AS [v] ON [d].[IdVenta] = [v].[IdVenta]
INNER JOIN [Producto] AS [p] ON [d].[IdProducto] = [p].[IdProducto]
INNER JOIN [Categoria] AS [c] ON [p].[IdCategoria] = [c].[IdCategoria]
WHERE [v].[Fecha] >= @__desde_0 AND [v].[Fecha] <= @__hasta_1
GROUP BY [c].[Nombre]
ORDER BY COALESCE(SUM(CAST([d].[Cantidad] AS decimal(10,2)) * [d].[PrecioUnitario]), 0.0) DESC