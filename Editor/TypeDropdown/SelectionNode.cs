namespace TypeReferences.Editor.TypeDropdown
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Sirenix.Utilities.Editor;
  using Test.Editor.OdinAttributeDrawers;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.Assertions;
  using EditorIcon = Test.Editor.OdinAttributeDrawers.EditorIcon;
  using EditorIcons = Test.Editor.OdinAttributeDrawers.EditorIcons;

  [Serializable]
  public class SelectionNode
  {
    public readonly List<SelectionNode> ChildNodes = new List<SelectionNode>();
    public readonly string Name;
    public readonly Type Type;

    private static SelectionNode _handleClickEventOnMouseUp;

    private readonly SelectionTree _parentTree;
    private readonly SelectionNode _parentNode;

    private bool _expanded;
    private Rect _rect;
    private bool _wasMouseDownEvent;

    /// <summary>
    /// Default constructor that creates a child node of another parent node.
    /// </summary>
    /// <param name="name">Name that will show up in the popup.</param>
    /// <param name="parentNode">Parent node of this node.</param>
    /// <param name="parentTree">The tree this node belongs to.</param>
    /// <param name="type"><see cref="System.Type"/>> this node represents.</param>
    /// <param name="fullTypeName">
    /// Full name of the type. It will show up instead of the short name when performing search.
    /// </param>
    private SelectionNode(string name, SelectionNode parentNode, SelectionTree parentTree, Type type, string fullTypeName)
    {
      Assert.IsNotNull(name);

      Name = name;
      _parentNode = parentNode;
      _parentTree = parentTree;
      Type = type;
      FullTypeName = fullTypeName;
    }

    /// <summary>Constructor of a root node that does not have a parent and does not show up in the popup.</summary>
    /// <param name="parentTree">The tree this node belongs to.</param>
    private SelectionNode(SelectionTree parentTree)
    {
      _parentNode = null;
      _parentTree = parentTree;
      Name = string.Empty;
      Type = null;
      FullTypeName = null;
    }

    /// <summary>Creates a root node that does not have a parent and does not show up in the popup.</summary>
    /// <param name="parentTree">The tree this node belongs to.</param>
    /// <returns>The root node.</returns>
    public static SelectionNode CreateRoot(SelectionTree parentTree)
    {
      return new SelectionNode(parentTree);
    }

    /// <summary>Creates a dropdown item that represents a <see cref="System.Type"/>.</summary>
    /// <param name="name">Name that will show up in the popup.</param>
    /// <param name="type"><see cref="System.Type"/>> this node represents.</param>
    /// <param name="fullTypeName">
    /// Full name of the type. It will show up instead of the short name when performing search.
    /// </param>
    /// <returns>A <see cref="SelectionNode"/> instance that represents the dropdown item.</returns>
    public SelectionNode CreateChildItem(string name, Type type, string fullTypeName)
    {
      var child = new SelectionNode(name, this, _parentTree, type, fullTypeName);
      AddChild(child);
      return child;
    }

    /// <summary>Creates a folder that contains dropdown items.</summary>
    /// <param name="name">Name of the folder.</param>
    /// <returns>A <see cref="SelectionNode"/> instance that represents the folder.</returns>
    public SelectionNode CreateChildFolder(string name)
    {
      var child = new SelectionNode(name, this, _parentTree, null, null);
      AddChild(child);
      return child;
    }

    public Rect Rect => _rect;

    public string FullTypeName { get; }

    private bool IsSelected => _parentTree.SelectedNode == this;

    /// <summary>
    /// Makes a folder expanded or closed.
    /// It can be set for dropdown items but will do anything as they cannot be expanded.
    /// </summary>
    public bool Expanded
    {
      get => IsFolder && _expanded;
      set => _expanded = value;
    }

    private bool IsFolder => ChildNodes.Count != 0;

    public void Select()
    {
      _parentTree.SelectedNode = this;
    }

    public IEnumerable<SelectionNode> GetChildNodesRecursive()
    {
      if (_parentNode != null)
        yield return this;

      foreach (SelectionNode childNode in ChildNodes.SelectMany(node => node.GetChildNodesRecursive()))
        yield return childNode;
    }

    public IEnumerable<SelectionNode> GetParentNodesRecursive(
      bool includeSelf)
    {
      if (includeSelf)
        yield return this;

      if (_parentNode == null)
        yield break;

      foreach (SelectionNode node in _parentNode.GetParentNodesRecursive(true))
        yield return node;
    }

    /// <summary>
    /// Returns the direct child node with the matching name, or null if the matching node was not found.
    /// </summary>
    /// <remarks>
    /// One of the usages of FindNode is to build the selection tree. When a new item is added, it is checked whether
    /// its parent folder is already created. If the folder is created, it is usually the most recently created folder,
    /// so the list is iterated backwards to give the result as quickly as possible.
    /// </remarks>
    /// <param name="name">Name of the node to find.</param>
    /// <returns>Direct child node with the matching name or null.</returns>
    public SelectionNode FindChild(string name)
    {
      for (int index = ChildNodes.Count - 1; index >= 0; --index)
      {
        if (ChildNodes[index].Name == name)
          return ChildNodes[index];
      }

      return null;
    }

    public void DrawSelfAndChildren(int indentLevel, Rect visibleRect)
    {
      Draw(indentLevel, visibleRect);
      if ( ! Expanded)
        return;

      foreach (SelectionNode childItem in ChildNodes)
        childItem.DrawSelfAndChildren(indentLevel + 1, visibleRect);
    }

    private void AddChild(SelectionNode childNode) => ChildNodes.Add(childNode);

    private bool NodeIsOutsideOfVisibleRect(Rect visibleRect)
    {
      return _rect.y + _rect.height < visibleRect.y || _rect.y > visibleRect.y + visibleRect.height;
    }

    private void Draw(int indentLevel, Rect visibleRect)
    {
      Rect buttonRect = GUILayoutUtility.GetRect(0f, DropdownStyle.NodeHeight);
      Event currentEvent = Event.current;
      EventType currentEventType = currentEvent.type;

      if (currentEventType == EventType.Layout)
        return;

      if (currentEventType == EventType.Repaint || _rect.width == 0f)
        _rect = buttonRect;

      if (_rect.y > 1000f && NodeIsOutsideOfVisibleRect(visibleRect))
        return;

      if (currentEventType == EventType.Repaint)
        DrawNodeContent(indentLevel, currentEvent);

      _wasMouseDownEvent = currentEventType == EventType.MouseDown && _rect.Contains(currentEvent.mousePosition);
      if (_wasMouseDownEvent)
        _handleClickEventOnMouseUp = this;
      SelectOnClick();
      HandleMouseEvents(_rect);
    }

    private void DrawNodeContent(int indentLevel, Event currentEvent)
    {
      if (IsSelected)
      {
        EditorGUI.DrawRect(_rect, DropdownStyle.SelectedColor);
      }
      else if (_rect.Contains(currentEvent.mousePosition))
      {
        EditorGUI.DrawRect(_rect, DropdownStyle.MouseOverColor);
      }

      Rect indentedNodeRect = _rect;
      indentedNodeRect.xMin += DropdownStyle.GlobalOffset + indentLevel * DropdownStyle.IndentWidth;

      if (IsFolder)
      {
        Rect triangleRect = GetTriangleRect(indentedNodeRect);
        DrawTriangleIcon(triangleRect);
      }

      DrawLabel(indentedNodeRect);

      SirenixEditorGUI.DrawHorizontalLineSeperator(_rect.x, _rect.y, _rect.width, DropdownStyle.BorderAlpha);
    }

    private void DrawLabel(Rect indentedNodeRect)
    {
      Rect labelRect = indentedNodeRect.AlignMiddleVertically(DropdownStyle.LabelHeight);
      string label = _parentTree.DrawInSearchMode ? FullTypeName : Name;
      GUIStyle style = IsSelected ? DropdownStyle.SelectedLabelStyle : DropdownStyle.DefaultLabelStyle;
      GUI.Label(labelRect, label, style);
    }

    private void DrawTriangleIcon(Rect triangleRect) // TODO: refactor
    {
      EditorIcon triangleIcon = Expanded ? EditorIcons.TriangleDown : EditorIcons.TriangleRight;
      Debug.Log(EditorIcons.TriangleRightTest);

      if (DropdownStyle.DarkSkin)
      {
        if (IsSelected || _rect.Contains(Event.current.mousePosition))
          GUI.DrawTexture(triangleRect, triangleIcon.Highlighted);
        else
          GUI.DrawTexture(triangleRect, triangleIcon.Active);
      }
      else if (IsSelected)
      {
        GUI.DrawTexture(triangleRect, triangleIcon.Default);
      }
      else if (_rect.Contains(Event.current.mousePosition))
      {
        GUI.DrawTexture(triangleRect, triangleIcon.Active);
      }
      else
      {
        GUIHelper.PushColor(new Color(1f, 1f, 1f, 0.7f));
        GUI.DrawTexture(triangleRect, triangleIcon.Active);
        GUIHelper.PopColor();
      }
    }

    private Rect GetTriangleRect(Rect nodeRect)
    {
      Rect triangleRect = nodeRect.AlignMiddleVertically(DropdownStyle.IconSize);
      triangleRect.width = DropdownStyle.IconSize;
      triangleRect.x -= DropdownStyle.IconSize;
      return triangleRect;
    }

    private void SelectOnClick()
    {
      EventType type = Event.current.type;
      if (type == EventType.Layout || !Rect.Contains(Event.current.mousePosition))
        return;
      GUIHelper.RequestRepaint();
      if (type != EventType.MouseUp || IsFolder)
        return;
      Event.current.Use();
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
        if (ChildNodes.Any())
        {
          Expanded = ! Expanded;
        }
        else
        {
          Select();
        }
      }

      GUIHelper.RemoveFocusControl();
      Event.current.Use();
    }

    private IEnumerable<SelectionNode> ParentNodesBottomUp(
      bool includeSelf = true)
    {
      SelectionNode self = this;
      if (self._parentNode != null)
      {
        foreach (SelectionNode node in self._parentNode.ParentNodesBottomUp())
          yield return node;
      }

      if (includeSelf)
        yield return self;
    }
  }
}