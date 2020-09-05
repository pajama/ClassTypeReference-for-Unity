namespace TypeReferences.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Odin;
    using TrentTobler.Collections;
    using UnityEngine;
    using Debug = System.Diagnostics.Debug;

    public class TypeDropDownDrawer
    {
        private readonly TypeOptionsAttribute _attribute;
        private readonly Type _declaringType;
        private readonly TypeSelector _selector;
        private readonly Type _selectedType;

        public TypeDropDownDrawer(string typeName, TypeOptionsAttribute attribute, Type declaringType)
        {
            _attribute = attribute;
            _declaringType = declaringType;
            _selectedType = TypeCache.GetType(typeName);
        }

        public void Draw(Action<Type> onTypeSelected)
        {
            BTree<TypeItem> dropdownItems = null;
            Timer.LogTime("GetDropdownItems", () =>
            {
                dropdownItems = GetDropdownItems();
            });

            bool expandAllMenuItems = _attribute != null && _attribute.ExpandAllMenuItems;

            TypeSelector selector = null;
            Timer.LogTime("TypeSelector constructor", () =>
            {
                selector = new TypeSelector(dropdownItems, _selectedType, expandAllMenuItems);
            });

            int dropdownHeight = _attribute?.DropdownHeight ?? 0;

            Timer.LogTime("ShowInPopup", () =>
            {
                selector.ShowInPopup(dropdownHeight);
            });

            selector.SelectionConfirmed +=
                (Action<IEnumerable<Type>>) (selectedValues => onTypeSelected(selectedValues.FirstOrDefault()));
        }

        private BTree<TypeItem> GetDropdownItems()
        {
            var grouping = _attribute?.Grouping ?? TypeOptionsAttribute.DefaultGrouping;

            var types = GetFilteredTypes();

            var typesWithFormattedNames = new BTree<TypeItem>(new TypeItemComparer(), types.Count);

            for (int i = 0; i < types.Count; i++)
            {
                var typeItem = types.At(i);
                string menuLabel = TypeNameFormatter.Format(typeItem.Type, grouping);

                if (!string.IsNullOrEmpty(menuLabel))
                {
                    typeItem.Name = menuLabel;
                    typesWithFormattedNames.Add(typeItem);
                }
            }

            return typesWithFormattedNames;
        }

        private BTree<TypeItem> GetFilteredTypes()
        {
            var typeRelatedAssemblies = TypeCollector.GetAssembliesTypeHasAccessTo(_declaringType);

            if (_attribute?.IncludeAdditionalAssemblies != null)
                IncludeAdditionalAssemblies(typeRelatedAssemblies);

            var filteredTypes = TypeCollector.GetFilteredTypesFromAssemblies(
                typeRelatedAssemblies,
                _attribute);

            var sortedTypes = new BTree<TypeItem>(new TypeItemComparer(), filteredTypes.Count);
            // filteredTypes.ToDictionary(type => type.FullName)
            for (int i = 0; i < filteredTypes.Count; i++)
            {
                var type = filteredTypes[i];
                if (type.FullName != null)
                    sortedTypes.Add(new TypeItem(type.FullName, type));
            }

            return sortedTypes;
        }

        private void IncludeAdditionalAssemblies(ICollection<Assembly> typeRelatedAssemblies)
        {
            foreach (string assemblyName in _attribute.IncludeAdditionalAssemblies)
            {
                var additionalAssembly = TypeCollector.TryLoadAssembly(assemblyName);
                if (additionalAssembly == null)
                    continue;

                if ( ! typeRelatedAssemblies.Contains(additionalAssembly))
                    typeRelatedAssemblies.Add(additionalAssembly);
            }
        }
    }

    public class TypeItem
    {
        public string Name;
        public readonly Type Type;

        public TypeItem(string name, Type type)
        {
            Name = name;
            Type = type;
        }
    }

    public class TypeItemComparer : IComparer<TypeItem>
    {
        public int Compare(TypeItem x, TypeItem y)
        {
            Debug.Assert(x != null, nameof(x) + " != null");
            Debug.Assert(y != null, nameof(y) + " != null");
            return string.Compare(x.Name, y.Name, StringComparison.Ordinal);
        }
    }
}