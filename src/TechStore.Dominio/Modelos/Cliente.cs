namespace TechStore.Dominio.Modelos;

public class Cliente
{
    public int IdCliente { get; set; }
    public string Cedula { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
}
