using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer.Models;

public class BaseFolder(String path, String name) : ITableObject
{
    public int? Id { get; set; }

    public String Name { get; private set; } = name;

    public String Path { get; private set; } = path;

    public bool Selected { get; set; } = false;
}