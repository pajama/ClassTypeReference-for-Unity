namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using UnityEngine;

  public class MenuTreeSelection : IList<MenuItem>
  {
    private readonly List<MenuItem> _selection;

    public MenuTreeSelection()
    {
      _selection = new List<MenuItem>();
    }

    public event Action<SelectionChangedType> SelectionChanged;

    public event Action<MenuTreeSelection> SelectionConfirmed;

    public int Count => _selection.Count;

    bool ICollection<MenuItem>.IsReadOnly => false;

    MenuItem IList<MenuItem>.this[int index]
    {
      get => _selection[index];
      set => Add(value);
    }

    void IList<MenuItem>.Insert(int index, MenuItem item)
    {
      throw new NotSupportedException();
    }

    /// <summary>
    /// Adds a menu item to the selection. If the menu item is already selected, then the item is pushed to the bottom of the selection list.
    /// If multi selection is off, then the previous selected menu item is removed first.
    /// Adding a item to the selection triggers <see cref="E:Sirenix.OdinInspector.Editor.MenuTreeSelection.SelectionChanged" />.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Add(MenuItem item)
    {
      _selection.Clear();
      Remove(item);
      _selection.Add(item);
      ApplyChanges(SelectionChangedType.ItemAdded);
    }

    /// <summary>
    /// Clears the selection and triggers <see cref="E:Sirenix.OdinInspector.Editor.MenuTreeSelection.OnSelectionChanged" />.
    /// </summary>
    public void Clear()
    {
      _selection.Clear();
      ApplyChanges(SelectionChangedType.SelectionCleared);
    }

    /// <summary>Determines whether an MenuItem is selected.</summary>
    public bool Contains(MenuItem item)
    {
      return _selection.Contains(item);
    }

    /// <summary>
    /// Copies all the elements of the current array to the specified array starting at the specified destination array index.
    /// </summary>
    public void CopyTo(MenuItem[] array, int arrayIndex)
    {
      _selection.CopyTo(array, arrayIndex);
    }

    /// <summary>Gets the enumerator.</summary>
    public IEnumerator<MenuItem> GetEnumerator()
    {
      return _selection.GetEnumerator();
    }

    /// <summary>
    /// Searches for the specified menu item and returns the index location.
    /// </summary>
    public int IndexOf(MenuItem item)
    {
      return _selection.IndexOf(item);
    }

    /// <summary>
    /// Removes the specified menu item and triggers <see cref="E:Sirenix.OdinInspector.Editor.MenuTreeSelection.SelectionChanged" />.
    /// </summary>
    public bool Remove(MenuItem item)
    {
      bool flag = _selection.Remove(item);
      if (flag)
        ApplyChanges(SelectionChangedType.ItemRemoved);
      return flag;
    }

    /// <summary>
    /// Removes the menu item at the specified index and triggers <see cref="E:Sirenix.OdinInspector.Editor.MenuTreeSelection.SelectionChanged" />.
    /// </summary>
    public void RemoveAt(int index)
    {
      _selection.RemoveAt(index);
      ApplyChanges(SelectionChangedType.ItemRemoved);
    }

    /// <summary>Triggers OnSelectionConfirmed.</summary>
    public void ConfirmSelection()
    {
      SelectionConfirmed?.Invoke(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return _selection.GetEnumerator();
    }

    private void ApplyChanges(SelectionChangedType type)
    {
      try
      {
        if (SelectionChanged == null)
          return;
        SelectionChanged(type);
      }
      catch (Exception ex)
      {
        Debug.LogException(ex);
      }
    }
  }
}