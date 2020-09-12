namespace TypeReferences.Editor.Odin
{
  using UnityEditor;
  using UnityEngine;

  public static class DropdownStyle
  {
    public const int Height = 23;
    public const float GlobalOffset = 20f;
    public const float IndentWidth = 15f;
    public const float TriangleSize = 16f;
    public const float BorderAlpha = 0.323f;
    public const int MaxWindowHeight = 600;

    public static readonly GUIStyle DefaultLabelStyle = new GUIStyle(EditorStyles.label)
    {
      margin = new RectOffset(0, 0, 0, 0)
    };

    public static readonly GUIStyle SelectedLabelStyle = new GUIStyle(EditorStyles.label)
    {
      margin = new RectOffset(0, 0, 0, 0),
      normal = { textColor = Color.white },
      onNormal = { textColor = Color.white }
    };

    private static readonly Color SelectedColorDarkSkin = new Color(0.243f, 0.373f, 0.588f, 1f);
    private static readonly Color SelectedInactiveColorDarkSkin = new Color(0.838f, 0.838f, 0.838f, 0.134f);
    private static readonly Color SelectedColorLightSkin = new Color(0.243f, 0.49f, 0.9f, 1f);
    private static readonly Color SelectedInactiveColorLightSkin = new Color(0.5f, 0.5f, 0.5f, 1f);

    public static Color SelectedColor => EditorGUIUtility.isProSkin ? SelectedColorDarkSkin : SelectedColorLightSkin;

    public static Color SelectedInactiveColor =>
      EditorGUIUtility.isProSkin ? SelectedInactiveColorDarkSkin : SelectedInactiveColorLightSkin;
  }
}