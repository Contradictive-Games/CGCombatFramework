using System.Text.RegularExpressions;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Tests
{
    public sealed class LooksTests
    {
        private const string k_Folder = "Assets/CGLooksTests";
        private const string k_Texture = k_Folder + "/Icon.asset";
        private const string k_Vector = k_Folder + "/Vector.asset";

        [BlockInfo("Base block", Color = "#FF0000", Icon = "stroke:M0 0L10 10")]
        private class BaseBlock
        {
        }

        [BlockInfo("Derived block", Example = "An example")]
        private class DerivedBlock : BaseBlock
        {
        }

        private class PlainBlock : BaseBlock
        {
        }

        [BlockInfo(Icon = "faction")]
        private sealed class IconOnlyBlock : BaseBlock
        {
        }

        [UnitPart("Base Part", Group = "AI", Color = "red", Icon = "query", Hidden = true, Description = "Base")]
        private class BasePart
        {
        }

        private sealed class DerivedPart : BasePart
        {
        }

        [UnitPart("Own Part", Group = "Presentation")]
        private sealed class RegroupedPart : BasePart
        {
        }

        [DefinitionInfo("Lore", Group = "Lore", Color = "#C792EA", Icon = "faction", Description = "Base")]
        private class LoreBase
        {
        }

        private class LorePerson : LoreBase
        {
        }

        private sealed class LoreElder : LorePerson
        {
        }

        [DefinitionInfo("Places", Color = "#00FF00")]
        private sealed class LorePlace : LoreBase
        {
        }

        private sealed class Unmarked
        {
        }

        [BlockInfo(Color = "#nothex")]
        private sealed class BadColor
        {
        }

        [BlockInfo(Color = "not a color")]
        private sealed class OtherBadColor
        {
        }

        [BlockInfo(Icon = "Assets/Nowhere/Missing.png")]
        private sealed class MissingIcon
        {
        }

        [BlockInfo(Icon = "path:M1 1X")]
        private sealed class BadPath
        {
        }

        [BlockInfo(Icon = k_Texture)]
        private sealed class TextureIcon
        {
        }

        private sealed class Notes : IDefinitionData
        {
        }

        [SetUp]
        public void SetUp() => Looks.Clear();

        [TearDown]
        public void TearDown()
        {
            Looks.Clear();
            if(AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.DeleteAsset(k_Folder);
        }

        private static Texture2D MakeTexture()
        {
            if(!AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.CreateFolder("Assets", "CGLooksTests");
            var texture = new Texture2D(4, 4);
            AssetDatabase.CreateAsset(texture, k_Texture);
            return texture;
        }

        [TestCase("#F00", 1f, 0f, 0f, 1f)]
        [TestCase("#00FF00", 0f, 1f, 0f, 1f)]
        [TestCase("#0000FF80", 0f, 0f, 1f, 0.502f)]
        [TestCase("red", 1f, 0f, 0f, 1f)]
        [TestCase("  #FFFFFF  ", 1f, 1f, 1f, 1f)]
        public void ColorStrings_Parse(string text, float r, float g, float b, float a)
        {
            Assert.IsTrue(Looks.TryColor(text, out var color));
            Assert.AreEqual(r, color.r, 0.01f);
            Assert.AreEqual(g, color.g, 0.01f);
            Assert.AreEqual(b, color.b, 0.01f);
            Assert.AreEqual(a, color.a, 0.01f);
        }

        [TestCase("orange")]
        [TestCase("darkblue")]
        [TestCase("magenta")]
        public void ColorNames_Parse(string text) => Assert.IsTrue(Looks.TryColor(text, out _));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("#GG0000")]
        [TestCase("not a color")]
        public void BadColorStrings_DoNotParse(string text) => Assert.IsFalse(Looks.TryColor(text, out _));

        [Test]
        public void Icon_PathData_IsAFilledShape()
        {
            var icon = Looks.Icon("path:M4 4h16v12H8l-4 4z");
            Assert.AreEqual(IconForm.Fill, icon.Form);
            Assert.IsNotNull(icon.Shape);
        }

        [Test]
        public void Icon_StrokeData_IsAnOutlinedShape()
        {
            var icon = Looks.Icon("stroke:M5 12h14M12 5v14");
            Assert.AreEqual(IconForm.Stroke, icon.Form);
            Assert.IsNotNull(icon.Shape);
        }

        [TestCase("faction", "faction")]
        [TestCase("Faction", "faction")]
        [TestCase("data", "data")]
        [TestCase("other", "other")]
        public void Icon_GlyphNames(string text, string glyph)
        {
            var icon = Looks.Icon(text);
            Assert.AreEqual(IconForm.Glyph, icon.Form);
            Assert.AreEqual(glyph, icon.Glyph);
        }

        [Test]
        public void Icon_AssetPath_LoadsTheTexture()
        {
            var texture = MakeTexture();
            var icon = Looks.Icon(k_Texture);
            Assert.AreEqual(IconForm.Image, icon.Form);
            Assert.AreSame(texture, icon.Image);
        }

        [Test]
        public void Icon_AssetPath_PrefersAVectorImage()
        {
            MakeTexture();
            var vector = ScriptableObject.CreateInstance<VectorImage>();
            AssetDatabase.CreateAsset(vector, k_Vector);
            var icon = Looks.Icon(k_Vector);
            Assert.AreEqual(IconForm.Image, icon.Form);
            Assert.AreSame(vector, icon.Image);
        }

        [Test]
        public void Icon_EditorIconName_LoadsUnitysIcon()
        {
            var icon = Looks.Icon("console.warnicon");
            Assert.IsNotNull(icon);
            Assert.AreEqual(IconForm.Image, icon.Form);
            Assert.IsInstanceOf<Texture2D>(icon.Image);
        }

        [Test]
        public void Icon_NothingMatches_IsNull()
        {
            Assert.IsNull(Looks.Icon("Assets/Nowhere/Missing.png"));
            Assert.IsNull(Looks.Icon("no such icon anywhere"));
            Assert.IsNull(Looks.Icon("path:not path data"));
            Assert.IsNull(Looks.Icon(null));
            Assert.IsNull(Looks.Icon(" "));
        }

        [Test]
        public void Icon_AnAssetAddedLater_IsFoundAfterTheProjectChanges()
        {
            Assert.IsNull(Looks.Icon(k_Texture));
            var texture = MakeTexture();
            Looks.OnProjectChanged();
            Assert.AreSame(texture, Looks.Icon(k_Texture).Image);
        }

        [Test]
        public void PathAndGlyphIcons_StayCachedWhenTheProjectChanges()
        {
            var shape = Looks.Icon("path:M0 0L10 10L0 10Z");
            Looks.OnProjectChanged();
            Assert.AreSame(shape, Looks.Icon("path:M0 0L10 10L0 10Z"));
        }

        [Test]
        public void TypeWithoutAnAttribute_HasNoLook()
        {
            var look = Looks.OfBlock(typeof(Unmarked));
            Assert.IsNull(look.Color);
            Assert.IsNull(look.Icon);
            Assert.IsNull(Looks.OfBlock(null).Color);
        }

        [Test]
        public void BlockLook_FromBlockInfo()
        {
            var look = Looks.OfBlock(typeof(BaseBlock));
            Assert.AreEqual(Color.red, look.Color);
            Assert.AreEqual(IconForm.Stroke, look.Icon.Form);
        }

        [Test]
        public void BlockLook_AssetIcon()
        {
            var texture = MakeTexture();
            Assert.AreSame(texture, Looks.OfBlock(typeof(TextureIcon)).Icon.Image);
        }

        [Test]
        public void ColorAndIcon_InheritFromTheNearestBaseThatSetsThem()
        {
            Assert.AreEqual(Color.red, Looks.OfBlock(typeof(DerivedBlock)).Color);
            Assert.AreEqual(IconForm.Stroke, Looks.OfBlock(typeof(PlainBlock)).Icon.Form);

            var iconOnly = Looks.OfBlock(typeof(IconOnlyBlock));
            Assert.AreEqual(Color.red, iconOnly.Color);
            Assert.AreEqual("faction", iconOnly.Icon.Glyph);

            var place = Looks.OfDefinition(typeof(LorePlace));
            Assert.AreEqual(Color.green, place.Color);
            Assert.AreEqual("faction", place.Icon.Glyph);

            var elder = Looks.OfDefinition(typeof(LoreElder));
            Assert.IsTrue(Looks.TryColor("#C792EA", out var lore));
            Assert.AreEqual(lore, elder.Color);
            Assert.AreEqual("faction", elder.Icon.Glyph);

            var part = Looks.OfPart(typeof(DerivedPart));
            Assert.AreEqual(Color.red, part.Color);
            Assert.AreEqual("query", part.Icon.Glyph);
        }

        [Test]
        public void Group_Inherits()
        {
            Assert.AreEqual("AI", Looks.PartGroup(typeof(BasePart)));
            Assert.AreEqual("AI", Looks.PartGroup(typeof(DerivedPart)));
            Assert.AreEqual("Presentation", Looks.PartGroup(typeof(RegroupedPart)));
            Assert.AreEqual("Lore", Looks.DefinitionGroup(typeof(LoreElder)));
            Assert.AreEqual("Lore", Looks.DefinitionGroup(typeof(LorePlace)));
            Assert.IsNull(Looks.DefinitionGroup(typeof(Unmarked)));
        }

        [Test]
        public void LabelTitleDescriptionAndHidden_DoNotInherit()
        {
            Assert.IsNull(Looks.Own<UnitPartAttribute>(typeof(DerivedPart)));
            Assert.AreEqual("Own Part", Looks.Own<UnitPartAttribute>(typeof(RegroupedPart)).Label);
            Assert.IsFalse(Looks.Own<UnitPartAttribute>(typeof(RegroupedPart)).Hidden);
            Assert.IsNull(Looks.Own<UnitPartAttribute>(typeof(RegroupedPart)).Description);

            Assert.IsNull(Looks.Own<DefinitionInfoAttribute>(typeof(LorePerson)));
            Assert.AreEqual("Places", Looks.Own<DefinitionInfoAttribute>(typeof(LorePlace)).Title);
            Assert.IsNull(Looks.Own<DefinitionInfoAttribute>(typeof(LorePlace)).Description);

            Assert.IsNull(BlockTypes.InfoOf(typeof(PlainBlock)));
            Assert.AreEqual("Derived block", BlockTypes.InfoOf(typeof(DerivedBlock)).Description);
            Assert.IsNull(BlockTypes.TooltipOf(typeof(PlainBlock)));
        }

        [Test]
        public void BadColor_FallsBack_WithOneWarningPerType()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Couldn't read Color \"#nothex\" on BadColor"));
            Assert.IsNull(Looks.OfBlock(typeof(BadColor)).Color);
            Assert.IsNull(Looks.OfBlock(typeof(BadColor)).Color);

            LogAssert.Expect(LogType.Warning, new Regex("Couldn't read Color \"not a color\" on OtherBadColor"));
            Assert.IsNull(Looks.OfBlock(typeof(OtherBadColor)).Color);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MissingIcon_FallsBack_WithOneWarningPerType()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Couldn't find Icon \"Assets/Nowhere/Missing.png\" on MissingIcon"));
            Assert.IsNull(Looks.OfBlock(typeof(MissingIcon)).Icon);
            Assert.IsNull(Looks.OfBlock(typeof(MissingIcon)).Icon);
            Looks.OnProjectChanged();
            Assert.IsNull(Looks.OfBlock(typeof(MissingIcon)).Icon);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BadPathData_FallsBack_WithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Couldn't read the SVG path data in Icon \"path:M1 1X\" on BadPath"));
            Assert.IsNull(Looks.OfBlock(typeof(BadPath)).Icon);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DefinitionData_HasItsOwnCategory()
        {
            Assert.AreEqual(BlockCategory.Data, BlockCategories.Of(typeof(Notes)));
            Assert.AreEqual(BlockCategory.Data, BlockCategories.Of(typeof(IDefinitionData)));
            Assert.AreEqual("Data", BlockCategories.Label(BlockCategory.Data));
            Assert.AreEqual("cg-data", BlockCategories.ClassName(BlockCategory.Data));
            Assert.IsTrue(Looks.IsGlyph("data"));
        }

        [Test]
        public void BlockIcon_ImageIcons_BecomeItsBackground()
        {
            var texture = MakeTexture();
            var icon = new BlockIcon();
            icon.SetIcon(k_Texture);
            Assert.AreEqual(texture.GetInstanceID(), icon.style.backgroundImage.value.texture.GetInstanceID());

            icon.SetIcon("faction");
            Assert.AreEqual(StyleKeyword.Null, icon.style.backgroundImage.keyword);

            icon.SetIcon(k_Texture);
            icon.SetCategory(BlockCategory.Payload);
            Assert.AreEqual(StyleKeyword.Null, icon.style.backgroundImage.keyword);
        }
    }
}
