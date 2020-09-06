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

  public class MenuTree : IEnumerable
  {
    public static MenuTree ActiveMenuTree;
    public static Rect VisibleRect;
    public static float CurrentEditorTimeHelperDeltaTime;
    public static Event CurrentEvent;
    public static EventType CurrentEventType;

    public readonly List<MenuItem> FlatMenuTree = new List<MenuItem>();

    private static bool _preventAutoFocus;
    private static EditorTimeHelper _currentEditorTimeHelper;

    private readonly GUIFrameCounter _frameCounter = new GUIFrameCounter();
    private readonly EditorTimeHelper _timeHelper = new EditorTimeHelper();
    private readonly MenuItem _root;
    private readonly string _searchFieldControlName;

    private bool _isFirstFrame = true;
    private bool _hasRepaintedCurrentSearchResult = true;
    private MenuTreeDrawingConfig _defaultConfig;
    private bool _regainSearchFieldFocus;
    private bool _hadSearchFieldFocus;
    private Rect _outerScrollViewRect;
    private int _hideScrollbarsWhileContentIsExpanding;
    private Rect _innerScrollViewRect;
    private int _forceRegainFocusCounter;
    private bool _requestRepaint;
    private bool _scrollToCenter;
    private MenuItem _scrollToWhenReady;
    private bool _isDirty;
    private bool _updateSearchResults;
    private bool _regainFocusWhenWindowFocus;
    private bool _currWindowHasFocus;

    public MenuTree()
    {
      DefaultMenuStyle = new OdinMenuStyle();
      Selection = new MenuTreeSelection();
      _root = new MenuItem(this, nameof(_root), null);
      SetupAutoScroll();
      _searchFieldControlName = Guid.NewGuid().ToString();
    }

    public MenuItem Root => _root;

    public MenuTreeSelection Selection { get; }

    public List<MenuItem> MenuItems => _root.ChildMenuItems;

    public bool DrawInSearchMode { get; private set; }

    public OdinMenuStyle DefaultMenuStyle
    {
      get => Config.DefaultMenuStyle;
      set => Config.DefaultMenuStyle = value;
    }

    public MenuTreeDrawingConfig Config
    {
      get
      {
        MenuTreeDrawingConfig treeDrawingConfig = _defaultConfig ?? new MenuTreeDrawingConfig
        {
          DrawScrollView = true,
          DrawSearchToolbar = false,
          AutoHandleKeyboardNavigation = false
        };

        _defaultConfig = treeDrawingConfig;
        return _defaultConfig;
      }
      set => _defaultConfig = value;
    }

    public IEnumerable<MenuItem> EnumerateTree(bool includeRootNode = false)
    {
      return _root.GetChildMenuItemsRecursive(includeRootNode);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return MenuItems.GetEnumerator();
    }

    /// <summary>Enumerates the tree with a DFS.</summary>
    public void EnumerateTree(Action<MenuItem> action)
    {
      _root.GetChildMenuItemsRecursive(false).ForEach(action);
    }

    public void DrawSearchToolbar(GUIStyle toolbarStyle = null)
    {
      MenuTreeDrawingConfig config = Config;
      Rect rect1 = GUILayoutUtility.GetRect(0.0f, config.SearchToolbarHeight, GUILayoutOptions.ExpandWidth());
      if (Event.current.type == EventType.Repaint)
        (toolbarStyle ?? SirenixGUIStyles.ToolbarBackground).Draw(rect1, GUIContent.none, 0);
      Rect rect2 = rect1.HorizontalPadding(5f).AlignMiddle(16f);
      rect2.xMin += 3f;
      ++rect2.y;
      EditorGUI.BeginChangeCheck();
      config.SearchTerm = DrawSearchField(rect2, config.SearchTerm, config.AutoFocusSearchBar);
      if ((EditorGUI.EndChangeCheck() || _updateSearchResults) && _hasRepaintedCurrentSearchResult)
      {
        _updateSearchResults = false;
        _hasRepaintedCurrentSearchResult = false;
        if (!string.IsNullOrEmpty(config.SearchTerm))
        {
          if (!DrawInSearchMode)
            config.ScrollPos = default;
          DrawInSearchMode = true;
          if (config.SearchFunction != null)
          {
            FlatMenuTree.Clear();
            foreach (MenuItem odinMenuItem in EnumerateTree())
            {
              if (config.SearchFunction(odinMenuItem))
                FlatMenuTree.Add(odinMenuItem);
            }
          }
          else
          {
            FlatMenuTree.Clear();
            FlatMenuTree.AddRange(EnumerateTree().Where(x => x.Value != null).Select(x =>
            {
              bool flag = FuzzySearch.Contains(Config.SearchTerm, x.SearchString, out int score);
              return new
              {
                score,
                item = x,
                include = flag
              };
            }).Where(x => x.include).OrderByDescending(x => x.score).Select(x => x.item));
          }

          _root.UpdateFlatMenuItemNavigation();
        }
        else
        {
          DrawInSearchMode = false;
          FlatMenuTree.Clear();
          MenuItem menuItem = Selection.LastOrDefault();
          UpdateMenuTree();
          Selection.SelectMany(x => x.GetParentMenuItemsRecursive(false)).ForEach(x => x.Toggled = true);
          if (menuItem != null)
            ScrollToMenuItem(menuItem);
          _root.UpdateFlatMenuItemNavigation();
        }
      }

      if (Event.current.type != EventType.Repaint)
        return;
      _hasRepaintedCurrentSearchResult = true;
    }

    /// <summary>Draws the menu tree recursively.</summary>
    public void DrawMenuTree()
    {
      EditorTimeHelper time = EditorTimeHelper.Time;
      EditorTimeHelper.Time = _timeHelper;
      EditorTimeHelper.Time.Update();
      try
      {
        _timeHelper.Update();
        _frameCounter.Update();
        MenuTreeDrawingConfig config = Config;
        if (_requestRepaint)
        {
          GUIHelper.RequestRepaint();
          _requestRepaint = false;
        }
        if (config.DrawSearchToolbar)
          DrawSearchToolbar();
        Rect outerRect = EditorGUILayout.BeginVertical();
        HandleActiveMenuTreeState(outerRect);
        if (config.DrawScrollView)
        {
          if (Event.current.type == EventType.Repaint)
            _outerScrollViewRect = outerRect;
          config.ScrollPos = _hideScrollbarsWhileContentIsExpanding <= 0 ? EditorGUILayout.BeginScrollView(config.ScrollPos, GUILayoutOptions.ExpandHeight(false)) : EditorGUILayout.BeginScrollView(config.ScrollPos, GUIStyle.none, GUIStyle.none, GUILayoutOptions.ExpandHeight(false));
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
        }
        if (_isDirty && Event.current.type == EventType.Layout)
        {
          UpdateMenuTree();
          _isDirty = false;
        }
        VisibleRect = GUIClipInfo.VisibleRect.Expand(300f);
        CurrentEvent = Event.current;
        CurrentEventType = CurrentEvent.type;
        _currentEditorTimeHelper = EditorTimeHelper.Time;
        CurrentEditorTimeHelperDeltaTime = _currentEditorTimeHelper.DeltaTime;
        List<MenuItem> odinMenuItemList = DrawInSearchMode ? FlatMenuTree : MenuItems;
        int count = odinMenuItemList.Count;
        if (config.EXPERIMENTALINTERNALDrawFlatTreeFastNoLayout)
        {
          int height = DefaultMenuStyle.Height;
          Rect rect = GUILayoutUtility.GetRect(0.0f, count * height);
          rect.height = height;
          for (int index = 0; index < count; ++index)
          {
            MenuItem menuItem = odinMenuItemList[index];
            menuItem.Rect = rect;
            menuItem.DrawMenuItem(0);
            rect.y += height;
          }
        }
        else
        {
          for (int index = 0; index < count; ++index)
            odinMenuItemList[index].DrawMenuItems(0);
        }
        if (config.DrawScrollView)
        {
          EditorGUILayout.EndVertical();
          EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndVertical();
        if (config.AutoHandleKeyboardNavigation)
          HandleKeyboardMenuNavigation();
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
      _root.UpdateFlatMenuItemNavigation();
    }

    private void SetupAutoScroll()
    {
      Selection.SelectionChanged += (Action<SelectionChangedType>) (x =>
      {
        if (!Config.AutoScrollOnSelectionChanged || x != SelectionChangedType.ItemAdded)
          return;
        _requestRepaint = true;
        GUIHelper.RequestRepaint();
        if (_isFirstFrame)
          ScrollToMenuItem(Selection.LastOrDefault(), true);
        else
          ScrollToMenuItem(Selection.LastOrDefault());
      });
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
        MenuTreeDrawingConfig config = Config;
        Rect rect1 = menuItem.Rect;
        float num1;
        float num2;
        if (centerMenuItem)
        {
          Rect rect2 = _outerScrollViewRect.AlignCenterY(rect1.height);
          num1 = rect1.yMin - (_innerScrollViewRect.y + config.ScrollPos.y - rect2.y);
          num2 = (float) (rect1.yMax - (double) rect2.height + _innerScrollViewRect.y - (config.ScrollPos.y + (double) rect2.y));
        }
        else
        {
          _outerScrollViewRect.y = 0.0f;
          float num3 = (float) (rect1.yMin - (_innerScrollViewRect.y + (double) config.ScrollPos.y) - 1.0);
          float num4 = rect1.yMax - _outerScrollViewRect.height + _innerScrollViewRect.y - config.ScrollPos.y;
          num1 = num3 - rect1.height;
          num2 = num4 + rect1.height;
        }
        if (num1 < 0.0)
          config.ScrollPos.y += num1;
        if (num2 > 0.0)
          config.ScrollPos.y += num2;
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

    private string DrawSearchField(Rect rect, string searchTerm, bool autoFocus)
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
      searchTerm = SirenixEditorGUI.SearchField(rect, searchTerm, autoFocus && _regainSearchFieldFocus && ActiveMenuTree == this, _searchFieldControlName);
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

      if (autoFocus && _forceRegainFocusCounter < 4 && ActiveMenuTree == this)
        _regainSearchFieldFocus = true;
      GUIHelper.RequestRepaint();
      HandleUtility.Repaint();
      if (Event.current.type == EventType.Repaint)
        ++_forceRegainFocusCounter;
      return searchTerm;
    }

    private void HandleKeyboardMenuNavigation()
    {
      if (Event.current.type != EventType.KeyDown || ActiveMenuTree != this)
        return;

      GUIHelper.RequestRepaint();
      KeyCode keyCode = Event.current.keyCode;
      if (Selection.Count == 0 || !Selection.Any(x => x._IsVisible()))
      {
        var source = DrawInSearchMode ? FlatMenuTree : EnumerateTree().Where(x => x._IsVisible());
        MenuItem menuItem = null;
        switch (keyCode)
        {
          case KeyCode.UpArrow:
            menuItem = source.LastOrDefault();
            break;
          case KeyCode.DownArrow:
            menuItem = source.FirstOrDefault();
            break;
          case KeyCode.RightAlt:
            menuItem = source.FirstOrDefault();
            break;
          case KeyCode.LeftAlt:
            menuItem = source.FirstOrDefault();
            break;
        }

        if (menuItem == null)
          return;

        menuItem.Select();
        Event.current.Use();
      }
      else
      {
        if (keyCode == KeyCode.LeftArrow && !DrawInSearchMode)
        {
          bool flag = true;
          foreach (MenuItem odinMenuItem1 in Selection.ToList())
          {
            if (odinMenuItem1.Toggled && odinMenuItem1.ChildMenuItems.Any())
            {
              flag = false;
              odinMenuItem1.Toggled = false;
            }

            if ((Event.current.modifiers & EventModifiers.Alt) == EventModifiers.None)
              continue;

            flag = false;
            foreach (MenuItem odinMenuItem2 in odinMenuItem1.GetChildMenuItemsRecursive(false))
              odinMenuItem2.Toggled = odinMenuItem1.Toggled;
          }

          if (flag)
            keyCode = KeyCode.UpArrow;
          Event.current.Use();
        }

        if (keyCode == KeyCode.RightArrow && !DrawInSearchMode)
        {
          bool flag = true;
          foreach (MenuItem odinMenuItem1 in Selection.ToList())
          {
            if (!odinMenuItem1.Toggled && odinMenuItem1.ChildMenuItems.Any())
            {
              odinMenuItem1.Toggled = true;
              flag = false;
            }

            if ((Event.current.modifiers & EventModifiers.Alt) == EventModifiers.None)
              continue;

            flag = false;
            foreach (MenuItem odinMenuItem2 in odinMenuItem1.GetChildMenuItemsRecursive(false))
              odinMenuItem2.Toggled = odinMenuItem1.Toggled;
          }

          if (flag)
            keyCode = KeyCode.DownArrow;
          Event.current.Use();
        }

        switch (keyCode)
        {
          case KeyCode.UpArrow when (Event.current.modifiers & EventModifiers.Shift) != EventModifiers.None:
          {
            MenuItem menuItem = Selection.Last();
            MenuItem prevVisualMenuItem = menuItem.PrevVisualMenuItem;
            if (prevVisualMenuItem != null)
            {
              if (prevVisualMenuItem.IsSelected)
                menuItem.Deselect();
              else
                prevVisualMenuItem.Select(true);
              Event.current.Use();
            }

            break;
          }

          case KeyCode.UpArrow:
          {
            MenuItem prevVisualMenuItem = Selection.Last().PrevVisualMenuItem;
            if (prevVisualMenuItem != null)
            {
              prevVisualMenuItem.Select();
              Event.current.Use();
            }

            break;
          }

          case KeyCode.DownArrow when (Event.current.modifiers & EventModifiers.Shift) != EventModifiers.None:
          {
            MenuItem menuItem = Selection.Last();
            MenuItem nextVisualMenuItem = menuItem.NextVisualMenuItem;
            if (nextVisualMenuItem != null)
            {
              if (nextVisualMenuItem.IsSelected)
                menuItem.Deselect();
              else
                nextVisualMenuItem.Select(true);
              Event.current.Use();
            }

            break;
          }

          case KeyCode.DownArrow:
          {
            MenuItem nextVisualMenuItem = Selection.Last().NextVisualMenuItem;
            if (nextVisualMenuItem != null)
            {
              nextVisualMenuItem.Select();
              Event.current.Use();
            }

            break;
          }

          case KeyCode.Return:
            Selection.ConfirmSelection();
            Event.current.Use();
            return;
        }
      }
    }
  }
}