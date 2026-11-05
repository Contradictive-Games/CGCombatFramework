using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal enum IconForm
    {
        Glyph,
        Fill,
        Stroke,
        Image
    }

    internal sealed class IconLook
    {
        public readonly IconForm Form;
        public readonly string Glyph;
        public readonly SvgShape Shape;
        public readonly Object Image;

        private IconLook(IconForm form, string glyph, SvgShape shape, Object image)
        {
            Form = form;
            Glyph = glyph;
            Shape = shape;
            Image = image;
        }

        public static IconLook OfGlyph(string glyph) => new(IconForm.Glyph, glyph, null, null);

        public static IconLook OfShape(SvgShape shape, bool outline) => new(outline ? IconForm.Stroke : IconForm.Fill, null, shape, null);

        public static IconLook OfImage(Object image) => new(IconForm.Image, null, null, image);

        public Background ToBackground()
        {
            switch(Image)
            {
                case VectorImage vector: return UnityEngine.UIElements.Background.FromVectorImage(vector);
                case Sprite sprite: return UnityEngine.UIElements.Background.FromSprite(sprite);
                case Texture2D texture: return UnityEngine.UIElements.Background.FromTexture2D(texture);
                default: return default;
            }
        }
    }

    internal readonly struct Look
    {
        public readonly Color? Color;
        public readonly IconLook Icon;

        public Look(Color? color, IconLook icon)
        {
            Color = color;
            Icon = icon;
        }
    }

    internal static class Looks
    {
        private const string k_Fill = "path:";
        private const string k_Stroke = "stroke:";
        private const int k_Shown = 48;

        private enum Field
        {
            Group,
            Color,
            Icon
        }

        private static readonly HashSet<string> glyphs = new(StringComparer.OrdinalIgnoreCase)
        {
            "payload", "emission", "query", "condition", "scaler", "gate", "firemode", "damagestep", "regen", "behavior", "trigger",
            "unit", "tag", "faction", "stat", "resource", "damage", "pipeline", "slot", "profile", "modifier", "test", "other", "data"
        };

        private static readonly Dictionary<string, IconLook> icons = new();
        private static readonly Dictionary<Type, Look> blocks = new();
        private static readonly Dictionary<Type, Look> parts = new();
        private static readonly Dictionary<Type, Look> definitions = new();
        private static readonly HashSet<string> warned = new();
        private static bool hooked;
        private static bool readAssets;

        public static bool IsGlyph(string name) => !string.IsNullOrEmpty(name) && glyphs.Contains(name);

        public static Look OfBlock(Type type) => Of(type, typeof(BlockInfoAttribute), blocks);

        public static Look OfPart(Type type) => Of(type, typeof(UnitPartAttribute), parts);

        public static Look OfDefinition(Type type) => Of(type, typeof(DefinitionInfoAttribute), definitions);

        public static string PartGroup(Type type) => Inherited(type, typeof(UnitPartAttribute), Field.Group);

        public static string DefinitionGroup(Type type) => Inherited(type, typeof(DefinitionInfoAttribute), Field.Group);

        public static T Own<T>(Type type) where T : Attribute => type == null ? null : (T)Attribute.GetCustomAttribute(type, typeof(T), false);

        public static bool TryColor(string text, out Color color)
        {
            color = default;
            return !string.IsNullOrWhiteSpace(text) && ColorUtility.TryParseHtmlString(text.Trim(), out color);
        }

        public static IconLook Icon(string text)
        {
            if(string.IsNullOrWhiteSpace(text)) return null;
            Hook();
            if(icons.TryGetValue(text, out var cached)) return cached;
            var icon = Resolve(text.Trim());
            icons[text] = icon;
            return icon;
        }

        public static void Accent(VisualElement element, Color? color)
        {
            if(color.HasValue) element.style.borderLeftColor = color.Value;
            else element.style.borderLeftColor = StyleKeyword.Null;
        }

        public static void AccentText(VisualElement element, Color? color)
        {
            if(color.HasValue) element.style.color = CombatEditorStyles.Readable(color.Value);
            else element.style.color = StyleKeyword.Null;
        }

        public static void Border(VisualElement element, Color? color)
        {
            var value = color.HasValue ? new StyleColor(color.Value) : new StyleColor(StyleKeyword.Null);
            element.style.borderTopColor = value;
            element.style.borderRightColor = value;
            element.style.borderBottomColor = value;
            element.style.borderLeftColor = value;
        }

        internal static void Clear()
        {
            Forget();
            warned.Clear();
        }

        private static Look Of(Type type, Type attribute, Dictionary<Type, Look> cache)
        {
            if(type == null) return default;
            Hook();
            if(cache.TryGetValue(type, out var look)) return look;
            look = new Look(ColorOf(type, attribute), IconOf(type, attribute));
            cache[type] = look;
            return look;
        }

        private static Color? ColorOf(Type type, Type attribute)
        {
            string text = Inherited(type, attribute, Field.Color);
            if(string.IsNullOrWhiteSpace(text)) return null;
            if(TryColor(text, out var color)) return color;
            Warn(type, attribute, Field.Color, $"Couldn't read Color \"{Shorten(text)}\" on {type.Name}'s [{NameOf(attribute)}]. Use #RGB, #RRGGBB, #RRGGBBAA or a color name like \"orange\"");
            return null;
        }

        private static IconLook IconOf(Type type, Type attribute)
        {
            string text = Inherited(type, attribute, Field.Icon);
            if(string.IsNullOrWhiteSpace(text)) return null;
            var icon = Icon(text);
            if(icon != null) return icon;
            string trimmed = text.Trim();
            bool pathData = trimmed.StartsWith(k_Fill, StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith(k_Stroke, StringComparison.OrdinalIgnoreCase);
            Warn(type, attribute, Field.Icon, pathData
                ? $"Couldn't read the SVG path data in Icon \"{Shorten(text)}\" on {type.Name}'s [{NameOf(attribute)}]"
                : $"Couldn't find Icon \"{Shorten(text)}\" on {type.Name}'s [{NameOf(attribute)}]. Use path: or stroke: SVG path data, a glyph name, an asset path or a Unity editor icon name");
            return null;
        }

        private static string Inherited(Type type, Type attribute, Field field)
        {
            for(var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                var found = Attribute.GetCustomAttribute(current, attribute, false);
                if(found == null) continue;
                string value = Read(found, field);
                if(!string.IsNullOrWhiteSpace(value)) return value;
            }
            return null;
        }

        private static string Read(Attribute attribute, Field field)
        {
            switch(attribute)
            {
                case BlockInfoAttribute block:
                    return field == Field.Color ? block.Color : field == Field.Icon ? block.Icon : null;
                case UnitPartAttribute part:
                    return field == Field.Color ? part.Color : field == Field.Icon ? part.Icon : part.Group;
                case DefinitionInfoAttribute definition:
                    return field == Field.Color ? definition.Color : field == Field.Icon ? definition.Icon : definition.Group;
                default:
                    return null;
            }
        }

        private static IconLook Resolve(string text)
        {
            if(text.StartsWith(k_Fill, StringComparison.OrdinalIgnoreCase))
            {
                return SvgPath.TryParse(text.Substring(k_Fill.Length), out var fill) ? IconLook.OfShape(fill, false) : null;
            }
            if(text.StartsWith(k_Stroke, StringComparison.OrdinalIgnoreCase))
            {
                return SvgPath.TryParse(text.Substring(k_Stroke.Length), out var stroke) ? IconLook.OfShape(stroke, true) : null;
            }
            if(glyphs.Contains(text)) return IconLook.OfGlyph(text.ToLowerInvariant());

            readAssets = true;
            var asset = LoadAsset(text);
            if(asset != null) return IconLook.OfImage(asset);
            var editorIcon = CombatEditorStyles.Texture(text);
            return editorIcon != null ? IconLook.OfImage(editorIcon) : null;
        }

        private static Object LoadAsset(string path)
        {
            if(path.IndexOf('/') < 0) return null;
            Object found = AssetDatabase.LoadAssetAtPath<VectorImage>(path);
            if(found == null) found = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(found == null) found = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return found;
        }

        private static void Warn(Type type, Type attribute, Field field, string message)
        {
            if(!warned.Add($"{type.AssemblyQualifiedName}|{attribute.Name}|{field}")) return;
            Debug.LogWarning($"[CG Combat Framework] {message}");
        }

        private static string NameOf(Type attribute)
        {
            string name = attribute.Name;
            return name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Attribute".Length) : name;
        }

        private static string Shorten(string text) => text.Length <= k_Shown ? text : text.Substring(0, k_Shown) + "…";

        private static void Hook()
        {
            if(hooked) return;
            hooked = true;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        internal static void OnProjectChanged()
        {
            if(readAssets) Forget();
        }

        private static void Forget()
        {
            readAssets = false;
            icons.Clear();
            blocks.Clear();
            parts.Clear();
            definitions.Clear();
        }
    }
}
