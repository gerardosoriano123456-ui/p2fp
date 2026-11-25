using System.Diagnostics;
using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TechStore.Datos.Ado;
using TechStore.Datos.Ef;
using TechStore.Dominio.Modelos;

namespace TechStore.Consola;

internal static class PruebasRendimiento
{
    private const string CadenaAdo =
        "Server=.;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string CadenaEf =
        "Server=.;Database=TechStoreDB_EF;Trusted_Connection=True;TrustServerCertificate=True;";

    public static void Ejecutar()
    {
        var opcionesEf = new DbContextOptionsBuilder<TechStoreContext>()
            .UseSqlServer(CadenaEf, sql => sql.CommandTimeout(180))
            .Options;
        var ado = new ProductoRepositorioAdo(CadenaAdo);
        using var contextoEf = new TechStoreContext(opcionesEf);
        var ef = new ProductoRepositorioEf(contextoEf);
        var resultados = new List<Resultado>();
        var cultura = CultureInfo.InvariantCulture;

        Console.WriteLine("Todos los tiempos son promedios en ms de 4 ejecuciones; la primera es calentamiento.");
        Console.WriteLine("En lecturas, EF Core 1 = con seguimiento; EF Core 2 = AsNoTracking.");
        Console.WriteLine("En ajustes, EF Core 1 = entidades; EF Core 2 = ExecuteUpdate.");
        Console.WriteLine("{0,-34} {1,14} {2,17} {3,20}", "Escenario", "ADO.NET", "EF Core 1", "EF Core 2");

        foreach (var cantidad in new[] { 1_000, 10_000, 50_000 })
        {
            Console.WriteLine($"Midiendo lectura de {cantidad:N0} productos...");
            var adoMs = Medir(() => _ = ado.ObtenerPrimeros(cantidad));
            var efConSeguimiento = Medir(
                () => _ = ef.ObtenerPrimeros(cantidad, seguimiento: true),
                contextoEf.ChangeTracker.Clear);
            var efSinSeguimiento = Medir(
                () => _ = ef.ObtenerPrimeros(cantidad, seguimiento: false),
                contextoEf.ChangeTracker.Clear);
            Agregar($"1. Leer {cantidad:N0}", adoMs, efConSeguimiento, efSinSeguimiento);
        }

        var ids = Enumerable.Range(0, 1_000)
            .Select(_ => Random.Shared.Next(1, 50_001))
            .ToArray();
        var adoId = Medir(() =>
        {
            foreach (var id in ids) _ = ado.ObtenerPorId(id);
        });
        var efId = Medir(() =>
        {
            foreach (var id in ids) _ = ef.ObtenerPorId(id);
        });
        Agregar("2. Buscar por Id (1,000)", adoId, efId);

        var filtros = Enumerable.Range(0, 100)
            .Select(i => (Texto: "Producto", Categoria: i % 5 + 1))
            .ToArray();
        var adoFiltro = Medir(() =>
        {
            foreach (var filtro in filtros) _ = ado.Buscar(filtro.Texto, filtro.Categoria);
        });
        var efFiltro = Medir(() =>
        {
            foreach (var filtro in filtros) _ = ef.Buscar(filtro.Texto, filtro.Categoria);
        });
        Agregar("3. Filtros (100)", adoFiltro, efFiltro);

        var token = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var prefijoAdo = "";
        var adoInsertar = Medir(() =>
        {
            prefijoAdo = $"AT{token}{Guid.NewGuid():N}"[..12];
            for (var i = 0; i < 1_000; i++)
                ado.Agregar(CrearProducto($"{prefijoAdo}{i:D4}", "ADO.NET"));
        }, () => EliminarProductos(CadenaAdo, prefijoAdo));

        var prefijoEf = "";
        var efInsertar = Medir(() =>
        {
            prefijoEf = $"EF{token}{Guid.NewGuid():N}"[..12];
            for (var i = 0; i < 1_000; i++)
            {
                using var contexto = new TechStoreContext(opcionesEf);
                new ProductoRepositorioEf(contexto).Agregar(CrearProducto($"{prefijoEf}{i:D4}", "EF Core"));
            }
        }, () =>
        {
            EliminarProductos(CadenaEf, prefijoEf);
        });
        Agregar("4. Insertar 1,000", adoInsertar, efInsertar);

        var preciosAdoOriginales = CapturarPrecios(CadenaAdo);
        var preciosEfOriginales = CapturarPrecios(CadenaEf);
        double adoAjuste;
        double efAjusteEntidades;
        double efAjusteMasivo;
        try
        {
            adoAjuste = Medir(() => ado.AjustarPrecios(1, 0.01m));
            efAjusteEntidades = Medir(
                () => ef.AjustarPrecios(1, 0.01m),
                contextoEf.ChangeTracker.Clear);
            efAjusteMasivo = Medir(
                () => ef.AjustarPreciosExecuteUpdate(1, 0.01m),
                contextoEf.ChangeTracker.Clear);
        }
        finally
        {
            RestaurarPrecios(CadenaAdo, preciosAdoOriginales);
            RestaurarPrecios(CadenaEf, preciosEfOriginales);
            contextoEf.ChangeTracker.Clear();
        }
        Agregar("5. Ajuste (ADO / entidades)", adoAjuste, efAjusteEntidades, efAjusteMasivo);

        var desde = DateTime.Today.AddYears(-2);
        var hasta = DateTime.Today.AddDays(1);
        var adoReporte = Medir(() => _ = ado.ReporteVentasPorCategoria(desde, hasta));
        var efReporte = Medir(() => _ = ef.ReporteVentasPorCategoria(desde, hasta));
        Agregar("6. Reporte por categoría", adoReporte, efReporte);
        var sqlReporte = ef.ConsultaReporteVentasPorCategoria(desde, hasta).ToQueryString();
        File.WriteAllText(
            Path.Combine(Environment.CurrentDirectory, "ConsultaReporteEF.sql"),
            sqlReporte);

        var adoVentas = MedirVentasAdo();
        var efVentas = MedirVentasEf(opcionesEf);
        Agregar("7. Ventas (100 x 3 productos)", adoVentas, efVentas);

        File.WriteAllLines(
            Path.Combine(Environment.CurrentDirectory, "ResultadosRendimiento.csv"),
            new[]
            {
                "Escenario,ADO_NET_ms,EF_Variante_1_ms,EF_Variante_2_ms"
            }.Concat(resultados.Select(r => string.Join(",",
                Csv(r.Escenario),
                r.Ado?.ToString("F2", cultura) ?? "",
                r.EfSeguimiento?.ToString("F2", cultura) ?? "",
                r.EfSinSeguimiento?.ToString("F2", cultura) ?? ""))));
        Console.WriteLine($"Resultados guardados en {Path.Combine(Environment.CurrentDirectory, "ResultadosRendimiento.csv")}.");
        Console.WriteLine($"SQL del reporte guardado en {Path.Combine(Environment.CurrentDirectory, "ConsultaReporteEF.sql")}.");

        void Agregar(string escenario, double? tiempoAdo, double? tiempoEf, double? tiempoEfNoTracking = null)
        {
            resultados.Add(new Resultado(escenario, tiempoAdo, tiempoEf, tiempoEfNoTracking));
            Console.WriteLine("{0,-34} {1,14:F2} {2,17:F2} {3,20:F2}",
                escenario, tiempoAdo, tiempoEf, tiempoEfNoTracking);
        }
    }

    public static void ComprobarRollback()
    {
        ComprobarRollbackAdo();
        ComprobarRollbackEf();
    }

    private static double Medir(Action accion, Action? limpieza = null, int repeticiones = 5)
    {
        var tiempos = new List<double>(repeticiones - 1);
        for (var i = 0; i < repeticiones; i++)
        {
            var reloj = Stopwatch.StartNew();
            accion();
            reloj.Stop();
            if (i > 0) tiempos.Add(reloj.Elapsed.TotalMilliseconds);
            limpieza?.Invoke();
        }
        return tiempos.Average();
    }

    private static double MedirVentasAdo()
    {
        var stocks = LeerStocks(CadenaAdo);
        var venta = new VentaRepositorioAdo(CadenaAdo);
        var idsVentas = new List<int>();
        try
        {
            PrepararStock(CadenaAdo);
            return Medir(() =>
            {
                for (var i = 0; i < 100; i++)
                    idsVentas.Add(venta.RegistrarVenta(1, ItemsPrueba()));
            }, () =>
            {
                LimpiarVentas(CadenaAdo, idsVentas);
                idsVentas.Clear();
            });
        }
        finally
        {
            LimpiarVentas(CadenaAdo, idsVentas);
            RestaurarStocks(CadenaAdo, stocks);
        }
    }

    private static double MedirVentasEf(DbContextOptions<TechStoreContext> opciones)
    {
        var stocks = LeerStocks(CadenaEf);
        var idsVentas = new List<int>();
        try
        {
            PrepararStock(CadenaEf);
            return Medir(() =>
            {
                for (var i = 0; i < 100; i++)
                {
                    using var contexto = new TechStoreContext(opciones);
                    idsVentas.Add(new VentaRepositorioEf(contexto).RegistrarVenta(1, ItemsPrueba()));
                }
            }, () =>
            {
                LimpiarVentas(CadenaEf, idsVentas);
                idsVentas.Clear();
            });
        }
        finally
        {
            LimpiarVentas(CadenaEf, idsVentas);
            RestaurarStocks(CadenaEf, stocks);
        }
    }

    private static void ComprobarRollbackAdo()
    {
        ComprobarRollback(
            CadenaAdo,
            () => new VentaRepositorioAdo(CadenaAdo).RegistrarVenta(1, new() { (1, int.MaxValue) }),
            "ADO.NET");
    }

    private static void ComprobarRollbackEf()
    {
        var opciones = new DbContextOptionsBuilder<TechStoreContext>().UseSqlServer(CadenaEf).Options;
        ComprobarRollback(
            CadenaEf,
            () =>
            {
                using var contexto = new TechStoreContext(opciones);
                return new VentaRepositorioEf(contexto).RegistrarVenta(1, new() { (1, int.MaxValue) });
            },
            "EF Core");
    }

    private static void ComprobarRollback(string cadena, Func<int> registrar, string tecnologia)
    {
        var antes = ContarVentasYDetalles(cadena);
        var stockAntes = LeerStocks(cadena);
        try
        {
            _ = registrar();
            Console.WriteLine($"{tecnologia}: ERROR, la venta sin stock se aceptó.");
        }
        catch (InvalidOperationException)
        {
            var despues = ContarVentasYDetalles(cadena);
            var stockDespues = LeerStocks(cadena);
            var correcto = antes == despues &&
                stockAntes.OrderBy(x => x.Key).SequenceEqual(stockDespues.OrderBy(x => x.Key));
            Console.WriteLine($"{tecnologia}: {(correcto ? "Rollback verificado" : "ERROR: la base cambió")}.");
        }
    }

    private static Dictionary<int, int> LeerStocks(string cadena)
    {
        using var conexion = new SqlConnection(cadena);
        conexion.Open();
        using var comando = new SqlCommand(
            "SELECT IdProducto, Stock FROM dbo.Producto WHERE IdProducto IN (1, 2, 3)", conexion);
        using var lector = comando.ExecuteReader();
        var stocks = new Dictionary<int, int>();
        while (lector.Read()) stocks.Add(lector.GetInt32(0), lector.GetInt32(1));
        return stocks;
    }

    private static void PrepararStock(string cadena)
    {
        using var conexion = new SqlConnection(cadena);
        conexion.Open();
        using var comando = new SqlCommand(
            "UPDATE dbo.Producto SET Stock = 100000 WHERE IdProducto IN (1, 2, 3)", conexion);
        comando.ExecuteNonQuery();
    }

    private static DataTable CapturarPrecios(string cadena)
    {
        var tabla = new DataTable();
        using var conexion = new SqlConnection(cadena);
        conexion.Open();
        using var comando = new SqlCommand(
            "SELECT IdProducto, Precio FROM dbo.Producto WHERE IdCategoria = 1", conexion);
        using var lector = comando.ExecuteReader();
        tabla.Load(lector);
        return tabla;
    }

    private static void RestaurarPrecios(string cadena, DataTable precios)
    {
        if (precios.Rows.Count == 0) return;
        using var conexion = new SqlConnection(cadena);
        conexion.Open();
        using var transaccion = conexion.BeginTransaction();
        using (var crear = new SqlCommand(
            "CREATE TABLE #PrecioOriginal (IdProducto int NOT NULL PRIMARY KEY, Precio decimal(10,2) NOT NULL)",
            conexion, transaccion))
            crear.ExecuteNonQuery();

        using (var carga = new SqlBulkCopy(conexion, SqlBulkCopyOptions.Default, transaccion))
        {
            carga.DestinationTableName = "#PrecioOriginal";
            carga.ColumnMappings.Add("IdProducto", "IdProducto");
            carga.ColumnMappings.Add("Precio", "Precio");
            carga.WriteToServer(precios);
        }

        using (var restaurar = new SqlCommand(
            @"UPDATE p SET p.Precio = original.Precio
              FROM dbo.Producto p
              INNER JOIN #PrecioOriginal original ON original.IdProducto = p.IdProducto;",
            conexion, transaccion))
            restaurar.ExecuteNonQuery();
        transaccion.Commit();
    }

    private static void RestaurarStocks(string cadena, Dictionary<int, int> stocks)
    {
        using var conexion = new SqlConnection(cadena);
        conexion.Open();
        using var transaccion = conexion.BeginTransaction();
        foreach (var (id, stock) in stocks)
        {
            using var comando = new SqlCommand(
                "UPDATE dbo.Producto SET Stock = @stock WHERE IdProducto = @id", conexion, transaccion);
            comando.Parameters.Add("@stock", System.Data.SqlDbType.Int).Value = stock;
            comando.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;
            comando.ExecuteNonQuery();
        }
        transaccion.Commit();
    }

    private static void LimpiarVentas(string cadena, List<int> ids)
    {
        if (ids.Count == 0) return;
        using var conexion = new SqlConnection(cadena);
        conexion.Open();
        using var transaccion = conexion.BeginTransaction();
        using var comando = new SqlCommand("", conexion, transaccion);
        var parametros = new List<string>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            var nombre = $"@id{i}";
            parametros.Add(nombre);
            comando.Parameters.Add(nombre, System.Data.SqlDbType.Int).Value = ids[i];
        }
        var lista = string.Join(", ", parametros);
        comando.CommandText = $@"
            UPDATE p SET p.Stock = p.Stock + d.CantidadTotal
            FROM dbo.Producto p
            INNER JOIN (
                SELECT IdProducto, SUM(Cantidad) AS CantidadTotal
                FROM dbo.DetalleVenta WHERE IdVenta IN ({lista}) GROUP BY IdProducto
            ) d ON d.IdProducto = p.IdProducto;
            DELETE FROM dbo.DetalleVenta WHERE IdVenta IN ({lista});
            DELETE FROM dbo.Venta WHERE IdVenta IN ({lista});";
        comando.ExecuteNonQuery();
        transaccion.Commit();
    }

    private static (int Ventas, int Detalles) ContarVentasYDetalles(string cadena)
    {
        using var conexion = new SqlConnection(cadena);
        conexion.Open();
        using var comando = new SqlCommand(
            "SELECT (SELECT COUNT(*) FROM dbo.Venta), (SELECT COUNT(*) FROM dbo.DetalleVenta)", conexion);
        using var lector = comando.ExecuteReader();
        lector.Read();
        return (lector.GetInt32(0), lector.GetInt32(1));
    }

    private static void EliminarProductos(string cadena, string prefijo)
    {
        if (string.IsNullOrEmpty(prefijo)) return;
        using var conexion = new SqlConnection(cadena);
        conexion.Open();
        using var comando = new SqlCommand("DELETE FROM dbo.Producto WHERE Codigo LIKE @prefijo", conexion);
        comando.Parameters.Add("@prefijo", System.Data.SqlDbType.NVarChar, 20).Value = prefijo + "%";
        comando.ExecuteNonQuery();
    }

    private static Producto CrearProducto(string codigo, string sufijo) => new()
    {
        Codigo = codigo,
        Nombre = $"Benchmark {sufijo}",
        Marca = "TechStore",
        Precio = 10m,
        Stock = 100,
        IdCategoria = 1
    };

    private static List<(int IdProducto, int Cantidad)> ItemsPrueba() =>
        new() { (1, 1), (2, 1), (3, 1) };

    private static string Csv(string valor) => $"\"{valor.Replace("\"", "\"\"")}\"";

    private sealed record Resultado(
        string Escenario,
        double? Ado,
        double? EfSeguimiento,
        double? EfSinSeguimiento);
}
