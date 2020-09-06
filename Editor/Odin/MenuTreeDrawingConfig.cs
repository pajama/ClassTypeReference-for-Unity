namespace TypeReferences.Editor.Odin
{
  using System;
  using Sirenix.OdinInspector.Editor;
  using UnityEngine;

  [Serializable]
  public class MenuTreeDrawingConfig
  {
    [HideInInspector] public bool ConfirmSelectionOnDoubleClick = true;
    public bool AutoScrollOnSelectionChanged = true;
    public bool DrawScrollView = true;
    public bool AutoHandleKeyboardNavigation = true;
    public bool DrawSearchToolbar = true;
    public bool UseCachedExpandedStates = true;
    public bool AutoFocusSearchBar = true;
    public string SearchTerm = string.Empty;
    public int SearchToolbarHeight = 24;
    public Vector2 ScrollPos;
    public bool EXPERIMENTALINTERNALDrawFlatTreeFastNoLayout;

    private Func<MenuItem, bool> _searchFunction;
    [SerializeField] private OdinMenuStyle menuItemStyle;

    public OdinMenuStyle DefaultMenuStyle
    {
      get => menuItemStyle ?? (menuItemStyle = new OdinMenuStyle());
      set => menuItemStyle = value;
    }

    public Func<MenuItem, bool> SearchFunction
    {
      get => _searchFunction;
      set => _searchFunction = value;
    }
  }
}