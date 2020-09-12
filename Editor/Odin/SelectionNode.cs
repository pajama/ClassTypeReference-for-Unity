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

    private bool _isInitialized;
    private bool _isToggled;
    private SelectionNode _parentNode;
    private Rect _triangleRect;
    private Rect _labelRect;
    private Rect _rect;
    private bool _wasMouseDownEvent;

    public SelectionNode(SelectionTree tree, string name, Type type)
    {
      Assert.IsNotNull(tree);
      Assert.IsNotNull(name);

      _parentTree = tree;
      Name = name;
      Type = type;
    }

    public Rect Rect => _rect;

    public bool Toggled
    {
      get => ChildNodes.Count != 0 && _isToggled;
      set => _isToggled = value;
    }

    private bool IsSelected => _parentTree.SelectedNode == this;

    private SelectionNode Parent
    {
      get
      {
        EnsureInitialized();
        return _parentNode;
      }
    }

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
      if (includeSelf || self.Parent == null & includeRoot)
        yield return self;

      if (self.Parent == null)
        yield break;

      foreach (SelectionNode node in self.Parent.GetParentNodesRecursive(true, includeRoot))
        yield return node;
    }

    public void DrawSelfAndChildren(int indentLevel, Rect visibleRect)
    {
      Draw(indentLevel, visibleRect);
      if ( ! Toggled)
        return;

      foreach (SelectionNode childItem in ChildNodes)
        childItem.DrawSelfAndChildren(indentLevel + 1, visibleRect);
    }

    public void UpdateSelectionTreeRecursive(bool isRoot = false)
    {
      _isInitialized = true;

      foreach (SelectionNode childNode in ChildNodes)
      {
        childNode._parentNode = isRoot ? null : this;
        childNode.UpdateSelectionTreeRecursive();
      }
    }

    public bool _IsVisible()
    {
      return _parentTree.DrawInSearchMode ? _parentTree.FlatTree.Contains(this) : ParentNodesBottomUp(false).All(x => x.Toggled);
    }

    private void Draw(int indentLevel, Rect visibleRect)
    {
      Rect rect1 = GUILayoutUtility.GetRect(0.0f, DropdownStyle.Height);
      Event currentEvent = SelectionTree.CurrentEvent;
      EventType currentEventType = SelectionTree.CurrentEventType;
      if (currentEventType == EventType.Layout)
        return;

      if (currentEventType == EventType.Repaint || (currentEventType != EventType.Layout && _rect.width == 0.0))
        _rect = rect1;

      if (_rect.y > 1000.0 && (_rect.y + (double) _rect.height < visibleRect.y ||
                               _rect.y > visibleRect.y + (double) visibleRect.height))
        return;

      if (currentEventType == EventType.Repaint)
      {
        _labelRect = _rect.AddXMin(DropdownStyle.GlobalOffset + indentLevel * DropdownStyle.IndentWidth);
        bool isSelected = IsSelected;
        if (isSelected)
        {
          EditorGUI.DrawRect(
            _rect,
            SelectionTree.ActiveSelectionTree == _parentTree
              ? DropdownStyle.SelectedColor
              : DropdownStyle.SelectedInactiveColor);
        }

        if (!isSelected && _rect.Contains(currentEvent.mousePosition))
          EditorGUI.DrawRect(_rect, MouseOverColor);
        if (ChildNodes.Count > 0 && !_parentTree.DrawInSearchMode)
        {
          EditorIcon editorIcon = Toggled ? EditorIcons.TriangleDown : EditorIcons.TriangleRight;
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
        GUI.Label(_labelRect, Name, style);
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

    private string GetFullPath()
    {
      EnsureInitialized();
      SelectionNode parent = Parent;
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
        if (ChildNodes.Any())
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

    private void EnsureInitialized()
    {
      if (!_isInitialized)
        _parentTree.UpdateSelectionTree();
    }
  }
}