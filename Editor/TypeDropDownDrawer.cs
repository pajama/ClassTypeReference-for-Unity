namespace TypeReferences.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Sirenix.OdinInspector.Editor;
    using Test.Editor.OdinAttributeDrawers;
    using UnityEngine;

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

        private OdinSelector<Type> ShowSelector(Rect popupArea)
        {
            var dropdownItems = GetDropdownItems();
            var selector = CreateSelector(dropdownItems);
            ShowInPopup(ref selector, dropdownItems, popupArea);
            return selector;
        }

        private void ShowInPopup(ref GenericSelector<Type> selector, List<GenericSelectorItem<Type>> dropdownItems, Rect popupArea)
        {
            popupArea.RoundUpCoordinates();

            int dropdownWidth = CalculateOptimalWidth(dropdownItems, selector);

            selector.ShowInPopup(popupArea, new Vector2(dropdownWidth, _attribute.DropdownHeight));
        }

        private static int CalculateOptimalWidth(List<GenericSelectorItem<Type>> dropdownItems, GenericSelector<Type> selector)
        {
            var itemTextValues = dropdownItems.Select(item => item.Name);
            var style = selector.SelectionTree.DefaultMenuStyle.DefaultLabelStyle;
            return PopupHelper.CalculatePopupWidth(itemTextValues, style, '/', selector.FlattenedTree);
        }

        private List<GenericSelectorItem<Type>> GetDropdownItems()
        {
            var grouping = _attribute?.Grouping ?? TypeOptionsAttribute.DefaultGrouping;

            var types = GetFilteredTypes();

            var dropdownItems = new List<GenericSelectorItem<Type>>(types.Count);

            foreach (var nameTypePair in types)
            {
                string menuLabel = TypeNameFormatter.Format(nameTypePair.Value, grouping);
                if (! string.IsNullOrEmpty(menuLabel))
                    dropdownItems.Add(new GenericSelectorItem<Type>(menuLabel, nameTypePair.Value));
            }

            return dropdownItems;
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

        private GenericSelector<Type> CreateSelector(List<GenericSelectorItem<Type>> genericSelectorItems)
        {
            var genericSelector = new GenericSelector<Type>(null, false, genericSelectorItems);
            genericSelector.SelectionTree.Config.DrawSearchToolbar = true;

            genericSelector.EnableSingleClickToSelect();

            genericSelector.SetSelection(_selectedType);

            if (_attribute.ExpandAllMenuItems)
                genericSelector.SelectionTree.EnumerateTree(folder => folder.Toggled = true);

            return genericSelector;
        }
    }
}