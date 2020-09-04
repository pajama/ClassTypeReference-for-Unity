namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Sirenix.OdinInspector;
  using Sirenix.OdinInspector.Editor;
  using Sirenix.Utilities;
  using Sirenix.Utilities.Editor;
  using UnityEditor;
  using UnityEngine;

  public class TypeSelector
  {
    private static EditorWindow _selectorFieldWindow;
    private static IEnumerable<Type> _selectedValues;
    private static bool _selectionWasConfirmed;
    private static bool _selectionWasChanged;
    private static GUIStyle _titleStyle;
    private static bool _wasKeyboard;
    private static int _prevKeyboardId;
    private static GUIContent _tmpValueLabel;

    [SerializeField, HideInInspector]
    private readonly OdinMenuTreeDrawingConfig _config = new OdinMenuTreeDrawingConfig
    {
      SearchToolbarHeight = 22,
      AutoScrollOnSelectionChanged = true,
      DefaultMenuStyle = new OdinMenuStyle { Height = 22 }
    };

    private readonly SortedList<string, Type> _genericSelectorCollection;

    private Func<Type, string> _getMenuItemName;
    private bool _requestCheckboxUpdate;
    private OdinMenuTree _selectionTree;

    public TypeSelector(
      SortedList<string, Type> collection)
    {
      _genericSelectorCollection = collection;
    }

    /// <summary>
    /// Occurs when the menuTrees selection is confirmed.
    /// </summary>
    public event Action<IEnumerable<Type>> SelectionConfirmed;

    /// <summary>Gets the selection menu tree.</summary>
    public OdinMenuTree SelectionTree
    {
      get
      {
        if (_selectionTree != null)
          return _selectionTree;

        _selectionTree = new OdinMenuTree(true) { Config = _config };
        OdinMenuTree.ActiveMenuTree = _selectionTree;
        BuildSelectionTree(_selectionTree);
        _selectionTree.Selection.SelectionConfirmed += (Action<OdinMenuTreeSelection>) (x =>
        {
          SelectionConfirmed?.Invoke(GetCurrentSelection());
        });
        return _selectionTree;
      }
    }

    public void SetSelection(Type selected)
    {
      if (selected == null)
          return;
      SelectionTree.EnumerateTree().Where(x => x.Value is Type).Where(x => EqualityComparer<Type>.Default.Equals((Type) x.Value, selected)).ToList().ForEach(x => x.Select(true));
    }

    /// <summary>Enables the single click to select.</summary>
    public void EnableSingleClickToSelect()
    {
      SelectionTree.EnumerateTree(x =>
      {
        x.OnDrawItem -= EnableSingleClickToSelect;
        x.OnDrawItem -= EnableSingleClickToSelect;
        x.OnDrawItem += EnableSingleClickToSelect;
      });
    }

    /// <summary>
    /// Opens up the selector instance in a popup at the specified rect position.
    /// </summary>
    public void ShowInPopup(Rect btnRect, Vector2 windowSize)
    {
      EditorWindow focusedWindow = EditorWindow.focusedWindow;
      OdinEditorWindow window = OdinEditorWindow.InspectObjectInDropDown(this, btnRect, windowSize);
      SetupWindow(window, focusedWindow);
    }

    private static void EnableSingleClickToSelect(OdinMenuItem obj)
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

    /// <summary>
    /// Draws the selection tree. This gets drawn using the OnInspectorGUI attribute.
    /// </summary>
    [OnInspectorGUI]
    [PropertyOrder(-1)]
    private void DrawSelectionTree()
    {
      if (_requestCheckboxUpdate && Event.current.type == EventType.Repaint)
        _requestCheckboxUpdate = false;

      Rect rect1 = EditorGUILayout.BeginVertical();
      EditorGUI.DrawRect(rect1, SirenixGUIStyles.DarkEditorBackground);
      GUILayout.Space(1f);
      bool drawSearchToolbar1 = SelectionTree.Config.DrawSearchToolbar;
      if (drawSearchToolbar1)
      {
        SirenixEditorGUI.BeginHorizontalToolbar(SelectionTree.Config.SearchToolbarHeight);
        SelectionTree.DrawSearchToolbar(GUIStyle.none);
        EditorGUI.DrawRect(GUILayoutUtility.GetLastRect().AlignLeft(1f), SirenixGUIStyles.BorderColor);
        SirenixEditorGUI.EndHorizontalToolbar();
      }

      bool drawSearchToolbar2 = SelectionTree.Config.DrawSearchToolbar;
      SelectionTree.Config.DrawSearchToolbar = false;
      if (SelectionTree.MenuItems.Count == 0)
      {
        GUILayout.BeginVertical(SirenixGUIStyles.ContentPadding);
        SirenixEditorGUI.InfoMessageBox("There are no possible values to select.");
        GUILayout.EndVertical();
      }

      SelectionTree.DrawMenuTree();
      SelectionTree.Config.DrawSearchToolbar = drawSearchToolbar2;
      SirenixEditorGUI.DrawBorders(rect1, 1);
      EditorGUILayout.EndVertical();
    }

    private void SetupWindow(OdinEditorWindow window, EditorWindow prevSelectedWindow)
    {
      int prevFocusId = GUIUtility.hotControl;
      int prevKeyboardFocus = GUIUtility.keyboardControl;
      window.WindowPadding = default;
      SelectionTree.Selection.SelectionConfirmed += (Action<OdinMenuTreeSelection>) (x =>
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
      return SelectionTree.Selection.Select(x => x.Value).OfType<Type>();
    }

    /// <summary>Builds the selection tree.</summary>
    private void BuildSelectionTree(OdinMenuTree tree)
    {
      tree.Selection.SupportsMultiSelect = false;
      tree.DefaultMenuStyle = OdinMenuStyle.TreeViewStyle;
      _getMenuItemName = _getMenuItemName ?? (x => (object) x != null ? x.ToString() : string.Empty);
      if (_genericSelectorCollection == null)
        return;

      foreach (var item in _genericSelectorCollection)
        tree.AddObjectAtPath(item.Key, item.Value);
    }
  }
}