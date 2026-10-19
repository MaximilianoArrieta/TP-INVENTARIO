using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace InventarioApi.Infrastructure.Data;

// Lo usan solo las herramientas de migraciones (Add-Migration / Update-Database).
// Busca el appsettings.json de la API sin importar desde qué proyecto se ejecuten.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var actual = Directory.GetCurrentDirectory();
        var candidatos = new[]
        {
            actual,
            Path.Combine(actual, "..", "InventarioApi"),
            Path.Combine(actual, "..", "InventarioApi.Api"),
            Path.Combine(actual, "InventarioApi")
        };

        var rutaConfig = candidatos.FirstOrDefault(r => File.Exists(Path.Combine(r, "appsettings.json")))
            ?? throw new FileNotFoundException(
                "No se encontró el appsettings.json de la API. Elegí 'InventarioApi' como proyecto de inicio.");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.GetFullPath(rutaConfig))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));

        return new AppDbContext(optionsBuilder.Options);
    }
}
