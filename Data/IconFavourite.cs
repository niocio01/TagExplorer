using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
