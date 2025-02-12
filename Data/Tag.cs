using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TagExplorer.Data;

public class Tag
{
    [Key]
    public int Id { get; set; }
    
    [Column(TypeName = "varchar(50)")]
    public string Name { get; set; }
    
    public int ColorId { get; set; }
    public Color Color { get; set; }
    
    [Column(TypeName = "varchar(200)")]
    public string Description { get; set; }
    
    [Column(TypeName = "varchar(50)")]
    public string IconName { get; set; }
    
    public bool IsSystemTag { get; set; }
    
    // Parameterless constructor required by EF
    public Tag() { }
    
    public Tag(string name, Color color, string description, string iconName, bool isSystemTag = false)
    {
        Name = name;
        Color = color;
        ColorId = color.Id;
        Description = description;
        IconName = iconName;
        IsSystemTag = isSystemTag;
    }
}

public static class SystemTags
{
    private static readonly SystemTag[] DefaultTags =
    [
        new SystemTag("Priority", "Priority", "Orange", "PriorityHigh"),
            new SystemTag("High", "High Priority", "Orange", "numeric1CircleOutline"),
            new SystemTag("Medium", "Medium Priority", "Orange", "numeric2CircleOutline"),
            new SystemTag("Low", "Low Priority", "Orange", "numeric3CircleOutline"),
            new SystemTag("Status", "Status", "Blue", "ListStatus"),
            new SystemTag("Not Started", "Not Started Status", "Blue", "Cancel"),
            new SystemTag("In Progress", "In Progress Status", "Blue", "FastForward"),
            new SystemTag("Waiting", "In Progress, but waiting Status", "Blue", "Hourglass"),
            new SystemTag("Completed", "Completed Status", "Blue", "Check")
    ];
    
    public static void UpdateSystemTags(AppDbContext db)
    {
        int noOfSystemTags = db.Tags.Count(e => e.IsSystemTag == true);
        if (noOfSystemTags == DefaultTags.Length)
            return;
        
        var systemTags = db.Tags.Where(e => e.IsSystemTag == true);
        db.Tags.RemoveRange(systemTags);
        db.SaveChanges();
        
        IQueryable<Color> defaultColors = db.Colors.Where(e => e.IsSystemColor == true);
        foreach (SystemTag tag in DefaultTags)
        {
            db.Add(new Tag(tag.Name, defaultColors.First(x => x.Name == tag.ColorName), tag.Description, tag.Icon, true));
        }
        db.SaveChanges();
    }
    
    private record SystemTag(string Name, string Description, string ColorName, string Icon);
}