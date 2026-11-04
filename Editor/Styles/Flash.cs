using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal static class Flash
    {
        private const long k_HoldMs = 150;
        private const float k_FadeSeconds = 0.6f;
        private const long k_CleanupMs = 900;
        private static readonly Color color = new(0.23f, 0.52f, 0.95f, 0.4f);
        private static readonly List<StylePropertyName> fades = new() { new StylePropertyName("background-color") };

        public static void Play(VisualElement element)
        {
            if(element == null) return;
            element.style.transitionProperty = fades;
            element.style.transitionDuration = new List<TimeValue> { new TimeValue(0f) };
            element.style.backgroundColor = color;
            element.schedule.Execute(() =>
            {
                element.style.transitionDuration = new List<TimeValue> { new TimeValue(k_FadeSeconds) };
                element.style.backgroundColor = StyleKeyword.Null;
            }).ExecuteLater(k_HoldMs);
            element.schedule.Execute(() =>
            {
                element.style.transitionProperty = StyleKeyword.Null;
                element.style.transitionDuration = StyleKeyword.Null;
            }).ExecuteLater(k_CleanupMs);
        }

        public static bool RevealInInspector(VisualElement from, Object target)
        {
            var root = from?.panel?.visualTree;
            if(root == null || target == null) return false;
            VisualElement found = null;
            root.Query<VisualElement>().ForEach(each =>
            {
                if(found == null && EditorOf(each)?.target == target) found = each;
            });
            if(found == null)
            {
                EditorGUIUtility.PingObject(target);
                return false;
            }
            var scroll = found.GetFirstAncestorOfType<ScrollView>();
            if(scroll != null)
            {
                float y = scroll.scrollOffset.y + found.worldBound.y - scroll.contentViewport.worldBound.y;
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, Mathf.Max(0f, y));
            }
            Play(found);
            return true;
        }

        private static UnityEditor.Editor EditorOf(VisualElement element)
        {
            if(element.GetType().Name != "EditorElement") return null;
            var property = element.GetType().GetProperty("editor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.GetValue(element) as UnityEditor.Editor;
        }
    }
}
