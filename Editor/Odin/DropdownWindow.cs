namespace TypeReferences.Editor.Odin
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
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
    private static readonly PropertyInfo MaterialForceVisibleProperty = typeof(MaterialEditor).GetProperty("forceVisible", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
    private static bool _hasUpdatedOdinEditors;
    [SerializeField, HideInInspector] private float labelWidth = 0.33f;
    [SerializeField, HideInInspector] private int wrappedAreaMaxHeight = 1000;
    private object[] _currentTargets = new object[0];
    private Editor[] _editors = new Editor[0];
    private PropertyTree[] _propertyTrees = new PropertyTree[0];
    private const float DefaultEditorPreviewHeight = 170f;
    private readonly EditorTimeHelper _timeHelper = new EditorTimeHelper();
    [SerializeField, HideInInspector] private SerializationData serializationData;
    [SerializeField, HideInInspector] private Object inspectorTargetSerialized;
    [NonSerialized] private object _inspectTargetObject;
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
    private int _prevFocusId;
    private int _prevKeyboardFocus;

    public event Action OnBeginGUI;
    public event Action OnEndGUI;

    private bool DrawUnityEditorPreview
    {
      get => drawUnityEditorPreview;
      set => drawUnityEditorPreview = value;
    }

    private object GetTarget()
    {
      if (_inspectTargetObject != null)
        return _inspectTargetObject;
      return inspectorTargetSerialized != (Object) null ? inspectorTargetSerialized : (object) this;
    }

    private IEnumerable<object> GetTargets()
    {
      yield return GetTarget();
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

    public static DropdownWindow Create(object obj, Rect btnRect, Vector2 windowSize, int prevFocusId, int prevKeyboardFocus)
    {
      DropdownWindow window = CreateOdinEditorWindowInstanceForObject(obj);
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

      window._prevFocusId = prevFocusId;
      window._prevKeyboardFocus = prevKeyboardFocus;

      return window;
    }

    private static DropdownWindow CreateOdinEditorWindowInstanceForObject(
      object obj)
    {
      DropdownWindow instance = CreateInstance<DropdownWindow>();
      GUIUtility.hotControl = 0;
      GUIUtility.keyboardControl = 0;
      Object @object = obj as Object;
      if ((bool) @object)
        instance.inspectorTargetSerialized = @object;
      else
        instance._inspectTargetObject = obj;
      if ((bool) (@object as Component))
        instance.titleContent = new GUIContent((@object as Component).gameObject.name);
      else if ((bool) @object)
        instance.titleContent = new GUIContent(@object.name);
      else
        instance.titleContent = new GUIContent(obj.ToString());
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

    /// <summary>Draws the Odin Editor Window.</summary>
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
          UpdateEditors();
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
        DrawEditors();
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

        if (Event.current.isMouse || Event.current.type == EventType.Used || (_currentTargets == null || _currentTargets.Length == 0))
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

    private void DrawEditors()
    {
      for (int index = 0; index < _currentTargets.Length; ++index)
        DrawEditor(index);
    }

    private void UpdateEditors()
    {
      _currentTargets = _currentTargets ?? new object[0];
      _editors = _editors ?? new Editor[0];
      _propertyTrees = _propertyTrees ?? new PropertyTree[0];
      IList<object> objectList = GetTargets().ToArray() ?? new object[0];
      if (_currentTargets.Length != objectList.Count)
      {
        if (_editors.Length > objectList.Count)
        {
          int num = _editors.Length - objectList.Count;
          for (int index = 0; index < num; ++index)
          {
            Editor editor = _editors[_editors.Length - index - 1];
            if ((bool) editor)
              DestroyImmediate(editor);
          }
        }

        if (_propertyTrees.Length > objectList.Count)
        {
          int num = _propertyTrees.Length - objectList.Count;
          for (int index = 0; index < num; ++index)
            _propertyTrees[_propertyTrees.Length - index - 1]?.Dispose();
        }

        Array.Resize(ref _currentTargets, objectList.Count);
        Array.Resize(ref _editors, objectList.Count);
        Array.Resize(ref _propertyTrees, objectList.Count);
        Repaint();
      }

      for (int index = 0; index < objectList.Count; ++index)
      {
        object target = objectList[index];
        object currentTarget = _currentTargets[index];
        if (target == currentTarget)
          continue;

        GUIHelper.RequestRepaint();
        _currentTargets[index] = target;
        if (target == null)
        {
          if (_propertyTrees[index] != null)
            _propertyTrees[index].Dispose();
          _propertyTrees[index] = null;
          if ((bool) _editors[index])
            DestroyImmediate(_editors[index]);
          _editors[index] = null;
        }
        else
        {
          var editorWindow = target as EditorWindow;
          if (target.GetType().InheritsFrom<Object>() && !(bool) editorWindow)
          {
            var targetObject = target as Object;
            if ((bool) targetObject)
            {
              if (_propertyTrees[index] != null)
                _propertyTrees[index].Dispose();
              _propertyTrees[index] = null;
              if ((bool) _editors[index])
                DestroyImmediate(_editors[index]);
              _editors[index] = Editor.CreateEditor(targetObject);
              MaterialEditor editor = _editors[index] as MaterialEditor;
              if (editor != null && MaterialForceVisibleProperty != null)
                MaterialForceVisibleProperty.SetValue(editor, true, null);
            }
            else
            {
              if (_propertyTrees[index] != null)
                _propertyTrees[index].Dispose();
              _propertyTrees[index] = null;
              if ((bool) _editors[index])
                DestroyImmediate(_editors[index]);
              _editors[index] = null;
            }
          }
          else
          {
            if (_propertyTrees[index] != null)
              _propertyTrees[index].Dispose();
            if ((bool) _editors[index])
              DestroyImmediate(_editors[index]);
            _editors[index] = null;
            _propertyTrees[index] = !(target is IList) ? PropertyTree.Create(target) : PropertyTree.Create(target as IList);
          }
        }
      }
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

    private void DrawEditor(int index)
    {
      PropertyTree propertyTree = _propertyTrees[index];
      Editor editor = _editors[index];
      if (propertyTree != null || (editor != null && editor.target != null))
      {
        if (propertyTree != null)
        {
          bool applyUndo = (bool) (propertyTree.WeakTargets.FirstOrDefault() as Object);
          propertyTree.Draw(applyUndo);
        }
        else
        {
          OdinEditor.ForceHideMonoScriptInEditor = true;
          try
          {
            editor.OnInspectorGUI();
          }
          finally
          {
            OdinEditor.ForceHideMonoScriptInEditor = false;
          }
        }
      }

      if (!DrawUnityEditorPreview)
        return;
      DrawEditorPreview(index, DefaultEditorPreviewHeight);
    }

    private void DrawEditorPreview(int index, float height)
    {
      Editor editor = _editors[index];
      if (!(editor != null) || !editor.HasPreviewGUI())
        return;
      Rect controlRect = EditorGUILayout.GetControlRect(false, height);
      editor.DrawPreview(controlRect);
    }

    protected void OnDestroy()
    {
      if (_editors != null)
      {
        for (int index = 0; index < _editors.Length; ++index)
        {
          if ((bool) _editors[index])
          {
            DestroyImmediate(_editors[index]);
            _editors[index] = null;
          }
        }
      }

      if (_propertyTrees != null)
      {
        for (int index = 0; index < _propertyTrees.Length; ++index)
        {
          if (_propertyTrees[index] != null)
          {
            _propertyTrees[index].Dispose();
            _propertyTrees[index] = null;
          }
        }
      }

      Selection.selectionChanged -= SelectionChanged;
      Selection.selectionChanged -= SelectionChanged;

      GUIUtility.hotControl = _prevFocusId;
      GUIUtility.keyboardControl = _prevKeyboardFocus;
    }
  }
}