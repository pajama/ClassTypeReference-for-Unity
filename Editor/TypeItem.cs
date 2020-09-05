namespace TypeReferences.Editor
{
    using System;
    using System.Collections.Generic;

    public readonly struct TypeItem
    {
        public readonly string Name;
        public readonly Type Type;

        public TypeItem(Type type, Grouping grouping)
        {
            Name = TypeNameFormatter.Format(type, grouping);
            Type = type;
        }
    }

    public class TypeItemComparer : IComparer<TypeItem>
    {
        public int Compare(TypeItem x, TypeItem y)
        {
            return string.Compare(x.Name, y.Name, StringComparison.Ordinal);
        }
    }
}