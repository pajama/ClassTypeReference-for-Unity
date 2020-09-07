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

    public TypeSelector(SortedSet<TypeItem> collection, Type selectedType, bool expandAllMenuItems, Action<Type> onSelectionConfirmed)
    {
      _nameTypeList = collection;
      _selectionTree = new MenuTree(_nameTypeList);
      _selectionTree.SelectionChanged += (Action) (() => { onSelectionConfirmed(_selectionTree.SelectedItem.Type); });

      SetSelection(selectedType);

      if (expandAllMenuItems)
        _selectionTree.OpenAllFolders();
    }

    public void Draw(int dropdownHeight)
    {
      var popupArea = new Rect(Event.current.mousePosition, Vector2.zero);
      int dropdownWidth = CalculateOptimalWidth();
      var windowSize = new Vector2(dropdownWidth, dropdownHeight);

      EditorWindow prevSelectedWindow = EditorWindow.focusedWindow;
      int prevFocusId = GUIUtility.hotControl;
      int prevKeyboardFocus = GUIUtility.keyboardControl;
      var window = DropdownWindow.Create(this, popupArea, windowSize, prevFocusId, prevKeyboardFocus);
      SetupWindow(window, prevSelectedWindow);
    }

    private void SetSelection(Type selected)
    {
      if (selected == null)
        return;

      string nameOfItemToSelect = _nameTypeList.First(item => item.Type == selected).Name;
      _selectionTree.SetSelection(nameOfItemToSelect);
    }

    private int CalculateOptimalWidth()
    {
      var itemTextValues = _nameTypeList.Select(item => item.Name);
      var style = OdinMenuStyle.TreeViewStyle.DefaultLabelStyle;
      return PopupHelper.CalculatePopupWidth(itemTextValues, style, false); // TODO: Make CalculatePopupWidth accept less variables
    }

    private void SetupWindow(DropdownWindow window, EditorWindow prevSelectedWindow)
    {
      _selectionTree.SelectionChanged += (Action) (() =>
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
    }

    [OnInspectorGUI]
    [PropertyOrder(-1)]
    private void DrawSelectionTree()
    {
      Rect rect1 = EditorGUILayout.BeginVertical();
      EditorGUI.DrawRect(rect1, SirenixGUIStyles.DarkEditorBackground);
      GUILayout.Space(1f);
      SirenixEditorGUI.BeginHorizontalToolbar(MenuTree.SearchToolbarHeight);
      _selectionTree.DrawSearchToolbar(GUIStyle.none);
      EditorGUI.DrawRect(GUILayoutUtility.GetLastRect().AlignLeft(1f), SirenixGUIStyles.BorderColor);
      SirenixEditorGUI.EndHorizontalToolbar();

      if (_nameTypeList.Count == 0)
      {
        GUILayout.BeginVertical(SirenixGUIStyles.ContentPadding);
        SirenixEditorGUI.InfoMessageBox("There are no possible values to select.");
        GUILayout.EndVertical();
      }

      _selectionTree.DrawMenuTree(false);
      SirenixEditorGUI.DrawBorders(rect1, 1);
      EditorGUILayout.EndVertical();
    }
  }
}