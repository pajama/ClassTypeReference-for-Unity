namespace TypeReferences.Editor.Odin
{
    /// <summary>
    /// Constants which describe the type of change that was made to the OdinMenuTrees's Selection
    /// </summary>
    /// <seealso cref="T:Sirenix.OdinInspector.Editor.OdinMenuTreeSelection" />
    public enum SelectionChangedType
    {
        /// <summary>A menu item was removed.</summary>
        ItemRemoved,

        /// <summary>A menu item was selected.</summary>
        ItemAdded,

        /// <summary>The selection was cleared.</summary>
        SelectionCleared,
    }
}