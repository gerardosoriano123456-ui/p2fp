using Microsoft.EntityFrameworkCore;
using TechStore.Datos.Ado;
using TechStore.Datos.Ef;

namespace TechStore.Consola;

public static class Program
{
    private const string CadenaAdo =
        "Server=.;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string CadenaEf =
        "Server=.;Database=TechStoreDB_EF;Trusted_Connection=True;TrustServerCertificate=True;";

    public static void Main(string[] args)
    {
        Console.WriteLine("=== TechStore: ADO.NET vs EF Core ===");

        var salir = false;
        while (!salir)
        {
            MostrarMenu();
            var opcion = Console.ReadLine();
            Console.WriteLine();

            switch (opcion)
            {
                case "1":
                    ListarProductos();
                    break;
                case "2":
                    BuscarProductos();
                    break;
                case "3":
                    RegistrarVenta();
                    break;
                case "4":
                    PruebasRendimiento.Ejecutar();
                    break;
                case "5":
                    PruebasRendimiento.ComprobarRollback();
                    break;
                case "0":
                    salir = true;
                    break;
                default:
                    Console.WriteLine("Opción inválida.");
                    break;
            }

            Console.WriteLine();
        }
    }

    private static void MostrarMenu()
    {
        Console.WriteLine("1. Listar productos");
        Console.WriteLine("2. Buscar productos");
        Console.WriteLine("3. Registrar venta");
        Console.WriteLine("4. Pruebas de rendimiento (7 escenarios)");
        Console.WriteLine("5. Verificar Rollback por falta de stock");
        Console.WriteLine("0. Salir");
        Console.Write("Opción: ");
    }

    private static TechStoreContext CrearContextoEf()
    {
        var opciones = new DbContextOptionsBuilder<TechStoreContext>()
            .UseSqlServer(CadenaEf)
            .Options;
        return new TechStoreContext(opciones);
    }

    private static void ListarProductos()
    {
        var ado = new ProductoRepositorioAdo(CadenaAdo).ObtenerPrimeros(10);
        using var contexto = CrearContextoEf();
        var ef = new ProductoRepositorioEf(contexto).ObtenerPrimeros(10, seguimiento: false);

        Console.WriteLine("Primeros productos ADO.NET:");
        foreach (var producto in ado)
            Console.WriteLine($"{producto.IdProducto,-6} {producto.Codigo,-12} {producto.Nombre,-22} {producto.Marca}");

        Console.WriteLine("\nPrimeros productos EF Core:");
        foreach (var producto in ef)
            Console.WriteLine($"{producto.IdProducto,-6} {producto.Codigo,-12} {producto.Nombre,-22} {producto.Marca}");
    }

    private static void BuscarProductos()
    {
        Console.Write("Texto: ");
        var texto = Console.ReadLine() ?? "";
        Console.Write("Categoría (vacío = todas): ");
        var entrada = Console.ReadLine();
        int? categoria = int.TryParse(entrada, out var valor) ? valor : null;

        var ado = new ProductoRepositorioAdo(CadenaAdo).Buscar(texto, categoria);
        using var contexto = CrearContextoEf();
        var ef = new ProductoRepositorioEf(contexto).Buscar(texto, categoria);
        Console.WriteLine($"ADO.NET: {ado.Count} resultado(s); EF Core: {ef.Count} resultado(s).");
    }

    private static void RegistrarVenta()
    {
        Console.Write("Id cliente: ");
        if (!int.TryParse(Console.ReadLine(), out var idCliente)) return;
        Console.Write("Id producto: ");
        if (!int.TryParse(Console.ReadLine(), out var idProducto)) return;
        Console.Write("Cantidad: ");
        if (!int.TryParse(Console.ReadLine(), out var cantidad)) return;

        try
        {
            var id = new VentaRepositorioAdo(CadenaAdo)
                .RegistrarVenta(idCliente, new() { (idProducto, cantidad) });
            Console.WriteLine($"ADO.NET registró la venta {id}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ADO.NET revirtió la operación: {ex.Message}");
        }

        try
        {
            using var contexto = CrearContextoEf();
            var id = new VentaRepositorioEf(contexto)
                .RegistrarVenta(idCliente, new() { (idProducto, cantidad) });
            Console.WriteLine($"EF Core registró la venta {id}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EF Core revirtió la operación: {ex.Message}");
        }
    }
}
