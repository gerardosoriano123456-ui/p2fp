using TechStore.Dominio.Modelos;
using TechStore.Dominio.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace TechStore.Datos.Ef;

public class ProductoRepositorioEf : IProductoRepositorio
{
    private readonly TechStoreContext _db;
    public ProductoRepositorioEf(TechStoreContext db) { _db = db; }

    public List<Producto> ObtenerTodos()
    {
        return _db.Productos.AsNoTracking().ToList();
    }

    public List<Producto> ObtenerPrimeros(int cantidad, bool seguimiento)
    {
        IQueryable<Producto> consulta = _db.Productos.OrderBy(p => p.IdProducto).Take(cantidad);
        if (!seguimiento)
            consulta = consulta.AsNoTracking();
        return consulta.ToList();
    }

    public Producto? ObtenerPorId(int id)
    {
        return _db.Productos.AsNoTracking().FirstOrDefault(p => p.IdProducto == id);
    }

    public List<Producto> Buscar(string texto, int? idCategoria)
    {
        IQueryable<Producto> consulta = _db.Productos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(texto))
            consulta = consulta.Where(p => p.Nombre.Contains(texto));
        if (idCategoria.HasValue)
            consulta = consulta.Where(p => p.IdCategoria == idCategoria.Value);
        return consulta.OrderBy(p => p.Nombre).ToList();
    }

    public int Agregar(Producto producto)
    {
        _db.Productos.Add(producto);
        _db.SaveChanges();
        return producto.IdProducto;
    }

    public bool Actualizar(Producto producto)
    {
        var existente = _db.Productos.Find(producto.IdProducto);
        if (existente is null) return false;
        _db.Entry(existente).CurrentValues.SetValues(producto);
        return _db.SaveChanges() > 0;
    }

    public bool Eliminar(int id)
    {
        var producto = _db.Productos.Find(id);
        if (producto is null) return false;
        try
        {
            _db.Productos.Remove(producto);
            return _db.SaveChanges() > 0;
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    public int AjustarPrecios(int idCategoria, decimal porcentaje)
    {
        var productos = _db.Productos.Where(p => p.IdCategoria == idCategoria).ToList();
        foreach (var producto in productos)
            producto.Precio *= 1 + porcentaje / 100m;
        return _db.SaveChanges();
    }

    public List<ReporteCategoria> ReporteVentasPorCategoria(DateTime desde, DateTime hasta)
    {
        return ConsultaReporteVentasPorCategoria(desde, hasta).ToList();
    }

    public IQueryable<ReporteCategoria> ConsultaReporteVentasPorCategoria(DateTime desde, DateTime hasta)
    {
        var consulta =
            from detalle in _db.DetallesVenta.AsNoTracking()
            join venta in _db.Ventas.AsNoTracking() on detalle.IdVenta equals venta.IdVenta
            join producto in _db.Productos.AsNoTracking() on detalle.IdProducto equals producto.IdProducto
            join categoria in _db.Categorias.AsNoTracking() on producto.IdCategoria equals categoria.IdCategoria
            where venta.Fecha >= desde && venta.Fecha <= hasta
            group detalle by categoria.Nombre into g
            select new ReporteCategoria
            {
                Categoria = g.Key,
                UnidadesVendidas = g.Sum(x => x.Cantidad),
                TotalVendido = g.Sum(x => x.Cantidad * x.PrecioUnitario)
            };

        return consulta.OrderByDescending(x => x.TotalVendido);
    }

    public int AjustarPreciosExecuteUpdate(int idCategoria, decimal porcentaje)
    {
        return _db.Productos
            .Where(p => p.IdCategoria == idCategoria)
            .ExecuteUpdate(setters => setters.SetProperty(
                p => p.Precio,
                p => p.Precio * (1 + porcentaje / 100m)));
    }
}
