namespace TypeReferences.Editor
{
    using System;
    using System.Collections.Generic;
    using Sirenix.OdinInspector;

    public class ValueDropdownEqualityComparer : IEqualityComparer<object>
    {
        private readonly bool _isTypeLookup;

        public ValueDropdownEqualityComparer(bool isTypeLookup)
        {
            _isTypeLookup = isTypeLookup;
        }

        public new bool Equals(object x, object y)
        {
            if (x is ValueDropdownItem item)
                x = item.Value;

            if (y is ValueDropdownItem dropdownItem)
                y = dropdownItem.Value;

            if (EqualityComparer<object>.Default.Equals(x, y))
                return true;

            if (x == null != (y == null) || !_isTypeLookup)
                return false;

            if (!(x is Type type))
                type = x?.GetType();

            var type1 = type;
            if (!(y is Type type2))
                type2 = y?.GetType();

            var type3 = type2;
            return type1 == type3;
        }

        public int GetHashCode(object obj)
        {
            if (obj is ValueDropdownItem item)
                obj = item.Value;

            if (obj == null)
                return -1;

            if (!_isTypeLookup)
                return obj.GetHashCode();

            if (!(obj is Type type))
                type = obj.GetType();

            return type.GetHashCode();
        }
    }
}