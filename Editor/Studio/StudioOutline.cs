using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal enum OutlineKind { Root, Section, Block }

    internal sealed class OutlineNode
    {
        public readonly int Id;
        public readonly OutlineKind Kind;
        public readonly string Path;
        public readonly string Label;
        public readonly int Depth;

        public OutlineNode(int id, OutlineKind kind, string path, string label, int depth)
        {
            Id = id;
            Kind = kind;
            Path = path;
            Label = label;
            Depth = depth;
        }
    }

    internal sealed class StudioOutline
    {
        private const string k_ArrayData = ".Array.data[";

        private readonly Dictionary<string, int> ids = new();
        private readonly Dictionary<int, OutlineNode> nodes = new();
        private readonly Dictionary<int, int> parents = new();
        private readonly List<OutlineNode> ordered = new();
        private readonly HashSet<int> visible = new();
        private readonly HashSet<int> continued = new();
        private readonly HashSet<int> branches = new();
        private int nextId = 1;

        public IReadOnlyList<OutlineNode> Ordered => ordered;
        public OutlineNode Root => Find(string.Empty);
        public string Signature { get; private set; }

        public List<TreeViewItemData<OutlineNode>> Build(SerializedObject serializedObject, string filter)
        {
            nodes.Clear();
            parents.Clear();
            ordered.Clear();
            visible.Clear();
            continued.Clear();
            branches.Clear();

            var root = Add(OutlineKind.Root, string.Empty, null, null);
            var children = new List<TreeViewItemData<OutlineNode>>();
            foreach(var property in BlockTypes.Children(serializedObject))
            {
                Collect(property, null, children, root, true);
            }

            Signature = SignatureOf(serializedObject);
            var terms = StudioIndex.Terms(filter);
            if(terms.Length > 0) children = Prune(children, serializedObject, terms);
            else
            {
                foreach(var node in ordered)
                {
                    visible.Add(node.Id);
                }
            }
            visible.Add(root.Id);
            if(children.Count > 0) branches.Add(root.Id);
            Mark(children);
            return new List<TreeViewItemData<OutlineNode>> { new TreeViewItemData<OutlineNode>(root.Id, root, children) };
        }

        public bool IsVisible(int id) => visible.Contains(id);

        public bool IsBranch(OutlineNode node) => node != null && branches.Contains(node.Id);

        public bool[] GuidesOf(OutlineNode node)
        {
            var levels = new bool[node.Depth];
            for(var each = node; each != null && each.Depth > 0; each = Parent(each))
            {
                levels[each.Depth - 1] = continued.Contains(each.Id);
            }
            return levels;
        }

        public OutlineNode Find(string path)
        {
            if(path == null || !ids.TryGetValue(path, out int id)) return null;
            return nodes.TryGetValue(id, out var node) ? node : null;
        }

        public OutlineNode Nearest(string path)
        {
            while(!string.IsNullOrEmpty(path))
            {
                var node = Find(path);
                if(node != null) return node;
                path = Trim(path);
            }
            return Root;
        }

        public OutlineNode Parent(OutlineNode node)
        {
            if(node == null || !parents.TryGetValue(node.Id, out int id)) return null;
            return nodes.TryGetValue(id, out var parent) ? parent : null;
        }

        public static string SignatureOf(SerializedObject serializedObject)
        {
            var builder = new StringBuilder();
            var iterator = serializedObject.GetIterator();
            while(iterator.NextVisible(true))
            {
                if(iterator.propertyType == SerializedPropertyType.ManagedReference)
                {
                    builder.Append(iterator.propertyPath).Append('=').Append(iterator.managedReferenceId).Append(';');
                }
                else if(iterator.isArray && iterator.propertyType == SerializedPropertyType.Generic && (BlockTypes.IsBlockArray(iterator) || BlockTypes.HoldsBlocks(iterator)))
                {
                    builder.Append(iterator.propertyPath).Append('#').Append(iterator.arraySize).Append(';');
                }
            }
            return builder.ToString();
        }

        public static string TitleOf(OutlineNode node, SerializedObject serializedObject)
        {
            switch(node.Kind)
            {
                case OutlineKind.Root:
                    return StudioKinds.NameOf(serializedObject.targetObject);
                case OutlineKind.Section:
                    return node.Label;
                default:
                    var property = serializedObject.FindProperty(node.Path);
                    return property == null ? "Missing" : BlockTypes.DisplayName(BlockTypes.ValueType(property));
            }
        }

        public static string SummaryOf(OutlineNode node, SerializedObject serializedObject)
        {
            if(node.Kind == OutlineKind.Root) return ObjectNames.NicifyVariableName(serializedObject.targetObject.GetType().Name);
            var property = serializedObject.FindProperty(node.Path);
            if(property == null) return string.Empty;
            if(node.Kind == OutlineKind.Section) return property.arraySize == 1 ? "1 block" : $"{property.arraySize} blocks";
            return BlockSummary.Of(property);
        }

        public static BlockCategory CategoryOf(OutlineNode node, SerializedObject serializedObject)
        {
            if(node.Kind == OutlineKind.Root) return BlockCategory.Other;
            var property = serializedObject.FindProperty(node.Path);
            if(property == null) return BlockCategory.Other;
            var type = node.Kind == OutlineKind.Block ? BlockTypes.ValueType(property) ?? BlockTypes.SlotType(property) : BlockTypes.SlotType(property);
            return BlockCategories.Of(type);
        }

        public static bool IsEmpty(OutlineNode node, SerializedObject serializedObject)
        {
            if(node.Kind != OutlineKind.Block) return false;
            var property = serializedObject.FindProperty(node.Path);
            return property == null || BlockTypes.ValueType(property) == null;
        }

        private void Collect(SerializedProperty property, string prefix, List<TreeViewItemData<OutlineNode>> into, OutlineNode parent, bool topLevel)
        {
            string label = Join(prefix, property.displayName);
            if(property.propertyType == SerializedPropertyType.ManagedReference)
            {
                into.Add(Block(property, label, parent));
                return;
            }

            if(BlockTypes.IsBlockArray(property))
            {
                if(!topLevel && property.arraySize == 0) return;
                var section = Add(OutlineKind.Section, property.propertyPath, label, parent);
                var items = new List<TreeViewItemData<OutlineNode>>();
                for(int i = 0; i < property.arraySize; i++)
                {
                    items.Add(Block(property.GetArrayElementAtIndex(i), null, section));
                }
                into.Add(new TreeViewItemData<OutlineNode>(section.Id, section, items));
                return;
            }

            if(!BlockTypes.HoldsBlocks(property)) return;
            if(property.isArray)
            {
                for(int i = 0; i < property.arraySize; i++)
                {
                    foreach(var child in BlockTypes.Children(property.GetArrayElementAtIndex(i)))
                    {
                        Collect(child, Join(label, (i + 1).ToString()), into, parent, false);
                    }
                }
                return;
            }

            foreach(var child in BlockTypes.Children(property))
            {
                Collect(child, label, into, parent, false);
            }
        }

        private TreeViewItemData<OutlineNode> Block(SerializedProperty property, string label, OutlineNode parent)
        {
            var node = Add(OutlineKind.Block, property.propertyPath, label, parent);
            var children = new List<TreeViewItemData<OutlineNode>>();
            if(property.managedReferenceValue != null)
            {
                foreach(var child in BlockTypes.Children(property))
                {
                    Collect(child, null, children, node, false);
                }
            }
            return new TreeViewItemData<OutlineNode>(node.Id, node, children);
        }

        private OutlineNode Add(OutlineKind kind, string path, string label, OutlineNode parent)
        {
            if(!ids.TryGetValue(path, out int id))
            {
                id = nextId++;
                ids.Add(path, id);
            }
            var node = new OutlineNode(id, kind, path, label, parent == null ? 0 : parent.Depth + 1);
            nodes[id] = node;
            if(parent != null) parents[id] = parent.Id;
            ordered.Add(node);
            return node;
        }

        private List<TreeViewItemData<OutlineNode>> Prune(IEnumerable<TreeViewItemData<OutlineNode>> items, SerializedObject serializedObject, string[] terms)
        {
            var kept = new List<TreeViewItemData<OutlineNode>>();
            foreach(var item in items)
            {
                var children = Prune(item.children, serializedObject, terms);
                if(children.Count == 0 && !Matches(item.data, serializedObject, terms)) continue;
                kept.Add(new TreeViewItemData<OutlineNode>(item.id, item.data, children));
                visible.Add(item.id);
            }
            return kept;
        }

        private void Mark(IEnumerable<TreeViewItemData<OutlineNode>> items)
        {
            var list = new List<TreeViewItemData<OutlineNode>>(items);
            for(int i = 0; i < list.Count; i++)
            {
                if(i < list.Count - 1) continued.Add(list[i].id);
                if(!list[i].hasChildren) continue;
                branches.Add(list[i].id);
                Mark(list[i].children);
            }
        }

        private static bool Matches(OutlineNode node, SerializedObject serializedObject, string[] terms)
        {
            var text = new StringBuilder();
            text.Append(node.Label).Append(' ').Append(TitleOf(node, serializedObject)).Append(' ').Append(SummaryOf(node, serializedObject));
            if(node.Kind == OutlineKind.Block)
            {
                var type = BlockTypes.ValueType(serializedObject.FindProperty(node.Path));
                if(type != null) text.Append(' ').Append(BlockTypes.MenuPath(type));
            }
            return StudioIndex.Matches(text.ToString().ToLowerInvariant(), terms);
        }

        private static string Join(string prefix, string name) => string.IsNullOrEmpty(prefix) ? name : $"{prefix} · {name}";

        private static string Trim(string path)
        {
            if(path.EndsWith("]", StringComparison.Ordinal))
            {
                int at = path.LastIndexOf(k_ArrayData, StringComparison.Ordinal);
                return at < 0 ? string.Empty : path.Substring(0, at);
            }
            int dot = path.LastIndexOf('.');
            return dot < 0 ? string.Empty : path.Substring(0, dot);
        }
    }
}
