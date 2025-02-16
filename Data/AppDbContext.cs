using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TagExplorer.Models;

namespace TagExplorer.Data;

public class AppDbContext : DbContext
{
    protected readonly IConfiguration Configuration;
    
    public AppDbContext(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // connect to Postgres DB with 
        options.UseNpgsql(Configuration.GetConnectionString("database"));
    }
    
    public DbSet<BaseFolder> BaseFolders { get; set; }
    public DbSet<Color> Colors { get; set; }
    public DbSet<TagDTO> Tags { get; set; }
    public DbSet<IconFavourite?> IconFavourites { get; set; }
}