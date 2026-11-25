namespace TechStore.Dominio.Modelos;

public class Producto
{
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Marca { get; set; } = "Generica";
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int IdCategoria { get; set; }

    // Opcional: se llena solo cuando la consulta hace JOIN con Categoria,
    // para no tener que pedirla aparte al mostrar un listado.
    public string? NombreCategoria { get; set; }
}
