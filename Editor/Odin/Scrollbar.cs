namespace TypeReferences.Editor.Odin
{
    using Test.Editor.OdinAttributeDrawers;
    using UnityEditor;
    using UnityEngine;

    public class Scrollbar
    {
        public bool Visible = true;
        private Vector2 _position = default;
        private SelectionNode _nodeToScrollTo = null;

        private bool ScrollCannotBePerformed => Event.current.type != EventType.Repaint;

        public void Draw()
        {
            _position = Visible
                ? EditorGUILayout.BeginScrollView(_position, GUILayout.ExpandHeight(false))
                : EditorGUILayout.BeginScrollView(_position, GUIStyle.none, GUIStyle.none, GUILayout.ExpandHeight(false));
        }

        public void ToTop()
        {
            _position.y = 0f;
        }

        private void ScrollToNode(Rect nodeRect, Rect outerScrollViewRect, Rect innerScrollViewRect)
        {
            Rect rect2 = outerScrollViewRect.AlignCenterY(nodeRect.height);

            float num1 = nodeRect.yMin - (innerScrollViewRect.y + _position.y - rect2.y);
            float num2 = (float) (nodeRect.yMax - (double) rect2.height + innerScrollViewRect.y - (_position.y + (double) rect2.y));

            if (num1 < 0.0)
                _position.y += num1;
            if (num2 > 0.0)
                _position.y += num2;
        }

        public void ScrollToNode(SelectionNode node, Rect outerScrollViewRect = default, Rect innerScrollViewRect = default)
        {
            if (node == null)
                return;

            _nodeToScrollTo = node;

            foreach (SelectionNode parentNode in node.GetParentNodesRecursive(false))
                parentNode.Expanded = true;

            if (ScrollCannotBePerformed || outerScrollViewRect == default || innerScrollViewRect == default)
                return;

            ScrollToNode(node.Rect, outerScrollViewRect, innerScrollViewRect);
            _nodeToScrollTo = null;
        }

        public void ScrollToNodeIfNeeded(Rect outerScrollViewRect, Rect innerScrollViewRect)
        {
            if (_nodeToScrollTo == null || ScrollCannotBePerformed)
                return;

            ScrollToNode(_nodeToScrollTo.Rect, outerScrollViewRect, innerScrollViewRect);
            _nodeToScrollTo = null;
        }
    }
}