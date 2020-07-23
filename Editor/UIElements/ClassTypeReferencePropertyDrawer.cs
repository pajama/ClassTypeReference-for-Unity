namespace TypeReferences.Editor.UIElements
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    [CustomPropertyDrawer(typeof(ClassTypeReference))]
    [CustomPropertyDrawer(typeof(ClassTypeConstraintAttribute), true)]
    public class ClassTypeReferencePropertyDrawer : PropertyDrawer
    {
        private const string DropDownTemplateName = "DropDown";
        private const string PackagePath = "Packages/com.solidalloy.type.references";

        private VisualTreeAsset _typeDropDownTemplate;
        private VisualElement _root;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var dropDownTemplatePath = GetDropDownTemplatePath();
            _typeDropDownTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(dropDownTemplatePath);
            Debug.Log($"{_typeDropDownTemplate}");

            _root = new VisualElement();
            _typeDropDownTemplate.CloneTree(_root);

            // binding logic here

            return _root;
        }

        private static string GetDropDownTemplatePath()
        {
            var guids = AssetDatabase.FindAssets(
                DropDownTemplateName,
                new[] { PackagePath });

            if (guids.Length != 1)
            {
                throw new FileNotFoundException(
                    $"When searching for {DropDownTemplateName} in {PackagePath}, " +
                    $"found {guids.Length} results. Should've found only one.");
            }

            string dropDownTemplateGUID = guids[0];
            string dropDownTemplatePath = AssetDatabase.GUIDToAssetPath(dropDownTemplateGUID);
            return dropDownTemplatePath;
        }
    }
}