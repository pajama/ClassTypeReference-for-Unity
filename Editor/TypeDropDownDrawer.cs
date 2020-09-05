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
        private readonly Type _selectedType;

        public TypeDropDownDrawer(string typeName, TypeOptionsAttribute attribute, Type declaringType)
        {
            _attribute = attribute;
            _declaringType = declaringType;
            _selectedType = TypeCache.GetType(typeName);
        }

        public void Draw(Action<Type> onTypeSelected)
        {
            var dropdownItems = GetDropdownItems();

            bool expandAllMenuItems = _attribute != null && _attribute.ExpandAllMenuItems;

            var selector = new TypeSelector(dropdownItems, _selectedType, expandAllMenuItems);

            int dropdownHeight = _attribute?.DropdownHeight ?? 0;
            selector.ShowInPopup(dropdownHeight);

            selector.SelectionConfirmed +=
                (Action<IEnumerable<Type>>) (selectedValues => onTypeSelected(selectedValues.FirstOrDefault()));
        }

        private SortedSet<TypeItem> GetDropdownItems()
        {
            var grouping = _attribute?.Grouping ?? TypeOptionsAttribute.DefaultGrouping;
            var types = GetFilteredTypes(grouping);
            return types;
        }

        private SortedSet<TypeItem> GetFilteredTypes(Grouping grouping)
        {
            var typeRelatedAssemblies = TypeCollector.GetAssembliesTypeHasAccessTo(_declaringType);

            if (_attribute?.IncludeAdditionalAssemblies != null)
                IncludeAdditionalAssemblies(typeRelatedAssemblies);

            var filteredTypes = TypeCollector.GetFilteredTypesFromAssemblies(
                typeRelatedAssemblies,
                _attribute);

            var sortedTypes = new SortedSet<TypeItem>(new TypeItemComparer());

            for (int i = 0; i < filteredTypes.Count; i++)
            {
                var type = filteredTypes[i];
                if (type.FullName != null)
                    sortedTypes.Add(new TypeItem(type, grouping));
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
}