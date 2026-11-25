using Microsoft.EntityFrameworkCore;
using TechStore.Dominio.Modelos;

namespace TechStore.Datos.Ef;

public class TechStoreContext : DbContext
{
    public TechStoreContext(DbContextOptions<TechStoreContext> options) : base(options) { }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(e =>
        {
            e.ToTable("Categoria");
            e.HasKey(x => x.IdCategoria);
            e.Property(x => x.Nombre).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
        });

        modelBuilder.Entity<Producto>(e =>
        {
            e.ToTable("Producto", table =>
            {
                table.HasCheckConstraint("CK_Producto_Precio", "[Precio] > 0");
                table.HasCheckConstraint("CK_Producto_Stock", "[Stock] >= 0");
            });
            e.HasKey(x => x.IdProducto);
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Marca).HasMaxLength(80).HasDefaultValue("Generica").IsRequired();
            e.Property(x => x.Precio).HasColumnType("decimal(10,2)");
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasOne<Categoria>()
                .WithMany()
                .HasForeignKey(x => x.IdCategoria)
                .OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.NombreCategoria);
        });

        modelBuilder.Entity<Cliente>(e =>
        {
            e.ToTable("Cliente");
            e.HasKey(x => x.IdCliente);
            e.Property(x => x.Cedula).HasMaxLength(15).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Cedula).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Venta>(e =>
        {
            e.ToTable("Venta");
            e.HasKey(x => x.IdVenta);
            e.Property(x => x.Fecha).HasDefaultValueSql("GETDATE()");
            e.Property(x => x.Total).HasColumnType("decimal(12,2)");
            e.HasOne<Cliente>()
                .WithMany()
                .HasForeignKey(x => x.IdCliente)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Detalles)
                .WithOne()
                .HasForeignKey(x => x.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DetalleVenta>(e =>
        {
            e.ToTable("DetalleVenta", table =>
                table.HasCheckConstraint("CK_DetalleVenta_Cantidad", "[Cantidad] > 0"));
            e.HasKey(x => x.IdDetalle);
            e.Property(x => x.PrecioUnitario).HasColumnType("decimal(10,2)");
            e.HasOne<Producto>()
                .WithMany()
                .HasForeignKey(x => x.IdProducto)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
