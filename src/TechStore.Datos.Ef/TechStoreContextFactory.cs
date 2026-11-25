using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TechStore.Datos.Ef;

public sealed class TechStoreContextFactory : IDesignTimeDbContextFactory<TechStoreContext>
{
    public TechStoreContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TECHSTORE_EF_CONNECTION")
            ?? "Server=.;Database=TechStoreDB_EF;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<TechStoreContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new TechStoreContext(options);
    }
}
