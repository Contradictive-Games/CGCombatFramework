using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public sealed class BuiltInLooksTests
    {
        private const string k_StyleSheet = "CGCombatEditor";

        private static readonly Regex rule = new(@"\.(cg-kind-[\w-]+)\s*\{([^}]*)\}");
        private static readonly Regex accent = new(@"--cg-accent:\s*([^;]+);");
        private static readonly Regex glyph = new(@"--cg-glyph:\s*""([^""]+)""");

        private readonly Dictionary<string, (string accent, string glyph)> sheet = new();

        private static IEnumerable<Type> Parts => UnitParts.All.Select(p => p.Type);

        private static IEnumerable<Type> DatabaseTypes => DatabaseCatalog.Categories(Type.EmptyTypes).Where(c => !c.IsAll).Select(c => c.Type);

        [OneTimeSetUp]
        public void ReadStyleSheet()
        {
            string path = AssetDatabase.FindAssets($"{k_StyleSheet} t:StyleSheet")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == k_StyleSheet);
            Assert.IsNotNull(path, $"{k_StyleSheet}.uss not found");
            foreach(Match match in rule.Matches(File.ReadAllText(Path.GetFullPath(path))))
            {
                var color = accent.Match(match.Groups[2].Value);
                var icon = glyph.Match(match.Groups[2].Value);
                if(color.Success && icon.Success) sheet[match.Groups[1].Value] = (color.Groups[1].Value.Trim(), icon.Groups[1].Value);
            }
        }

        [Test]
        public void TheStyleMap_HoldsEveryBuiltInPartAndDatabaseType()
        {
            CollectionAssert.AreEquivalent(Parts.Concat(DatabaseTypes), BuiltInStyles.All.Keys);
        }

        [Test]
        public void EveryBuiltInClass_IsInTheStyleSheet()
        {
            foreach(var style in BuiltInStyles.All.Values.Distinct())
            {
                Assert.IsTrue(sheet.ContainsKey(style), $".{style} has no --cg-accent and --cg-glyph in {k_StyleSheet}.uss");
            }
        }

        [Test]
        public void BuiltInParts_DeclareTheLookTheStyleSheetDraws()
        {
            foreach(var type in Parts)
            {
                var attribute = Looks.Own<UnitPartAttribute>(type);
                Assert.IsNotNull(attribute, $"{type.Name} has no [UnitPart]");
                Assert.IsFalse(string.IsNullOrWhiteSpace(attribute.Label), $"{type.Name} has no Label");
                Assert.IsFalse(string.IsNullOrWhiteSpace(attribute.Description), $"{type.Name} has no Description");
                Assert.IsNull(attribute.Group, $"{type.Name} sets a Group");
                AssertLook(type, attribute.Color, attribute.Icon);
            }
        }

        [Test]
        public void BuiltInDatabaseTypes_DeclareTheLookTheStyleSheetDraws()
        {
            foreach(var type in DatabaseTypes)
            {
                var attribute = Looks.Own<DefinitionInfoAttribute>(type);
                Assert.IsNotNull(attribute, $"{type.Name} has no [DefinitionInfo]");
                Assert.IsFalse(string.IsNullOrWhiteSpace(attribute.Title), $"{type.Name} has no Title");
                Assert.IsFalse(string.IsNullOrWhiteSpace(attribute.Description), $"{type.Name} has no Description");
                Assert.AreEqual(DatabaseCatalog.k_Combat, attribute.Group, $"{type.Name} isn't in Combat");
                AssertLook(type, attribute.Color, attribute.Icon);
            }
        }

        [Test]
        public void BuiltInOrders_AreBelowTheDefaultAndUnique()
        {
            var parts = Parts.Select(Looks.PartOrder).ToList();
            var definitions = DatabaseTypes.Select(Looks.DefinitionOrder).ToList();

            Assert.That(parts, Is.All.LessThan(0));
            Assert.That(parts, Is.Unique);
            Assert.That(definitions, Is.All.LessThan(0));
            Assert.That(definitions, Is.Unique);
        }

        private void AssertLook(Type type, string color, string icon)
        {
            string style = BuiltInStyles.Of(type);
            Assert.IsTrue(sheet.TryGetValue(style, out var drawn), $".{style} isn't in {k_StyleSheet}.uss");
            Assert.IsTrue(Looks.TryColor(color, out var declared), $"{type.Name}'s Color \"{color}\" doesn't parse");
            Assert.IsTrue(Looks.TryColor(drawn.accent, out var sheetColor), $".{style}'s --cg-accent \"{drawn.accent}\" doesn't parse");
            Assert.AreEqual(ColorUtility.ToHtmlStringRGBA(sheetColor), ColorUtility.ToHtmlStringRGBA(declared), $"{type.Name}'s Color doesn't match .{style}'s --cg-accent");
            Assert.AreEqual(drawn.glyph, icon, $"{type.Name}'s Icon doesn't match .{style}'s --cg-glyph");
            Assert.IsTrue(Looks.IsGlyph(icon), $"{type.Name}'s Icon \"{icon}\" isn't a built-in glyph");
        }
    }
}
