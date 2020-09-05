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
    private bool isFirstFrame = true;
    private GUIFrameCounter frameCounter = new GUIFrameCounter();
    private bool hasRepaintedCurrentSearchResult = true;
    private EditorTimeHelper timeHelper = new EditorTimeHelper();
    public List<MenuItem> FlatMenuTree = new List<MenuItem>();
    private static bool preventAutoFocus;
    /// <summary>Gets the currently active menu tree.</summary>
    public static MenuTree ActiveMenuTree;
    private readonly MenuItem root;
    private readonly MenuTreeSelection selection;
    private MenuTreeDrawingConfig defaultConfig;
    private bool regainSearchFieldFocus;
    private bool hadSearchFieldFocus;
    private Rect outerScrollViewRect;
    private int hideScrollbarsWhileContentIsExpanding;
    private Rect innerScrollViewRect;
    private int forceRegainFocusCounter;
    private bool requestRepaint;
    private bool scollToCenter;
    private MenuItem scrollToWhenReady;
    private string searchFieldControlName;
    private bool isDirty;
    private bool updateSearchResults;
    private bool regainFocusWhenWindowFocus;
    private bool currWindowHasFocus;
    internal static Rect VisibleRect;
    internal static EditorTimeHelper CurrentEditorTimeHelper;
    internal static float CurrentEditorTimeHelperDeltaTime;
    internal static Event CurrentEvent;
    internal static EventType CurrentEventType;

    internal MenuItem Root => root;

    /// <summary>Gets the selection.</summary>
    public MenuTreeSelection Selection => selection;

    /// <summary>Gets the root menu items.</summary>
    public List<MenuItem> MenuItems => root.ChildMenuItems;

    /// <summary>
    /// If true, all indent levels will be ignored, and all menu items with IsVisible == true will be drawn.
    /// </summary>
    public bool DrawInSearchMode { get; private set; }

    /// <summary>
    /// Gets or sets the default menu item style from Config.DefaultStyle.
    /// </summary>
    public OdinMenuStyle DefaultMenuStyle
    {
      get => Config.DefaultMenuStyle;
      set => Config.DefaultMenuStyle = value;
    }

    /// <summary>Gets or sets the default drawing configuration.</summary>
    public MenuTreeDrawingConfig Config
    {
      get
      {
        MenuTreeDrawingConfig treeDrawingConfig = defaultConfig ?? new MenuTreeDrawingConfig
        {
          DrawScrollView = true,
          DrawSearchToolbar = false,
          AutoHandleKeyboardNavigation = false
        };

        defaultConfig = treeDrawingConfig;
        return defaultConfig;
      }
      set
      {
        defaultConfig = value;
      }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Sirenix.OdinInspector.Editor.OdinMenuTree" /> class.
    /// </summary>
    /// <param name="supportsMultiSelect">if set to <c>true</c> [supports multi select].</param>
    public MenuTree(bool supportsMultiSelect)
      : this(supportsMultiSelect, new OdinMenuStyle())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Sirenix.OdinInspector.Editor.OdinMenuTree" /> class.
    /// </summary>
    /// <param name="supportsMultiSelect">if set to <c>true</c> [supports multi select].</param>
    /// <param name="defaultMenuStyle">The default menu item style.</param>
    private MenuTree(bool supportsMultiSelect, OdinMenuStyle defaultMenuStyle)
    {
      DefaultMenuStyle = defaultMenuStyle;
      selection = new MenuTreeSelection(supportsMultiSelect);
      root = new MenuItem(this, nameof(root), null);
      SetupAutoScroll();
      searchFieldControlName = Guid.NewGuid().ToString();
    }

    private void SetupAutoScroll()
    {
      selection.SelectionChanged += (Action<SelectionChangedType>) (x =>
      {
        if (!Config.AutoScrollOnSelectionChanged || x != SelectionChangedType.ItemAdded)
          return;
        requestRepaint = true;
        GUIHelper.RequestRepaint();
        if (isFirstFrame)
          ScrollToMenuItem(selection.LastOrDefault(), true);
        else
          ScrollToMenuItem(selection.LastOrDefault());
      });
    }

    /// <summary>Scrolls to the specified menu item.</summary>
    private void ScrollToMenuItem(MenuItem menuItem, bool centerMenuItem = false)
    {
      if (menuItem == null)
        return;
      scollToCenter = centerMenuItem;
      scrollToWhenReady = menuItem;
      if (!menuItem._IsVisible())
      {
        foreach (MenuItem odinMenuItem in menuItem.GetParentMenuItemsRecursive(false))
          odinMenuItem.Toggled = true;
      }
      else
      {
        foreach (MenuItem odinMenuItem in menuItem.GetParentMenuItemsRecursive(false))
          odinMenuItem.Toggled = true;
        if (outerScrollViewRect.height == 0.0 || (menuItem.Rect.height <= 0.00999999977648258 || Event.current == null || Event.current.type != EventType.Repaint))
          return;
        MenuTreeDrawingConfig config = Config;
        Rect rect1 = menuItem.Rect;
        float num1;
        float num2;
        if (centerMenuItem)
        {
          Rect rect2 = outerScrollViewRect.AlignCenterY(rect1.height);
          num1 = rect1.yMin - (innerScrollViewRect.y + config.ScrollPos.y - rect2.y);
          num2 = (float) (rect1.yMax - (double) rect2.height + innerScrollViewRect.y - (config.ScrollPos.y + (double) rect2.y));
        }
        else
        {
          outerScrollViewRect.y = 0.0f;
          float num3 = (float) (rect1.yMin - (innerScrollViewRect.y + (double) config.ScrollPos.y) - 1.0);
          float num4 = rect1.yMax - outerScrollViewRect.height + innerScrollViewRect.y - config.ScrollPos.y;
          num1 = num3 - rect1.height;
          num2 = num4 + rect1.height;
        }
        if (num1 < 0.0)
          config.ScrollPos.y += num1;
        if (num2 > 0.0)
          config.ScrollPos.y += num2;
        if (frameCounter.FrameCount > 6)
          scrollToWhenReady = null;
        else
          GUIHelper.RequestRepaint();
      }
    }

    /// <summary>Enumerates the tree with a DFS.</summary>
    /// <param name="includeRootNode">if set to <c>true</c> then the invisible root menu item is included.</param>
    public IEnumerable<MenuItem> EnumerateTree(bool includeRootNode = false)
    {
      return root.GetChildMenuItemsRecursive(includeRootNode);
    }

    /// <summary>Enumerates the tree with a DFS.</summary>
    public void EnumerateTree(Action<MenuItem> action)
    {
      root.GetChildMenuItemsRecursive(false).ForEach(action);
    }

    /// <summary>Draws the menu tree recursively.</summary>
    public void DrawMenuTree()
    {
      EditorTimeHelper time = EditorTimeHelper.Time;
      EditorTimeHelper.Time = timeHelper;
      EditorTimeHelper.Time.Update();
      try
      {
        timeHelper.Update();
        frameCounter.Update();
        MenuTreeDrawingConfig config = Config;
        if (requestRepaint)
        {
          GUIHelper.RequestRepaint();
          requestRepaint = false;
        }
        if (config.DrawSearchToolbar)
          DrawSearchToolbar();
        Rect outerRect = EditorGUILayout.BeginVertical();
        HandleActiveMenuTreeState(outerRect);
        if (config.DrawScrollView)
        {
          if (Event.current.type == EventType.Repaint)
            outerScrollViewRect = outerRect;
          config.ScrollPos = hideScrollbarsWhileContentIsExpanding <= 0 ? EditorGUILayout.BeginScrollView(config.ScrollPos, GUILayoutOptions.ExpandHeight(false)) : EditorGUILayout.BeginScrollView(config.ScrollPos, GUIStyle.none, GUIStyle.none, GUILayoutOptions.ExpandHeight(false));
          Rect rect = EditorGUILayout.BeginVertical();
          if (innerScrollViewRect.height == 0.0 || Event.current.type == EventType.Repaint)
          {
            float num = Mathf.Abs(innerScrollViewRect.height - rect.height);
            float f = Mathf.Abs(innerScrollViewRect.height - outerScrollViewRect.height);
            if (innerScrollViewRect.height - 40.0 <= outerScrollViewRect.height && num > 0.0)
            {
              hideScrollbarsWhileContentIsExpanding = 5;
              GUIHelper.RequestRepaint();
            }
            else if (Mathf.Abs(f) < 1.0)
            {
              hideScrollbarsWhileContentIsExpanding = 5;
            }
            else
            {
              --hideScrollbarsWhileContentIsExpanding;
              if (hideScrollbarsWhileContentIsExpanding < 0)
                hideScrollbarsWhileContentIsExpanding = 0;
              else
                GUIHelper.RequestRepaint();
            }
            innerScrollViewRect = rect;
          }
          GUILayout.Space(-1f);
        }
        if (isDirty && Event.current.type == EventType.Layout)
        {
          UpdateMenuTree();
          isDirty = false;
        }
        VisibleRect = GUIClipInfo.VisibleRect.Expand(300f);
        CurrentEvent = Event.current;
        CurrentEventType = CurrentEvent.type;
        CurrentEditorTimeHelper = EditorTimeHelper.Time;
        CurrentEditorTimeHelperDeltaTime = CurrentEditorTimeHelper.DeltaTime;
        List<MenuItem> odinMenuItemList = DrawInSearchMode ? FlatMenuTree : MenuItems;
        int count = odinMenuItemList.Count;
        if (config.EXPERIMENTAL_INTERNAL_DrawFlatTreeFastNoLayout)
        {
          int height = DefaultMenuStyle.Height;
          Rect rect = GUILayoutUtility.GetRect(0.0f, count * height);
          rect.height = height;
          for (int index = 0; index < count; ++index)
          {
            MenuItem menuItem = odinMenuItemList[index];
            menuItem.EXPERIMENTAL_DontAllocateNewRect = true;
            menuItem.rect = rect;
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
        if (scrollToWhenReady != null)
          ScrollToMenuItem(scrollToWhenReady, scollToCenter);
        if (Event.current.type != EventType.Repaint)
          return;
        isFirstFrame = false;
      }
      finally
      {
        EditorTimeHelper.Time = time;
      }
    }

    private void HandleActiveMenuTreeState(Rect outerRect)
    {
      if (Event.current.type == EventType.Repaint)
      {
        if (currWindowHasFocus != GUIHelper.CurrentWindowHasFocus)
        {
          currWindowHasFocus = GUIHelper.CurrentWindowHasFocus;
          if (currWindowHasFocus && regainFocusWhenWindowFocus)
          {
            if (!preventAutoFocus)
              ActiveMenuTree = this;
            regainFocusWhenWindowFocus = false;
          }
        }
        if (!currWindowHasFocus && ActiveMenuTree == this)
          ActiveMenuTree = null;
        if (currWindowHasFocus)
          regainFocusWhenWindowFocus = ActiveMenuTree == this;
        if (currWindowHasFocus && ActiveMenuTree == null)
          ActiveMenuTree = this;
      }
      MenuTreeActivationZone(outerRect);
    }

    private void MenuTreeActivationZone(Rect rect)
    {
      if (ActiveMenuTree == this || Event.current.rawType != EventType.MouseDown || (!rect.Contains(Event.current.mousePosition) || !GUIHelper.CurrentWindowHasFocus))
        return;
      regainSearchFieldFocus = true;
      preventAutoFocus = true;
      ActiveMenuTree = this;
      UnityEditorEventUtility.EditorApplication_delayCall += (Action) (() => preventAutoFocus = false);
      GUIHelper.RequestRepaint();
    }

    /// <summary>
    /// Marks the dirty. This will cause a tree.UpdateTree() in the beginning of the next Layout frame.
    /// </summary>
    public void MarkDirty()
    {
      isDirty = true;
      updateSearchResults = true;
    }

    /// <summary>Draws the search toolbar.</summary>
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
      if ((EditorGUI.EndChangeCheck() || updateSearchResults) && hasRepaintedCurrentSearchResult)
      {
        updateSearchResults = false;
        hasRepaintedCurrentSearchResult = false;
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
              int score;
              bool flag = FuzzySearch.Contains(Config.SearchTerm, x.SearchString, out score);
              return new
              {
                score,
                item = x,
                include = flag
              };
            }).Where(x => x.include).OrderByDescending(x => x.score).Select(x => x.item));
          }

          root.UpdateFlatMenuItemNavigation();
        }
        else
        {
          DrawInSearchMode = false;
          FlatMenuTree.Clear();
          MenuItem menuItem = selection.LastOrDefault();
          UpdateMenuTree();
          Selection.SelectMany(x => x.GetParentMenuItemsRecursive(false)).ForEach(x => x.Toggled = true);
          if (menuItem != null)
            ScrollToMenuItem(menuItem);
          root.UpdateFlatMenuItemNavigation();
        }
      }

      if (Event.current.type != EventType.Repaint)
        return;
      hasRepaintedCurrentSearchResult = true;
    }

    private string DrawSearchField(Rect rect, string searchTerm, bool autoFocus)
    {
      bool flag1 = GUI.GetNameOfFocusedControl() == searchFieldControlName;
      if (hadSearchFieldFocus != flag1)
      {
        if (flag1)
          ActiveMenuTree = this;
        hadSearchFieldFocus = flag1;
      }

      bool flag2 = flag1 && (Event.current.keyCode == KeyCode.DownArrow || Event.current.keyCode == KeyCode.UpArrow || (Event.current.keyCode == KeyCode.LeftArrow || Event.current.keyCode == KeyCode.RightArrow) || Event.current.keyCode == KeyCode.Return);
      if (flag2)
        GUIHelper.PushEventType(Event.current.type);
      searchTerm = SirenixEditorGUI.SearchField(rect, searchTerm, autoFocus && regainSearchFieldFocus && ActiveMenuTree == this, searchFieldControlName);
      if (regainSearchFieldFocus && Event.current.type == EventType.Layout)
        regainSearchFieldFocus = false;
      if (flag2)
      {
        GUIHelper.PopEventType();
        if (ActiveMenuTree == this)
          regainSearchFieldFocus = true;
      }

      if (forceRegainFocusCounter >= 20)
        return searchTerm;

      if (autoFocus && forceRegainFocusCounter < 4 && ActiveMenuTree == this)
        regainSearchFieldFocus = true;
      GUIHelper.RequestRepaint();
      HandleUtility.Repaint();
      if (Event.current.type == EventType.Repaint)
        ++forceRegainFocusCounter;
      return searchTerm;
    }

    /// <summary>
    /// Updates the menu tree. This method is usually called automatically when needed.
    /// </summary>
    public void UpdateMenuTree()
    {
      root.UpdateMenuTreeRecursive(true);
      root.UpdateFlatMenuItemNavigation();
    }

    /// <summary>
    /// Handles the keybaord menu navigation. Call this at the end of your GUI scope, to prevent the menu tree from stealing input events from other text fields.
    /// </summary>
    /// <returns>Returns true, if anything was changed via the keyboard.</returns>
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

    IEnumerator IEnumerable.GetEnumerator()
    {
      return MenuItems.GetEnumerator();
    }
  }
}