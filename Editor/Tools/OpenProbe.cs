using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    [InitializeOnLoad]
    internal static class OpenProbe
    {
        private const BindingFlags k_Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static string last;

        static OpenProbe()
        {
            EditorApplication.update += Poll;
            Undo.undoRedoPerformed += () => Debug.Log($"[OpenProbe] f{Time.frameCount} undo/redo performed");
        }

        private static void Poll()
        {
            var text = new StringBuilder();
            foreach(var window in Resources.FindObjectsOfTypeAll<FirableLabWindow>())
            {
                text.Append(Describe(window, "Firable"));
            }
            foreach(var window in Resources.FindObjectsOfTypeAll<EffectTimelineWindow>())
            {
                text.Append(Describe(window, "Effect"));
            }
            string now = text.ToString();
            if(now == last) return;
            last = now;
            Debug.Log($"[OpenProbe] f{Time.frameCount} {(now.Length == 0 ? "no windows" : now)}");
        }

        private static string Describe(EditorWindow window, string field)
        {
            var type = window.GetType();
            var settings = type.GetField("settings", k_Flags)?.GetValue(window);
            var value = settings?.GetType().GetField(field)?.GetValue(settings) as Object;
            var serialized = type.GetField("serializedWindow", k_Flags)?.GetValue(window) as SerializedObject;
            string inSerialized = "-";
            if(serialized != null)
            {
                try
                {
                    var property = serialized.FindProperty("settings." + field);
                    inSerialized = property?.objectReferenceValue != null ? property.objectReferenceValue.name : "null";
                }
                catch(System.Exception exception)
                {
                    inSerialized = "disposed:" + exception.GetType().Name;
                }
            }
            var results = type.GetField("results", k_Flags)?.GetValue(window);
            return $"[{type.Name} #{window.GetInstanceID()} {field}={(value != null ? value.name : "null")} so={inSerialized} gui={(results != null ? "built" : "none")} children={window.rootVisualElement.childCount} focus={window.hasFocus} docked={window.docked}] ";
        }
    }
}
