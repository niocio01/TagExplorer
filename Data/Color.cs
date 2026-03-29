using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TagExplorer.Data;

public class Color
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "varchar(50)")]
    public string? Name { get; set; }

    [Required]
    [Column(TypeName = "varchar(7)")]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Hex code must be 7 characters long")]
    public string HexCode { get; set; }

    [NotMapped]
    public string HexCodeNoHash => HexCode[1..];

    public bool IsSystemColor { get; set; }
    public Color(string name, string hexCode, bool isSystemColor = false)
    {
        Name = name;
        HexCode = hexCode;
        IsSystemColor = isSystemColor;
    }
}

public static class SystemColors
{
    private static readonly Dictionary<string, string> DefaultColors = new Dictionary<string, string>()
    {
        {"Red", "#F44336"},
        {"Pink", "#E91E63"},
        {"Purple", "#9C27B0"},
        {"DeepPurple", "#673AB7"},
        {"Indigo", "#3F51B5"},
        {"Blue", "#2196F3"},
        {"LightBlue", "#03A9F4"},
        {"Cyan", "#00BCD4"},
        {"Teal", "#009688"},
        {"Green", "#4CAF50"},
        {"LightGreen", "#8BC34A"},
        {"Lime", "#CDDC39"},
        {"Yellow", "#FFEB3B"},
        {"Amber", "#FFC107"},
        {"Orange", "#FF9800"},
        {"DeepOrange", "#FF5722"},
        {"Brown", "#795548"},
        {"Grey", "#9E9E9E"},
        {"BlueGrey", "#607D8B"}
    };
    public static void UpdateDefaultColors(AppDbContext db)
    {
        int noOfSystemColors = db.Colors.Count(e => e.IsSystemColor == true);
        if (noOfSystemColors == DefaultColors.Count)
            return;

        var systemColors = db.Colors.Where(e => e.IsSystemColor == true);
        db.Colors.RemoveRange(systemColors);
        db.SaveChanges();

        foreach (KeyValuePair<string, string> color in DefaultColors)
        {
            db.Colors.Add(new Color(color.Key, color.Value, true));
        }
        db.SaveChanges();
    }
}