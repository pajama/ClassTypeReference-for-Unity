namespace TypeReferences.Editor
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using Odin;
  using Sirenix.OdinInspector;
  using Sirenix.OdinInspector.Editor;
  using Sirenix.OdinInspector.Editor.Drawers;
  using Sirenix.Serialization;
  using Sirenix.Utilities;
  using Sirenix.Utilities.Editor;
  using UnityEditor;
  using UnityEngine;

  [DrawerPriority(0.0, 0.0, 2002.0)]
  public class ClassTypeReferenceOdinDrawer : OdinAttributeDrawer<OdinClassTypeConstraintAttribute>
  {
    private GUIContent _label;
    private bool _isList;
    private bool _isListElement;
    private Func<IEnumerable<ValueDropdownItem>> _getValues;
    private Func<IEnumerable<object>> _getSelection;
    private IEnumerable<object> _result;
    private bool _enableMultiSelect;
    private Dictionary<object, string> _nameLookup;
    private InspectorPropertyValueGetter<object> _rawGetter;
    private LocalPersistentContext<bool> _isToggled;
    private GenericSelector<object> _inlineSelector;
    private IEnumerable<object> _nextResult;

    /// <summary>Initializes this instance.</summary>
    protected override void Initialize()
    {
      _rawGetter = new InspectorPropertyValueGetter<object>(Property, Attribute.MemberName); // Function that returns IList collection
      _isToggled = this.GetPersistentValue("Toggled", SirenixEditorGUI.ExpandFoldoutByDefault);
      _isList = Property.ChildResolver is ICollectionResolver;
      _isListElement = Property.Info.GetMemberInfo() == null;
      _getSelection = () => Property.ValueEntry.WeakValues.Cast<object>();
      _getValues = () => // Get text and value of each item in IList collection
      { // For each item there is ValueDropdownItem struct that holds item's name and object value.
        object obj = _rawGetter.GetValue();
        return (obj as IEnumerable)?.Cast<object>().Where(x => x != null).Select(x =>
        {
          switch (x)
          {
            case ValueDropdownItem valueDropdownItem2:
              return valueDropdownItem2;
            case IValueDropdownItem valueDropdownItem1:
              return new ValueDropdownItem(valueDropdownItem1.GetText(), valueDropdownItem1.GetValue());
            default:
              return new ValueDropdownItem(null, x);
          }
        });
      };
      ReloadDropdownCollections();
    }

    private void ReloadDropdownCollections() // Acquires names of the items in the collection and collects them to a dictionary. Does not seem to be needed for us
    {
      object obj1 = null;
      object obj2 = _rawGetter.GetValue();
      if (obj2 != null)
        obj1 = (obj2 as IEnumerable).Cast<object>().FirstOrDefault(); // First item in IList collection of values
      if (obj1 is IValueDropdownItem)  // If first item can be transformed to text and object value
      {
        var valueDropdownItems = _getValues();  // Get Collection (equals TypeDropDownDrawer.AddTypesToDropdown())
        _nameLookup = new Dictionary<object, string>(new ValueDropdownEqualityComparer(false));
        foreach (var valueDropdownItem in valueDropdownItems)
          _nameLookup[valueDropdownItem] = valueDropdownItem.Text;
      }
      else
      {
        _nameLookup = null;
      }
    }

    /// <summary>
    /// Draws the property with GUILayout support. This method is called by DrawPropertyImplementation if the GUICallType is set to GUILayout, which is the default.
    /// </summary>
    protected override void DrawPropertyLayout(GUIContent label) // Just a drawer of the property. No need to change anything here
    {
      _label = label;
      if (Property.ValueEntry == null)
      {
          CallNextDrawer(label);
      }
      else if (_isList)
      {
        if (Attribute.DisableListAddButtonBehaviour)
        {
          CallNextDrawer(label);
        }
        else
        {
          CollectionDrawerStaticInfo.NextCustomAddFunction = OpenSelector;
          CallNextDrawer(label);
          if (_result == null)
            return;
          AddResult(_result);
          _result = null;
        }
      }
      else if (Attribute.DrawDropdownForListElements || ! _isListElement)
      {
          DrawDropdown();
      }
      else
      {
          CallNextDrawer(label);
      }
    }

    private void AddResult(IEnumerable<object> query) // No idea what this does
    {
      if (_isList)
      {
        var childResolver = Property.ChildResolver as ICollectionResolver;
        if (_enableMultiSelect)
          childResolver.QueueClear();

        foreach (object obj in query)
        {
          var values = new object[Property.ParentValues.Count];
          for (int index = 0; index < values.Length; ++index)
            values[index] = SerializationUtility.CreateCopy(obj);
          childResolver.QueueAdd(values);
        }
      }
      else
      {
        object obj = query.FirstOrDefault();
        for (int index = 0; index < Property.ValueEntry.WeakValues.Count; ++index)
          Property.ValueEntry.WeakValues[index] = SerializationUtility.CreateCopy(obj);
      }
    }

    private void DrawDropdown()
    {
      IEnumerable<object> objects;
      string currentValueName = GetCurrentValueName();  // Equals to _selectedType = CachedTypeReference.GetType(typeName);
      if (Property.Children.Count > 0)
      {
        _isToggled.Value = SirenixEditorGUI.Foldout(_isToggled.Value, _label, out Rect valueRect);
        objects = OdinSelector<object>.DrawSelectorDropdown(valueRect, currentValueName, ShowSelector);
        if (SirenixEditorGUI.BeginFadeGroup(this, _isToggled.Value))
        {
          ++EditorGUI.indentLevel;
          foreach (InspectorProperty child in Property.Children)
            child.Draw(child.Label);

          --EditorGUI.indentLevel;
        }

        SirenixEditorGUI.EndFadeGroup();
      }
      else
      {
        objects = OdinSelector<object>.DrawSelectorDropdown(_label, currentValueName, ShowSelector);
      }

      if (objects == null || !objects.Any())
        return;

      AddResult(objects);
    }

    private void OpenSelector()
    {
      ReloadDropdownCollections();
      ShowSelector(new Rect(Event.current.mousePosition, Vector2.zero)).SelectionConfirmed += (Action<IEnumerable<object>>) (x => _result = x);
    }

    private OdinSelector<object> ShowSelector(Rect rect) // No need to change this
    {
      var selector = CreateSelector();
      rect.x = (int) rect.x;
      rect.y = (int) rect.y;
      rect.width = (int) rect.width;
      rect.height = (int) rect.height;
      selector.ShowInPopup(rect, new Vector2(Attribute.DropdownWidth, Attribute.DropdownHeight));
      return selector;
    }

    private GenericSelector<object> CreateSelector()
    {
      var source = _getValues() ?? Enumerable.Empty<ValueDropdownItem>();
      if (source.Any())
      {
        if ((_isList && Attribute.ExcludeExistingValuesInList) || _isListElement)
        {
          var list = source.ToList();
          InspectorProperty parent = Property.FindParent(x => x.ChildResolver is ICollectionResolver, true);
          var comparer = new ValueDropdownEqualityComparer(false);
          parent.ValueEntry.WeakValues.Cast<IEnumerable>().SelectMany(x => x.Cast<object>()).ForEach(x => list.RemoveAll(c => comparer.Equals(c, x)));
          source = list;
        }

        if (_nameLookup != null)  // Reverted Dictionary of object-string for some reason
        {
          foreach (ValueDropdownItem valueDropdownItem in source)
          {
            if (valueDropdownItem.Value != null)
              _nameLookup[valueDropdownItem.Value] = valueDropdownItem.Text;
          }
        }
      }

      bool flag = Attribute.NumberOfItemsBeforeEnablingSearch == 0 || source.Take(Attribute.NumberOfItemsBeforeEnablingSearch).Count() == Attribute.NumberOfItemsBeforeEnablingSearch;
      var genericSelector = new GenericSelector<object>(null, false, source.Select(x => new GenericSelectorItem<object>(x.Text, x.Value)));
      _enableMultiSelect = _isList && !Attribute.ExcludeExistingValuesInList;
      if (_isList && !Attribute.ExcludeExistingValuesInList)
      {
        genericSelector.CheckboxToggle = true;
      }
      else if (!_enableMultiSelect)
      {
        genericSelector.EnableSingleClickToSelect();
      }

      if (_isList && _enableMultiSelect)
      {
        genericSelector.SelectionTree.Selection.SupportsMultiSelect = true;
        genericSelector.DrawConfirmSelectionButton = true;
      }

      genericSelector.SelectionTree.Config.DrawSearchToolbar = flag;
      var selection = Enumerable.Empty<object>();
      if (!_isList)
        selection = _getSelection();
      else if (_enableMultiSelect)
        selection = _getSelection().SelectMany(x => (x as IEnumerable).Cast<object>());
      genericSelector.SetSelection(selection);
      genericSelector.SelectionTree.EnumerateTree().AddThumbnailIcons(true);
      if (Attribute.ExpandAllMenuItems)
        genericSelector.SelectionTree.EnumerateTree(x => x.Toggled = true);
      return genericSelector;
    }

    private string GetCurrentValueName() // Seems to just be responsible for getting nice name of the item. Must be replaced with TypeNameFormatter
    {
      if (EditorGUI.showMixedValue)
        return "—";
      object weakSmartValue = Property.ValueEntry.WeakSmartValue;
      string name = null;
      if (_nameLookup != null && weakSmartValue != null)
        _nameLookup.TryGetValue(weakSmartValue, out name);
      return new GenericSelectorItem<object>(name, weakSmartValue).GetNiceName();
    }
  }
}