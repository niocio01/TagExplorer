using System.ComponentModel.DataAnnotations;

namespace TagExplorer.Data
{
    public class IconFavourite
    {
        [Key]
        public string IconName { get; set; }

        public IconFavourite(string iconName)
        {
            IconName = iconName;
        }
    }
}
