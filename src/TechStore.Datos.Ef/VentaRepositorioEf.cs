using TechStore.Dominio.Repositorios;
using Microsoft.EntityFrameworkCore;
using TechStore.Dominio.Modelos;

namespace TechStore.Datos.Ef;

public class VentaRepositorioEf : IVentaRepositorio
{
    private readonly TechStoreContext _db;
    public VentaRepositorioEf(TechStoreContext db) { _db = db; }

    public int RegistrarVenta(int idCliente, List<(int IdProducto, int Cantidad)> items)
    {
        if (items.Count == 0)
            throw new ArgumentException("La venta debe tener al menos un producto.", nameof(items));
        if (items.Any(i => i.Cantidad <= 0))
            throw new ArgumentOutOfRangeException(nameof(items), "Las cantidades deben ser mayores que cero.");

        var necesarias = items
            .GroupBy(i => i.IdProducto)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Cantidad));

        using var tx = _db.Database.BeginTransaction();
        try
        {
            var precios = new Dictionary<int, decimal>();
            foreach (var (idProducto, cantidad) in necesarias)
            {
                var producto = _db.Productos
                    .FromSqlRaw("SELECT * FROM Producto WITH (UPDLOCK, ROWLOCK) WHERE IdProducto = {0}", idProducto)
                    .AsTracking()
                    .SingleOrDefault();
                if (producto is null)
                    throw new InvalidOperationException($"El producto {idProducto} no existe.");
                if (producto.Stock < cantidad)
                    throw new InvalidOperationException(
                        $"Stock insuficiente para el producto {idProducto} " +
                        $"(disponible: {producto.Stock}, solicitado: {cantidad}).");
                precios[idProducto] = producto.Precio;
            }

            var venta = new Venta { IdCliente = idCliente, Fecha = DateTime.Now, Total = 0 };
            _db.Ventas.Add(venta);
            _db.SaveChanges();

            decimal total = 0;
            foreach (var (idProducto, cantidad) in items)
            {
                var producto = _db.Productos.Find(idProducto)!;
                var precio = precios[idProducto];
                _db.DetallesVenta.Add(new DetalleVenta
                {
                    IdVenta = venta.IdVenta,
                    IdProducto = idProducto,
                    Cantidad = cantidad,
                    PrecioUnitario = precio
                });
                producto.Stock -= cantidad;
                total += precio * cantidad;
            }
            venta.Total = total;
            _db.SaveChanges();
            tx.Commit();
            return venta.IdVenta;
        }
        catch
        {
            tx.Rollback();
            _db.ChangeTracker.Clear();
            throw;
        }
    }
}
