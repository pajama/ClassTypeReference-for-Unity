namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Sirenix.Utilities;
  using Sirenix.Utilities.Editor;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.Assertions;

  [Serializable]
  public class SelectionNode
  {
    public readonly List<SelectionNode> ChildNodes = new List<SelectionNode>();
    public readonly string Name;
    public readonly Type Type;

    private static readonly Color MouseOverColor = new Color(1f, 1f, 1f, 0.028f);
    private static bool _previousNodeWasSelected;
    private static SelectionNode _handleClickEventOnMouseUp;

    private readonly SelectionTree _parentTree;

    private bool _expanded;
    private SelectionNode _parentNode;
    private Rect _triangleRect;
    private Rect _labelRect;
    private Rect _rect;
    private bool _wasMouseDownEvent;

    private SelectionNode(string name, SelectionNode parentNode, SelectionTree parentTree, Type type, string fullTypeName)
    {
      Assert.IsNotNull(name);

      Name = name;
      _parentNode = parentNode;
      _parentTree = parentTree;
      Type = type;
      FullTypeName = fullTypeName;
    }

    /// <summary>Creates a root node.</summary>
    /// <param name="parentTree"></param>
    private SelectionNode(SelectionTree parentTree)
    {
      _parentNode = null;
      _parentTree = parentTree;
      Name = string.Empty;
      Type = null;
      FullTypeName = null;
    }

    public static SelectionNode CreateRoot(SelectionTree tree)
    {
      return new SelectionNode(tree);
    }

    public SelectionNode CreateChildItem(string name, Type type, string fullTypeName)
    {
      var child = new SelectionNode(name, this, _parentTree, type, fullTypeName);
      AddChild(child);
      return child;
    }

    public SelectionNode CreateChildFolder(string name)
    {
      var child = new SelectionNode(name, this, _parentTree, null, null);
      AddChild(child);
      return child;
    }

    public Rect Rect => _rect;

    public string FullTypeName { get; }

    public bool Expanded
    {
      get => ChildNodes.Count != 0 && _expanded;
      set => _expanded = value;
    }

    private bool IsSelected => _parentTree.SelectedNode == this;

    public void Select()
    {
      _parentTree.SelectedNode = this;
    }

    public IEnumerable<SelectionNode> GetChildNodesRecursive(
      bool includeSelf)
    {
      SelectionNode self = this;
      if (includeSelf)
        yield return self;
      foreach (SelectionNode childNode in self.ChildNodes.SelectMany(node => node.GetChildNodesRecursive(true)))
        yield return childNode;
    }

    public IEnumerable<SelectionNode> GetParentNodesRecursive(
      bool includeSelf,
      bool includeRoot = false)
    {
      SelectionNode self = this;
      if (includeSelf || self._parentNode == null & includeRoot)
        yield return self;

      if (self._parentNode == null)
        yield break;

      foreach (SelectionNode node in self._parentNode.GetParentNodesRecursive(true, includeRoot))
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
    public SelectionNode FindNode(string name)
    {
      SelectionNode foundNode = null;
      for (int index = ChildNodes.Count - 1; index >= 0; --index)
      {
        if (ChildNodes[index].Name == name)
        {
          foundNode = ChildNodes[index];
          break;
        }
      }

      return foundNode;
    }

    public void DrawSelfAndChildren(int indentLevel, Rect visibleRect)
    {
      Draw(indentLevel, visibleRect);
      if ( ! Expanded)
        return;

      foreach (SelectionNode childItem in ChildNodes)
        childItem.DrawSelfAndChildren(indentLevel + 1, visibleRect);
    }

    public bool IsVisible()
    {
      return _parentTree.DrawInSearchMode ?
        _parentTree.SearchModeTree.Contains(this) :
        ParentNodesBottomUp(false).All(x => x.Expanded);
    }

    private void AddChild(SelectionNode childNode) => ChildNodes.Add(childNode);

    private void Draw(int indentLevel, Rect visibleRect)
    {
      Rect rect1 = GUILayoutUtility.GetRect(0.0f, DropdownStyle.Height);
      Event currentEvent = SelectionTree.CurrentEvent;
      EventType currentEventType = SelectionTree.CurrentEventType;
      if (currentEventType == EventType.Layout)
        return;

      if (currentEventType == EventType.Repaint || (currentEventType != EventType.Layout && _rect.width == 0.0))
        _rect = rect1;

      if (_rect.y > 1000f && (_rect.y + _rect.height < visibleRect.y || _rect.y > visibleRect.y + visibleRect.height))
        return;

      if (currentEventType == EventType.Repaint)
      {
        _labelRect = _rect.AddXMin(DropdownStyle.GlobalOffset + indentLevel * DropdownStyle.IndentWidth);
        bool isSelected = IsSelected;
        if (isSelected)
        {
          EditorGUI.DrawRect(
            _rect,
            DropdownStyle.SelectedColor);
        }
        else if (_rect.Contains(currentEvent.mousePosition))
        {
          EditorGUI.DrawRect(_rect, MouseOverColor);
        }

        if (ChildNodes.Count > 0 && !_parentTree.DrawInSearchMode)
        {
          EditorIcon editorIcon = Expanded ? EditorIcons.TriangleDown : EditorIcons.TriangleRight;
          _triangleRect = _labelRect.AlignLeft(DropdownStyle.TriangleSize).AlignMiddle(DropdownStyle.TriangleSize);
          _triangleRect.x -= DropdownStyle.TriangleSize;

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

        GUIStyle style = isSelected ? DropdownStyle.SelectedLabelStyle : DropdownStyle.DefaultLabelStyle;
        _labelRect = _labelRect.AlignMiddle(16f);
        string label = _parentTree.DrawInSearchMode ? FullTypeName : Name;
        GUI.Label(_labelRect, label, style);
        bool flag = true;
        if (isSelected || _previousNodeWasSelected)
        {
          if (!EditorGUIUtility.isProSkin)
            flag = false;
        }

        _previousNodeWasSelected = isSelected;
        if (flag)
        {
          Rect rect2 = _rect;
          SirenixEditorGUI.DrawHorizontalLineSeperator(rect2.x, rect2.y, rect2.width, DropdownStyle.BorderAlpha);
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
      if (type != EventType.MouseUp || ChildNodes.Count != 0)
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