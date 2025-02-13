using System.ComponentModel.DataAnnotations;

namespace TagExplorer.Data
{
    public class HierarchicalRelation
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }
        public TagDTO Parent { get; set; }

        public int ChildId { get; set; }
        public TagDTO Child { get; set; }
    }
}
