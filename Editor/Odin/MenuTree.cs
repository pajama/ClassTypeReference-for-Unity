namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using Sirenix.OdinInspector.Editor;
  using Sirenix.Utilities;
  using Sirenix.Utilities.Editor;
  using UnityEditor;
  using UnityEngine;

  [Serializable]
  public class MenuTree : IEnumerable
  {
    public const int SearchToolbarHeight = 22;

    public static MenuTree ActiveMenuTree;
    public static Event CurrentEvent;
    public static EventType CurrentEventType;

    public readonly List<MenuItem> FlatMenuTree = new List<MenuItem>(); // needed to show search results

    private static bool _preventAutoFocus;

    private readonly GUIFrameCounter _frameCounter = new GUIFrameCounter();
    private readonly EditorTimeHelper _timeHelper = new EditorTimeHelper(); // For some reason, it should be used to fold out selection tree
    private readonly MenuItem _root;
    private readonly string _searchFieldControlName;

    [SerializeField] private Vector2 _scrollPos;
    [SerializeField] private string _searchTerm = string.Empty;
    private MenuItem _selectedItem;
    private MenuItem _scrollToWhenReady;
    private Rect _outerScrollViewRect;
    private Rect _innerScrollViewRect;
    private int _hideScrollbarsWhileContentIsExpanding;
    private int _forceRegainFocusCounter;
    private bool _isFirstFrame = true;
    private bool _hasRepaintedCurrentSearchResult = true;
    private bool _regainSearchFieldFocus;
    private bool _hadSearchFieldFocus;
    private bool _requestRepaint;
    private bool _scrollToCenter;
    private bool _regainFocusWhenWindowFocus;
    private bool _currWindowHasFocus;

    public MenuTree(SortedSet<TypeItem> items)
    {
      _root = new MenuItem(this, nameof(_root), null);
      SetupAutoScroll();
      _searchFieldControlName = Guid.NewGuid().ToString();
      ActiveMenuTree = this;
      BuildSelectionTree(items);
    }

    public event Action SelectionChanged;

    public MenuItem SelectedItem
    {
      get => _selectedItem;
      set
      {
        _selectedItem = value;
        SelectionChanged?.Invoke();
        }
    }

    public bool DrawInSearchMode { get; private set; }

    public List<MenuItem> MenuItems => _root.ChildMenuItems;

    public IEnumerable<MenuItem> EnumerateTree(bool includeRootNode = false)
    {
      return _root.GetChildMenuItemsRecursive(includeRootNode);
    }

    public void SetSelection(string itemName)
    {
      MenuItem itemToSelect = _root;
      foreach (string part in itemName.Split('/'))
      {
        itemToSelect = itemToSelect.ChildMenuItems.First(item => item.Name == part);
      }

      itemToSelect.Select();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return MenuItems.GetEnumerator();
    }

    public void OpenAllFolders()
    {
      _root.GetChildMenuItemsRecursive(false).ForEach(item => item.Toggled = true);
    }

    public void DrawSearchToolbar(GUIStyle toolbarStyle = null)
    {
      Rect rect1 = GUILayoutUtility.GetRect(0.0f, SearchToolbarHeight, GUILayoutOptions.ExpandWidth());
      if (Event.current.type == EventType.Repaint)
        (toolbarStyle ?? SirenixGUIStyles.ToolbarBackground).Draw(rect1, GUIContent.none, 0);
      Rect rect2 = rect1.HorizontalPadding(5f).AlignMiddle(16f);
      rect2.xMin += 3f;
      ++rect2.y;
      EditorGUI.BeginChangeCheck();
      _searchTerm = DrawSearchField(rect2, _searchTerm);
      if (EditorGUI.EndChangeCheck() && _hasRepaintedCurrentSearchResult)
      {
        _hasRepaintedCurrentSearchResult = false;
        if (!string.IsNullOrEmpty(_searchTerm))
        {
          if (!DrawInSearchMode)
            _scrollPos = default;
          DrawInSearchMode = true;
          FlatMenuTree.Clear();
          FlatMenuTree.AddRange(EnumerateTree().Where(x => x.Type != null).Select(x =>
          {
            bool flag = FuzzySearch.Contains(_searchTerm, x.Name, out int score);
            return new
            {
              score,
              item = x,
              include = flag
            };
          }).Where(x => x.include).OrderByDescending(x => x.score).Select(x => x.item));
        }
        else
        {
          DrawInSearchMode = false;
          FlatMenuTree.Clear();
          UpdateMenuTree();

          foreach (MenuItem item in SelectedItem.GetParentMenuItemsRecursive(false))
            item.Toggled = true;

          if (SelectedItem != null)
            ScrollToMenuItem(SelectedItem);
        }
      }

      if (Event.current.type != EventType.Repaint)
        return;
      _hasRepaintedCurrentSearchResult = true;
    }

    public void DrawMenuTree(bool drawSearchBar)
    {
      EditorTimeHelper time = EditorTimeHelper.Time;
      EditorTimeHelper.Time = _timeHelper;
      EditorTimeHelper.Time.Update();
      try
      {
        _timeHelper.Update();
        _frameCounter.Update();
        if (_requestRepaint)
        {
          GUIHelper.RequestRepaint();
          _requestRepaint = false;
        }

        if (drawSearchBar)
          DrawSearchToolbar();
        Rect outerRect = EditorGUILayout.BeginVertical();
        HandleActiveMenuTreeState(outerRect);
        if (Event.current.type == EventType.Repaint)
          _outerScrollViewRect = outerRect;
        _scrollPos = _hideScrollbarsWhileContentIsExpanding <= 0 ? EditorGUILayout.BeginScrollView(_scrollPos, GUILayoutOptions.ExpandHeight(false)) : EditorGUILayout.BeginScrollView(_scrollPos, GUIStyle.none, GUIStyle.none, GUILayoutOptions.ExpandHeight(false));
        Rect rect = EditorGUILayout.BeginVertical();
        if (_innerScrollViewRect.height == 0.0 || Event.current.type == EventType.Repaint)
        {
          float num = Mathf.Abs(_innerScrollViewRect.height - rect.height);
          float f = Mathf.Abs(_innerScrollViewRect.height - _outerScrollViewRect.height);
          if (_innerScrollViewRect.height - 40.0 <= _outerScrollViewRect.height && num > 0.0)
          {
            _hideScrollbarsWhileContentIsExpanding = 5;
            GUIHelper.RequestRepaint();
          }
          else if (Mathf.Abs(f) < 1.0)
          {
            _hideScrollbarsWhileContentIsExpanding = 5;
          }
          else
          {
            --_hideScrollbarsWhileContentIsExpanding;
            if (_hideScrollbarsWhileContentIsExpanding < 0)
              _hideScrollbarsWhileContentIsExpanding = 0;
            else
              GUIHelper.RequestRepaint();
          }

          _innerScrollViewRect = rect;
        }

        GUILayout.Space(-1f);

        var visibleRect = GUIClipInfo.VisibleRect.Expand(300f);
        CurrentEvent = Event.current;
        CurrentEventType = CurrentEvent.type;
        List<MenuItem> odinMenuItemList = DrawInSearchMode ? FlatMenuTree : MenuItems;
        int count = odinMenuItemList.Count;
        for (int index = 0; index < count; ++index)
          odinMenuItemList[index].DrawMenuItems(0, visibleRect);

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
        if (_scrollToWhenReady != null)
          ScrollToMenuItem(_scrollToWhenReady, _scrollToCenter);
        if (Event.current.type != EventType.Repaint)
          return;
        _isFirstFrame = false;
      }
      finally
      {
        EditorTimeHelper.Time = time;
      }
    }

    public void UpdateMenuTree()
    {
      _root.UpdateMenuTreeRecursive(true);
    }

    private void BuildSelectionTree(SortedSet<TypeItem> items)
    {
      if (items == null)
        return;

      foreach (TypeItem item in items)
      {
        AddTypeAtPath(item.Name, item.Type);
      }
    }

    private void SetupAutoScroll()
    {
      SelectionChanged += () =>
      {
        _requestRepaint = true;
        GUIHelper.RequestRepaint();
        if (_isFirstFrame)
          ScrollToMenuItem(SelectedItem, true);
        else
          ScrollToMenuItem(SelectedItem);
      };
    }

    private void ScrollToMenuItem(MenuItem menuItem, bool centerMenuItem = false)
    {
      if (menuItem == null)
        return;
      _scrollToCenter = centerMenuItem;
      _scrollToWhenReady = menuItem;
      if (!menuItem._IsVisible())
      {
        foreach (MenuItem odinMenuItem in menuItem.GetParentMenuItemsRecursive(false))
          odinMenuItem.Toggled = true;
      }
      else
      {
        foreach (MenuItem odinMenuItem in menuItem.GetParentMenuItemsRecursive(false))
          odinMenuItem.Toggled = true;
        if (_outerScrollViewRect.height == 0.0 || (menuItem.Rect.height <= 0.00999999977648258 || Event.current == null || Event.current.type != EventType.Repaint))
          return;

        Rect rect1 = menuItem.Rect;
        float num1;
        float num2;
        if (centerMenuItem)
        {
          Rect rect2 = _outerScrollViewRect.AlignCenterY(rect1.height);
          num1 = rect1.yMin - (_innerScrollViewRect.y + _scrollPos.y - rect2.y);
          num2 = (float) (rect1.yMax - (double) rect2.height + _innerScrollViewRect.y - (_scrollPos.y + (double) rect2.y));
        }
        else
        {
          _outerScrollViewRect.y = 0.0f;
          float num3 = (float) (rect1.yMin - (_innerScrollViewRect.y + (double) _scrollPos.y) - 1.0);
          float num4 = rect1.yMax - _outerScrollViewRect.height + _innerScrollViewRect.y - _scrollPos.y;
          num1 = num3 - rect1.height;
          num2 = num4 + rect1.height;
        }

        if (num1 < 0.0)
          _scrollPos.y += num1;
        if (num2 > 0.0)
          _scrollPos.y += num2;
        if (_frameCounter.FrameCount > 6)
          _scrollToWhenReady = null;
        else
          GUIHelper.RequestRepaint();
      }
    }

    private void HandleActiveMenuTreeState(Rect outerRect)
    {
      if (Event.current.type == EventType.Repaint)
      {
        if (_currWindowHasFocus != GUIHelper.CurrentWindowHasFocus)
        {
          _currWindowHasFocus = GUIHelper.CurrentWindowHasFocus;
          if (_currWindowHasFocus && _regainFocusWhenWindowFocus)
          {
            if (!_preventAutoFocus)
              ActiveMenuTree = this;
            _regainFocusWhenWindowFocus = false;
          }
        }
        if (!_currWindowHasFocus && ActiveMenuTree == this)
          ActiveMenuTree = null;
        if (_currWindowHasFocus)
          _regainFocusWhenWindowFocus = ActiveMenuTree == this;
        if (_currWindowHasFocus && ActiveMenuTree == null)
          ActiveMenuTree = this;
      }
      MenuTreeActivationZone(outerRect);
    }

    private void MenuTreeActivationZone(Rect rect)
    {
      if (ActiveMenuTree == this || Event.current.rawType != EventType.MouseDown || (!rect.Contains(Event.current.mousePosition) || !GUIHelper.CurrentWindowHasFocus))
        return;
      _regainSearchFieldFocus = true;
      _preventAutoFocus = true;
      ActiveMenuTree = this;
      UnityEditorEventUtility.EditorApplication_delayCall += (Action) (() => _preventAutoFocus = false);
      GUIHelper.RequestRepaint();
    }

    private string DrawSearchField(Rect rect, string searchTerm)
    {
      bool flag1 = GUI.GetNameOfFocusedControl() == _searchFieldControlName;
      if (_hadSearchFieldFocus != flag1)
      {
        if (flag1)
          ActiveMenuTree = this;
        _hadSearchFieldFocus = flag1;
      }

      bool flag2 = flag1 && (Event.current.keyCode == KeyCode.DownArrow || Event.current.keyCode == KeyCode.UpArrow || (Event.current.keyCode == KeyCode.LeftArrow || Event.current.keyCode == KeyCode.RightArrow) || Event.current.keyCode == KeyCode.Return);
      if (flag2)
        GUIHelper.PushEventType(Event.current.type);
      searchTerm = SirenixEditorGUI.SearchField(rect, searchTerm, _regainSearchFieldFocus && ActiveMenuTree == this, _searchFieldControlName);
      if (_regainSearchFieldFocus && Event.current.type == EventType.Layout)
        _regainSearchFieldFocus = false;
      if (flag2)
      {
        GUIHelper.PopEventType();
        if (ActiveMenuTree == this)
          _regainSearchFieldFocus = true;
      }

      if (_forceRegainFocusCounter >= 20)
        return searchTerm;

      if (_forceRegainFocusCounter < 4 && ActiveMenuTree == this)
        _regainSearchFieldFocus = true;
      GUIHelper.RequestRepaint();
      HandleUtility.Repaint();
      if (Event.current.type == EventType.Repaint)
        ++_forceRegainFocusCounter;
      return searchTerm;
    }

    public void AddTypeAtPath(string menuPath, Type type)
    {
      SplitMenuPath(menuPath, out menuPath, out string name);
      AddMenuItemAtPath(menuPath, new MenuItem(this, name, type));
    }

    private static void SplitMenuPath(string menuPath, out string path, out string name)
    {
      menuPath = menuPath.Trim('/');
      int length = menuPath.LastIndexOf('/');
      if (length == -1)
      {
        path = string.Empty;
        name = menuPath;
      }
      else
      {
        path = menuPath.Substring(0, length);
        name = menuPath.Substring(length + 1);
      }
    }

    private void AddMenuItemAtPath(
      string path,
      MenuItem menuItem)
    {
      MenuItem menuItem1 = _root;
      if (!string.IsNullOrEmpty(path))
      {
        if (path[0] == '/' || path[path.Length - 1] == '/')
          path = path.Trim();
        int startIndex = 0;
        int num;
        do
        {
          num = path.IndexOf('/', startIndex);
          string name;
          if (num < 0)
          {
            num = path.Length - 1;
            name = path.Substring(startIndex, num - startIndex + 1);
          }
          else
          {
            name = path.Substring(startIndex, num - startIndex);
          }

          List<MenuItem> childMenuItems = menuItem1.ChildMenuItems;
          MenuItem menuItem2 = null;
          for (int index = childMenuItems.Count - 1; index >= 0; --index)
          {
            if (childMenuItems[index].Name != name)
              continue;

            menuItem2 = childMenuItems[index];
            break;
          }

          if (menuItem2 == null)
          {
            menuItem2 = new MenuItem(this, name, null);
            menuItem1.ChildMenuItems.Add(menuItem2);
          }

          menuItem1 = menuItem2;
          startIndex = num + 1;
        }
        while (num != path.Length - 1);
      }

      List<MenuItem> childMenuItems1 = menuItem1.ChildMenuItems;
      MenuItem menuItem3 = null;
      for (int index = childMenuItems1.Count - 1; index >= 0; --index)
      {
        if (childMenuItems1[index].Name != menuItem.Name)
          continue;

        menuItem3 = childMenuItems1[index];
        break;
      }

      if (menuItem3 != null)
      {
        menuItem1.ChildMenuItems.Remove(menuItem3);
        menuItem.ChildMenuItems.AddRange(menuItem3.ChildMenuItems);
      }

      menuItem1.ChildMenuItems.Add(menuItem);
    }
  }
}