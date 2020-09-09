namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Linq;
  using Sirenix.OdinInspector;
  using Sirenix.OdinInspector.Editor;
  using Sirenix.Serialization;
  using Sirenix.Utilities;
  using Sirenix.Utilities.Editor;
  using UnityEditor;
  using UnityEngine;
  using Object = UnityEngine.Object;

  [ShowOdinSerializedPropertiesInInspector]
  public class DropdownWindow : EditorWindow, ISerializationCallbackReceiver
  {
    private static bool _hasUpdatedOdinEditors;
    [SerializeField, HideInInspector] private float labelWidth = 0.33f;
    private int _wrappedAreaMaxHeight = 1000;
    private Editor _editor;
    private PropertyTree _propertyTree;
    private const float DefaultEditorPreviewHeight = 170f;
    private readonly EditorTimeHelper _timeHelper = new EditorTimeHelper();
    [SerializeField, HideInInspector] private SerializationData serializationData;
    [NonSerialized] private TypeSelector _parentSelector; // TODO: Change to TypeSelector
    [SerializeField, HideInInspector] private bool drawUnityEditorPreview;
    [NonSerialized] private int _drawCountWarmup;
    [NonSerialized] private bool _isInitialized;
    private GUIStyle _marginStyle;
    private Vector2 _scrollPos;
    private int _mouseDownId;
    private EditorWindow _mouseDownWindow;
    private int _mouseDownKeyboardControl;
    private Vector2 _contentSize;
    private bool _preventContentFromExpanding;
    private bool _updatedEditorOnce;
    private int _framesSinceFirstUpdate;
    private Rect _positionUponCreation;

    private bool DrawUnityEditorPreview
    {
      get => drawUnityEditorPreview;
      set => drawUnityEditorPreview = value;
    }

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

      _positionUponCreation.height = Math.Min(_contentSize.y, _wrappedAreaMaxHeight);
      minSize = new Vector2(minSize.x, _positionUponCreation.height);
      maxSize = new Vector2(maxSize.x, _positionUponCreation.height);
      float screenHeight = Screen.currentResolution.height - 40f;
      if (_positionUponCreation.yMax >= screenHeight)
        _positionUponCreation.y -= _positionUponCreation.yMax - screenHeight;

      position = _positionUponCreation;
    }

    public static DropdownWindow Create(TypeSelector parentSelector, Rect btnRect, Vector2 windowSize)
    {
      DropdownWindow window = CreateOdinEditorWindowInstanceForObject(parentSelector);
      if (windowSize.x <= 1.0)
        windowSize.x = btnRect.width;
      if (windowSize.x <= 1.0)
        windowSize.x = 400f;

      window.labelWidth = 0.33f;
      window.DrawUnityEditorPreview = true;
      btnRect.position = GUIUtility.GUIToScreenPoint(btnRect.position);
      if ((int) windowSize.y == 0)
      {
        window.ShowAsDropDown(btnRect, new Vector2(windowSize.x, 10f));
        window._preventContentFromExpanding = true;
        const int maxHeight = 600;
        window._wrappedAreaMaxHeight = maxHeight;
        window.SetupAutomaticHeightAdjustment();
      }
      else
      {
        window.ShowAsDropDown(btnRect, windowSize);
      }

      return window;
    }

    private static DropdownWindow CreateOdinEditorWindowInstanceForObject(
      TypeSelector parentSelector)
    {
      DropdownWindow instance = CreateInstance<DropdownWindow>();
      GUIUtility.hotControl = 0;
      GUIUtility.keyboardControl = 0;
      instance._parentSelector = parentSelector;
      instance.titleContent = new GUIContent(parentSelector.ToString()); // TODO: check if titleContent can be removed
      instance.position = GUIHelper.GetEditorWindowRect().AlignCenter(600f, 600f);
      EditorUtility.SetDirty(instance);
      return instance;
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
        bool contentFromExpanding = _preventContentFromExpanding;
        if (contentFromExpanding)
          GUILayout.BeginArea(new Rect(0.0f, 0.0f, position.width, _wrappedAreaMaxHeight));

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
        Vector2 vector2 = !_preventContentFromExpanding ? EditorGUILayout.BeginVertical().size : EditorGUILayout.BeginVertical(GUILayoutOptions.ExpandHeight(false)).size;
        if (_contentSize == Vector2.zero || Event.current.type == EventType.Repaint)
          _contentSize = vector2;
        GUIHelper.PushHierarchyMode(false);
        GUIHelper.PushLabelWidth((double) labelWidth >= 1.0 ? labelWidth : _contentSize.x * labelWidth);
        GUILayout.BeginVertical(_marginStyle);
        DrawEditor();
        GUILayout.EndVertical();
        GUIHelper.PopLabelWidth();
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
        if (!contentFromExpanding)
          return;
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
      _propertyTree?.Dispose();
      if ((bool) _editor)
        DestroyImmediate(_editor);
      _editor = null;
      _propertyTree = PropertyTree.Create(_parentSelector);
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
      if (_propertyTree != null || (_editor != null && _editor.target != null))
      {
        if (_propertyTree != null)
        {
          bool applyUndo = (bool) (_propertyTree.WeakTargets.FirstOrDefault() as Object);
          _propertyTree.Draw(applyUndo);
        }
        else
        {
          OdinEditor.ForceHideMonoScriptInEditor = true;
          try
          {
            _editor.OnInspectorGUI();
          }
          finally
          {
            OdinEditor.ForceHideMonoScriptInEditor = false;
          }
        }
      }

      if (!DrawUnityEditorPreview)
        return;
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

      if (_propertyTree != null)
      {
        _propertyTree.Dispose();
        _propertyTree = null;
      }

      Selection.selectionChanged -= SelectionChanged;
      Selection.selectionChanged -= SelectionChanged;
    }
  }
}