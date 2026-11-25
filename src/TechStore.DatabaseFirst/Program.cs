using Microsoft.EntityFrameworkCore;
using TechStore.DatabaseFirst.Models;

var opciones = new DbContextOptionsBuilder<TechStoreDbContext>()
    .UseSqlServer("Server=.;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;")
    .Options;

using var db = new TechStoreDbContext(opciones);
var total = await db.Productos.CountAsync();
Console.WriteLine($"Database First: {total} productos encontrados.");
