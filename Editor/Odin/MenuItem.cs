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
  using Object = UnityEngine.Object;

  public class MenuItem
  {
    private static readonly Color MouseOverColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.028f) : new Color(1f, 1f, 1f, 0.3f);
    private static bool _previousMenuItemWasSelected;
    private static MenuItem _handleClickEventOnMouseUp;
    private static int _mouseDownClickCount;

    private float _t = -1f;
    private Func<Texture> _iconGetter;
    private bool _isInitialized;
    private LocalPersistentContext<bool> _isToggledContext;
    private string _prevName;
    private MenuItem _nextMenuItem;
    private MenuItem _nextMenuItemFlat;
    private MenuItem _parentMenuItem;
    private MenuItem _previousMenuItem;
    private MenuItem _previousMenuItemFlat;
    private OdinMenuStyle _style;
    private Rect _triangleRect;
    private Rect _labelRect;
    private StringMemberHelper _nameValueGetter;
    private bool? _nonCachedToggledState;
    private Rect _rectValue;
    private bool _wasMouseDownEvent;

    public MenuItem(MenuTree tree, string name, object value)
    {
      if (tree == null)
        throw new ArgumentNullException(nameof(tree));
      if (name == null)
        throw new ArgumentNullException(nameof(name));
      MenuTree = tree;
      Name = name;
      SearchString = name;
      Value = value;
      ChildMenuItems = new List<MenuItem>();
    }

    public List<MenuItem> ChildMenuItems { get; }

    public bool IsSelected => MenuTree.Selection.Contains(this);

    public MenuTree MenuTree { get; }

    public string Name { get; }

    public string SearchString { get; }

    public MenuItem NextVisualMenuItem
    {
      get
      {
        EnsureInitialized();
        if (MenuTree.DrawInSearchMode)
          return _nextMenuItemFlat;
        return ChildMenuItems.Count > 0 && _nextMenuItem != null && (!Toggled && _IsVisible()) ? _nextMenuItem : GetAllNextMenuItems().FirstOrDefault(x => x._IsVisible());
      }
    }

    public Rect Rect
    {
      get => _rectValue;
      set => _rectValue = value;
    }

    public MenuItem PrevVisualMenuItem
    {
      get
      {
        EnsureInitialized();
        if (MenuTree.DrawInSearchMode)
          return _previousMenuItemFlat;

        if (ChildMenuItems.Count <= 0 || Toggled || !_IsVisible())
          return GetAllPreviousMenuItems().FirstOrDefault(x => x._IsVisible());

        if (_previousMenuItem != null)
        {
          if (_previousMenuItem.ChildMenuItems.Count == 0 || !_previousMenuItem.Toggled)
            return _previousMenuItem;
        }
        else if (_parentMenuItem != null)
        {
          return _parentMenuItem;
        }

        return GetAllPreviousMenuItems().FirstOrDefault(x => x._IsVisible());
      }
    }

    public bool Toggled
    {
      get
      {
        if (ChildMenuItems.Count == 0)
          return false;
        if (MenuTree.Config.UseCachedExpandedStates)
        {
          if (_isToggledContext == null)
            _isToggledContext = LocalPersistentContext<bool>.Create(PersistentContext.Get("[OdinMenuItem]" + GetFullPath(), false));
          return _isToggledContext.Value;
        }

        if (!_nonCachedToggledState.HasValue)
          _nonCachedToggledState = false;
        return _nonCachedToggledState.Value;
      }
      set
      {
        if (MenuTree.Config.UseCachedExpandedStates)
        {
          if (_isToggledContext == null)
            _isToggledContext = LocalPersistentContext<bool>.Create(PersistentContext.Get("[OdinMenuItem]" + GetFullPath(), false));
          _isToggledContext.Value = value;
        }
        else
        {
          _nonCachedToggledState = value;
        }
      }
    }

    public object Value { get; }

    private MenuItem Parent
    {
      get
      {
        EnsureInitialized();
        return _parentMenuItem;
      }
    }

    private string SmartName
    {
      get
      {
        object instance = Value;
        if (Value is Func<object> func)
          instance = func();
        if (Name == null || Name == "$")
        {
          if (instance == null)
            return string.Empty;
          var @object = instance as Object;
          return (bool) @object ? @object.name.SplitPascalCase() : instance.ToString();
        }

        bool flag = false;
        if (_nameValueGetter == null)
        {
          flag = true;
        }
        else if (_prevName != Name)
        {
          flag = true;
          _prevName = Name;
        }
        else if (_nameValueGetter != null && instance != null && _nameValueGetter.ObjectType != instance.GetType())
        {
          flag = true;
        }

        if (instance == null)
          _nameValueGetter = null;
        else if (flag)
          _nameValueGetter = new StringMemberHelper(instance.GetType(), false, Name);
        return _nameValueGetter != null ? _nameValueGetter.ForceGetString(instance) : Name;
      }
    }

    private OdinMenuStyle Style => _style ?? (_style = MenuTree.DefaultMenuStyle);

    public void Deselect()
    {
      MenuTree.Selection.Remove(this);
    }

    public void Select(bool addToSelection = false)
    {
      if (!addToSelection)
        MenuTree.Selection.Clear();
      MenuTree.Selection.Add(this);
    }

    public IEnumerable<MenuItem> GetChildMenuItemsRecursive(
      bool includeSelf)
    {
      MenuItem menuItem1 = this;
      if (includeSelf)
        yield return menuItem1;
      foreach (MenuItem odinMenuItem2 in menuItem1.ChildMenuItems.SelectMany(x => x.GetChildMenuItemsRecursive(true)))
        yield return odinMenuItem2;
    }

    public IEnumerable<MenuItem> GetParentMenuItemsRecursive(
      bool includeSelf,
      bool includeRoot = false)
    {
      MenuItem menuItem1 = this;
      if (includeSelf || menuItem1.Parent == null & includeRoot)
        yield return menuItem1;
      if (menuItem1.Parent != null)
      {
        foreach (MenuItem odinMenuItem2 in menuItem1.Parent.GetParentMenuItemsRecursive(true, includeRoot))
          yield return odinMenuItem2;
      }
    }

    public void DrawMenuItems(int indentLevel)
    {
      DrawMenuItem(indentLevel);
      List<MenuItem> childMenuItems = ChildMenuItems;
      int count = childMenuItems.Count;
      if (count == 0)
        return;
      bool toggled = Toggled;
      if (_t < 0.0)
        _t = toggled ? 1f : 0.0f;
      if (MenuTree.CurrentEventType == EventType.Layout)
        _t = Mathf.MoveTowards(_t, toggled ? 1f : 0.0f, MenuTree.CurrentEditorTimeHelperDeltaTime * (1f / SirenixEditorGUI.DefaultFadeGroupDuration));
      if (SirenixEditorGUI.BeginFadeGroup(_t))
      {
        for (int index = 0; index < count; ++index)
          childMenuItems[index].DrawMenuItems(indentLevel + 1);
      }

      SirenixEditorGUI.EndFadeGroup();
    }

    public void DrawMenuItem(int indentLevel)
    {
      Rect rect1 = GUILayoutUtility.GetRect(0.0f, Style.Height);
      Event currentEvent = MenuTree.CurrentEvent;
      EventType currentEventType = MenuTree.CurrentEventType;
      if (currentEventType == EventType.Layout)
        return;
      if (currentEventType == EventType.Repaint || (currentEventType != EventType.Layout && _rectValue.width == 0.0))
        _rectValue = rect1;
      float y1 = _rectValue.y;
      if (y1 > 1000.0)
      {
        float y2 = MenuTree.VisibleRect.y;
        if (y1 + (double) _rectValue.height < y2 || y1 > y2 + (double) MenuTree.VisibleRect.height)
        {
          return;
        }
      }
      if (currentEventType == EventType.Repaint)
      {
        _labelRect = _rectValue.AddXMin(Style.Offset + indentLevel * Style.IndentAmount);
        bool isSelected = IsSelected;
        if (isSelected)
        {
          if (MenuTree.ActiveMenuTree == MenuTree)
          {
            EditorGUI.DrawRect(
                _rectValue,
                EditorGUIUtility.isProSkin ? Style.SelectedColorDarkSkin : Style.SelectedColorLightSkin);
          }
          else if (EditorGUIUtility.isProSkin)
          {
            EditorGUI.DrawRect(_rectValue, Style.SelectedInactiveColorDarkSkin);
          }
          else
          {
            EditorGUI.DrawRect(_rectValue, Style.SelectedInactiveColorLightSkin);
          }
        }

        if (!isSelected && _rectValue.Contains(currentEvent.mousePosition))
          EditorGUI.DrawRect(_rectValue, MouseOverColor);
        if (ChildMenuItems.Count > 0 && !MenuTree.DrawInSearchMode && Style.DrawFoldoutTriangle)
        {
          EditorIcon editorIcon = Toggled ? EditorIcons.TriangleDown : EditorIcons.TriangleRight;
          if (Style.AlignTriangleLeft)
          {
            _triangleRect = _labelRect.AlignLeft(Style.TriangleSize).AlignMiddle(Style.TriangleSize);
            _triangleRect.x -= Style.TriangleSize - Style.TrianglePadding;
          }
          else
          {
            _triangleRect = _rectValue.AlignRight(Style.TriangleSize).AlignMiddle(Style.TriangleSize);
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
        GUI.Label(_labelRect, SmartName, style);
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
            Rect rect2 = _rectValue;
            rect2.x += num;
            rect2.width -= num * 2f;
            SirenixEditorGUI.DrawHorizontalLineSeperator(rect2.x, rect2.y, rect2.width, Style.BorderAlpha);
          }
        }
      }

      _wasMouseDownEvent = currentEventType == EventType.MouseDown && _rectValue.Contains(currentEvent.mousePosition);
      if (_wasMouseDownEvent)
        _handleClickEventOnMouseUp = this;
      SelectOnClick();
      HandleMouseEvents(_rectValue, _triangleRect);
    }

    public void UpdateMenuTreeRecursive(bool isRoot = false)
    {
      _isInitialized = true;
      MenuItem menuItem = null;
      foreach (MenuItem childMenuItem in ChildMenuItems)
      {
        childMenuItem._parentMenuItem = null;
        childMenuItem._nextMenuItem = null;
        childMenuItem._previousMenuItemFlat = null;
        childMenuItem._nextMenuItemFlat = null;
        childMenuItem._previousMenuItem = null;
        if (!isRoot)
          childMenuItem._parentMenuItem = this;
        if (menuItem != null)
        {
          menuItem._nextMenuItem = childMenuItem;
          childMenuItem._previousMenuItem = menuItem;
        }

        menuItem = childMenuItem;
        childMenuItem.UpdateMenuTreeRecursive();
      }
    }

    public void UpdateFlatMenuItemNavigation()
    {
      int num = 0;
      MenuItem menuItem1 = null;
      foreach (MenuItem odinMenuItem2 in MenuTree.DrawInSearchMode ? (IEnumerable<MenuItem>) MenuTree.FlatMenuTree : MenuTree.EnumerateTree())
      {
        num++;
        odinMenuItem2._nextMenuItemFlat = null;
        odinMenuItem2._previousMenuItemFlat = null;
        if (menuItem1 != null)
        {
          odinMenuItem2._previousMenuItemFlat = menuItem1;
          menuItem1._nextMenuItemFlat = odinMenuItem2;
        }
        menuItem1 = odinMenuItem2;
      }
    }

    public bool _IsVisible()
    {
      return MenuTree.DrawInSearchMode ? MenuTree.FlatMenuTree.Contains(this) : ParentMenuItemsBottomUp(false).All(x => x.Toggled);
    }

    private void SelectOnClick()
    {
      EventType type = Event.current.type;
      if (type == EventType.Layout || !Rect.Contains(Event.current.mousePosition))
        return;
      GUIHelper.RequestRepaint();
      if (type != EventType.MouseUp || ChildMenuItems.Count != 0)
        return;
      MenuTree.Selection.ConfirmSelection();
      Event.current.Use();
    }

    private string GetFullPath()
    {
      EnsureInitialized();
      MenuItem parent = Parent;
      return parent == null ? SmartName : parent.GetFullPath() + "/" + SmartName;
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
      switch (Event.current.button)
      {
        case 0:
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
            if (MenuTree.Config.ConfirmSelectionOnDoubleClick && Event.current.clickCount == 2)
              MenuTree.Selection.ConfirmSelection();
          }

          break;
        }
      }

      GUIHelper.RemoveFocusControl();
      Event.current.Use();
    }

    private IEnumerable<MenuItem> GetAllNextMenuItems()
    {
      if (_nextMenuItemFlat == null)
        yield break;

      yield return _nextMenuItemFlat;
      foreach (MenuItem allNextMenuItem in _nextMenuItemFlat.GetAllNextMenuItems())
        yield return allNextMenuItem;
    }

    private IEnumerable<MenuItem> GetAllPreviousMenuItems()
    {
      if (_previousMenuItemFlat == null)
        yield break;

      yield return _previousMenuItemFlat;
      foreach (MenuItem previousMenuItem in _previousMenuItemFlat.GetAllPreviousMenuItems())
        yield return previousMenuItem;
    }

    private IEnumerable<MenuItem> ParentMenuItemsBottomUp(
      bool includeSelf = true)
    {
      MenuItem menuItem1 = this;
      if (menuItem1._parentMenuItem != null)
      {
        foreach (MenuItem odinMenuItem2 in menuItem1._parentMenuItem.ParentMenuItemsBottomUp())
          yield return odinMenuItem2;
      }

      if (includeSelf)
        yield return menuItem1;
    }

    private void EnsureInitialized()
    {
      if (_isInitialized)
        return;
      MenuTree.UpdateMenuTree();
      if (_isInitialized)
        return;
      Debug.LogWarning("Could not initialize menu item. Is the menu item not part of a menu tree?");
    }
  }
}