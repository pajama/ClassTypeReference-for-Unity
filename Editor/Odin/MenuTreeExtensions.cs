namespace TypeReferences.Editor.Odin
{
  using System.Collections.Generic;
  using Sirenix.OdinInspector;
  using Object = UnityEngine.Object;

  public static class MenuTreeExtensions
  {
    private static readonly List<MenuItem> Cache = new List<MenuItem>(5);

    /// <summary>
    /// Adds the menu item at the specified menu item path and populates the result list with all menu items created in order to add the menuItem at the specified path.
    /// </summary>
    /// <param name="tree">The tree instance.</param>
    /// <param name="result">The result list.</param>
    /// <param name="path">The menu item path.</param>
    /// <param name="menuItem">The menu item.</param>
    private static void AddMenuItemAtPath(
      this MenuTree tree,
      ICollection<MenuItem> result,
      string path,
      MenuItem menuItem)
    {
      MenuItem menuItem1 = tree.Root;
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
            menuItem2 = new MenuItem(tree, name, null);
            menuItem1.ChildMenuItems.Add(menuItem2);
          }

          result.Add(menuItem2);
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
      result.Add(menuItem);
    }

    /// <summary>
    /// Adds the menu item at specified menu item path, and returns all menu items created in order to add the menuItem at the specified path.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="path">The menu item path.</param>
    /// <param name="menuItem">The menu item.</param>
    /// <returns>Returns all menu items created in order to add the menu item at the specified menu item path.</returns>
    private static IEnumerable<MenuItem> AddMenuItemAtPath(
      this MenuTree tree,
      string path,
      MenuItem menuItem)
    {
      Cache.Clear();
      tree.AddMenuItemAtPath(Cache, path, menuItem);
      return Cache;
    }

    /// <summary>
    /// Adds the specified object at the specified menu item path and returns all menu items created in order to end up at the specified menu path.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="menuPath">The menu path.</param>
    /// <param name="instance">The object instance.</param>
    /// <param name="forceShowOdinSerializedMembers">Set this to true if you want Odin serialzied members such as dictionaries and generics to be shown as well.</param>
    /// <returns>Returns all menu items created in order to add the menu item at the specified menu item path.</returns>
    public static IEnumerable<MenuItem> AddObjectAtPath(
      this MenuTree tree,
      string menuPath,
      object instance,
      bool forceShowOdinSerializedMembers = false)
    {
      string name;
      SplitMenuPath(menuPath, out menuPath, out name);
      return forceShowOdinSerializedMembers && !(bool) (instance as Object) ? tree.AddMenuItemAtPath(menuPath, new MenuItem(tree, name, new SerializedValueWrapper(instance))) : tree.AddMenuItemAtPath(menuPath, new MenuItem(tree, name, instance));
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

    [ShowOdinSerializedPropertiesInInspector]
    private class SerializedValueWrapper
    {
      private readonly object _instance;

      [HideLabel]
      [ShowInInspector]
      [HideReferenceObjectPicker]
      public object Instance
      {
        get => _instance;
        set
        {
        }
      }

      public SerializedValueWrapper(object obj)
      {
        _instance = obj;
      }
    }
  }
}