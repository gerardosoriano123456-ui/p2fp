using TechStore.Dominio.Modelos;

namespace TechStore.Dominio.Repositorios;

public interface IProductoRepositorio
{
    List<Producto> ObtenerTodos();
    Producto? ObtenerPorId(int id);

    // Filtros combinados: texto en el nombre y, opcionalmente, categoría
    List<Producto> Buscar(string texto, int? idCategoria);

    int Agregar(Producto producto); // devuelve el nuevo Id
    bool Actualizar(Producto producto);
    bool Eliminar(int id); // false si el producto tiene ventas

    // Actualización masiva: sube o baja precios de una categoría
    int AjustarPrecios(int idCategoria, decimal porcentaje);

    List<ReporteCategoria> ReporteVentasPorCategoria(DateTime desde, DateTime hasta);
}
