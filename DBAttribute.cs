using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer;

public class DbAttribute(
    int index,
    string name,
    string setupString,
    bool writeProperty = true)
{
    public int Index { get; private set; } = index;
    public bool WriteProperty { get; private set; } = writeProperty;
    public string Name { get; private set; } = name;
    public string SetupString { get; private set; } = setupString;
}