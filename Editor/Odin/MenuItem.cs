namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Sirenix.OdinInspector.Editor;
  using Sirenix.Utilities;
  using Sirenix.Utilities.Editor;
  using UnityEditor;
  using UnityEngine;

  public class MenuItem
  {
    public readonly List<MenuItem> ChildMenuItems = new List<MenuItem>();
    public readonly string Name;
    public readonly Type Value;

    private static readonly Color MouseOverColor = new Color(1f, 1f, 1f, 0.028f);
    private static bool _previousMenuItemWasSelected;
    private static MenuItem _handleClickEventOnMouseUp;

    private readonly MenuTree _menuTree;

    private float _t = -1f;
    private bool _isInitialized;
    private LocalPersistentContext<bool> _isToggledContext;
    private string _prevName;
    private MenuItem _parentMenuItem;
    private Rect _triangleRect;
    private Rect _labelRect;
    private StringMemberHelper _nameValueGetter;
    private Rect _rect;
    private bool _wasMouseDownEvent;

    public MenuItem(MenuTree tree, string name, Type value)
    {
      if (tree == null)
        throw new ArgumentNullException(nameof(tree));
      if (name == null)
        throw new ArgumentNullException(nameof(name));
      _menuTree = tree;
      Name = name;
      Value = value;
    }


    public Rect Rect => _rect;

    public bool Toggled
    {
      get
      {
        if (ChildMenuItems.Count == 0)
          return false;

        if (_isToggledContext == null)
          _isToggledContext = LocalPersistentContext<bool>.Create(PersistentContext.Get("[OdinMenuItem]" + GetFullPath(), false));

        return _isToggledContext.Value;
      }
      set
      {
        if (_isToggledContext == null)
            _isToggledContext = LocalPersistentContext<bool>.Create(PersistentContext.Get("[OdinMenuItem]" + GetFullPath(), false));
        _isToggledContext.Value = value;
      }
    }

    private static OdinMenuStyle Style => OdinMenuStyle.TreeViewStyle;

    private bool IsSelected => _menuTree.Selection.Contains(this);


    private MenuItem Parent
    {
      get
      {
        EnsureInitialized();
        return _parentMenuItem;
      }
    }

    public void Select(bool addToSelection)
    {
      if (!addToSelection)
        _menuTree.Selection.Clear();
      _menuTree.Selection.Add(this);
    }

    public IEnumerable<MenuItem> GetChildMenuItemsRecursive(
      bool includeSelf)
    {
      MenuItem self = this;
      if (includeSelf)
        yield return self;
      foreach (MenuItem menuItem in self.ChildMenuItems.SelectMany(x => x.GetChildMenuItemsRecursive(true)))
        yield return menuItem;
    }

    public IEnumerable<MenuItem> GetParentMenuItemsRecursive(
      bool includeSelf,
      bool includeRoot = false)
    {
      MenuItem self = this;
      if (includeSelf || self.Parent == null & includeRoot)
        yield return self;

      if (self.Parent == null)
        yield break;

      foreach (MenuItem menuItem in self.Parent.GetParentMenuItemsRecursive(true, includeRoot))
        yield return menuItem;
    }

    public void DrawMenuItems(int indentLevel, Rect visibleRect, float currentEditorTimeHelperDeltaTime)
    {
      DrawMenuItem(indentLevel, visibleRect);
      List<MenuItem> childMenuItems = ChildMenuItems;
      int count = childMenuItems.Count;
      if (count == 0)
        return;

      if (_t < 0.0)
        _t = Toggled ? 1f : 0.0f;

      if (MenuTree.CurrentEventType == EventType.Layout)
        _t = Mathf.MoveTowards(_t, Toggled ? 1f : 0.0f, currentEditorTimeHelperDeltaTime * (1f / SirenixEditorGUI.DefaultFadeGroupDuration));

      if (SirenixEditorGUI.BeginFadeGroup(_t))
      {
        for (int index = 0; index < count; ++index)
          childMenuItems[index].DrawMenuItems(indentLevel + 1, visibleRect, currentEditorTimeHelperDeltaTime);
      }

      SirenixEditorGUI.EndFadeGroup();
    }

    public void UpdateMenuTreeRecursive(bool isRoot = false)
    {
      _isInitialized = true;

      foreach (MenuItem childMenuItem in ChildMenuItems)
      {
        childMenuItem._parentMenuItem = isRoot ? null : this;
        childMenuItem.UpdateMenuTreeRecursive();
      }
    }

    public bool _IsVisible()
    {
      return _menuTree.DrawInSearchMode ? _menuTree.FlatMenuTree.Contains(this) : ParentMenuItemsBottomUp(false).All(x => x.Toggled);
    }

    private void DrawMenuItem(int indentLevel, Rect visibleRect)
    {
      Rect rect1 = GUILayoutUtility.GetRect(0.0f, Style.Height);
      Event currentEvent = MenuTree.CurrentEvent;
      EventType currentEventType = MenuTree.CurrentEventType;
      if (currentEventType == EventType.Layout)
        return;

      if (currentEventType == EventType.Repaint || (currentEventType != EventType.Layout && _rect.width == 0.0))
        _rect = rect1;

      if (_rect.y > 1000.0 && (_rect.y + (double) _rect.height < visibleRect.y ||
                               _rect.y > visibleRect.y + (double) visibleRect.height))
        return;

      if (currentEventType == EventType.Repaint)
      {
        _labelRect = _rect.AddXMin(Style.Offset + indentLevel * Style.IndentAmount);
        bool isSelected = IsSelected;
        if (isSelected)
        {
          if (MenuTree.ActiveMenuTree == _menuTree)
          {
            EditorGUI.DrawRect(
              _rect,
              EditorGUIUtility.isProSkin ? Style.SelectedColorDarkSkin : Style.SelectedColorLightSkin);
          }
          else if (EditorGUIUtility.isProSkin)
          {
            EditorGUI.DrawRect(_rect, Style.SelectedInactiveColorDarkSkin);
          }
          else
          {
            EditorGUI.DrawRect(_rect, Style.SelectedInactiveColorLightSkin);
          }
        }

        if (!isSelected && _rect.Contains(currentEvent.mousePosition))
          EditorGUI.DrawRect(_rect, MouseOverColor);
        if (ChildMenuItems.Count > 0 && !_menuTree.DrawInSearchMode && Style.DrawFoldoutTriangle)
        {
          EditorIcon editorIcon = Toggled ? EditorIcons.TriangleDown : EditorIcons.TriangleRight;
          if (Style.AlignTriangleLeft)
          {
            _triangleRect = _labelRect.AlignLeft(Style.TriangleSize).AlignMiddle(Style.TriangleSize);
            _triangleRect.x -= Style.TriangleSize - Style.TrianglePadding;
          }
          else
          {
            _triangleRect = _rect.AlignRight(Style.TriangleSize).AlignMiddle(Style.TriangleSize);
            _triangleRect.x -= Style.TrianglePadding;
          }

          if (currentEventType == EventType.Repaint)
          {
            if (EditorGUIUtility.isProSkin)
            {
              if (isSelected || _triangleRect.Contains(currentEvent.mousePosition))
                GUI.DrawTexture(_triangleRect, editorIcon.Highlighted);
              else
                GUI.DrawTexture(_triangleRect, editorIcon.Active);
            }
            else if (isSelected)
            {
              GUI.DrawTexture(_triangleRect, editorIcon.Raw);
            }
            else if (_triangleRect.Contains(currentEvent.mousePosition))
            {
              GUI.DrawTexture(_triangleRect, editorIcon.Active);
            }
            else
            {
              GUIHelper.PushColor(new Color(1f, 1f, 1f, 0.7f));
              GUI.DrawTexture(_triangleRect, editorIcon.Active);
              GUIHelper.PopColor();
            }
          }
        }

        GUIStyle style = isSelected ? Style.SelectedLabelStyle : Style.DefaultLabelStyle;
        _labelRect = _labelRect.AlignMiddle(16f).AddY(Style.LabelVerticalOffset);
        GUI.Label(_labelRect, Name, style);
        if (Style.Borders)
        {
          float num = Style.BorderPadding;
          bool flag = true;
          if (isSelected || _previousMenuItemWasSelected)
          {
            num = 0.0f;
            if (!EditorGUIUtility.isProSkin)
              flag = false;
          }

          _previousMenuItemWasSelected = isSelected;
          if (flag)
          {
            Rect rect2 = _rect;
            rect2.x += num;
            rect2.width -= num * 2f;
            SirenixEditorGUI.DrawHorizontalLineSeperator(rect2.x, rect2.y, rect2.width, Style.BorderAlpha);
          }
        }
      }

      _wasMouseDownEvent = currentEventType == EventType.MouseDown && _rect.Contains(currentEvent.mousePosition);
      if (_wasMouseDownEvent)
        _handleClickEventOnMouseUp = this;
      SelectOnClick();
      HandleMouseEvents(_rect, _triangleRect);
    }

    private void SelectOnClick()
    {
      EventType type = Event.current.type;
      if (type == EventType.Layout || !Rect.Contains(Event.current.mousePosition))
        return;
      GUIHelper.RequestRepaint();
      if (type != EventType.MouseUp || ChildMenuItems.Count != 0)
        return;
      _menuTree.Selection.ConfirmSelection();
      Event.current.Use();
    }

    private string GetFullPath()
    {
      EnsureInitialized();
      MenuItem parent = Parent;
      return parent == null ? Name : parent.GetFullPath() + "/" + Name;
    }

    private void HandleMouseEvents(Rect rect, Rect triangleRect)
    {
      EventType type = Event.current.type;
      if (type == EventType.Used && _wasMouseDownEvent)
      {
        _wasMouseDownEvent = false;
        _handleClickEventOnMouseUp = this;
      }

      int num1;
      switch (type)
      {
        case EventType.MouseDown:
          num1 = 1;
          break;
        case EventType.MouseUp:
          num1 = _handleClickEventOnMouseUp == this ? 1 : 0;
          break;
        default:
          num1 = 0;
          break;
      }

      if (num1 == 0)
        return;

      _handleClickEventOnMouseUp = null;
      _wasMouseDownEvent = false;
      if (!rect.Contains(Event.current.mousePosition))
        return;

      bool flag1 = ChildMenuItems.Any();
      bool isSelected = IsSelected;
      if (Event.current.button == 0)
      {
        bool flag2 = false;
        if (flag1)
        {
          if (isSelected && Event.current.modifiers == EventModifiers.None)
            flag2 = true;
          else if (triangleRect.Contains(Event.current.mousePosition))
            flag2 = true;
        }

        if (flag2 && triangleRect.Contains(Event.current.mousePosition))
        {
          bool flag3 = !Toggled;
          if (Event.current.modifiers == EventModifiers.Alt)
          {
            foreach (MenuItem odinMenuItem in GetChildMenuItemsRecursive(true))
              odinMenuItem.Toggled = flag3;
          }
          else
          {
            Toggled = flag3;
          }
        }
        else
        {
          bool addToSelection = Event.current.modifiers == EventModifiers.Control;
          Select(addToSelection);
          _menuTree.Selection.ConfirmSelection();
        }
      }

      GUIHelper.RemoveFocusControl();
      Event.current.Use();
    }

    private IEnumerable<MenuItem> ParentMenuItemsBottomUp(
      bool includeSelf = true)
    {
      MenuItem self = this;
      if (self._parentMenuItem != null)
      {
        foreach (MenuItem menuItem in self._parentMenuItem.ParentMenuItemsBottomUp())
          yield return menuItem;
      }

      if (includeSelf)
        yield return self;
    }

    private void EnsureInitialized()
    {
      if (!_isInitialized)
        _menuTree.UpdateMenuTree();
    }
  }
}