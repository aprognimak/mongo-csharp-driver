using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ownable.DataObjects
{
    public class EntityEnumDto 
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
        public string Name { get;set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class EnumDto 
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }

        public object NativeValue { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
