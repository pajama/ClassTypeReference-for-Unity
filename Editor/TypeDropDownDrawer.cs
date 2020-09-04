namespace TypeReferences.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Odin;

    public class TypeDropDownDrawer
    {
        private readonly TypeOptionsAttribute _attribute;
        private readonly Type _declaringType;
        private readonly TypeSelector _selector;

        public TypeDropDownDrawer(string typeName, TypeOptionsAttribute attribute, Type declaringType)
        {
            _attribute = attribute;
            _declaringType = declaringType;

            Type selectedType = TypeCache.GetType(typeName);
            var dropdownItems = GetDropdownItems();
            bool expandAllMenuItems = _attribute != null && _attribute.ExpandAllMenuItems;
            _selector = new TypeSelector(dropdownItems, selectedType, expandAllMenuItems);
        }

        public void Draw(Action<Type> onTypeSelected)
        {
            int dropdownHeight = _attribute?.DropdownHeight ?? 0;
            _selector.ShowInPopup(dropdownHeight);

            _selector.SelectionConfirmed +=
                (Action<IEnumerable<Type>>) (selectedValues => onTypeSelected(selectedValues.FirstOrDefault()));
        }

        private SortedList<string, Type> GetDropdownItems()
        {
            var grouping = _attribute?.Grouping ?? TypeOptionsAttribute.DefaultGrouping;

            var types = GetFilteredTypes();

            var typesWithFormattedNames = new SortedList<string, Type>();

            foreach (var nameTypePair in types)
            {
                string menuLabel = TypeNameFormatter.Format(nameTypePair.Value, grouping);

                if ( ! string.IsNullOrEmpty(menuLabel))
                    typesWithFormattedNames.Add(menuLabel, nameTypePair.Value);
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
}