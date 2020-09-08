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
    [SerializeField, HideInInspector] private int wrappedAreaMaxHeight = 1000;
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

    public event Action OnBeginGUI;
    public event Action OnEndGUI;

    private bool DrawUnityEditorPreview
    {
      get => drawUnityEditorPreview;
      set => drawUnityEditorPreview = value;
    }

    private void SetupAutomaticHeightAdjustment(int maxHeight)
    {
      _preventContentFromExpanding = true;
      wrappedAreaMaxHeight = maxHeight;
      int screenHeight = Screen.currentResolution.height - 40;
      Rect originalP = position;
      originalP.x = (int) originalP.x;
      originalP.y = (int) originalP.y;
      originalP.width = (int) originalP.width;
      originalP.height = (int) originalP.height;
      Rect currentP = originalP;
      DropdownWindow wnd = this;
      int getGoodOriginalPointer = 0;
      int tmpFrameCount = 0;
      EditorApplication.CallbackFunction callback = null;
      callback = () =>
      {
        EditorApplication.update -= callback;
        EditorApplication.update -= callback;
        if (wnd == null)
          return;
        if (tmpFrameCount++ < 10)
          wnd.Repaint();
        if (getGoodOriginalPointer <= 1 && originalP.y < 1.0)
        {
          ++getGoodOriginalPointer;
          originalP = position;
        }
        else
        {
          int y = (int) _contentSize.y;
          if (y != (double) currentP.height)
          {
            tmpFrameCount = 0;
            currentP = originalP;
            currentP.height = Math.Min(y, maxHeight);
            wnd.minSize = new Vector2(wnd.minSize.x, currentP.height);
            wnd.maxSize = new Vector2(wnd.maxSize.x, currentP.height);
            if (currentP.yMax >= (double) screenHeight)
              currentP.y -= currentP.yMax - screenHeight;
            wnd.position = currentP;
          }
        }
        EditorApplication.update += callback;
      };
      EditorApplication.update += callback;
    }

    public static DropdownWindow Create(TypeSelector parentSelector, Rect btnRect, Vector2 windowSize)
    {
      DropdownWindow window = CreateOdinEditorWindowInstanceForObject(parentSelector);
      if (windowSize.x <= 1.0)
        windowSize.x = btnRect.width;
      if (windowSize.x <= 1.0)
        windowSize.x = 400f;
      btnRect.x = (int) btnRect.x;
      btnRect.width = (int) btnRect.width;
      btnRect.height = (int) btnRect.height;
      btnRect.y = (int) btnRect.y;
      windowSize.x = (int) windowSize.x;
      windowSize.y = (int) windowSize.y;
      try
      {
        EditorWindow curr = GUIHelper.CurrentWindow;
        if (curr != null)
          window.OnBeginGUI += (Action) (() => curr.Repaint());
      }
      catch
      {
      }

      if (!EditorGUIUtility.isProSkin)
      {
        window.OnBeginGUI += (Action) (() =>
        {
          Rect position = window.position;
          double width = position.width;
          position = window.position;
          double height = position.height;
          SirenixEditorGUI.DrawSolidRect(new Rect(0.0f, 0.0f, (float) width, (float) height), SirenixGUIStyles.MenuBackgroundColor);
        });
      }

      window.OnEndGUI += (Action) (() =>
      {
        Rect position = window.position;
        double width = position.width;
        position = window.position;
        double height = position.height;
        SirenixEditorGUI.DrawBorders(new Rect(0.0f, 0.0f, (float) width, (float) height), 1);
      });
      window.labelWidth = 0.33f;
      window.DrawUnityEditorPreview = true;
      btnRect.position = GUIUtility.GUIToScreenPoint(btnRect.position);
      if ((int) windowSize.y == 0)
      {
        window.ShowAsDropDown(btnRect, new Vector2(windowSize.x, 10f));
        window.SetupAutomaticHeightAdjustment(600);
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
          GUILayout.BeginArea(new Rect(0.0f, 0.0f, position.width, wrappedAreaMaxHeight));
        OnBeginGUI?.Invoke();
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
        OnEndGUI?.Invoke();
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