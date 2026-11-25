using Microsoft.EntityFrameworkCore;
using TechStore.Datos.Ado;
using TechStore.Datos.Ef;
using TechStore.Dominio.Modelos;

const string cadenaAdo = "Server=.;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;";
const string cadenaEf = "Server=.;Database=TechStoreDB_EF;Trusted_Connection=True;TrustServerCertificate=True;";

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<TechStoreContext>(o => o.UseSqlServer(cadenaEf));
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/productos", (string? proveedor, string? texto, int? categoria, TechStoreContext db) =>
{
    var buscar = !string.IsNullOrWhiteSpace(texto) || categoria.HasValue;
    if (string.Equals(proveedor, "ado", StringComparison.OrdinalIgnoreCase))
    {
        var repo = new ProductoRepositorioAdo(cadenaAdo);
        return Results.Ok(buscar ? repo.Buscar(texto ?? "", categoria) : repo.ObtenerPrimeros(100));
    }

    var ef = new ProductoRepositorioEf(db);
    return Results.Ok(buscar ? ef.Buscar(texto ?? "", categoria) : ef.ObtenerPrimeros(100, seguimiento: false));
});

app.MapGet("/api/categorias", (TechStoreContext db) =>
    Results.Ok(db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToList()));

app.MapPost("/api/ventas", (VentaRequest request, TechStoreContext db) =>
{
    if (request.IdCliente <= 0 || request.IdProducto <= 0 || request.Cantidad <= 0)
        return (IResult)Results.BadRequest(new { mensaje = "Indica un cliente, producto y cantidad válidos." });
    try
    {
        var items = new List<(int IdProducto, int Cantidad)> { (request.IdProducto, request.Cantidad) };
        var id = string.Equals(request.Proveedor, "ado", StringComparison.OrdinalIgnoreCase)
            ? new VentaRepositorioAdo(cadenaAdo).RegistrarVenta(request.IdCliente, items)
            : new VentaRepositorioEf(db).RegistrarVenta(request.IdCliente, items);
        return (IResult)Results.Ok(new { idVenta = id, proveedor = request.Proveedor });
    }
    catch (Exception ex)
    {
        return (IResult)Results.BadRequest(new { mensaje = ex.Message });
    }
});

app.MapGet("/api/estado", () => Results.Ok(new { listo = true }));

app.MapGet("/api/revision", () =>
{
    var raiz = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", ".."));
    var archivos = new[]
    {
        "TechStoreDB.sql", "SeedTechStoreDbEf.sql", "ResultadosRendimiento.csv",
        "MetricasDesarrollo.csv", "InformeTecnico.pdf", "ConsultaReporteEF.sql"
    };
    var disponibles = archivos.Select(nombre => new
    {
        nombre,
        existe = File.Exists(Path.Combine(raiz, nombre))
    }).ToArray();
    var csv = Path.Combine(raiz, "ResultadosRendimiento.csv");
    var resultados = File.Exists(csv)
        ? File.ReadAllLines(csv).Skip(1).Where(linea => !string.IsNullOrWhiteSpace(linea)).ToArray()
        : Array.Empty<string>();
    return Results.Ok(new { archivos = disponibles, resultados });
});

app.MapGet("/api/archivo/{nombre}", (string nombre) =>
{
    var permitidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "TechStoreDB.sql", "SeedTechStoreDbEf.sql", "ResultadosRendimiento.csv",
        "MetricasDesarrollo.csv", "InformeTecnico.pdf", "ConsultaReporteEF.sql"
    };
    if (!permitidos.Contains(nombre)) return (IResult)Results.NotFound();
    var raiz = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", ".."));
    var ruta = Path.Combine(raiz, nombre);
    return File.Exists(ruta)
        ? (IResult)Results.File(ruta, "application/octet-stream", nombre)
        : (IResult)Results.NotFound();
});
app.Run();

record VentaRequest(string Proveedor, int IdCliente, int IdProducto, int Cantidad);
