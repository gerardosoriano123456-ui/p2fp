using System.Text;
using Microsoft.Data.SqlClient;
using TechStore.Dominio.Modelos;
using TechStore.Dominio.Repositorios;

namespace TechStore.Datos.Ado;

public class ProductoRepositorioAdo : IProductoRepositorio
{
    private readonly string _cadenaConexion;

    public ProductoRepositorioAdo(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public List<Producto> ObtenerTodos()
    {
        const string sql = @"
            SELECT p.IdProducto, p.Codigo, p.Nombre, p.Marca, p.Precio, p.Stock, p.IdCategoria,
                   c.Nombre AS NombreCategoria
            FROM Producto p
            INNER JOIN Categoria c ON c.IdCategoria = p.IdCategoria
            ORDER BY p.Nombre";

        var productos = new List<Producto>();

        using var conexion = new SqlConnection(_cadenaConexion);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion);
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            productos.Add(LeerProducto(lector));
        }

        return productos;
    }

    public List<Producto> ObtenerPrimeros(int cantidad)
    {
        const string sql = @"
            SELECT TOP (@cantidad) p.IdProducto, p.Codigo, p.Nombre, p.Marca, p.Precio, p.Stock, p.IdCategoria,
                   CAST(N'' AS NVARCHAR(50)) AS NombreCategoria
            FROM Producto p
            ORDER BY p.IdProducto";

        using var conexion = new SqlConnection(_cadenaConexion);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion);
        comando.CommandTimeout = 180;
        comando.Parameters.Add("@cantidad", System.Data.SqlDbType.Int).Value = cantidad;
        using var lector = comando.ExecuteReader();
        var productos = new List<Producto>();
        while (lector.Read())
            productos.Add(LeerProducto(lector));
        return productos;
    }

    public Producto? ObtenerPorId(int id)
    {
        const string sql = @"
            SELECT p.IdProducto, p.Codigo, p.Nombre, p.Marca, p.Precio, p.Stock, p.IdCategoria,
                   c.Nombre AS NombreCategoria
            FROM Producto p
            INNER JOIN Categoria c ON c.IdCategoria = p.IdCategoria
            WHERE p.IdProducto = @id";

        using var conexion = new SqlConnection(_cadenaConexion);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@id", id);

        using var lector = comando.ExecuteReader();
        return lector.Read() ? LeerProducto(lector) : null;
    }

    public List<Producto> Buscar(string texto, int? idCategoria)
    {
        // El SQL se arma dinámicamente (el fragmento de categoría solo se
        // agrega si idCategoria tiene valor), pero el VALOR del usuario
        // nunca entra al texto del SQL: siempre viaja como parámetro.
        var sql = new StringBuilder(@"
            SELECT p.IdProducto, p.Codigo, p.Nombre, p.Marca, p.Precio, p.Stock, p.IdCategoria,
                   c.Nombre AS NombreCategoria
            FROM Producto p
            INNER JOIN Categoria c ON c.IdCategoria = p.IdCategoria
            WHERE p.Nombre LIKE @texto");

        if (idCategoria.HasValue)
        {
            sql.Append(" AND p.IdCategoria = @idCategoria");
        }

        sql.Append(" ORDER BY p.Nombre");

        var productos = new List<Producto>();

        using var conexion = new SqlConnection(_cadenaConexion);
        conexion.Open();
        using var comando = new SqlCommand(sql.ToString(), conexion);

        // "%texto%" sigue siendo un valor de parámetro, no SQL: seguimos
        // protegidos de inyección aunque el usuario escriba comillas o "--".
        comando.Parameters.AddWithValue("@texto", $"%{texto}%");

        if (idCategoria.HasValue)
        {
            comando.Parameters.AddWithValue("@idCategoria", idCategoria.Value);
        }

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            productos.Add(LeerProducto(lector));
        }

        return productos;
    }

    public int Agregar(Producto producto)
    {
        const string sql = @"
            INSERT INTO Producto (Codigo, Nombre, Marca, Precio, Stock, IdCategoria)
            VALUES (@codigo, @nombre, @marca, @precio, @stock, @idCategoria);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = new SqlConnection(_cadenaConexion);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@codigo", producto.Codigo);
        comando.Parameters.AddWithValue("@nombre", producto.Nombre);
        comando.Parameters.AddWithValue("@marca", producto.Marca);
        comando.Parameters.AddWithValue("@precio", producto.Precio);
        comando.Parameters.AddWithValue("@stock", producto.Stock);
        comando.Parameters.AddWithValue("@idCategoria", producto.IdCategoria);

        return Convert.ToInt32(comando.ExecuteScalar());
    }

    public bool Actualizar(Producto producto)
    {
        const string sql = @"
            UPDATE Producto
            SET Codigo = @codigo, Nombre = @nombre, Marca = @marca, Precio = @precio,
                Stock = @stock, IdCategoria = @idCategoria
            WHERE IdProducto = @id";

        using var conexion = new SqlConnection(_cadenaConexion);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@codigo", producto.Codigo);
        comando.Parameters.AddWithValue("@nombre", producto.Nombre);
        comando.Parameters.AddWithValue("@marca", producto.Marca);
        comando.Parameters.AddWithValue("@precio", producto.Precio);
        comando.Parameters.AddWithValue("@stock", producto.Stock);
        comando.Parameters.AddWithValue("@idCategoria", producto.IdCategoria);
        comando.Parameters.AddWithValue("@id", producto.IdProducto);

        return comando.ExecuteNonQuery() > 0;
    }

    public bool Eliminar(int id)
    {
        const string sql = "DELETE FROM Producto WHERE IdProducto = @id";

        using var conexion = new SqlConnection(_cadenaConexion);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@id", id);

        try
        {
            return comando.ExecuteNonQuery() > 0;
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            // 547 = violación de FK: el producto tiene ventas en DetalleVenta.
            // No es un bug del programa, así que lo convertimos en un
            // "no se pudo eliminar" en vez de dejar que tumbe la aplicación.
            return false;
        }
    }

    public int AjustarPrecios(int idCategoria, decimal porcentaje)
    {
        const string sql = @"
            UPDATE Producto
            SET Precio = Precio * (1 + @porcentaje / 100.0)
            WHERE IdCategoria = @idCategoria";

        using var conexion = new SqlConnection(_cadenaConexion);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@porcentaje", porcentaje);
        comando.Parameters.AddWithValue("@idCategoria", idCategoria);

        return comando.ExecuteNonQuery();
    }

    public List<ReporteCategoria> ReporteVentasPorCategoria(DateTime desde, DateTime hasta)
    {
        const string sql = @"
            SELECT c.Nombre AS Categoria,
                   SUM(dv.Cantidad) AS UnidadesVendidas,
                   SUM(dv.Cantidad * dv.PrecioUnitario) AS TotalVendido
            FROM Categoria c
            INNER JOIN Producto p ON p.IdCategoria = c.IdCategoria
            INNER JOIN DetalleVenta dv ON dv.IdProducto = p.IdProducto
            INNER JOIN Venta v ON v.IdVenta = dv.IdVenta
            WHERE v.Fecha BETWEEN @desde AND @hasta
            GROUP BY c.Nombre
            ORDER BY TotalVendido DESC";

        var reporte = new List<ReporteCategoria>();

        using var conexion = new SqlConnection(_cadenaConexion);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@desde", desde);
        comando.Parameters.AddWithValue("@hasta", hasta);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            reporte.Add(new ReporteCategoria
            {
                Categoria = lector.GetString(0),
                UnidadesVendidas = lector.GetInt32(1),
                TotalVendido = lector.GetDecimal(2)
            });
        }

        return reporte;
    }

    // Centraliza la lectura de un Producto desde el SqlDataReader para no
    // repetir los mismos GetOrdinal/GetString en 3 métodos distintos.
    private static Producto LeerProducto(SqlDataReader lector)
    {
        return new Producto
        {
            IdProducto = lector.GetInt32(lector.GetOrdinal("IdProducto")),
            Codigo = lector.GetString(lector.GetOrdinal("Codigo")),
            Nombre = lector.GetString(lector.GetOrdinal("Nombre")),
            Marca = lector.GetString(lector.GetOrdinal("Marca")),
            Precio = lector.GetDecimal(lector.GetOrdinal("Precio")),
            Stock = lector.GetInt32(lector.GetOrdinal("Stock")),
            IdCategoria = lector.GetInt32(lector.GetOrdinal("IdCategoria")),
            NombreCategoria = lector.GetString(lector.GetOrdinal("NombreCategoria"))
        };
    }
}
