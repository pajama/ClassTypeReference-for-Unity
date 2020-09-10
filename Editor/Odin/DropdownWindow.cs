﻿namespace TypeReferences.Editor.Odin
{
  using System;
  using Sirenix.OdinInspector;
  using Sirenix.OdinInspector.Editor;
  using Sirenix.Serialization;
  using Sirenix.Utilities;
  using Sirenix.Utilities.Editor;
  using UnityEditor;
  using UnityEngine;

  [ShowOdinSerializedPropertiesInInspector]
  public class DropdownWindow : EditorWindow, ISerializationCallbackReceiver
  {
    private const int MaxWindowHeight = 600;
    private const float DefaultEditorPreviewHeight = 170f;

    private static bool _hasUpdatedOdinEditors;

    private readonly EditorTimeHelper _timeHelper = new EditorTimeHelper();

    [SerializeField, HideInInspector] private SerializationData serializationData;

    private Editor _editor;
    private GUIStyle _marginStyle;
    private EditorWindow _mouseDownWindow;
    private Vector2 _scrollPos;
    private Vector2 _contentSize;
    private Rect _positionUponCreation;
    private int _mouseDownKeyboardControl;
    private int _mouseDownId;
    private int _drawCountWarmup;
    private bool _isInitialized;
    private bool _preventContentFromExpanding;
    private bool _updatedEditorOnce;
    private int _framesSinceFirstUpdate;

    private MenuTree _selectionTree;

    private void SetupAutomaticHeightAdjustment()
    {
      void OnApplicationUpdate()
      {
        bool windowClosed = this == null;
        if (windowClosed)
        {
          EditorApplication.update -= OnApplicationUpdate;
        }
        else
        {
          AdjustHeightIfNeeded();
        }
      }

      EditorApplication.update += OnApplicationUpdate;
    }

    private void AdjustHeightIfNeeded()
    {
      // two frames are needed to move the window to the correct place from the top left corner
      if (_framesSinceFirstUpdate < 2)
      {
        _framesSinceFirstUpdate++;
        _positionUponCreation = position;
        return;
      }

      if (_contentSize.y.ApproximatelyEquals(_positionUponCreation.height))
        return;

      _positionUponCreation.height = Math.Min(_contentSize.y, MaxWindowHeight);
      minSize = new Vector2(minSize.x, _positionUponCreation.height);
      maxSize = new Vector2(maxSize.x, _positionUponCreation.height);
      float screenHeight = Screen.currentResolution.height - 40f;
      if (_positionUponCreation.yMax >= screenHeight)
        _positionUponCreation.y -= _positionUponCreation.yMax - screenHeight;

      position = _positionUponCreation;
    }

    public static DropdownWindow Create(MenuTree selectionTree, Rect windowArea)
    {
      DropdownWindow window = CreateEditorWindow();

      window._selectionTree = selectionTree;
      window._selectionTree.SelectionChanged += (Action) (() =>
      {
        window.Close();
      });

      if (windowArea.width == 0f)
        windowArea.width = 400f;

      windowArea.position = GUIUtility.GUIToScreenPoint(windowArea.position);

      if (windowArea.height == 0f)
      {
        window.ShowAsDropDown(windowArea, new Vector2(windowArea.width, 10f));
        window._preventContentFromExpanding = true;
        window.SetupAutomaticHeightAdjustment();
      }
      else
      {
        window.ShowAsDropDown(windowArea, windowArea.size);
      }

      return window;
    }

    private static DropdownWindow CreateEditorWindow()
    {
      var window = CreateInstance<DropdownWindow>();
      GUIUtility.hotControl = 0;
      GUIUtility.keyboardControl = 0;
      window.position = GUIHelper.GetEditorWindowRect().AlignCenter(600f, 600f);
      EditorUtility.SetDirty(window);
      return window;
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
      UnitySerializationUtility.DeserializeUnityObject(this, ref serializationData);
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
      UnitySerializationUtility.SerializeUnityObject(this, ref serializationData);
    }

    protected void OnGUI()
    {
      EditorTimeHelper time = EditorTimeHelper.Time;
      EditorTimeHelper.Time = _timeHelper;
      EditorTimeHelper.Time.Update();
      try
      {
        if (_preventContentFromExpanding)
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

        if (!_hasUpdatedOdinEditors)
        {
          // GlobalConfig<InspectorConfig>.Instance.EnsureEditorsHaveBeenUpdated(); // TODO: check if it can be removed safely
          _hasUpdatedOdinEditors = true;
        }

        InitializeIfNeeded();
        GUIStyle guiStyle = _marginStyle ?? new GUIStyle { padding = new RectOffset() };
        _marginStyle = guiStyle;
        if (Event.current.type == EventType.Layout)
        {
          _marginStyle.padding.left = 0;
          _marginStyle.padding.right = 0;
          _marginStyle.padding.top = 0;
          _marginStyle.padding.bottom = 0;
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
        Vector2 vector2 = _preventContentFromExpanding ? EditorGUILayout.BeginVertical(GUILayoutOptions.ExpandHeight(false)).size : EditorGUILayout.BeginVertical().size;
        if (_contentSize == Vector2.zero || Event.current.type == EventType.Repaint)
          _contentSize = vector2;
        GUIHelper.PushHierarchyMode(false);
        GUILayout.BeginVertical(_marginStyle);
        DrawEditor();
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
        if (_preventContentFromExpanding)
          GUILayout.EndArea();
      }
      finally
      {
        EditorTimeHelper.Time = time;
      }
    }

    private void UpdateEditor()
    {
      if (_updatedEditorOnce)
        return;

      _updatedEditorOnce = true;
      Repaint();
      GUIHelper.RequestRepaint();
      if ((bool) _editor)
        DestroyImmediate(_editor);
      _editor = null;
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

    private void DrawEditor()
    {
      _selectionTree.Draw();
      DrawEditorPreview(DefaultEditorPreviewHeight);
    }

    private void DrawEditorPreview(float height)
    {
      if (!(_editor != null) || !_editor.HasPreviewGUI())
        return;
      Rect controlRect = EditorGUILayout.GetControlRect(false, height);
      _editor.DrawPreview(controlRect);
    }

    protected void OnDestroy()
    {
      if (_editor != null)
      {
        if ((bool) _editor)
        {
          DestroyImmediate(_editor);
          _editor = null;
        }
      }

      Selection.selectionChanged -= SelectionChanged;
      Selection.selectionChanged -= SelectionChanged;
    }
  }
}