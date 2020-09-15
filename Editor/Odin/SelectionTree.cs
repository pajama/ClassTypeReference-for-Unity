namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Sirenix.Utilities.Editor;
  using Test.Editor.OdinAttributeDrawers;
  using UnityEditor;
  using UnityEngine;
  using FuzzySearch = Test.Editor.OdinAttributeDrawers.FuzzySearch;

  public partial class SelectionTree
  {
    public static Event CurrentEvent;
    public static EventType CurrentEventType;

    private readonly List<SelectionNode> _searchModeTree = new List<SelectionNode>();

    private readonly SelectionNode _root;
    private readonly string _searchFieldControlName = Guid.NewGuid().ToString();
    private readonly Action<Type> _onTypeSelected;
    private readonly Scrollbar _scrollbar = new Scrollbar();

    private string _searchString = string.Empty;
    private SelectionNode _selectedNode;
    private SelectionNode _scrollToWhenReady;
    private Rect _outerScrollViewRect;
    private Rect _innerScrollViewRect;

    public SelectionTree(SortedSet<TypeItem> items, Type selectedType, Action<Type> onTypeSelected)
    {
      _root = SelectionNode.CreateRoot(this);
      _onTypeSelected = onTypeSelected;
      SelectionPaths = items.Select(item => item.Path).ToArray();
      FillTreeWithItems(items);
      SetSelection(items, selectedType);
    }

    public event Action SelectionChanged;

    public string[] SelectionPaths { get; }

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

    private List<SelectionNode> Nodes => _root.ChildNodes;

    public void ExpandAllFolders()
    {
      foreach (SelectionNode node in EnumerateTree())
        node.Expanded = true;
    }

    public void Draw()
    {
      if (Nodes.Count == 0)
      {
        DrawInfoMessage();
      }
      else
      {
        EditorDrawHelper.DrawWithSearchToolbarStyle(DrawSearchToolbar, DropdownStyle.SearchToolbarHeight);

        _scrollbar.DrawWithScrollbar(DrawTree);
        _scrollbar.ScrollToNodeIfNeeded();
      }
    }

    private static void DrawInfoMessage()
    {
      DrawHelper.DrawVertically(DropdownStyle.NoPadding, () =>
      {
        EditorDrawHelper.DrawInfoMessage("No types to select.");
      });
    }

    private IEnumerable<SelectionNode> EnumerateTree() => _root.GetChildNodesRecursive();

    private void SetSelection(SortedSet<TypeItem> items, Type selectedType)
    {
      if (selectedType == null)
        return;

      string nameOfItemToSelect = items.First(item => item.Type == selectedType).Path;

      if (string.IsNullOrEmpty(nameOfItemToSelect))
        return;

      SelectionNode itemToSelect = _root;

      foreach (string part in nameOfItemToSelect.Split('/'))
        itemToSelect = itemToSelect.FindChild(part);

      itemToSelect.Select();
      _scrollbar.BeginScrollToNode(itemToSelect);
    }

    private void DrawSearchToolbar()
    {
      Rect innerToolbarArea = GetInnerToolbarArea();

      bool changed = EditorDrawHelper.CheckIfChanged(() =>
      {
        _searchString = DrawSearchField(innerToolbarArea, _searchString);
      });

      if ( ! changed)
        return;

      if (string.IsNullOrEmpty(_searchString))
      {
        DisableSearchMode();
      }
      else
      {
        EnableSearchMode();
      }
    }

    private void DisableSearchMode()
    {
      DrawInSearchMode = false;
      _scrollbar.BeginScrollToNode(SelectedNode);
    }

    private void EnableSearchMode()
    {
      if ( ! DrawInSearchMode)
        _scrollbar.ToTop();

      DrawInSearchMode = true;
      _searchModeTree.Clear();
      _searchModeTree.AddRange(EnumerateTree().Where(x => x.Type != null).Select(x =>
      {
        bool includeInSearch = FuzzySearch.CanBeIncluded(_searchString, x.FullTypeName, out int score);
        return new
        {
          score,
          item = x,
          include = includeInSearch
        };
      }).Where(x => x.include).OrderByDescending(x => x.score).Select(x => x.item));
    }

    private static Rect GetInnerToolbarArea()
    {
      Rect outerToolbarArea = GUILayoutUtility.GetRect(
        0.0f,
        DropdownStyle.SearchToolbarHeight,
        GUILayout.ExpandWidth(true));

      Rect innerToolbarArea = outerToolbarArea
        .AddHorizontalPadding(10f, 2f)
        .AlignMiddleVertically(16f);

      return innerToolbarArea;
    }

    private void DrawTree(Rect visibleRect)
    {
      CurrentEvent = Event.current;
      CurrentEventType = CurrentEvent.type;
      List<SelectionNode> nodes = DrawInSearchMode ? _searchModeTree : Nodes;
      int count = nodes.Count;
      for (int index = 0; index < count; ++index)
        nodes[index].DrawSelfAndChildren(0, visibleRect);
    }

    private string DrawSearchField(Rect innerToolbarArea, string searchText)
    {
      (Rect searchFieldArea, Rect buttonArea) = innerToolbarArea.CutVertically(DropdownStyle.IconSize, true);

      searchText = EditorDrawHelper.FocusedTextField(searchFieldArea, searchText, "Search",
        DropdownStyle.SearchToolbarStyle, _searchFieldControlName);

      if (DrawHelper.CloseButton(buttonArea))
      {
        searchText = string.Empty;
        GUI.FocusControl(null); // Without this, the old text does not disappear for some reason.
        GUI.changed = true;
      }

      HandleUtility.Repaint();

      return searchText;
    }
  }
}