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
  public class SelectionTree : IEnumerable
  {
    public const int SearchToolbarHeight = 22;

    public static SelectionTree ActiveSelectionTree;
    public static Event CurrentEvent;
    public static EventType CurrentEventType;

    public readonly List<SelectionNode> FlatTree = new List<SelectionNode>(); // needed to show search results

    private static bool _preventAutoFocus;

    private readonly GUIFrameCounter _frameCounter = new GUIFrameCounter();
    private readonly EditorTimeHelper _timeHelper = new EditorTimeHelper(); // For some reason, it should be used to fold out selection tree
    private readonly SelectionNode _root;
    private readonly string _searchFieldControlName;

    [SerializeField] private Vector2 _scrollPos;
    [SerializeField] private string _searchTerm = string.Empty;
    private SelectionNode _selectedNode;
    private SelectionNode _scrollToWhenReady;
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
    private Action<Type> _onTypeSelected;
    private string[] _selectionPaths;

    public event Action SelectionChanged;

    public SelectionTree(SortedSet<TypeItem> items, Type selectedType, Action<Type> onTypeSelected)
    {
      _root = new SelectionNode(this, nameof(_root), null);
      _onTypeSelected = onTypeSelected;
      _selectionPaths = items.Select(item => item.Name).ToArray();
      SetupAutoScroll();
      _searchFieldControlName = Guid.NewGuid().ToString();
      ActiveSelectionTree = this;
      BuildSelectionTree(items);
      SetSelection(items, selectedType);
    }

    public string[] SelectionPaths => _selectionPaths;

    public SelectionNode SelectedNode
    {
      get => _selectedNode;
      set
      {
        _selectedNode = value;
        _onTypeSelected(_selectedNode.Type);
        SelectionChanged?.Invoke();
      }
    }

    public bool DrawInSearchMode { get; private set; }

    public List<SelectionNode> Nodes => _root.ChildNodes;

    public IEnumerable<SelectionNode> EnumerateTree(bool includeRootNode = false)
    {
      return _root.GetChildNodesRecursive(includeRootNode);
    }

    private void SetSelection(SortedSet<TypeItem> items, Type selectedType)
    {
      if (selectedType == null)
        return;

      string nameOfItemToSelect = items.First(item => item.Type == selectedType).Name;

      if (string.IsNullOrEmpty(nameOfItemToSelect))
        return;

      SelectionNode itemToSelect = _root;
      foreach (string part in nameOfItemToSelect.Split('/'))
      {
        itemToSelect = itemToSelect.ChildNodes.First(item => item.Name == part);
      }

      itemToSelect.Select();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return Nodes.GetEnumerator();
    }

    public void ExpandAllFolders()
    {
      _root.GetChildNodesRecursive(false).ForEach(item => item.Toggled = true);
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
          FlatTree.Clear();
          FlatTree.AddRange(EnumerateTree().Where(x => x.Type != null).Select(x =>
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
          FlatTree.Clear();
          UpdateSelectionTree();

          foreach (SelectionNode item in SelectedNode.GetParentNodesRecursive(false))
            item.Toggled = true;

          if (SelectedNode != null)
            ScrollToNode(SelectedNode);
        }
      }

      if (Event.current.type != EventType.Repaint)
        return;
      _hasRepaintedCurrentSearchResult = true;
    }

    public void Draw()
    {
      Rect rect = EditorGUILayout.BeginVertical();
      EditorGUI.DrawRect(rect, SirenixGUIStyles.DarkEditorBackground);
      GUILayout.Space(1f);
      SirenixEditorGUI.BeginHorizontalToolbar(SearchToolbarHeight);
      DrawSearchToolbar(GUIStyle.none);
      EditorGUI.DrawRect(GUILayoutUtility.GetLastRect().AlignLeft(1f), SirenixGUIStyles.BorderColor);
      SirenixEditorGUI.EndHorizontalToolbar();

      if (Nodes.Count == 0)
      {
        GUILayout.BeginVertical(SirenixGUIStyles.ContentPadding);
        SirenixEditorGUI.InfoMessageBox("There are no possible values to select.");
        GUILayout.EndVertical();
      }

      DrawTree();
      SirenixEditorGUI.DrawBorders(rect, 1);
      EditorGUILayout.EndVertical();
    }

    private void DrawTree()
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

        Rect outerRect = EditorGUILayout.BeginVertical();
        HandleActiveSelectionTreeState(outerRect);
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
        List<SelectionNode> nodes = DrawInSearchMode ? FlatTree : Nodes;
        int count = nodes.Count;
        for (int index = 0; index < count; ++index)
          nodes[index].DrawSelfAndChildren(0, visibleRect);

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
        if (_scrollToWhenReady != null)
          ScrollToNode(_scrollToWhenReady, _scrollToCenter);
        if (Event.current.type != EventType.Repaint)
          return;
        _isFirstFrame = false;
      }
      finally
      {
        EditorTimeHelper.Time = time;
      }
    }

    public void UpdateSelectionTree()
    {
      _root.UpdateSelectionTreeRecursive(true);
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
          ScrollToNode(SelectedNode, true);
        else
          ScrollToNode(SelectedNode);
      };
    }

    private void ScrollToNode(SelectionNode node, bool centerNode = false)
    {
      if (node == null)
        return;
      _scrollToCenter = centerNode;
      _scrollToWhenReady = node;
      if (!node._IsVisible())
      {
        foreach (SelectionNode parentNode in node.GetParentNodesRecursive(false))
          parentNode.Toggled = true;
      }
      else
      {
        foreach (SelectionNode parentNode in node.GetParentNodesRecursive(false))
          parentNode.Toggled = true;
        if (_outerScrollViewRect.height == 0.0 || (node.Rect.height <= 0.00999999977648258 || Event.current == null || Event.current.type != EventType.Repaint))
          return;

        Rect rect1 = node.Rect;
        float num1;
        float num2;
        if (centerNode)
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

    private void HandleActiveSelectionTreeState(Rect outerRect)
    {
      if (Event.current.type == EventType.Repaint)
      {
        if (_currWindowHasFocus != GUIHelper.CurrentWindowHasFocus)
        {
          _currWindowHasFocus = GUIHelper.CurrentWindowHasFocus;
          if (_currWindowHasFocus && _regainFocusWhenWindowFocus)
          {
            if (!_preventAutoFocus)
              ActiveSelectionTree = this;
            _regainFocusWhenWindowFocus = false;
          }
        }
        if (!_currWindowHasFocus && ActiveSelectionTree == this)
          ActiveSelectionTree = null;
        if (_currWindowHasFocus)
          _regainFocusWhenWindowFocus = ActiveSelectionTree == this;
        if (_currWindowHasFocus && ActiveSelectionTree == null)
          ActiveSelectionTree = this;
      }
      SelectionTreeActivationZone(outerRect);
    }

    private void SelectionTreeActivationZone(Rect rect)
    {
      if (ActiveSelectionTree == this || Event.current.rawType != EventType.MouseDown || (!rect.Contains(Event.current.mousePosition) || !GUIHelper.CurrentWindowHasFocus))
        return;
      _regainSearchFieldFocus = true;
      _preventAutoFocus = true;
      ActiveSelectionTree = this;
      UnityEditorEventUtility.EditorApplication_delayCall += (Action) (() => _preventAutoFocus = false);
      GUIHelper.RequestRepaint();
    }

    private string DrawSearchField(Rect rect, string searchTerm)
    {
      bool flag1 = GUI.GetNameOfFocusedControl() == _searchFieldControlName;
      if (_hadSearchFieldFocus != flag1)
      {
        if (flag1)
          ActiveSelectionTree = this;
        _hadSearchFieldFocus = flag1;
      }

      bool flag2 = flag1 && (Event.current.keyCode == KeyCode.DownArrow || Event.current.keyCode == KeyCode.UpArrow || (Event.current.keyCode == KeyCode.LeftArrow || Event.current.keyCode == KeyCode.RightArrow) || Event.current.keyCode == KeyCode.Return);
      if (flag2)
        GUIHelper.PushEventType(Event.current.type);
      searchTerm = SirenixEditorGUI.SearchField(rect, searchTerm, _regainSearchFieldFocus && ActiveSelectionTree == this, _searchFieldControlName);
      if (_regainSearchFieldFocus && Event.current.type == EventType.Layout)
        _regainSearchFieldFocus = false;
      if (flag2)
      {
        GUIHelper.PopEventType();
        if (ActiveSelectionTree == this)
          _regainSearchFieldFocus = true;
      }

      if (_forceRegainFocusCounter >= 20)
        return searchTerm;

      if (_forceRegainFocusCounter < 4 && ActiveSelectionTree == this)
        _regainSearchFieldFocus = true;
      GUIHelper.RequestRepaint();
      HandleUtility.Repaint();
      if (Event.current.type == EventType.Repaint)
        ++_forceRegainFocusCounter;
      return searchTerm;
    }

    public void AddTypeAtPath(string path, Type type)
    {
      SplitNodePath(path, out path, out string name);
      AddNodeAtPath(path, new SelectionNode(this, name, type));
    }

    private static void SplitNodePath(string nodePath, out string path, out string name)
    {
      nodePath = nodePath.Trim('/');
      int length = nodePath.LastIndexOf('/');
      if (length == -1)
      {
        path = string.Empty;
        name = nodePath;
      }
      else
      {
        path = nodePath.Substring(0, length);
        name = nodePath.Substring(length + 1);
      }
    }

    private void AddNodeAtPath(
      string path,
      SelectionNode node)
    {
      SelectionNode node1 = _root;
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

          List<SelectionNode> childNodes = node1.ChildNodes;
          SelectionNode node2 = null;
          for (int index = childNodes.Count - 1; index >= 0; --index)
          {
            if (childNodes[index].Name != name)
              continue;

            node2 = childNodes[index];
            break;
          }

          if (node2 == null)
          {
            node2 = new SelectionNode(this, name, null);
            node1.ChildNodes.Add(node2);
          }

          node1 = node2;
          startIndex = num + 1;
        }
        while (num != path.Length - 1);
      }

      List<SelectionNode> childNodes1 = node1.ChildNodes;
      SelectionNode node3 = null;
      for (int index = childNodes1.Count - 1; index >= 0; --index)
      {
        if (childNodes1[index].Name != node.Name)
          continue;

        node3 = childNodes1[index];
        break;
      }

      if (node3 != null)
      {
        node1.ChildNodes.Remove(node3);
        node.ChildNodes.AddRange(node3.ChildNodes);
      }

      node1.ChildNodes.Add(node);
    }
  }
}