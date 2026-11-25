namespace TechStore.Dominio.Modelos;

public class Venta
{
    public int IdVenta { get; set; }
    public int IdCliente { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public List<DetalleVenta> Detalles { get; set; } = new();
}
