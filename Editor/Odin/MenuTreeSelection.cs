namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using UnityEngine;

  public class MenuTreeSelection : IList<MenuItem>
  {
    private readonly List<MenuItem> selection;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Sirenix.OdinInspector.Editor.MenuTreeSelection" /> class.
    /// </summary>
    /// <param name="supportsMultiSelect">if set to <c>true</c> [supports multi select].</param>
    public MenuTreeSelection(bool supportsMultiSelect)
    {
      this.SupportsMultiSelect = supportsMultiSelect;
      selection = new List<MenuItem>();
    }

    /// <summary>Occurs whenever the selection has changed.</summary>
    [Obsolete("Use SelectionChanged which also provides a SelectionChangedType argument")]
    public event Action OnSelectionChanged;

    /// <summary>Occurs whenever the selection has changed.</summary>
    public event Action<SelectionChangedType> SelectionChanged;

    /// <summary>
    /// Usually occurs whenever the user hits return, or double click a menu item.
    /// </summary>
    public event Action<MenuTreeSelection> SelectionConfirmed;

    /// <summary>Gets the count.</summary>
    public int Count => selection.Count;

    /// <summary>
    /// Gets or sets a value indicating whether multi selection is supported.
    /// </summary>
    public bool SupportsMultiSelect { get; set; }

    /// <summary>
    /// Adds a menu item to the selection. If the menu item is already selected, then the item is pushed to the bottom of the selection list.
    /// If multi selection is off, then the previous selected menu item is removed first.
    /// Adding a item to the selection triggers <see cref="E:Sirenix.OdinInspector.Editor.MenuTreeSelection.SelectionChanged" />.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Add(MenuItem item)
    {
      if (!SupportsMultiSelect)
        selection.Clear();
      Remove(item);
      selection.Add(item);
      ApplyChanges(SelectionChangedType.ItemAdded);
    }

    /// <summary>
    /// Clears the selection and triggers <see cref="E:Sirenix.OdinInspector.Editor.MenuTreeSelection.OnSelectionChanged" />.
    /// </summary>
    public void Clear()
    {
      selection.Clear();
      ApplyChanges(SelectionChangedType.SelectionCleared);
    }

    /// <summary>Determines whether an MenuItem is selected.</summary>
    public bool Contains(MenuItem item)
    {
      return selection.Contains(item);
    }

    /// <summary>
    /// Copies all the elements of the current array to the specified array starting at the specified destination array index.
    /// </summary>
    public void CopyTo(MenuItem[] array, int arrayIndex)
    {
      selection.CopyTo(array, arrayIndex);
    }

    /// <summary>Gets the enumerator.</summary>
    public IEnumerator<MenuItem> GetEnumerator()
    {
      return selection.GetEnumerator();
    }

    /// <summary>
    /// Searches for the specified menu item and returns the index location.
    /// </summary>
    public int IndexOf(MenuItem item)
    {
      return selection.IndexOf(item);
    }

    /// <summary>
    /// Removes the specified menu item and triggers <see cref="E:Sirenix.OdinInspector.Editor.MenuTreeSelection.SelectionChanged" />.
    /// </summary>
    public bool Remove(MenuItem item)
    {
      bool flag = selection.Remove(item);
      if (flag)
        ApplyChanges(SelectionChangedType.ItemRemoved);
      return flag;
    }

    /// <summary>
    /// Removes the menu item at the specified index and triggers <see cref="E:Sirenix.OdinInspector.Editor.MenuTreeSelection.SelectionChanged" />.
    /// </summary>
    public void RemoveAt(int index)
    {
      selection.RemoveAt(index);
      ApplyChanges(SelectionChangedType.ItemRemoved);
    }

    /// <summary>Triggers OnSelectionConfirmed.</summary>
    public void ConfirmSelection()
    {
      SelectionConfirmed?.Invoke(this);
    }

    private void ApplyChanges(SelectionChangedType type)
    {
      try
      {
        if (OnSelectionChanged != null)
          OnSelectionChanged();
        if (SelectionChanged == null)
          return;
        SelectionChanged(type);
      }
      catch (Exception ex)
      {
        Debug.LogException(ex);
      }
    }

    bool ICollection<MenuItem>.IsReadOnly => false;

    void IList<MenuItem>.Insert(int index, MenuItem item)
    {
      throw new NotSupportedException();
    }

    MenuItem IList<MenuItem>.this[int index]
    {
      get => selection[index];
      set => Add(value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return selection.GetEnumerator();
    }
  }
}