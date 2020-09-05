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
    private static readonly Color mouseOverColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.028f) : new Color(1f, 1f, 1f, 0.3f);
    private static bool previousMenuItemWasSelected;
    private bool isVisible = true;
    private float t = -1f;
    private static MenuItem handleClickEventOnMouseUp;
    private List<MenuItem> childMenuItems;
    private int flatTreeIndex;
    private Func<Texture> iconGetter;
    private bool isInitialized;
    private LocalPersistentContext<bool> isToggledContext;
    private MenuTree menuTree;
    private string prevName;
    private string name;
    private MenuItem nextMenuItem;
    private MenuItem nextMenuItemFlat;
    private MenuItem parentMenuItem;
    private MenuItem previousMenuItem;
    private MenuItem previousMenuItemFlat;
    private OdinMenuStyle style;
    private Rect triangleRect;
    private Rect labelRect;
    private StringMemberHelper nameValueGetter;
    private bool? nonCachedToggledState;
    internal Rect rect;
    internal bool EXPERIMENTAL_DontAllocateNewRect;
    public bool MenuItemIsBeingRendered;
    /// <summary>The default toggled state</summary>
    public bool DefaultToggledState;
    /// <summary>
    /// Occurs right after the menu item is done drawing, and right before mouse input is handles so you can take control of that.
    /// </summary>
    public Action<MenuItem> OnDrawItem;
    /// <summary>Occurs when the user has right-clicked the menu item.</summary>
    public Action<MenuItem> OnRightClick;
    private bool wasMouseDownEvent;
    private static int mouseDownClickCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Sirenix.OdinInspector.Editor.OdinMenuItem" /> class.
    /// </summary>
    /// <param name="tree">The Odin menu tree instance the menu item belongs to.</param>
    /// <param name="name">The name of the menu item.</param>
    /// <param name="value">The instance the value item represents.</param>
    public MenuItem(MenuTree tree, string name, object value)
    {
      if (tree == null)
        throw new ArgumentNullException(nameof(tree));
      if (name == null)
        throw new ArgumentNullException(nameof (name));
      menuTree = tree;
      this.name = name;
      SearchString = name;
      Value = value;
      childMenuItems = new List<MenuItem>();
    }

    /// <summary>Gets the child menu items.</summary>
    /// <value>The child menu items.</value>
    public List<MenuItem> ChildMenuItems => childMenuItems;

    /// <summary>Gets the index location of the menu item.</summary>
    private int FlatTreeIndex => flatTreeIndex;

    /// <summary>
    /// Gets or sets the icon that is used when the menu item is not selected.
    /// </summary>
    public Texture Icon { get; set; }

    /// <summary>
    /// Gets or sets the icon that is used when the menu item is selected.
    /// </summary>
    public Texture IconSelected { get; set; }

    /// <summary>
    /// Gets a value indicating whether this instance is selected.
    /// </summary>
    public bool IsSelected => menuTree.Selection.Contains(this);

    /// <summary>Gets the menu tree instance.</summary>
    public MenuTree MenuTree => menuTree;

    /// <summary>Gets or sets the raw menu item name.</summary>
    public string Name => name;

    /// <summary>
    /// Gets or sets the search string used when searching for menu items.
    /// </summary>
    public string SearchString { get; }

    /// <summary>Gets the next visual menu item.</summary>
    public MenuItem NextVisualMenuItem
    {
      get
      {
        EnsureInitialized();
        if (MenuTree.DrawInSearchMode)
          return nextMenuItemFlat;
        return ChildMenuItems.Count > 0 && nextMenuItem != null && (!Toggled && _IsVisible()) ? nextMenuItem : GetAllNextMenuItems().FirstOrDefault(x => x._IsVisible());
      }
    }

    /// <summary>Gets the parent menu item.</summary>
    private MenuItem Parent
    {
      get
      {
        EnsureInitialized();
        return parentMenuItem;
      }
    }

    /// <summary>Gets the previous visual menu item.</summary>
    public MenuItem PrevVisualMenuItem
    {
      get
      {
        EnsureInitialized();
        if (MenuTree.DrawInSearchMode)
          return previousMenuItemFlat;
        if (ChildMenuItems.Count > 0 && !Toggled && _IsVisible())
        {
          if (previousMenuItem != null)
          {
            if (previousMenuItem.ChildMenuItems.Count == 0 || !previousMenuItem.Toggled)
              return previousMenuItem;
          }
          else if (parentMenuItem != null)
          {
            return parentMenuItem;
          }
        }
        return GetAllPreviousMenuItems().FirstOrDefault(x => x._IsVisible());
      }
    }

    /// <summary>Gets the drawn rect.</summary>
    public Rect Rect => rect;

    /// <summary>
    /// Gets or sets the style. If null is specified, then the menu trees DefaultMenuStyle is used.
    /// </summary>
    private OdinMenuStyle Style => style ?? (style = menuTree.DefaultMenuStyle);

    /// <summary>Deselects this instance.</summary>
    public void Deselect()
    {
      menuTree.Selection.Remove(this);
    }

    /// <summary>Selects the specified add to selection.</summary>
    public void Select(bool addToSelection = false)
    {
      if (!addToSelection)
        menuTree.Selection.Clear();
      menuTree.Selection.Add(this);
    }

    /// <summary>Gets the child menu items recursive in a DFS.</summary>
    /// <param name="includeSelf">Whether to include it self in the collection.</param>
    public IEnumerable<MenuItem> GetChildMenuItemsRecursive(
      bool includeSelf)
    {
      MenuItem menuItem1 = this;
      if (includeSelf)
        yield return menuItem1;
      foreach (MenuItem odinMenuItem2 in menuItem1.ChildMenuItems.SelectMany(x => x.GetChildMenuItemsRecursive(true)))
        yield return odinMenuItem2;
    }

    /// <summary>Gets the child menu items recursive in a DFS.</summary>
    /// <param name="includeSelf">Whether to include it self in the collection.</param>
    /// <param name="includeRoot">Whether to include the root.</param>
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

    /// <summary>Gets the full menu item path.</summary>
    private string GetFullPath()
    {
      EnsureInitialized();
      MenuItem parent = Parent;
      return parent == null ? SmartName : parent.GetFullPath() + "/" + SmartName;
    }

    /// <summary>Gets or sets the value the menu item represents.</summary>
    public object Value { get; }

    /// <summary>
    /// Gets a nice menu item name. If the raw name value is null or a dollar sign, then the name is retrieved from the object itself.
    /// </summary>
    public string SmartName
    {
      get
      {
        object instance = Value;
        if (Value is Func<object> func)
          instance = func();
        if (name == null || name == "$")
        {
          if (instance == null)
            return string.Empty;
          Object @object = instance as Object;
          return (bool) @object ? @object.name.SplitPascalCase() : instance.ToString();
        }
        bool flag = false;
        if (nameValueGetter == null)
        {
          flag = true;
        }
        else if (prevName != name)
        {
          flag = true;
          prevName = name;
        }
        else if (nameValueGetter != null && instance != null && nameValueGetter.ObjectType != instance.GetType())
        {
          flag = true;
        }
        if (instance == null)
          nameValueGetter = null;
        else if (flag)
          nameValueGetter = new StringMemberHelper(instance.GetType(), false, name);
        return nameValueGetter != null ? nameValueGetter.ForceGetString(instance) : name;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="T:Sirenix.OdinInspector.Editor.OdinMenuItem" /> is toggled / expanded. This value tries it best to be persistent.
    /// </summary>
    public bool Toggled
    {
      get
      {
        if (childMenuItems.Count == 0)
          return false;
        if (menuTree.Config.UseCachedExpandedStates)
        {
          if (isToggledContext == null)
            isToggledContext = LocalPersistentContext<bool>.Create(PersistentContext.Get("[OdinMenuItem]" + GetFullPath(), DefaultToggledState));
          return isToggledContext.Value;
        }

        if (!nonCachedToggledState.HasValue)
          nonCachedToggledState = DefaultToggledState;
        return nonCachedToggledState.Value;
      }
      set
      {
        if (menuTree.Config.UseCachedExpandedStates)
        {
          if (isToggledContext == null)
            isToggledContext = LocalPersistentContext<bool>.Create(PersistentContext.Get("[OdinMenuItem]" + GetFullPath(), DefaultToggledState));
          isToggledContext.Value = value;
        }
        else
        {
          nonCachedToggledState = value;
        }
      }
    }

    /// <summary>Gets or sets the icon getter.</summary>
    public Func<Texture> IconGetter
    {
      get { return iconGetter ?? (iconGetter = () => !IsSelected || !(bool) IconSelected ? Icon : IconSelected); }
      set => iconGetter = value;
    }

    /// <summary>
    /// Draws this menu item followed by all of its child menu items
    /// </summary>
    /// <param name="indentLevel">The indent level.</param>
    public void DrawMenuItems(int indentLevel)
    {
      DrawMenuItem(indentLevel);
      List<MenuItem> childMenuItems = ChildMenuItems;
      int count = childMenuItems.Count;
      if (count == 0)
        return;
      bool toggled = Toggled;
      if (t < 0.0)
        t = toggled ? 1f : 0.0f;
      if (MenuTree.CurrentEventType == EventType.Layout)
        t = Mathf.MoveTowards(t, toggled ? 1f : 0.0f, MenuTree.CurrentEditorTimeHelperDeltaTime * (1f / SirenixEditorGUI.DefaultFadeGroupDuration));
      if (SirenixEditorGUI.BeginFadeGroup(t))
      {
        for (int index = 0; index < count; ++index)
          childMenuItems[index].DrawMenuItems(indentLevel + 1);
      }
      SirenixEditorGUI.EndFadeGroup();
    }

    /// <summary>Draws the menu item with the specified indent level.</summary>
    public void DrawMenuItem(int indentLevel)
    {
      Rect rect1 = EXPERIMENTAL_DontAllocateNewRect ? rect : GUILayoutUtility.GetRect(0.0f, Style.Height);
      Event currentEvent = MenuTree.CurrentEvent;
      EventType currentEventType = MenuTree.CurrentEventType;
      if (currentEventType == EventType.Layout)
        return;
      if (currentEventType == EventType.Repaint || (currentEventType != EventType.Layout && rect.width == 0.0))
        rect = rect1;
      float y1 = rect.y;
      if (y1 > 1000.0)
      {
        float y2 = MenuTree.VisibleRect.y;
        if (y1 + (double) rect.height < y2 || y1 > y2 + (double) MenuTree.VisibleRect.height)
        {
          MenuItemIsBeingRendered = false;
          return;
        }
      }
      MenuItemIsBeingRendered = true;
      if (currentEventType == EventType.Repaint)
      {
        labelRect = rect.AddXMin(Style.Offset + indentLevel * Style.IndentAmount);
        bool isSelected = IsSelected;
        if (isSelected)
        {
          if (MenuTree.ActiveMenuTree == menuTree)
          {
            EditorGUI.DrawRect(
                rect,
                EditorGUIUtility.isProSkin ? Style.SelectedColorDarkSkin : Style.SelectedColorLightSkin);
          }
          else if (EditorGUIUtility.isProSkin)
          {
            EditorGUI.DrawRect(rect, Style.SelectedInactiveColorDarkSkin);
          }
          else
          {
            EditorGUI.DrawRect(rect, Style.SelectedInactiveColorLightSkin);
          }
        }

        if (!isSelected && rect.Contains(currentEvent.mousePosition))
          EditorGUI.DrawRect(rect, mouseOverColor);
        if (ChildMenuItems.Count > 0 && !MenuTree.DrawInSearchMode && Style.DrawFoldoutTriangle)
        {
          EditorIcon editorIcon = Toggled ? EditorIcons.TriangleDown : EditorIcons.TriangleRight;
          if (Style.AlignTriangleLeft)
          {
            triangleRect = labelRect.AlignLeft(Style.TriangleSize).AlignMiddle(Style.TriangleSize);
            triangleRect.x -= Style.TriangleSize - Style.TrianglePadding;
          }
          else
          {
            triangleRect = rect.AlignRight(Style.TriangleSize).AlignMiddle(Style.TriangleSize);
            triangleRect.x -= Style.TrianglePadding;
          }

          if (currentEventType == EventType.Repaint)
          {
            if (EditorGUIUtility.isProSkin)
            {
              if (isSelected || triangleRect.Contains(currentEvent.mousePosition))
                GUI.DrawTexture(triangleRect, editorIcon.Highlighted);
              else
                GUI.DrawTexture(triangleRect, editorIcon.Active);
            }
            else if (isSelected)
            {
              GUI.DrawTexture(triangleRect, editorIcon.Raw);
            }
            else if (triangleRect.Contains(currentEvent.mousePosition))
            {
              GUI.DrawTexture(triangleRect, editorIcon.Active);
            }
            else
            {
              GUIHelper.PushColor(new Color(1f, 1f, 1f, 0.7f));
              GUI.DrawTexture(triangleRect, editorIcon.Active);
              GUIHelper.PopColor();
            }
          }
        }

        Texture image = IconGetter();
        if ((bool) image)
        {
          Rect position = labelRect.AlignLeft(Style.IconSize).AlignMiddle(Style.IconSize);
          position.x += Style.IconOffset;
          if (!isSelected)
            GUIHelper.PushColor(new Color(1f, 1f, 1f, Style.NotSelectedIconAlpha));
          GUI.DrawTexture(position, image, ScaleMode.ScaleToFit);
          labelRect.xMin += Style.IconSize + Style.IconPadding;
          if (!isSelected)
            GUIHelper.PopColor();
        }
        GUIStyle style = isSelected ? Style.SelectedLabelStyle : Style.DefaultLabelStyle;
        labelRect = labelRect.AlignMiddle(16f).AddY(Style.LabelVerticalOffset);
        GUI.Label(labelRect, SmartName, style);
        if (Style.Borders)
        {
          float num = Style.BorderPadding;
          bool flag = true;
          if (isSelected || previousMenuItemWasSelected)
          {
            num = 0.0f;
            if (!EditorGUIUtility.isProSkin)
              flag = false;
          }

          previousMenuItemWasSelected = isSelected;
          if (flag)
          {
            Rect rect2 = rect;
            rect2.x += num;
            rect2.width -= num * 2f;
            SirenixEditorGUI.DrawHorizontalLineSeperator(rect2.x, rect2.y, rect2.width, Style.BorderAlpha);
          }
        }
      }

      wasMouseDownEvent = currentEventType == EventType.MouseDown && rect.Contains(currentEvent.mousePosition);
      if (wasMouseDownEvent)
        handleClickEventOnMouseUp = this;
      OnDrawItem?.Invoke(this);
      HandleMouseEvents(rect, triangleRect);
    }

    internal void UpdateMenuTreeRecursive(bool isRoot = false)
    {
      isInitialized = true;
      MenuItem menuItem = null;
      foreach (MenuItem childMenuItem in ChildMenuItems)
      {
        childMenuItem.parentMenuItem = null;
        childMenuItem.nextMenuItem = null;
        childMenuItem.previousMenuItemFlat = null;
        childMenuItem.nextMenuItemFlat = null;
        childMenuItem.previousMenuItem = null;
        if (!isRoot)
          childMenuItem.parentMenuItem = this;
        if (menuItem != null)
        {
          menuItem.nextMenuItem = childMenuItem;
          childMenuItem.previousMenuItem = menuItem;
        }

        menuItem = childMenuItem;
        childMenuItem.UpdateMenuTreeRecursive();
      }
    }

    internal void UpdateFlatMenuItemNavigation()
    {
      int num = 0;
      MenuItem menuItem1 = null;
      foreach (MenuItem odinMenuItem2 in menuTree.DrawInSearchMode ? (IEnumerable<MenuItem>) menuTree.FlatMenuTree : menuTree.EnumerateTree())
      {
        odinMenuItem2.flatTreeIndex = num++;
        odinMenuItem2.nextMenuItemFlat = null;
        odinMenuItem2.previousMenuItemFlat = null;
        if (menuItem1 != null)
        {
          odinMenuItem2.previousMenuItemFlat = menuItem1;
          menuItem1.nextMenuItemFlat = odinMenuItem2;
        }
        menuItem1 = odinMenuItem2;
      }
    }

    /// <summary>Handles the mouse events.</summary>
    /// <param name="rect">The rect.</param>
    /// <param name="triangleRect">The triangle rect.</param>
    private void HandleMouseEvents(Rect rect, Rect triangleRect)
    {
      EventType type = Event.current.type;
      if (type == EventType.Used && wasMouseDownEvent)
      {
        wasMouseDownEvent = false;
        handleClickEventOnMouseUp = this;
      }

      int num1;
      switch (type)
      {
        case EventType.MouseDown:
          num1 = 1;
          break;
        case EventType.MouseUp:
          num1 = handleClickEventOnMouseUp == this ? 1 : 0;
          break;
        default:
          num1 = 0;
          break;
      }

      if (num1 == 0)
        return;
      handleClickEventOnMouseUp = null;
      wasMouseDownEvent = false;
      if (!rect.Contains(Event.current.mousePosition))
        return;
      bool flag1 = ChildMenuItems.Any();
      bool isSelected = IsSelected;
      switch (Event.current.button)
      {
        case 1 when OnRightClick != null:
          OnRightClick(this);
          break;
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
              Toggled = flag3;
          }
          else if (menuTree.Selection.SupportsMultiSelect && Event.current.modifiers == EventModifiers.Shift && menuTree.Selection.Count > 0)
          {
            MenuItem menuItem = menuTree.Selection.First();
            int num2 = Mathf.Abs(menuItem.FlatTreeIndex - FlatTreeIndex) + 1;
            bool flag3 = menuItem.FlatTreeIndex < FlatTreeIndex;
            menuTree.Selection.Clear();
            for (int index = 0; index < num2 && menuItem != null; ++index)
            {
              menuItem.Select(true);
              if (menuItem != this)
                menuItem = flag3 ? menuItem.NextVisualMenuItem : menuItem.PrevVisualMenuItem;
              else
                break;
            }
          }
          else
          {
            bool addToSelection = Event.current.modifiers == EventModifiers.Control;
            if (addToSelection & isSelected && MenuTree.Selection.SupportsMultiSelect)
              Deselect();
            else
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

    internal bool _IsVisible()
    {
      return menuTree.DrawInSearchMode ? menuTree.FlatMenuTree.Contains(this) : ParentMenuItemsBottomUp(false).All(x => x.Toggled);
    }

    private IEnumerable<MenuItem> GetAllNextMenuItems()
    {
      if (nextMenuItemFlat != null)
      {
        yield return nextMenuItemFlat;
        foreach (MenuItem allNextMenuItem in nextMenuItemFlat.GetAllNextMenuItems())
          yield return allNextMenuItem;
      }
    }

    private IEnumerable<MenuItem> GetAllPreviousMenuItems()
    {
      if (previousMenuItemFlat == null)
        yield break;

      yield return previousMenuItemFlat;
      foreach (MenuItem previousMenuItem in previousMenuItemFlat.GetAllPreviousMenuItems())
        yield return previousMenuItem;
    }

    private IEnumerable<MenuItem> ParentMenuItemsBottomUp(
      bool includeSelf = true)
    {
      MenuItem menuItem1 = this;
      if (menuItem1.parentMenuItem != null)
      {
        foreach (MenuItem odinMenuItem2 in menuItem1.parentMenuItem.ParentMenuItemsBottomUp())
          yield return odinMenuItem2;
      }

      if (includeSelf)
        yield return menuItem1;
    }

    private void EnsureInitialized()
    {
      if (isInitialized)
        return;
      menuTree.UpdateMenuTree();
      if (isInitialized)
        return;
      Debug.LogWarning("Could not initialize menu item. Is the menu item not part of a menu tree?");
    }
  }
}