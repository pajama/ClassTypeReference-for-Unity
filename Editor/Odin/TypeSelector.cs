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
    private readonly HashSet<Type> _selection = new HashSet<Type>();
    private int _checkboxUpdateId;
    private readonly IEnumerable<GenericSelectorItem<Type>> _genericSelectorCollection;
    private Func<Type, string> getMenuItemName;
    private bool requestCheckboxUpdate;
    private static EditorWindow selectorFieldWindow;
    private static IEnumerable<Type> selectedValues;
    private static bool selectionWasConfirmed;
    private static bool selectionWasChanged;
    private static int confirmedPopupControlId = -1;
    private static int focusedControlId = -1;
    private static GUIStyle titleStyle;

    /// <summary>
    /// If true, a confirm selection button will be drawn in the title-bar.
    /// </summary>
    [HideInInspector]
    public bool DrawConfirmSelectionButton;
    private static bool wasKeyboard;
    private static int prevKeybaordId;
    private static GUIContent tmpValueLabel;

    protected void EnableSingleClickToSelect(OdinMenuItem obj)
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
    /// Occurs when the menuTrees selection is confirmed.
    /// </summary>
    public event Action<IEnumerable<Type>> SelectionConfirmed;

    [SerializeField]
    [HideInInspector]
    private OdinMenuTreeDrawingConfig config = new OdinMenuTreeDrawingConfig
    {
      SearchToolbarHeight = 22,
      AutoScrollOnSelectionChanged = true,
      DefaultMenuStyle = new OdinMenuStyle { Height = 22 }
    };

    private OdinMenuTree selectionTree;

    /// <summary>
    /// Gets or sets a value indicating whether [flattened tree].
    /// </summary>
    public bool FlattenedTree { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether [checkbox toggle].
    /// </summary>
    public bool CheckboxToggle { get; set; }

    public TypeSelector(
      IEnumerable<GenericSelectorItem<Type>> collection)
    {
      _genericSelectorCollection = collection;
    }

    /// <summary>Gets the selection menu tree.</summary>
    public OdinMenuTree SelectionTree
    {
      get
      {
        if (selectionTree == null)
        {
          selectionTree = new OdinMenuTree(true);
          selectionTree.Config = config;
          OdinMenuTree.ActiveMenuTree = selectionTree;
          BuildSelectionTree(selectionTree);
          selectionTree.Selection.SelectionConfirmed += (Action<OdinMenuTreeSelection>) (x =>
          {
            if (SelectionConfirmed == null)
              return;
            IEnumerable<Type> currentSelection = GetCurrentSelection();
            SelectionConfirmed(currentSelection);
          });
        }
        return selectionTree;
      }
    }

    /// <summary>
    /// Draws the selection tree. This gets drawn using the OnInspectorGUI attribute.
    /// </summary>
    [OnInspectorGUI]
    [PropertyOrder(-1)]
    protected void DrawSelectionTree()
    {
      if (CheckboxToggle && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Space && SelectionTree == OdinMenuTree.ActiveMenuTree))
      {
        IEnumerable<Type> source = SelectionTree.Selection.SelectMany(x => x.GetChildMenuItemsRecursive(true)).Select(x => x.Value).OfType<Type>();
        if (source.Any())
        {
          bool remove = _selection.Contains(source.FirstOrDefault());
          source.ForEach(x =>
          {
            if (remove)
              _selection.Remove(x);
            else
              _selection.Add(x);
          });
        }
        Event.current.Use();
        ++_checkboxUpdateId;
      }
      if (requestCheckboxUpdate && Event.current.type == EventType.Repaint)
      {
        requestCheckboxUpdate = false;
        ++_checkboxUpdateId;
      }

      Rect rect1 = EditorGUILayout.BeginVertical();
      EditorGUI.DrawRect(rect1, SirenixGUIStyles.DarkEditorBackground);
      GUILayout.Space(1f);
      bool drawSearchToolbar1 = SelectionTree.Config.DrawSearchToolbar;
      bool confirmSelectionButton = DrawConfirmSelectionButton;
      if (drawSearchToolbar1 | confirmSelectionButton)
      {
        SirenixEditorGUI.BeginHorizontalToolbar(SelectionTree.Config.SearchToolbarHeight);
        if (drawSearchToolbar1)
          SelectionTree.DrawSearchToolbar(GUIStyle.none);
        else
          GUILayout.FlexibleSpace();
        EditorGUI.DrawRect(GUILayoutUtility.GetLastRect().AlignLeft(1f), SirenixGUIStyles.BorderColor);
        if (confirmSelectionButton && SirenixEditorGUI.ToolbarButton(new GUIContent(EditorIcons.TestPassed)))
          SelectionTree.Selection.ConfirmSelection();
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

    private void DrawCheckboxMenuItems(OdinMenuItem xx)
    {
      List<Type> allChilds = xx.GetChildMenuItemsRecursive(true).Select(x => x.Value).OfType<Type>().ToList();
      bool isEmpty = allChilds.Count == 0;
      bool isSelected = false;
      bool isMixed = false;
      int prevUpdateId = -1;
      Action validate = () =>
      {
        if (isEmpty)
          return;
        isSelected = _selection.Contains(allChilds[0]);
        isMixed = false;
        for (int index = 1; index < allChilds.Count; ++index)
        {
          if (_selection.Contains(allChilds[index]) != isSelected)
          {
            isMixed = true;
            break;
          }
        }
      };
      xx.OnDrawItem += menuItem =>
      {
        if (isEmpty)
          return;
        Rect position = xx.LabelRect.AlignMiddle(18f).AlignLeft(16f);
        position.x -= 16f;
        if ((bool) xx.IconGetter())
          position.x -= 16f;
        if (Event.current.type != EventType.Repaint && xx.ChildMenuItems.Count == 0)
          position = xx.Rect;
        if (prevUpdateId != _checkboxUpdateId)
        {
          validate();
          prevUpdateId = _checkboxUpdateId;
        }
        EditorGUI.showMixedValue = isMixed;
        EditorGUI.BeginChangeCheck();
        bool flag = EditorGUI.Toggle(position, isSelected);
        if (EditorGUI.EndChangeCheck())
        {
          for (int index = 0; index < allChilds.Count; ++index)
          {
            if (flag)
              _selection.Add(allChilds[index]);
            else
              _selection.Remove(allChilds[index]);
          }
          xx.Select();
          validate();
          requestCheckboxUpdate = true;
          GUIHelper.RemoveFocusControl();
        }
        EditorGUI.showMixedValue = false;
      };
    }

    public void SetSelection(Type selected)
    {
      if (CheckboxToggle)
      {
        _selection.Clear();
        _selection.Add(selected);
      }
      else
      {
        if (selected == null)
          return;
        SelectionTree.EnumerateTree().Where(x => x.Value is Type).Where(x => EqualityComparer<Type>.Default.Equals((Type) x.Value, selected)).ToList().ForEach(x => x.Select(true));
      }
    }

    protected void SetupWindow(OdinEditorWindow window, EditorWindow prevSelectedWindow)
    {
      int prevFocusId = GUIUtility.hotControl;
      int prevKeybaorFocus = GUIUtility.keyboardControl;
      window.WindowPadding = new Vector4();
      bool wasConfirmed = false;
      SelectionTree.Selection.SelectionConfirmed += (Action<OdinMenuTreeSelection>) (x =>
      {
        bool ctrl = Event.current != null && Event.current.modifiers != EventModifiers.Control;
        UnityEditorEventUtility.DelayAction(() =>
        {
          wasConfirmed = true;
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
        UnityEditorEventUtility.DelayAction(() => window.Close());
        if ((bool) prevSelectedWindow)
          prevSelectedWindow.Focus();
        Event.current.Use();
      });
      window.OnClose += (Action) (() =>
      {
        GUIUtility.hotControl = prevFocusId;
        GUIUtility.keyboardControl = prevKeybaorFocus;
      });
    }

    public IEnumerable<Type> GetCurrentSelection()
    {
      return CheckboxToggle ? _selection : SelectionTree.Selection.Select(x => x.Value).OfType<Type>();;
    }

    /// <summary>Builds the selection tree.</summary>
    protected void BuildSelectionTree(OdinMenuTree tree)
    {
      tree.Selection.SupportsMultiSelect = false;
      tree.DefaultMenuStyle = OdinMenuStyle.TreeViewStyle;
      getMenuItemName = getMenuItemName ?? (x => (object) x != null ? x.ToString() : "");
      if (FlattenedTree)
      {
        if (_genericSelectorCollection != null)
        {
          foreach (GenericSelectorItem<Type> genericSelector in _genericSelectorCollection)
            tree.MenuItems.Add(new OdinMenuItem(tree, genericSelector.GetNiceName(), genericSelector.Value));
        }
      }
      else if (_genericSelectorCollection != null)
      {
        foreach (GenericSelectorItem<Type> genericSelector in _genericSelectorCollection)
          tree.AddObjectAtPath(genericSelector.GetNiceName(), genericSelector.Value);
      }
      if (!CheckboxToggle)
        return;
      tree.EnumerateTree().ForEach(DrawCheckboxMenuItems);
      tree.DefaultMenuStyle.TrianglePadding -= 17f;
      tree.DefaultMenuStyle.Offset += 18f;
      tree.DefaultMenuStyle.SelectedColorDarkSkin = new Color(1f, 1f, 1f, 0.05f);
      tree.DefaultMenuStyle.SelectedColorLightSkin = new Color(1f, 1f, 1f, 0.05f);
      tree.DefaultMenuStyle.SelectedLabelStyle = tree.DefaultMenuStyle.DefaultLabelStyle;
      tree.Config.ConfirmSelectionOnDoubleClick = false;
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
  }
}