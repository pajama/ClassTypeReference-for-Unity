/*****
 * This is a simple PropertyDrawer for string variables to allow drag and drop
 * of MonoScripts in the inspector of the Unity3d editor.
 *****/

namespace Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(MonoScriptAttribute), false)]
    public class MonoScriptPropertyDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, MonoScript> ScriptCache;
        private bool _viewString = false;

        static MonoScriptPropertyDrawer()
        {
            ScriptCache = new Dictionary<string, MonoScript>();
            var scripts = Resources.FindObjectsOfTypeAll<MonoScript>();

            foreach (MonoScript script in scripts)
            {
                var type = script.GetClass();
                if (type != null && !ScriptCache.ContainsKey(type.FullName))
                    ScriptCache.Add(type.FullName, script);
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                GUI.Label(position, "The MonoScript attribute can only be used on string variables");
                return;
            }

            Rect prefixLabelRect = EditorGUI.PrefixLabel(position, label);
            Rect labelRect = position;
            labelRect.xMax = prefixLabelRect.xMin;
            position = prefixLabelRect;
            _viewString = GUI.Toggle(labelRect, _viewString, string.Empty, "label");
            if (_viewString)
            {
                property.stringValue = EditorGUI.TextField(position, property.stringValue);
                return;
            }

            MonoScript script = null;
            string typeName = property.stringValue;
            if (!string.IsNullOrEmpty(typeName))
            {
                ScriptCache.TryGetValue(typeName, out script);
                if (script == null)
                    GUI.color = Color.red;
            }

            script = (MonoScript) EditorGUI.ObjectField(position, script, typeof(MonoScript), false);
            if (!GUI.changed)
                return;

            if (script == null)
            {
                property.stringValue = string.Empty;
                return;
            }

            var type = script.GetClass();
            var attr = (MonoScriptAttribute) attribute;
            if (attr.ParentType != null && !attr.ParentType.IsAssignableFrom(type))
            {
                Debug.LogWarning("The script file " + script.name + " doesn't contain an assignable class");
            }
            else
            {
                property.stringValue = script.GetClass().Name;
            }
        }
    }
}