using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagExplorer.Data;

namespace TagExplorer.Models;

internal class ManualTagAssignment
{
    public int? Id { get; set; }
    public TagDTO? TagDto { get; private set; }
    public Folder? Folder { get; private set; }

    public ManualTagAssignment(int id)
    {
        Id = id;
    }

    public ManualTagAssignment(TagDTO tagDto, Folder folder)
    {
        TagDto = tagDto;
        Folder = folder;
    }

    public ManualTagAssignment(int id, TagDTO tagDto, Folder folder)
    {
        Id = id;
        TagDto = tagDto;
        Folder = folder;
    }
}