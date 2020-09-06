﻿namespace TypeReferences.Editor
{
    using System;
    using System.Collections.Generic;
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
            bool expandAllMenuItems = _attribute.ExpandAllMenuItems;
            var selector = new TypeSelector(dropdownItems, _selectedType, expandAllMenuItems, onTypeSelected);

            int dropdownHeight = _attribute.DropdownHeight;
            selector.ShowInPopup(dropdownHeight);
        }

        private SortedSet<TypeItem> GetDropdownItems()
        {
            var types = GetFilteredTypes();

            foreach (var typeItem in GetIncludedTypes())
                types.Add(typeItem);

            return types;
        }

        private List<TypeItem> GetIncludedTypes()
        {
            var typesToInclude = _attribute.IncludeTypes;

            if (typesToInclude == null)
                return new List<TypeItem>();

            var typeItems = new List<TypeItem>(typesToInclude.Length);

            foreach (Type typeToInclude in _attribute.IncludeTypes)
            {
                if (typeToInclude != null && typeToInclude.FullName != null)
                    typeItems.Add(new TypeItem(typeToInclude, _attribute.Grouping));
            }

            return typeItems;
        }

        private SortedSet<TypeItem> GetFilteredTypes()
        {
            var typeRelatedAssemblies = TypeCollector.GetAssembliesTypeHasAccessTo(_declaringType);

            if (_attribute.IncludeAdditionalAssemblies != null)
                IncludeAdditionalAssemblies(typeRelatedAssemblies);

            var filteredTypes = TypeCollector.GetFilteredTypesFromAssemblies(
                typeRelatedAssemblies,
                _attribute);

            var sortedTypes = new SortedSet<TypeItem>(new TypeItemComparer());

            for (int i = 0; i < filteredTypes.Count; i++)
            {
                var type = filteredTypes[i];
                if (type.FullName != null)
                    sortedTypes.Add(new TypeItem(type, _attribute.Grouping));
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