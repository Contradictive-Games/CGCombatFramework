using System;
using System.Collections.Generic;
using System.Linq;

namespace CG.CombatFramework.Editor
{
    internal enum PickerRowKind
    {
        None,
        Heading,
        Block
    }

    internal sealed class PickerRow
    {
        public readonly PickerRowKind Kind;
        public readonly string Label;
        public readonly string Path;
        public readonly Type Type;
        public readonly int Depth;
        public readonly int Count;

        public PickerRow(PickerRowKind kind, string label, string path, Type type, int depth, int count)
        {
            Kind = kind;
            Label = label;
            Path = path ?? string.Empty;
            Type = type;
            Depth = depth;
            Count = count;
        }
    }

    internal sealed class BlockPickerModel
    {
        public const string k_KeyPrefix = "Picker.Closed.";
        public const string k_AllTitle = "Block Types";
        public const string k_NoneText = "Leaves the slot empty";
        public const string k_NoMatchText = "No blocks match";
        private const string k_NoDescription = "No description";

        private static readonly char[] spaces = { ' ', '\t' };

        private readonly Node root = new(string.Empty, string.Empty);
        private readonly List<Entry> entries = new();
        private readonly Dictionary<Type, string> folderOf = new();
        private readonly bool allowNone;
        private readonly bool keepTopOrder;

        public string Title { get; }
        public string Key { get; }
        public BlockCategory? Category { get; }

        public BlockPickerModel(Type baseType, bool allowNone)
        {
            this.allowNone = allowNone;
            Category = BlockCategories.Of(baseType);
            Title = BlockCategories.Label(Category.Value);
            Key = k_KeyPrefix + baseType.FullName;
            foreach(var type in BlockTypes.Candidates(baseType))
            {
                Add(type, BlockTypes.MenuPath(type));
            }
            Sort(root);
        }

        private BlockPickerModel(IReadOnlyList<Type> baseTypes)
        {
            keepTopOrder = true;
            Title = k_AllTitle;
            Key = k_KeyPrefix + "All";
            foreach(var baseType in baseTypes)
            {
                string category = BlockCategories.Label(BlockCategories.Of(baseType));
                foreach(var type in BlockTypes.Candidates(baseType))
                {
                    Add(type, category + "/" + BlockTypes.MenuPath(type));
                }
            }
            Sort(root);
        }

        public static BlockPickerModel ForAll(IReadOnlyList<Type> baseTypes) => new(baseTypes ?? Array.Empty<Type>());

        public IReadOnlyList<PickerRow> Rows(ICollection<string> closed)
        {
            var rows = new List<PickerRow>();
            if(allowNone) rows.Add(new PickerRow(PickerRowKind.None, "None", string.Empty, null, 0, 0));
            AddRows(root, 0, closed, rows);
            return rows;
        }

        public IReadOnlyList<PickerRow> Search(string query)
        {
            var words = Words(query);
            var rows = new List<PickerRow>();
            if(words.Length == 0) return rows;
            string whole = string.Join(" ", words);
            var found = new List<(Entry entry, int rank)>();
            foreach(var entry in entries)
            {
                bool matches = true;
                bool allInName = true;
                foreach(var word in words)
                {
                    bool inName = Contains(entry.Name, word);
                    if(!inName) allInName = false;
                    if(inName || Contains(entry.Folder, word)) continue;
                    matches = false;
                    break;
                }
                if(!matches) continue;
                int rank = entry.Name.StartsWith(whole, StringComparison.OrdinalIgnoreCase) ? 0 : allInName ? 1 : 2;
                found.Add((entry, rank));
            }
            found.Sort((a, b) =>
            {
                int rank = a.rank.CompareTo(b.rank);
                if(rank != 0) return rank;
                int name = Compare(a.entry.Name, b.entry.Name);
                return name != 0 ? name : Compare(a.entry.Folder, b.entry.Folder);
            });
            foreach(var (entry, _) in found)
            {
                rows.Add(new PickerRow(PickerRowKind.Block, entry.Name, entry.Folder, entry.Type, 0, 0));
            }
            return rows;
        }

        public IReadOnlyList<string> PathTo(Type type)
        {
            var paths = new List<string>();
            if(type == null || !folderOf.TryGetValue(type, out string folder) || folder.Length == 0) return paths;
            var segments = folder.Split('/');
            for(int i = 1; i <= segments.Length; i++)
            {
                paths.Add(string.Join("/", segments, 0, i));
            }
            return paths;
        }

        public static bool IsSearch(string query) => Words(query).Length > 0;

        public static void Describe(PickerRow row, out string title, out string text, out string example)
        {
            title = null;
            text = null;
            example = null;
            if(row == null)
            {
                text = k_NoMatchText;
                return;
            }
            switch(row.Kind)
            {
                case PickerRowKind.None:
                    title = "None";
                    text = k_NoneText;
                    break;
                case PickerRowKind.Heading:
                    title = row.Label;
                    text = row.Count == 1 ? $"{row.Label} · 1 block" : $"{row.Label} · {row.Count} blocks";
                    break;
                default:
                    title = row.Label;
                    var info = BlockTypes.InfoOf(row.Type);
                    text = string.IsNullOrWhiteSpace(info?.Description) ? k_NoDescription : info.Description.Trim();
                    if(!string.IsNullOrWhiteSpace(info?.Example)) example = "Example: " + info.Example.Trim();
                    break;
            }
        }

        private void Add(Type type, string menuPath)
        {
            var segments = (menuPath ?? string.Empty).Split('/').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            string name = segments.Length > 0 ? segments[segments.Length - 1] : BlockTypes.DisplayName(type);
            var folder = root;
            folder.Count++;
            for(int i = 0; i < segments.Length - 1; i++)
            {
                folder = folder.Child(segments[i]);
                folder.Count++;
            }
            folder.Blocks.Add(new Entry(type, name, folder.Path));
            entries.Add(new Entry(type, name, folder.Path));
            if(!folderOf.ContainsKey(type)) folderOf[type] = folder.Path;
        }

        private void Sort(Node folder)
        {
            folder.Blocks.Sort((a, b) =>
            {
                int name = Compare(a.Name, b.Name);
                return name != 0 ? name : string.CompareOrdinal(a.Type.FullName, b.Type.FullName);
            });
            if(!(keepTopOrder && folder == root)) folder.Children.Sort((a, b) => Compare(a.Name, b.Name));
            foreach(var child in folder.Children)
            {
                Sort(child);
            }
        }

        private static void AddRows(Node folder, int depth, ICollection<string> closed, List<PickerRow> rows)
        {
            foreach(var block in folder.Blocks)
            {
                rows.Add(new PickerRow(PickerRowKind.Block, block.Name, block.Folder, block.Type, depth, 0));
            }
            foreach(var child in folder.Children)
            {
                rows.Add(new PickerRow(PickerRowKind.Heading, child.Name, child.Path, null, depth, child.Count));
                if(closed == null || !closed.Contains(child.Path)) AddRows(child, depth + 1, closed, rows);
            }
        }

        private static string[] Words(string query) => string.IsNullOrWhiteSpace(query) ? Array.Empty<string>() : query.Split(spaces, StringSplitOptions.RemoveEmptyEntries);

        private static bool Contains(string text, string word) => text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;

        private static int Compare(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

        private readonly struct Entry
        {
            public readonly Type Type;
            public readonly string Name;
            public readonly string Folder;

            public Entry(Type type, string name, string folder)
            {
                Type = type;
                Name = name;
                Folder = folder;
            }
        }

        private sealed class Node
        {
            public readonly string Name;
            public readonly string Path;
            public readonly List<Node> Children = new();
            public readonly List<Entry> Blocks = new();
            public int Count;

            public Node(string name, string path)
            {
                Name = name;
                Path = path;
            }

            public Node Child(string name)
            {
                foreach(var node in Children)
                {
                    if(string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase)) return node;
                }
                var child = new Node(name, Path.Length == 0 ? name : Path + "/" + name);
                Children.Add(child);
                return child;
            }
        }
    }
}
