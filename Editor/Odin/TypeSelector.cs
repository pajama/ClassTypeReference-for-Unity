namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Sirenix.OdinInspector.Editor;
  using Test.Editor.OdinAttributeDrawers;
  using UnityEngine;

  public class TypeSelector
  {
    private readonly MenuTree _selectionTree;
    private readonly SortedSet<TypeItem> _nameTypeList;

    public TypeSelector(SortedSet<TypeItem> collection, Type selectedType, bool expandAllMenuItems, Action<Type> onTypeSelected)
    {
      _nameTypeList = collection;
      _selectionTree = new MenuTree(_nameTypeList, onTypeSelected);

      SetSelection(selectedType);

      if (expandAllMenuItems)
        _selectionTree.OpenAllFolders();
    }

    public void Draw(int dropdownHeight)
    {
      int dropdownWidth = CalculateOptimalWidth();
      var windowSize = new Vector2(dropdownWidth, dropdownHeight);
      var popupArea = new Rect(Event.current.mousePosition, windowSize);

      DropdownWindow.Create(_selectionTree, popupArea);
    }

    private void SetSelection(Type selected)
    {
      if (selected == null)
        return;

      string nameOfItemToSelect = _nameTypeList.First(item => item.Type == selected).Name;
      _selectionTree.SetSelection(nameOfItemToSelect);
    }

    private int CalculateOptimalWidth()
    {
      var itemTextValues = _nameTypeList.Select(item => item.Name);
      var style = OdinMenuStyle.TreeViewStyle.DefaultLabelStyle;
      return PopupHelper.CalculatePopupWidth(itemTextValues, style, false); // TODO: Make CalculatePopupWidth accept less variables
    }
  }
}