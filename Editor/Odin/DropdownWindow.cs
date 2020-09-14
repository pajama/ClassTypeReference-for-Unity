﻿namespace TypeReferences.Editor.Odin
{
  using System;
  using Test.Editor.OdinAttributeDrawers;
  using UnityEditor;
  using UnityEngine;

  public class DropdownWindow : EditorWindow
  {
    private SelectionTree _selectionTree;
    private PreventExpandingHeight _preventExpandingHeight;
    private Vector2 _scrollPos;
    private float _contentHeight;

    public static void Create(SelectionTree selectionTree, int windowHeight)
    {
      var window = CreateInstance<DropdownWindow>();
      window.OnCreate(selectionTree, windowHeight);
    }

    /// <summary>
    /// This is basically a constructor. Since ScriptableObjects cannot have constructors,
    /// this one is called from a factory method.
    /// </summary>
    /// <param name="selectionTree">Tree that contains the dropdown items to show.</param>
    /// <param name="windowHeight">Height of the window. If set to 0, it will be auto-adjusted.</param>
    private void OnCreate(SelectionTree selectionTree, float windowHeight)
    {
      ResetControl();
      wantsMouseMove = true;
      _selectionTree = selectionTree;
      _selectionTree.SelectionChanged += Close;

      float windowWidth = CalculateOptimalWidth();

      _preventExpandingHeight = new PreventExpandingHeight(windowHeight == 0f);

      var windowPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
      var windowSize = new Vector2(windowWidth, windowHeight);
      var windowArea = new Rect(windowPosition, windowSize);
      ShowAsDropDown(windowArea, windowSize);
    }

    private void OnGUI()
    {
      CloseOnKeyPress();
      DrawContent();
      RepaintIfMouseWasUsed();
    }

    private void Update()
    {
      // If called in OnGUI, the dropdown blinks before appearing for some reason. Thus, it works well only in Update.
      AdjustHeightIfNeeded();
    }

    private static void ResetControl()
    {
      GUIUtility.hotControl = 0;
      GUIUtility.keyboardControl = 0;
    }

    private float CalculateOptimalWidth()
    {
      var style = DropdownStyle.DefaultLabelStyle;
      float windowWidth = PopupHelper.CalculatePopupWidth(_selectionTree.SelectionPaths, style, false); // TODO: Make CalculatePopupWidth accept less variables
      return windowWidth == 0f ? 400f : windowWidth;
    }

    private void AdjustHeightIfNeeded()
    {
      if ( ! _preventExpandingHeight)
        return;

      if (_contentHeight.ApproximatelyEquals(position.height))
        return;

      Rect positionToAdjust = position;

      positionToAdjust.height = Math.Min(_contentHeight, DropdownStyle.MaxWindowHeight);
      minSize = new Vector2(minSize.x, positionToAdjust.height);
      maxSize = new Vector2(maxSize.x, positionToAdjust.height);
      float screenHeight = Screen.currentResolution.height - 40f;
      if (positionToAdjust.yMax >= screenHeight)
        positionToAdjust.y -= positionToAdjust.yMax - screenHeight;

      position = positionToAdjust;
    }

    private void CloseOnKeyPress()
    {
      if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
      {
        Close();
        Event.current.Use();
      }
    }

    private void DrawContent()
    {
      DrawInFixedRectIfNeeded(() =>
      {
        _scrollPos = EditorDrawHelper.DrawInScrollView(_scrollPos, () =>
        {
          float contentHeight = EditorDrawHelper.DrawVertically(_selectionTree.Draw, _preventExpandingHeight, DropdownStyle.BackgroundColor);

          if (_contentHeight == 0f || Event.current.type == EventType.Repaint)
            _contentHeight = contentHeight;
        });

        EditorDrawHelper.DrawBorders(position.width, position.height, DropdownStyle.BorderColor);
      });
    }

    private void RepaintIfMouseWasUsed()
    {
      if (Event.current.isMouse || Event.current.type == EventType.Used)
        Repaint();
    }

    private void DrawInFixedRectIfNeeded(Action drawContent)
    {
      if (_preventExpandingHeight)
        GUILayout.BeginArea(new Rect(0.0f, 0.0f, position.width, DropdownStyle.MaxWindowHeight));

      drawContent();

      if (_preventExpandingHeight)
        GUILayout.EndArea();
    }
  }
}