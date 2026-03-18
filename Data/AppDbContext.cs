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

    public DbSet<FolderBase> Folders { get; set; }
    public DbSet<TagAssignment> TagAssignments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FolderBase>()
            .HasDiscriminator<string>("FolderType")
            .HasValue<Folder>("Folder")
            .HasValue<BaseFolder>("BaseFolder");

        modelBuilder.Entity<TagAssignment>(entity =>
        {
            entity.ToTable("TagAssignments");

            entity.HasIndex(e => new { e.Enabled, e.TargetType, e.TargetPath });
            entity.HasIndex(e => new { e.Kind, e.Enabled });
            entity.HasIndex(e => e.TagId);
            entity.HasIndex(e => e.AutoRuleParentTagId);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_TagAssignments_Manual_AutoRuleParentTagIdNull", "\"Kind\" <> 0 OR (\"TagId\" IS NOT NULL AND \"AutoRuleParentTagId\" IS NULL)");
                t.HasCheckConstraint("CK_TagAssignments_AutoDirectChildren_AutoRuleParentTagIdRequired", "\"Kind\" <> 10 OR (\"TargetType\" = 0 AND \"TagId\" IS NULL AND \"AutoRuleParentTagId\" IS NOT NULL)");
            });
        });
    }
}