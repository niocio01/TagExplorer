using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TagExplorer;

public class DbAttribute(int index, string name, Type type, string dbType, string additionalProperties = "")
{
    public int Index { get; private set; } = index;
    public string Name { get; private set; } = name;
    public Type Type { get; private set; } = type;
    public string DbType { get; private set; } = dbType;

    public string DbAdditionalProperties { get; private set; } = additionalProperties;

    public void SetValue(string dbValue, ref object value)
    {
        value = Convert.ChangeType(dbValue, typeof(Type));
    }
}