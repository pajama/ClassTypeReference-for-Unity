namespace TypeReferences.Editor.Odin
{
  using System;
  using Sirenix.Serialization;
  using Sirenix.Utilities;
  using Sirenix.Utilities.Editor;
  using Test.Editor.OdinAttributeDrawers;
  using UnityEditor;
  using UnityEngine;

  public class DropdownWindow : EditorWindow, ISerializationCallbackReceiver
  {
    private const int MaxWindowHeight = 600;

    private static bool _hasUpdatedOdinEditors;

    [SerializeField] private SerializationData serializedInstance;

    private GUIStyle _marginStyle;
    private EditorWindow _mouseDownWindow;
    private SelectionTree _selectionTree;
    private Vector2 _scrollPos;
    private float _contentHeight;
    private Rect _positionToAdjust;
    private int _mouseDownKeyboardControl;
    private int _mouseDownId;
    private int _drawCountWarmup;
    private bool _isInitialized;
    private PreventExpandingHeight _preventExpandingHeight;
    private bool _updatedEditorOnce;
    private int _framesSinceFirstUpdate;

    public static void Create(SelectionTree selectionTree, int windowHeight)
    {
      var window = CreateInstance<DropdownWindow>();
      window.OnCreate(selectionTree, windowHeight);
    }

    private static void ResetControl()
    {
      GUIUtility.hotControl = 0;
      GUIUtility.keyboardControl = 0;
    }

    /// <summary>
    /// This is basically a constructor. Since ScriptableObjects cannot have constructors, this one is called from a factory method.
    /// </summary>
    /// <param name="selectionTree">Tree that contains the dropdown items to show.</param>
    /// <param name="windowHeight">Height of the window. If set to 0, it will be auto-adjusted.</param>
    private void OnCreate(SelectionTree selectionTree, float windowHeight)
    {
      ResetControl();
      _selectionTree = selectionTree;
      _selectionTree.SelectionChanged += Close;

      float windowWidth = CalculateOptimalWidth();

      if (windowHeight == 0f)
      {
        _preventExpandingHeight = new PreventExpandingHeight(true);
        EditorApplication.update += AdjustHeightIfNeeded;
      }
      else
      {
        _preventExpandingHeight = new PreventExpandingHeight(false);
      }

      var windowPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
      var windowSize = new Vector2(windowWidth, windowHeight);
      var windowArea = new Rect(windowPosition, windowSize);
      ShowAsDropDown(windowArea, windowSize);
    }

    private float CalculateOptimalWidth()
    {
      var style = DropdownStyle.DefaultLabelStyle;
      float windowWidth = PopupHelper.CalculatePopupWidth(_selectionTree.SelectionPaths, style, false); // TODO: Make CalculatePopupWidth accept less variables
      return windowWidth == 0f ? 400f : windowWidth;
    }

    private void AdjustHeightIfNeeded() // TODO: think of how it can be merged with OnGUI
    {
      // two frames are needed to move the window to the correct place from the top left corner
      if (_framesSinceFirstUpdate < 2)
      {
        _framesSinceFirstUpdate++;
        _positionToAdjust = position;
        return;
      }

      if (_contentHeight.ApproximatelyEquals(_positionToAdjust.height))
        return;

      _positionToAdjust.height = Math.Min(_contentHeight, MaxWindowHeight);
      minSize = new Vector2(minSize.x, _positionToAdjust.height);
      maxSize = new Vector2(maxSize.x, _positionToAdjust.height);
      float screenHeight = Screen.currentResolution.height - 40f;
      if (_positionToAdjust.yMax >= screenHeight)
        _positionToAdjust.y -= _positionToAdjust.yMax - screenHeight;

      position = _positionToAdjust;
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
      UnitySerializationUtility.DeserializeUnityObject(this, ref serializedInstance);
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
      UnitySerializationUtility.SerializeUnityObject(this, ref serializedInstance);
    }

    protected void OnGUI()
    {
      if (_preventExpandingHeight)
        GUILayout.BeginArea(new Rect(0.0f, 0.0f, position.width, MaxWindowHeight));

      if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
      {
        Close();
        Event.current.Use();
      }

      if (!EditorGUIUtility.isProSkin)
      {
        SirenixEditorGUI.DrawSolidRect(new Rect(0.0f, 0.0f, position.width, position.height), SirenixGUIStyles.MenuBackgroundColor);
      }

      if (GUIHelper.CurrentWindow != null)
        GUIHelper.CurrentWindow.Repaint();

      InitializeIfNeeded();
      GUIStyle guiStyle = _marginStyle ?? new GUIStyle { padding = new RectOffset() };
      _marginStyle = guiStyle;
      if (Event.current.type == EventType.Layout)
      {
        _marginStyle.padding = new RectOffset(0, 0, 0, 0);
        UpdateEditor();
      }

      EventType type = Event.current.type;
      if (Event.current.type == EventType.MouseDown)
      {
        _mouseDownId = GUIUtility.hotControl;
        _mouseDownKeyboardControl = GUIUtility.keyboardControl;
        _mouseDownWindow = focusedWindow;
      }

      _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

      float contentHeight = EditorGUILayout.BeginVertical(_preventExpandingHeight).height;
      if (_contentHeight == 0f || Event.current.type == EventType.Repaint)
        _contentHeight = contentHeight;

      GUIHelper.PushHierarchyMode(false);
      GUILayout.BeginVertical(_marginStyle);
      _selectionTree.Draw();
      GUILayout.EndVertical();
      GUIHelper.PopHierarchyMode();
      EditorGUILayout.EndVertical();
      EditorGUILayout.EndScrollView();

      SirenixEditorGUI.DrawBorders(new Rect(0.0f, 0.0f, position.width, position.height), 1);

      if (Event.current.type != type)
        _mouseDownId = -2;
      if (Event.current.type == EventType.MouseUp && GUIUtility.hotControl == _mouseDownId && (focusedWindow == _mouseDownWindow && GUIUtility.keyboardControl == _mouseDownKeyboardControl))
      {
        GUIHelper.RemoveFocusControl();
        GUI.FocusControl(null);
      }

      if (_drawCountWarmup < 10)
      {
        Repaint();
        if (Event.current.type == EventType.Repaint)
          ++_drawCountWarmup;
      }

      if (Event.current.isMouse || Event.current.type == EventType.Used)
        Repaint();
      this.RepaintIfRequested();
      if (_preventExpandingHeight)
        GUILayout.EndArea();
    }

    private void UpdateEditor()
    {
      if (_updatedEditorOnce)
        return;

      _updatedEditorOnce = true;
      Repaint();
      GUIHelper.RequestRepaint();
    }

    private void InitializeIfNeeded()
    {
      if (_isInitialized)
        return;

      _isInitialized = true;
      if (titleContent != null && titleContent.text == GetType().FullName)
        titleContent.text = GetType().GetNiceName().SplitPascalCase();
      wantsMouseMove = true;
      Selection.selectionChanged -= SelectionChanged;
      Selection.selectionChanged += SelectionChanged;
    }

    private void SelectionChanged()
    {
      Repaint();
    }

    protected void OnEnable()
    {
      InitializeIfNeeded();
    }

    protected void OnDestroy()
    {
      Selection.selectionChanged -= SelectionChanged;
      Selection.selectionChanged -= SelectionChanged;

      EditorApplication.update -= AdjustHeightIfNeeded;
    }
  }
}