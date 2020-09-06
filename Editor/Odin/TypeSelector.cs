namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Sirenix.OdinInspector;
  using Sirenix.OdinInspector.Editor;
  using Sirenix.Utilities;
  using Sirenix.Utilities.Editor;
  using Test.Editor.OdinAttributeDrawers;
  using UnityEditor;
  using UnityEngine;

  public class TypeSelector
  {
    private readonly MenuTree _selectionTree;
    private readonly SortedSet<TypeItem> _nameTypeList;

    public TypeSelector(SortedSet<TypeItem> collection, Type selectedType, bool expandAllMenuItems)
    {
      _nameTypeList = collection;

      var config = new MenuTreeDrawingConfig
      {
        SearchToolbarHeight = 22,
        AutoScrollOnSelectionChanged = true,
        DefaultMenuStyle = new OdinMenuStyle { Height = 22 },
        DrawSearchToolbar = true
      };

      _selectionTree = new MenuTree { Config = config };
      MenuTree.ActiveMenuTree = _selectionTree;
      BuildSelectionTree(_selectionTree);
      _selectionTree.Selection.SelectionConfirmed += (Action<MenuTreeSelection>) (x =>
      {
        SelectionConfirmed?.Invoke(GetCurrentSelection());
      });

      EnableSingleClickToSelect();
      SetSelection(selectedType);

      if (expandAllMenuItems)
        _selectionTree.EnumerateTree(folder => folder.Toggled = true);
    }

    /// <summary>
    /// Occurs when the menuTrees selection is confirmed.
    /// </summary>
    public event Action<IEnumerable<Type>> SelectionConfirmed;

    private void SetSelection(Type selected)
    {
      if (selected == null)
          return;

      foreach (var item in _selectionTree.EnumerateTree())
      {
        if ((Type) item.Value == selected)
          item.Select(true);
      }
    }

    /// <summary>Enables the single click to select.</summary>
    private void EnableSingleClickToSelect()
    {
      _selectionTree.EnumerateTree(x =>
      {
        x.OnDrawItem -= EnableSingleClickToSelect;
        x.OnDrawItem -= EnableSingleClickToSelect;
        x.OnDrawItem += EnableSingleClickToSelect;
      });
    }

    /// <summary>
    /// Opens up the selector instance in a popup at the specified rect position.
    /// </summary>
    public void ShowInPopup(int dropdownHeight)
    {
      var popupArea = new Rect(Event.current.mousePosition, Vector2.zero);
      int dropdownWidth = CalculateOptimalWidth();
      var windowSize = new Vector2(dropdownWidth, dropdownHeight);

      EditorWindow focusedWindow = EditorWindow.focusedWindow;
      OdinEditorWindow window = OdinEditorWindow.InspectObjectInDropDown(this, popupArea, windowSize);
      SetupWindow(window, focusedWindow);
    }

    private static void EnableSingleClickToSelect(MenuItem obj)
    {
      EventType type = Event.current.type;
      if (type == EventType.Layout || !obj.Rect.Contains(Event.current.mousePosition))
        return;
      GUIHelper.RequestRepaint();
      if (type != EventType.MouseUp || obj.ChildMenuItems.Count != 0)
        return;
      obj.MenuTree.Selection.ConfirmSelection();
      Event.current.Use();
    }

    private int CalculateOptimalWidth()
    {
      var itemTextValues = _nameTypeList.Select(item => item.Name);
      var style = _selectionTree.DefaultMenuStyle.DefaultLabelStyle;
      return PopupHelper.CalculatePopupWidth(itemTextValues, style, '/', false); // TODO: Make CalculatePopupWidth accept less variables
    }

    /// <summary>
    /// Draws the selection tree. This gets drawn using the OnInspectorGUI attribute.
    /// </summary>
    [OnInspectorGUI]
    [PropertyOrder(-1)]
    private void DrawSelectionTree()
    {
      Rect rect1 = EditorGUILayout.BeginVertical();
      EditorGUI.DrawRect(rect1, SirenixGUIStyles.DarkEditorBackground);
      GUILayout.Space(1f);
      bool drawSearchToolbar1 = _selectionTree.Config.DrawSearchToolbar;
      if (drawSearchToolbar1)
      {
        SirenixEditorGUI.BeginHorizontalToolbar(_selectionTree.Config.SearchToolbarHeight);
        _selectionTree.DrawSearchToolbar(GUIStyle.none);
        EditorGUI.DrawRect(GUILayoutUtility.GetLastRect().AlignLeft(1f), SirenixGUIStyles.BorderColor);
        SirenixEditorGUI.EndHorizontalToolbar();
      }

      bool drawSearchToolbar2 = _selectionTree.Config.DrawSearchToolbar;
      _selectionTree.Config.DrawSearchToolbar = false;
      if (_selectionTree.MenuItems.Count == 0)
      {
        GUILayout.BeginVertical(SirenixGUIStyles.ContentPadding);
        SirenixEditorGUI.InfoMessageBox("There are no possible values to select.");
        GUILayout.EndVertical();
      }

      _selectionTree.DrawMenuTree();
      _selectionTree.Config.DrawSearchToolbar = drawSearchToolbar2;
      SirenixEditorGUI.DrawBorders(rect1, 1);
      EditorGUILayout.EndVertical();
    }

    private void SetupWindow(OdinEditorWindow window, EditorWindow prevSelectedWindow)
    {
      int prevFocusId = GUIUtility.hotControl;
      int prevKeyboardFocus = GUIUtility.keyboardControl;
      window.WindowPadding = default;
      _selectionTree.Selection.SelectionConfirmed += (Action<MenuTreeSelection>) (x =>
      {
        bool ctrl = Event.current != null && Event.current.modifiers != EventModifiers.Control;
        UnityEditorEventUtility.DelayAction(() =>
        {
          if (!ctrl)
            return;
          window.Close();
          if (!(bool) prevSelectedWindow)
            return;
          prevSelectedWindow.Focus();
        });
      });
      window.OnBeginGUI += (Action) (() =>
      {
        if (Event.current.type != EventType.KeyDown || Event.current.keyCode != KeyCode.Escape)
          return;
        UnityEditorEventUtility.DelayAction(window.Close);
        if ((bool) prevSelectedWindow)
          prevSelectedWindow.Focus();
        Event.current.Use();
      });
      window.OnClose += (Action) (() =>
      {
        GUIUtility.hotControl = prevFocusId;
        GUIUtility.keyboardControl = prevKeyboardFocus;
      });
    }

    private IEnumerable<Type> GetCurrentSelection()
    {
      return _selectionTree.Selection.Select(x => x.Value).OfType<Type>();
    }

    /// <summary>Builds the selection tree.</summary>
    private void BuildSelectionTree(MenuTree tree)
    {
      tree.DefaultMenuStyle = OdinMenuStyle.TreeViewStyle;
      if (_nameTypeList == null)
        return;

      foreach (TypeItem item in _nameTypeList)
      {
        tree.AddObjectAtPath(item.Name, item.Type);
      }
    }
  }
}