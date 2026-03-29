using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TagExplorer.Data;

public class TagDTO
{
    [Key]
    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "varchar(20)")]
    public string? CreatedBy { get; set; }

    public bool IsArchived { get; set; }

    [Column(TypeName = "varchar(50)")]
    public string Name { get; set; }

    public int? ParentId { get; set; } // Optional
    public TagDTO? Parent { get; set; } // Optional
    public ICollection<TagDTO>? Children { get; set; }

    public int ColorId { get; set; }
    public Color Color { get; set; }

    [Column(TypeName = "varchar(200)")]
    public string? Description { get; set; }


    [Column(TypeName = "varchar(50)")]
    public string IconName { get; set; }

    [Column(TypeName = "varchar(8)")]
    public string? ShortCode { get; set; }

    public List<string>? Aliases { get; set; }

    public bool IsSystemTag { get; set; }

    // Parameterless constructor required by EF
    public TagDTO() { }

    public TagDTO(string name, Color color, string? description, List<string>? aliases, string iconName, bool isSystemTag = false, string? shortCode = null)
    {
        Name = name;
        Color = color;
        ColorId = color.Id;
        Description = description;
        Aliases = aliases;
        IconName = iconName;
        IsSystemTag = isSystemTag;
        ShortCode = shortCode;
    }
}

public static class SystemTags
{
    private static readonly SystemTag[] DefaultTags =
    [
        new SystemTag("Priority", "Priority", "Orange", "PriorityHigh"),
        new SystemTag("High", "High Priority", "Orange", "numeric1CircleOutline", "Priority"),
        new SystemTag("Medium", "Medium Priority", "Orange", "numeric2CircleOutline", "Priority"),
        new SystemTag("Low", "Low Priority", "Orange", "numeric3CircleOutline", "Priority"),

        new SystemTag("Status", "Status", "Blue", "ListStatus"),
        new SystemTag("Not Started", "Not Started Status", "Blue", "Cancel", "Status"),
        new SystemTag("In Progress", "In Progress Status", "Blue", "FastForward", "Status"),
        new SystemTag("Waiting", "In Progress, but waiting Status", "Blue", "Hourglass", "Status"),
        new SystemTag("Completed", "Completed Status", "Blue", "Check", "Status")
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
            db.Add(new TagDTO(tag.Name, defaultColors.First(x => x.Name == tag.ColorName), tag.Description, null, tag.Icon, true));
        }
        db.SaveChanges();

        // Assign parent relationships
        foreach (SystemTag childSystemTag in DefaultTags.Where(st => st.ParentName is not null))
        {
            TagDTO childTagDto = db.Tags.First(t => t.Name == childSystemTag.Name);
            TagDTO parentTagDto = db.Tags.First(t => t.Name == childSystemTag.ParentName);
            childTagDto.Parent = parentTagDto;
        }
        db.SaveChanges();
    }

    private record SystemTag(string Name, string Description, string ColorName, string Icon, string? ParentName = null);
}