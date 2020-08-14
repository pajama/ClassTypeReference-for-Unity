namespace TypeReferences.Odin
{
    public class OdinClassTypeConstraintAttribute : ClassTypeConstraintAttribute
    {
        /// <summary>
        /// The number of items before enabling search. Default is 10.
        /// </summary>
        public int NumberOfItemsBeforeEnablingSearch = 10;

        /// <summary>
        /// True by default. If the ValueDropdown attribute is applied to a list, then disabling this,
        /// will render all child elements normally without using the ValueDropdown. The ValueDropdown will
        /// still show up when you click the add button on the list drawer, unless <see cref="F:Sirenix.OdinInspector.ValueDropdownAttribute.DisableListAddButtonBehaviour" /> is true.
        /// </summary>
        public bool DrawDropdownForListElements = true;

        /// <summary>False by default.</summary>
        public bool DisableListAddButtonBehaviour;

        /// <summary>
        /// If the ValueDropdown attribute is applied to a list, and <see cref="F:Sirenix.OdinInspector.ValueDropdownAttribute.IsUniqueList" /> is set to true, then enabling this,
        /// will exclude existing values, instead of rendering a checkbox indicating whether the item is already included or not.
        /// </summary>
        public bool ExcludeExistingValuesInList;

        /// <summary>
        /// If the dropdown renders a tree-view, then setting this to true will ensure everything is expanded by default.
        /// </summary>
        public bool ExpandAllMenuItems;

        /// <summary>
        /// Gets or sets the width of the dropdown. Default is zero.
        /// </summary>
        public int DropdownWidth;

        /// <summary>
        /// Gets or sets the height of the dropdown. Default is zero.
        /// </summary>
        public int DropdownHeight;
    }
}