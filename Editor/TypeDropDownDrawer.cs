namespace TypeReferences.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Test.Editor.OdinAttributeDrawers;
    using UnityEngine;
    using TypeSelector = Odin.TypeSelector;

    public class TypeDropDownDrawer
    {
        private readonly Type _selectedType;
        private readonly TypeOptionsAttribute _attribute;
        private readonly Type _declaringType;

        public TypeDropDownDrawer(string typeName, TypeOptionsAttribute attribute, Type declaringType)
        {
            _selectedType = TypeCache.GetType(typeName);
            _attribute = attribute;
            _declaringType = declaringType;
        }

        public void Draw(Action<Type> onTypeSelected)
        {
            ShowSelector(new Rect(Event.current.mousePosition, Vector2.zero)).SelectionConfirmed +=
                (Action<IEnumerable<Type>>) (selectedValues => onTypeSelected(selectedValues.FirstOrDefault()));
        }

        private TypeSelector ShowSelector(Rect popupArea)
        {
            var dropdownItems = GetDropdownItems();
            var selector = CreateSelector(dropdownItems);
            ShowInPopup(ref selector, dropdownItems, popupArea);
            return selector;
        }

        private void ShowInPopup(ref TypeSelector selector, SortedList<string, Type> dropdownItems, Rect popupArea)
        {
            popupArea.RoundUpCoordinates();

            int dropdownWidth = CalculateOptimalWidth(dropdownItems, selector);
            int dropdownHeight = _attribute?.DropdownHeight ?? 0;

            selector.ShowInPopup(popupArea, new Vector2(dropdownWidth, dropdownHeight));
        }

        private static int CalculateOptimalWidth(SortedList<string, Type> dropdownItems, TypeSelector selector)
        {
            var itemTextValues = dropdownItems.Select(item => item.Key);
            var style = selector.SelectionTree.DefaultMenuStyle.DefaultLabelStyle;
            return PopupHelper.CalculatePopupWidth(itemTextValues, style, '/', false); // TODO: Make CalculatePopupWidth accept less variables
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

        private TypeSelector CreateSelector(SortedList<string, Type> genericSelectorItems)
        {
            var genericSelector = new TypeSelector(genericSelectorItems);
            genericSelector.SelectionTree.Config.DrawSearchToolbar = true;

            genericSelector.EnableSingleClickToSelect();

            genericSelector.SetSelection(_selectedType);

            if (_attribute != null && _attribute.ExpandAllMenuItems)
                genericSelector.SelectionTree.EnumerateTree(folder => folder.Toggled = true);

            return genericSelector;
        }
    }
}