using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace anna_bot.OutServices.DbContexts;

public class SongDbContextFactory : IDesignTimeDbContextFactory<SongDbContext>
{
    public SongDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SongDbContext>();
        optionsBuilder.UseSqlite(configuration.GetConnectionString("SongDb"));

        return new SongDbContext(optionsBuilder.Options);
    }
}
