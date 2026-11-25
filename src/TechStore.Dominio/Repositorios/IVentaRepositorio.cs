namespace TechStore.Dominio.Repositorios;

public interface IVentaRepositorio
{
    // Debe ser TRANSACCIONAL: si un producto no tiene stock suficiente,
    // no se guarda NADA (ni la venta, ni los detalles, ni los descuentos de stock).
    int RegistrarVenta(int idCliente, List<(int IdProducto, int Cantidad)> items);
}
