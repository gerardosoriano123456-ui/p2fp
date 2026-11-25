using Microsoft.Data.SqlClient;
using TechStore.Dominio.Repositorios;

namespace TechStore.Datos.Ado;

public class VentaRepositorioAdo : IVentaRepositorio
{
    private readonly string _cadenaConexion;

    public VentaRepositorioAdo(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public int RegistrarVenta(int idCliente, List<(int IdProducto, int Cantidad)> items)
    {
        if (items.Count == 0)
            throw new ArgumentException("La venta debe tener al menos un producto.", nameof(items));
        if (items.Any(i => i.Cantidad <= 0))
            throw new ArgumentOutOfRangeException(nameof(items), "Las cantidades deben ser mayores que cero.");

        // Si el mismo producto viene en más de un renglón, sumamos las
        // cantidades para verificar el stock TOTAL que hace falta de él.
        // Si no hiciéramos esto, dos renglones del mismo producto podrían
        // pasar la verificación cada uno por separado aunque juntos no
        // alcance el stock.
        var cantidadNecesariaPorProducto = items
            .GroupBy(i => i.IdProducto)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Cantidad));

        using var conexion = new SqlConnection(_cadenaConexion);
        conexion.Open();
        using var tx = conexion.BeginTransaction();

        try
        {
            var precios = new Dictionary<int, decimal>();

            // 1) Verificar stock de TODOS los productos antes de escribir
            //    nada. WITH (UPDLOCK, ROWLOCK) bloquea la fila mientras dura
            //    la transacción, para que otra venta simultánea no lea el
            //    mismo stock y termine vendiendo lo mismo dos veces.
            foreach (var (idProducto, cantidadNecesaria) in cantidadNecesariaPorProducto)
            {
                using var comandoStock = new SqlCommand(
                    "SELECT Precio, Stock FROM Producto WITH (UPDLOCK, ROWLOCK) WHERE IdProducto = @idProducto",
                    conexion, tx);
                comandoStock.Parameters.AddWithValue("@idProducto", idProducto);

                decimal precio;
                int stockDisponible;
                using (var lector = comandoStock.ExecuteReader())
                {
                    if (!lector.Read())
                        throw new InvalidOperationException($"El producto {idProducto} no existe.");

                    precio = lector.GetDecimal(0);
                    stockDisponible = lector.GetInt32(1);
                }

                if (stockDisponible < cantidadNecesaria)
                    throw new InvalidOperationException(
                        $"Stock insuficiente para el producto {idProducto} " +
                        $"(disponible: {stockDisponible}, solicitado: {cantidadNecesaria}).");

                precios[idProducto] = precio;
            }

            // 2) Insertar la Venta con Total en 0; lo actualizamos al final,
            //    cuando ya sabemos el total real.
            using var comandoVenta = new SqlCommand(
                "INSERT INTO Venta (IdCliente, Total) VALUES (@idCliente, 0); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                conexion, tx);
            comandoVenta.Parameters.AddWithValue("@idCliente", idCliente);
            var idVenta = Convert.ToInt32(comandoVenta.ExecuteScalar());

            // 3) Insertar un DetalleVenta por cada renglón recibido y
            //    descontar el stock correspondiente.
            decimal total = 0;
            foreach (var (idProducto, cantidad) in items)
            {
                var precio = precios[idProducto];

                using var comandoDetalle = new SqlCommand(
                    "INSERT INTO DetalleVenta (IdVenta, IdProducto, Cantidad, PrecioUnitario) " +
                    "VALUES (@idVenta, @idProducto, @cantidad, @precio)",
                    conexion, tx);
                comandoDetalle.Parameters.AddWithValue("@idVenta", idVenta);
                comandoDetalle.Parameters.AddWithValue("@idProducto", idProducto);
                comandoDetalle.Parameters.AddWithValue("@cantidad", cantidad);
                comandoDetalle.Parameters.AddWithValue("@precio", precio);
                comandoDetalle.ExecuteNonQuery();

                using var comandoStockUpdate = new SqlCommand(
                    "UPDATE Producto SET Stock = Stock - @cantidad WHERE IdProducto = @idProducto",
                    conexion, tx);
                comandoStockUpdate.Parameters.AddWithValue("@cantidad", cantidad);
                comandoStockUpdate.Parameters.AddWithValue("@idProducto", idProducto);
                comandoStockUpdate.ExecuteNonQuery();

                total += precio * cantidad;
            }

            // 4) Ahora sí, actualizar el total real de la venta.
            using var comandoTotal = new SqlCommand(
                "UPDATE Venta SET Total = @total WHERE IdVenta = @idVenta",
                conexion, tx);
            comandoTotal.Parameters.AddWithValue("@total", total);
            comandoTotal.Parameters.AddWithValue("@idVenta", idVenta);
            comandoTotal.ExecuteNonQuery();

            tx.Commit();
            return idVenta;
        }
        catch
        {
            // Cualquier fallo (stock insuficiente, producto inexistente,
            // error de conexión) deshace TODO lo que se alcanzó a escribir
            // en esta transacción: ni la Venta, ni los DetalleVenta, ni los
            // descuentos de stock quedan guardados.
            tx.Rollback();
            throw;
        }
    }
}
