// Copyright ClassTypeReference Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root.

namespace TypeReferences.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Custom property drawer for <see cref="ClassTypeReference"/> properties.
    /// </summary>
    [CustomPropertyDrawer(typeof(ClassTypeReference))]
    [CustomPropertyDrawer(typeof(ClassTypeConstraintAttribute), true)]
    public sealed class ClassTypeReferencePropertyDrawer : PropertyDrawer
    {
        /// <summary>
        /// Improves performance by avoiding extensive number of <see cref="M:Type.GetType"/> calls.
        /// </summary>
        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        /// <summary>
        /// Gets or sets a function that returns a collection of types that are
        /// to be excluded from drop-down. A value of <c>null</c> specifies that
        /// no types are to be excluded.
        /// </summary>
        /// <remarks>
        /// <para>This property must be set immediately before presenting a class
        /// type reference property field using <see cref="M:EditorGUI.PropertyField"/>
        /// or <see cref="M:EditorGUILayout.PropertyField"/> since the value of this
        /// property is reset to <c>null</c> each time the control is drawn.</para>
        /// <para>Since filtering makes extensive use of <see cref="ICollection{Type}.Contains"/>
        /// it is recommended to use a collection that is optimized for fast
        /// lookups such as <see cref="HashSet{Type}"/> for better performance.</para>
        /// </remarks>
        /// <example>
        /// <para>Exclude a specific type from being selected:</para>
        /// <code language="csharp"><![CDATA[
        /// private SerializedProperty _someClassTypeReferenceProperty;
        ///
        /// public override void OnInspectorGUI()
        /// {
        ///     serializedObject.Update();
        ///
        ///     ClassTypeReferencePropertyDrawer.ExcludedTypeCollectionGetter = GetExcludedTypeCollection;
        ///     EditorGUILayout.PropertyField(_someClassTypeReferenceProperty);
        ///
        ///     serializedObject.ApplyModifiedProperties();
        /// }
        ///
        /// private ICollection<Type> GetExcludedTypeCollection()
        /// {
        ///     var set = new HashSet<Type>();
        ///     set.Add(typeof(SpecialClassToHideInDropdown));
        ///     return set;
        /// }
        /// ]]></code>
        /// </example>
        private Func<ICollection<Type>> ExcludedTypeCollectionGetter { get; set; }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorStyles.popup.CalcHeight(GUIContent.none, 0);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var constraintAttribute = attribute as ClassTypeConstraintAttribute;
            DrawTypeSelectionControl(position, property.FindPropertyRelative("_classRef"), label, constraintAttribute);
        }

        private static Type CacheAndGetType(string typeName)
        {
            if (TypeCache.TryGetValue(typeName, out Type type))
                return type;

            type = ! string.IsNullOrEmpty(typeName) ? Type.GetType(typeName) : null;
            TypeCache[typeName] = type;
            return type;
        }

        private List<Type> GetFilteredTypes(ClassTypeConstraintAttribute filter)
        {
            var excludedTypes = ExcludedTypeCollectionGetter?.Invoke();
            var typeRelatedAssemblies = TypeCollector.GetTypeRelatedAssemblies(fieldInfo.DeclaringType);

            var filteredTypes = TypeCollector.GetFilteredTypesFromAssemblies(
                typeRelatedAssemblies,
                filter,
                excludedTypes);

            filteredTypes.Sort((a, b) => a.FullName.CompareTo(b.FullName));

            return filteredTypes;
        }

        #region Control Drawing / Event Handling

        private const string ReferenceUpdatedCommandName = "TypeReferenceUpdated";

        private static readonly int ControlHint = typeof(ClassTypeReferencePropertyDrawer).GetHashCode();

        private static readonly GUIContent TempContent = new GUIContent();

        private static readonly GenericMenu.MenuFunction2 SelectedTypeName = OnSelectedTypeName;

        private static int _selectionControlID;

        private static string _selectedClassRef;

        private static void DisplayDropDown(Rect position, List<Type> types, Type selectedType, ClassGrouping grouping)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("(None)"), selectedType == null, SelectedTypeName, null);
            menu.AddSeparator(string.Empty);

            foreach (var type in types)
            {
                var menuLabel = FormatGroupedTypeName(type, grouping);
                if (string.IsNullOrEmpty(menuLabel))
                {
                    continue;
                }

                var content = new GUIContent(menuLabel);
                menu.AddItem(content, type == selectedType, SelectedTypeName, type);
            }

            menu.DropDown(position);
        }

        private static string FormatGroupedTypeName(Type type, ClassGrouping grouping)
        {
            var name = type.FullName;

            switch (grouping)
            {
                default:
                    return name;

                case ClassGrouping.ByNamespace:
                    return name.Replace('.', '/');

                case ClassGrouping.ByNamespaceFlat:
                    var lastPeriodIndex = name.LastIndexOf('.');
                    if (lastPeriodIndex != -1)
                    {
                        name = name.Substring(0, lastPeriodIndex) + "/" + name.Substring(lastPeriodIndex + 1);
                    }

                    return name;

                case ClassGrouping.ByAddComponentMenu:
                    var addComponentMenuAttributes = type.GetCustomAttributes(typeof(AddComponentMenu), false);
                    if (addComponentMenuAttributes.Length == 1)
                    {
                        return ((AddComponentMenu)addComponentMenuAttributes[0]).componentMenu;
                    }

                    return "Scripts/" + type.FullName.Replace('.', '/');
            }
        }

        private static void OnSelectedTypeName(object userData)
        {
            var selectedType = userData as Type;

            _selectedClassRef = ClassTypeReference.GetClassRef(selectedType);
            var typeReferenceUpdatedEvent = EditorGUIUtility.CommandEvent(ReferenceUpdatedCommandName);
            EditorWindow.focusedWindow.SendEvent(typeReferenceUpdatedEvent);
        }

        private string DrawTypeSelectionControl(
            Rect position,
            GUIContent label,
            string classRef,
            ClassTypeConstraintAttribute filter)
        {
            if (label != null && label != GUIContent.none)
                position = EditorGUI.PrefixLabel(position, label);

            var controlID = GUIUtility.GetControlID(ControlHint, FocusType.Keyboard, position);

            var triggerDropDown = false;

            switch (Event.current.GetTypeForControl(controlID))
            {
                case EventType.ExecuteCommand:
                    if (Event.current.commandName == ReferenceUpdatedCommandName)
                    {
                        if (_selectionControlID == controlID)
                        {
                            if (classRef != _selectedClassRef)
                            {
                                classRef = _selectedClassRef;
                                GUI.changed = true;
                            }

                            _selectionControlID = 0;
                            _selectedClassRef = null;
                        }
                    }

                    break;

                case EventType.MouseDown:
                    if (GUI.enabled && position.Contains(Event.current.mousePosition))
                    {
                        GUIUtility.keyboardControl = controlID;
                        triggerDropDown = true;
                        Event.current.Use();
                    }

                    break;

                case EventType.KeyDown:
                    if (GUI.enabled && GUIUtility.keyboardControl == controlID)
                    {
                        if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.Space)
                        {
                            triggerDropDown = true;
                            Event.current.Use();
                        }
                    }

                    break;

                case EventType.Repaint:
                    // Remove assembly name from content of popup control.
                    var classRefParts = classRef.Split(',');

                    TempContent.text = classRefParts[0].Trim();
                    if (TempContent.text == string.Empty)
                    {
                        TempContent.text = "(None)";
                    }
                    else if (CacheAndGetType(classRef) == null)
                    {
                        TempContent.text += " {Missing}";
                    }

                    EditorStyles.popup.Draw(position, TempContent, controlID);
                    break;
            }

            if (!triggerDropDown)
                return classRef;

            _selectionControlID = controlID;
            _selectedClassRef = classRef;

            var filteredTypes = GetFilteredTypes(filter);
            var classGrouping = filter?.Grouping ?? ClassTypeConstraintAttribute.DefaultGrouping;
            DisplayDropDown(position, filteredTypes, CacheAndGetType(classRef), classGrouping);

            return classRef;
        }

        private void DrawTypeSelectionControl(Rect position, SerializedProperty property, GUIContent label, ClassTypeConstraintAttribute filter)
        {
            try
            {
                var restoreShowMixedValue = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

                property.stringValue = DrawTypeSelectionControl(position, label, property.stringValue, filter);

                EditorGUI.showMixedValue = restoreShowMixedValue;
            }
            finally
            {
                ExcludedTypeCollectionGetter = null;
            }
        }

        #endregion
    }
}
