using System;
using System.Collections.Generic;
using System.Linq;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    public sealed class DatabaseCatalogTests
    {
        [DefinitionInfo("Characters", Group = "Lore", Description = "  People in the story  ", Color = "#C792EA")]
        private sealed class CharacterDefinition : GameDefinition
        {
        }

        [DefinitionInfo(Group = " Lore / People ")]
        private sealed class ElderDefinition : GameDefinition
        {
        }

        [DefinitionInfo(Group = "Lore")]
        private abstract class LoreDefinition : GameDefinition
        {
        }

        private sealed class Place : LoreDefinition
        {
        }

        [DefinitionInfo(Group = "Audio", Description = "   ")]
        private sealed class SoundBank : GameDefinition
        {
        }

        private sealed class QuestDefinition : GameDefinition
        {
        }

        private sealed class Ability : GameDefinition
        {
        }

        private sealed class Skill : GameDefinition
        {
        }

        [DefinitionInfo(Group = "/ / ")]
        private sealed class BlankGroup : GameDefinition
        {
        }

        [DefinitionInfo(Group = "other")]
        private sealed class Almanac : GameDefinition
        {
        }

        private sealed class Wrapper<T> : GameDefinition
        {
        }

        private sealed class NotADefinition : ScriptableObject
        {
        }

        [DefinitionInfo("Armory", Group = "combat")]
        private sealed class ArmoryDefinition : GameDefinition
        {
        }

        [DefinitionInfo("Wounds", Group = "Combat", Order = -45)]
        private sealed class WoundDefinition : GameDefinition
        {
        }

        [DefinitionInfo("Zebras", Group = "Ranked", Order = -1)]
        private sealed class Zebra : GameDefinition
        {
        }

        [DefinitionInfo("Apples", Group = "Ranked")]
        private sealed class Apple : GameDefinition
        {
        }

        [DefinitionInfo("Mangos", Group = "Ranked", Order = 2)]
        private sealed class Mango : GameDefinition
        {
        }

        [DefinitionInfo("Firsts", Order = -500)]
        private sealed class First : GameDefinition
        {
        }

        private class Noted : GameDefinition
        {
            [SerializeField] private string description = "  Kept on the base  ";
        }

        private sealed class NotedChild : Noted
        {
        }

        private sealed class Unserialized : GameDefinition
        {
            private string description = "Hidden";

            public string Peek => description;
        }

        private static readonly Type[] given =
        {
            typeof(QuestDefinition), typeof(CharacterDefinition), typeof(ElderDefinition), typeof(LoreDefinition), typeof(Place), typeof(SoundBank),
            typeof(Skill), typeof(BlankGroup), typeof(Almanac), typeof(Wrapper<>), typeof(NotADefinition), typeof(FirableDefinition), typeof(QuestDefinition), null
        };

        private static readonly Type[] ordered =
        {
            typeof(Mango), typeof(ArmoryDefinition), typeof(Apple), typeof(WoundDefinition), typeof(Zebra), typeof(First)
        };

        private static readonly string[] builtIns =
        {
            "Units", "Firables", "Status Effects", "Damage Pipelines", "Stat Profiles", "Stat Types", "Resource Types", "Damage Types", "Stat Modifiers",
            "Tags", "Factions", "Firable Slots"
        };

        private readonly List<Object> made = new();

        [SetUp]
        public void SetUp() => Looks.Clear();

        [TearDown]
        public void TearDown()
        {
            foreach(var each in made)
            {
                if(each != null) Object.DestroyImmediate(each);
            }
            made.Clear();
            Looks.Clear();
        }

        [Test]
        public void Categories_StartWithEverythingThenTheBuiltInsInTheCombatSection()
        {
            var categories = DatabaseCatalog.Categories(given);

            Assert.That(categories[0].IsAll, Is.True);
            Assert.That(categories[0].Title, Is.EqualTo("Everything"));
            Assert.That(categories.Skip(1).Take(builtIns.Length).Select(c => c.Title), Is.EqualTo(builtIns));
            Assert.That(categories.Skip(1).Take(builtIns.Length).Select(c => c.Group), Is.All.EqualTo(DatabaseCatalog.k_Combat));
        }

        [Test]
        public void Categories_ListCustomTypesOnceEachByTitle()
        {
            var custom = DatabaseCatalog.Categories(given).Skip(1 + builtIns.Length).Select(c => c.Title);

            Assert.That(custom, Is.EqualTo(new[] { "Almanacs", "Blank Groups", "Characters", "Elders", "Places", "Quests", "Skills", "Sound Banks" }));
        }

        [Test]
        public void Categories_SkipAbstractGenericBuiltInAndNonDefinitionTypes()
        {
            var types = DatabaseCatalog.Categories(given).Select(c => c.Type).ToList();

            Assert.That(types, Has.No.Member(typeof(LoreDefinition)));
            Assert.That(types, Has.No.Member(typeof(Wrapper<>)));
            Assert.That(types, Has.No.Member(typeof(NotADefinition)));
            Assert.That(types.Count(t => t == typeof(FirableDefinition)), Is.EqualTo(1));
        }

        [Test]
        public void Categories_TakeTrimmedDescriptionsAndDropBlankOnes()
        {
            var categories = DatabaseCatalog.Categories(given);

            Assert.That(Find(categories, typeof(CharacterDefinition)).Description, Is.EqualTo("People in the story"));
            Assert.That(Find(categories, typeof(SoundBank)).Description, Is.Null);
            Assert.That(Find(categories, typeof(QuestDefinition)).Description, Is.Null);
        }

        [Test]
        public void DefinitionTypes_LeaveOutTestAssemblies()
        {
            var types = DatabaseCatalog.DefinitionTypes().ToList();

            Assert.That(types, Has.Member(typeof(Faction)));
            Assert.That(types, Has.No.Member(typeof(QuestDefinition)));
        }

        [Test]
        public void BuiltIns_TakeTitleDescriptionGroupAndOrderFromTheirAttribute()
        {
            var builtIn = DatabaseCatalog.Categories(Type.EmptyTypes).Skip(1).ToList();

            Assert.That(builtIn.Select(c => c.Title), Is.EqualTo(builtIns));
            foreach(var category in builtIn)
            {
                var attribute = Looks.Own<DefinitionInfoAttribute>(category.Type);
                Assert.That(category.Title, Is.EqualTo(attribute.Title));
                Assert.That(category.Description, Is.EqualTo(attribute.Description));
                Assert.That(category.Group, Is.EqualTo(DatabaseCatalog.k_Combat));
                Assert.That(category.Style, Is.EqualTo(BuiltInStyles.Of(category.Type)));
            }
            Assert.That(builtIn.Select(c => DatabaseCatalog.OrderOf(c.Type)), Is.Ordered);
        }

        [Test]
        public void Order_SortsEachSection_AndCustomCombatTypesFollowTheBuiltIns()
        {
            var roots = DatabaseCatalog.Layout(DatabaseCatalog.Categories(ordered), true);

            Assert.That(roots.Select(n => n.Category.Title), Is.EqualTo(new[] { "Everything", "Combat", "Ranked", "Other" }));
            Assert.That(Titles(roots[1]), Is.EqualTo(new[]
            {
                "Units", "Firables", "Status Effects", "Damage Pipelines", "Stat Profiles", "Stat Types", "Resource Types", "Damage Types", "Wounds", "Stat Modifiers",
                "Tags", "Factions", "Firable Slots", "Armory"
            }));
            Assert.That(Titles(roots[2]), Is.EqualTo(new[] { "Zebras", "Apples", "Mangos" }));
            Assert.That(Titles(roots[3]), Is.EqualTo(new[] { "Firsts" }));
        }

        [Test]
        public void Order_SortsTheFlatList()
        {
            var titles = DatabaseCatalog.Layout(DatabaseCatalog.Categories(ordered), false).Select(n => n.Category.Title);

            Assert.That(titles, Is.EqualTo(new[]
            {
                "Everything", "Firsts", "Units", "Firables", "Status Effects", "Damage Pipelines", "Stat Profiles", "Stat Types", "Resource Types", "Damage Types", "Wounds",
                "Stat Modifiers", "Tags", "Factions", "Firable Slots", "Zebras", "Apples", "Armory", "Mangos"
            }));
        }

        [Test]
        public void TitleOf_UsesTheAttributeThenThePluralTypeName()
        {
            Assert.That(DatabaseCatalog.TitleOf(typeof(Faction)), Is.EqualTo("Factions"));
            Assert.That(DatabaseCatalog.TitleOf(typeof(CharacterDefinition)), Is.EqualTo("Characters"));
            Assert.That(DatabaseCatalog.TitleOf(typeof(QuestDefinition)), Is.EqualTo("Quests"));
            Assert.That(DatabaseCatalog.TitleOf(typeof(Ability)), Is.EqualTo("Abilities"));
            Assert.That(DatabaseCatalog.TitleOf(typeof(Place)), Is.EqualTo("Places"));
        }

        [Test]
        public void SectionOf_PutsBuiltInsInCombatAndUngroupedTypesInOther()
        {
            Assert.That(DatabaseCatalog.SectionOf(typeof(Unit)), Is.EqualTo(DatabaseCatalog.k_Combat));
            Assert.That(DatabaseCatalog.SectionOf(typeof(StatType)), Is.EqualTo(DatabaseCatalog.k_Combat));
            Assert.That(DatabaseCatalog.SectionOf(typeof(QuestDefinition)), Is.EqualTo(DatabaseCatalog.k_Other));
            Assert.That(DatabaseCatalog.SectionOf(typeof(BlankGroup)), Is.EqualTo(DatabaseCatalog.k_Other));
            Assert.That(DatabaseCatalog.SectionOf(typeof(Almanac)), Is.EqualTo(DatabaseCatalog.k_Other));
        }

        [Test]
        public void SectionOf_ReadsTheGroupFromTheTypeOrItsBaseClass()
        {
            Assert.That(DatabaseCatalog.SectionOf(typeof(CharacterDefinition)), Is.EqualTo("Lore"));
            Assert.That(DatabaseCatalog.SectionOf(typeof(Place)), Is.EqualTo("Lore"));
            Assert.That(DatabaseCatalog.SectionOf(typeof(ElderDefinition)), Is.EqualTo("Lore/People"));
        }

        [Test]
        public void NormalizeGroup_TrimsSegmentsAndDropsEmptyOnes()
        {
            Assert.That(DatabaseCatalog.NormalizeGroup(" Lore / People "), Is.EqualTo("Lore/People"));
            Assert.That(DatabaseCatalog.NormalizeGroup("Lore//People/"), Is.EqualTo("Lore/People"));
            Assert.That(DatabaseCatalog.NormalizeGroup("combat / Extra"), Is.EqualTo("Combat/Extra"));
            Assert.That(DatabaseCatalog.NormalizeGroup("OTHER"), Is.EqualTo("Other"));
            Assert.That(DatabaseCatalog.NormalizeGroup("/ / "), Is.Null);
            Assert.That(DatabaseCatalog.NormalizeGroup("  "), Is.Null);
            Assert.That(DatabaseCatalog.NormalizeGroup(null), Is.Null);
        }

        [Test]
        public void Layout_Grouped_OrdersEverythingCombatNamedSectionsThenOther()
        {
            var roots = DatabaseCatalog.Layout(DatabaseCatalog.Categories(given), true);

            Assert.That(roots.Select(n => n.Category.Title), Is.EqualTo(new[] { "Everything", "Combat", "Audio", "Lore", "Other" }));
            Assert.That(roots[0].Children, Is.Empty);
            Assert.That(roots.Skip(1).Select(n => n.Category.IsSection), Is.All.True);
            Assert.That(roots[1].Children.Select(n => n.Category.Title), Is.EqualTo(builtIns));
            Assert.That(Titles(roots[2]), Is.EqualTo(new[] { "Sound Banks" }));
            Assert.That(Titles(roots[4]), Is.EqualTo(new[] { "Almanacs", "Blank Groups", "Quests", "Skills" }));
        }

        [Test]
        public void Layout_Grouped_NestsSubsectionsAfterTheirSectionsCategories()
        {
            var lore = DatabaseCatalog.Layout(DatabaseCatalog.Categories(given), true).Single(n => n.Category.Title == "Lore");
            var people = lore.Children[2];

            Assert.That(Titles(lore), Is.EqualTo(new[] { "Characters", "Places", "People" }));
            Assert.That(people.Category.IsSection, Is.True);
            Assert.That(people.Category.Group, Is.EqualTo("Lore/People"));
            Assert.That(people.Category.Key, Is.EqualTo(DatabaseCatalog.SectionKey("Lore/People")));
            Assert.That(Titles(people), Is.EqualTo(new[] { "Elders" }));
        }

        [Test]
        public void Layout_Grouped_SectionsHoldEverythingInsideThem()
        {
            var categories = DatabaseCatalog.Categories(given);
            var character = Make();
            var elder = Make();
            var quest = Make();
            Find(categories, typeof(CharacterDefinition)).Items.Add(character);
            Find(categories, typeof(ElderDefinition)).Items.Add(elder);
            Find(categories, typeof(QuestDefinition)).Items.Add(quest);

            var roots = DatabaseCatalog.Layout(categories, true);
            var lore = roots.Single(n => n.Category.Title == "Lore");

            Assert.That(lore.Category.Items, Is.EquivalentTo(new[] { character, elder }));
            Assert.That(lore.Children[2].Category.Items, Is.EquivalentTo(new[] { elder }));
            Assert.That(roots.Single(n => n.Category.Title == "Other").Category.Items, Is.EquivalentTo(new[] { quest }));
            Assert.That(roots.Single(n => n.Category.Title == "Combat").Category.IsMixed, Is.True);
        }

        [Test]
        public void Layout_Grouped_DropsSectionsWithNothingKept()
        {
            var categories = DatabaseCatalog.Categories(given);
            Find(categories, typeof(ElderDefinition)).Items.Add(Make());

            var roots = DatabaseCatalog.Layout(categories, true, c => c.Items.Count > 0);

            Assert.That(roots.Select(n => n.Category.Title), Is.EqualTo(new[] { "Everything", "Lore" }));
            Assert.That(Titles(roots[1]), Is.EqualTo(new[] { "People" }));
            Assert.That(Titles(roots[1].Children[0]), Is.EqualTo(new[] { "Elders" }));
        }

        [Test]
        public void Layout_Flat_ListsEveryKeptCategoryInOrderWithNoSections()
        {
            var categories = DatabaseCatalog.Categories(given);

            var roots = DatabaseCatalog.Layout(categories, false);
            var kept = DatabaseCatalog.Layout(categories, false, c => c.Type != typeof(Unit));

            Assert.That(roots.Select(n => n.Category), Is.EqualTo(categories));
            Assert.That(roots.SelectMany(n => n.Children), Is.Empty);
            Assert.That(kept.Count, Is.EqualTo(categories.Count - 1));
            Assert.That(kept[0].Category.IsAll, Is.True);
        }

        [Test]
        public void LookOf_UsesTheAttributeForCustomTypesOnly()
        {
            ColorUtility.TryParseHtmlString("#C792EA", out var lore);

            Assert.That(DatabaseCatalog.LookOf(typeof(CharacterDefinition)).Color, Is.EqualTo(lore));
            Assert.That(DatabaseCatalog.LookOf(typeof(QuestDefinition)).Color, Is.Null);
            Assert.That(DatabaseCatalog.LookOf(typeof(Faction)).Color, Is.Null);
            Assert.That(DatabaseCatalog.LookOf(typeof(Unit)).Color, Is.Null);
            Assert.That(DatabaseCatalog.LookOf((Type)null).Color, Is.Null);
        }

        [Test]
        public void StyleAndOrder_ComeFromTheStyleMapAndTheAttribute()
        {
            Assert.That(DatabaseCatalog.StyleOf(typeof(Faction)), Is.EqualTo("cg-kind-faction"));
            Assert.That(DatabaseCatalog.StyleOf(typeof(QuestDefinition)), Is.EqualTo(DatabaseCatalog.k_Definition));
            Assert.That(DatabaseCatalog.OrderOf(typeof(Unit)), Is.EqualTo(-120));
            Assert.That(DatabaseCatalog.OrderOf(typeof(FirableSlot)), Is.EqualTo(-10));
            Assert.That(DatabaseCatalog.OrderOf(typeof(QuestDefinition)), Is.EqualTo(0));
        }

        [Test]
        public void StudioKinds_TakePluralTitlesFromTheAttributeAndSingularNamesFromTheType()
        {
            Assert.That(StudioKinds.All.Select(k => k.Title), Is.EqualTo(new[] { "Firables", "Status Effects", "Damage Pipelines" }));
            Assert.That(StudioKinds.All.Select(k => k.Singular), Is.EqualTo(new[] { "Firable", "Status Effect", "Damage Pipeline" }));
        }

        [Test]
        public void AssetDescriptionOf_ReadsADescriptionFieldTrimmed()
        {
            var firable = Track(ScriptableObject.CreateInstance<FirableDefinition>());
            firable.Description = " Fires twice ";
            var blank = Track(ScriptableObject.CreateInstance<StatType>());
            blank.Description = "   ";
            var child = Track(ScriptableObject.CreateInstance<NotedChild>());
            var hidden = Track(ScriptableObject.CreateInstance<Unserialized>());
            var faction = Track(ScriptableObject.CreateInstance<Faction>());

            Assert.That(DatabaseCatalog.AssetDescriptionOf(firable), Is.EqualTo("Fires twice"));
            Assert.That(DatabaseCatalog.AssetDescriptionOf(blank), Is.Null);
            Assert.That(DatabaseCatalog.AssetDescriptionOf(child), Is.EqualTo("Kept on the base"));
            Assert.That(hidden.Peek, Is.EqualTo("Hidden"));
            Assert.That(DatabaseCatalog.AssetDescriptionOf(hidden), Is.Null);
            Assert.That(DatabaseCatalog.AssetDescriptionOf(faction), Is.Null);
            Assert.That(DatabaseCatalog.AssetDescriptionOf(null), Is.Null);
        }

        private T Track<T>(T asset) where T : Object
        {
            made.Add(asset);
            return asset;
        }

        private Object Make()
        {
            var texture = new Texture2D(1, 1);
            made.Add(texture);
            return texture;
        }

        private static DatabaseCategory Find(List<DatabaseCategory> categories, Type type) => categories.Single(c => c.Type == type);

        private static IEnumerable<string> Titles(CategoryNode node) => node.Children.Select(n => n.Category.Title);
    }
}
