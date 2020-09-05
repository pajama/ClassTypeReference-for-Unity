namespace TypeReferences.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Odin;
    using UnityEngine;

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
            List<TypeItem> dropdownItems = null;
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

        private List<TypeItem> GetDropdownItems()
        {
            var grouping = _attribute?.Grouping ?? TypeOptionsAttribute.DefaultGrouping;

            var types = GetFilteredTypes();

            var typesWithFormattedNames = new List<TypeItem>(types.Capacity);

            foreach (var nameTypePair in types)
            {
                string menuLabel = TypeNameFormatter.Format(nameTypePair.Value, grouping);

                if ( ! string.IsNullOrEmpty(menuLabel))
                    typesWithFormattedNames.Add(new TypeItem(menuLabel, nameTypePair.Value));
            }

            return typesWithFormattedNames;
        }

        private SortedList<string, Type> GetFilteredTypes()
        {
            var typeRelatedAssemblies = TypeCollector.GetAssembliesTypeHasAccessTo(_declaringType);

            if (_attribute?.IncludeAdditionalAssemblies != null)
                IncludeAdditionalAssemblies(typeRelatedAssemblies);

            var filteredTypes = TypeCollector.GetFilteredTypesFromAssemblies(
                typeRelatedAssemblies,
                _attribute);

            var sortedTypes = new SortedList<string, Type>(filteredTypes.ToDictionary(type => type.FullName));

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
        public readonly string Name;
        public readonly Type Type;

        public TypeItem(string name, Type type)
        {
            Name = name;
            Type = type;
        }
    }
}