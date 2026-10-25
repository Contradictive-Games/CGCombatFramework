using System;
using UnityEditor;

namespace CG.CombatFramework.Editor
{
    public static class BlockEdits
    {
        private const string k_ArrayData = ".Array.data[";

        public static bool TrySplit(string path, out string arrayPath, out int index)
        {
            arrayPath = null;
            index = -1;
            if(string.IsNullOrEmpty(path) || !path.EndsWith("]", StringComparison.Ordinal)) return false;
            int at = path.LastIndexOf(k_ArrayData, StringComparison.Ordinal);
            if(at < 0) return false;
            int start = at + k_ArrayData.Length;
            if(!int.TryParse(path.Substring(start, path.Length - start - 1), out index)) return false;
            arrayPath = path.Substring(0, at);
            return true;
        }

        public static string ElementPath(string arrayPath, int index) => $"{arrayPath}{k_ArrayData}{index}]";

        public static string SetType(SerializedObject serializedObject, string path, Type type)
        {
            return Assign(serializedObject, path, type == null ? null : Activator.CreateInstance(type), "Change Block Type");
        }

        public static string Paste(SerializedObject serializedObject, string path)
        {
            return Assign(serializedObject, path, BlockClipboard.Paste(), "Paste Block");
        }

        public static string Reset(SerializedObject serializedObject, string path)
        {
            var type = BlockTypes.ValueType(serializedObject.FindProperty(path));
            return type == null ? path : Assign(serializedObject, path, Activator.CreateInstance(type), "Reset Block");
        }

        public static string Assign(SerializedObject serializedObject, string path, object value, string undo)
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty(path);
            if(property == null) return null;
            property.managedReferenceValue = value;
            property.isExpanded = value != null;
            Apply(serializedObject, undo);
            return path;
        }

        public static string Add(SerializedObject serializedObject, string arrayPath, Type type)
        {
            serializedObject.Update();
            var array = serializedObject.FindProperty(arrayPath);
            if(array == null || !array.isArray) return null;
            int index = array.arraySize;
            array.arraySize = index + 1;
            var element = array.GetArrayElementAtIndex(index);
            element.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            element.isExpanded = true;
            Apply(serializedObject, "Add Block");
            return ElementPath(arrayPath, index);
        }

        public static string Duplicate(SerializedObject serializedObject, string path)
        {
            if(!TrySplit(path, out var arrayPath, out int index)) return path;
            serializedObject.Update();
            var array = serializedObject.FindProperty(arrayPath);
            var copy = BlockClipboard.Clone(array.GetArrayElementAtIndex(index).managedReferenceValue);
            array.InsertArrayElementAtIndex(index);
            array.GetArrayElementAtIndex(index + 1).managedReferenceValue = copy;
            Apply(serializedObject, "Duplicate Block");
            return ElementPath(arrayPath, index + 1);
        }

        public static string Move(SerializedObject serializedObject, string path, int offset)
        {
            if(!TrySplit(path, out _, out int index)) return path;
            return MoveTo(serializedObject, path, index + offset);
        }

        public static string MoveTo(SerializedObject serializedObject, string path, int target)
        {
            if(!TrySplit(path, out var arrayPath, out int index)) return path;
            serializedObject.Update();
            var array = serializedObject.FindProperty(arrayPath);
            if(array == null || target < 0 || target >= array.arraySize || target == index) return path;
            array.MoveArrayElement(index, target);
            Apply(serializedObject, "Move Block");
            return ElementPath(arrayPath, target);
        }

        public static string Remove(SerializedObject serializedObject, string path)
        {
            if(!TrySplit(path, out var arrayPath, out int index)) return Assign(serializedObject, path, null, "Clear Block");
            serializedObject.Update();
            var array = serializedObject.FindProperty(arrayPath);
            int size = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if(array.arraySize == size) array.DeleteArrayElementAtIndex(index);
            Apply(serializedObject, "Remove Block");
            return arrayPath;
        }

        private static void Apply(SerializedObject serializedObject, string undo)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName(undo);
        }
    }
}
