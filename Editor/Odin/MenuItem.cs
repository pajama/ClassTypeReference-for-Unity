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
  using UnityEngine.Assertions;

  [Serializable]
  public class MenuItem
  {
    public readonly List<MenuItem> ChildMenuItems = new List<MenuItem>();
    public readonly string Name;
    public readonly Type Type;

    private static readonly Color MouseOverColor = new Color(1f, 1f, 1f, 0.028f);
    private static bool _previousMenuItemWasSelected;
    private static MenuItem _handleClickEventOnMouseUp;

    private readonly MenuTree _menuTree;

    private bool _isInitialized;
    private bool _isToggled;
    private MenuItem _parentMenuItem;
    private Rect _triangleRect;
    private Rect _labelRect;
    private Rect _rect;
    private bool _wasMouseDownEvent;

    public MenuItem(MenuTree tree, string name, Type type)
    {
      Assert.IsNotNull(tree);
      Assert.IsNotNull(name);

      _menuTree = tree;
      Name = name;
      Type = type;
    }

    public Rect Rect => _rect;

    public bool Toggled
    {
      get => ChildMenuItems.Count != 0 && _isToggled;
      set => _isToggled = value;
    }

    private static OdinMenuStyle Style => OdinMenuStyle.TreeViewStyle;

    private bool IsSelected => _menuTree.SelectedItem == this;

    private MenuItem Parent
    {
      get
      {
        EnsureInitialized();
        return _parentMenuItem;
      }
    }

    public void Select()
    {
      _menuTree.SelectedItem = this;
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

    public void DrawMenuItems(int indentLevel, Rect visibleRect)
    {
      DrawMenuItem(indentLevel, visibleRect);
      if ( ! Toggled)
        return;

      foreach (MenuItem childItem in ChildMenuItems)
        childItem.DrawMenuItems(indentLevel + 1, visibleRect);
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
      HandleMouseEvents(_rect);
    }

    private void SelectOnClick()
    {
      EventType type = Event.current.type;
      if (type == EventType.Layout || !Rect.Contains(Event.current.mousePosition))
        return;
      GUIHelper.RequestRepaint();
      if (type != EventType.MouseUp || ChildMenuItems.Count != 0)
        return;
      Event.current.Use();
    }

    private string GetFullPath()
    {
      EnsureInitialized();
      MenuItem parent = Parent;
      return parent == null ? Name : parent.GetFullPath() + "/" + Name;
    }

    private void HandleMouseEvents(Rect rect)
    {
      switch (Event.current.type)
      {
        case EventType.Used when _wasMouseDownEvent:
        {
          _wasMouseDownEvent = false;
          _handleClickEventOnMouseUp = this;
          break;
        }

        case EventType.MouseUp:
        {
          if (_handleClickEventOnMouseUp != this)
            return;
          break;
        }

        case EventType.MouseDown:
          break;

        default:
          return;
      }

      _handleClickEventOnMouseUp = null;
      _wasMouseDownEvent = false;
      if (!rect.Contains(Event.current.mousePosition))
        return;

      if (Event.current.button == 0)
      {
        if (ChildMenuItems.Any())
        {
          Toggled = ! Toggled;
        }
        else
        {
          Select();
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