namespace TypeReferences.Editor.Odin
{
  using System;
  using Sirenix.OdinInspector.Editor;
  using UnityEngine;

  [Serializable]
  public class MenuTreeDrawingConfig
  {
    /// <summary>
    /// The automatic scroll on selection changed. True by default.
    /// </summary>
    public bool AutoScrollOnSelectionChanged = true;

    /// <summary>
    /// Whether to draw the tree in a scrollable view. True by default.
    /// </summary>
    public bool DrawScrollView = true;

    /// <summary>
    /// Whether to handle keyboard navigation after it's done drawing. True by default.
    /// </summary>
    public bool AutoHandleKeyboardNavigation = true;

    /// <summary>
    /// Whether to draw a searchbar above the menu tree. True by default.
    /// </summary>
    public bool DrawSearchToolbar = true;

    /// <summary>
    /// Whether to the menu items expanded state should be cached. True by default.
    /// </summary>
    public bool UseCachedExpandedStates = true;

    /// <summary>
    /// Whether to automatically set focus on the search bar when the tree is drawn for the first time. True by default.
    /// </summary>
    public bool AutoFocusSearchBar = true;

    /// <summary>The search term.</summary>
    public string SearchTerm = "";

    /// <summary>The height of the search toolbar.</summary>
    public int SearchToolbarHeight = 24;

    /// <summary>
    /// By default, the MenuTree.Selection is confirmed when menu items are double clicked,
    /// Set this to false if you don't want that behaviour.
    /// </summary>
    [HideInInspector]
    public bool ConfirmSelectionOnDoubleClick = true;
    private Func<MenuItem, bool> searchFunction;
    [SerializeField]
    private OdinMenuStyle menuItemStyle;

    /// <summary>The scroll-view position.</summary>
    public Vector2 ScrollPos;
    public bool EXPERIMENTAL_INTERNAL_DrawFlatTreeFastNoLayout;

    /// <summary>Gets or sets the default menu item style.</summary>
    public OdinMenuStyle DefaultMenuStyle
    {
      get => menuItemStyle ?? (menuItemStyle = new OdinMenuStyle());
      set => menuItemStyle = value;
    }

    /// <summary>Gets or sets the search function. Null by default.</summary>
    public Func<MenuItem, bool> SearchFunction
    {
      get => searchFunction;
      set => searchFunction = value;
    }
  }
}