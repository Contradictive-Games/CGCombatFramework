using System;
using System.Collections.Generic;
using System.Linq;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    public sealed class BlockPickerTests
    {
        private interface IPickerTestBlock
        {
        }

        [Serializable, TypeMenu("Strike")]
        [BlockInfo("Hits once", Example = "One sword swing")]
        private sealed class Strike : IPickerTestBlock
        {
        }

        [Serializable, TypeMenu("Alpha Pulse")]
        private sealed class AlphaPulse : IPickerTestBlock
        {
        }

        [Serializable, TypeMenu("Health/Heal")]
        [BlockInfo("Restores health to the subject")]
        private sealed class Heal : IPickerTestBlock
        {
        }

        [Serializable, TypeMenu("Health/Deal Damage")]
        [BlockInfo("Sends damage through the pipeline")]
        private sealed class DealDamage : IPickerTestBlock
        {
        }

        [Serializable, TypeMenu("Health/Over Time/Bleed")]
        private sealed class Bleed : IPickerTestBlock
        {
        }

        [Serializable, TypeMenu("Areas/Heal Zone")]
        private sealed class HealZone : IPickerTestBlock
        {
        }

        private sealed class NotSerializable : IPickerTestBlock
        {
        }

        private BlockPickerModel model;
        private BlockPickerWindow window;

        [SetUp]
        public void SetUp()
        {
            model = new BlockPickerModel(typeof(IPickerTestBlock), true);
            StudioPrefs.Remove(model.Key);
        }

        [TearDown]
        public void TearDown()
        {
            if(window != null) Object.DestroyImmediate(window);
            StudioPrefs.Remove(model.Key);
        }

        [Test]
        public void Rows_ListNoneThenLooseBlocksThenFoldersAtEachLevel()
        {
            var rows = model.Rows(null);

            Assert.That(rows.Select(r => r.Label), Is.EqualTo(new[] { "None", "Alpha Pulse", "Strike", "Areas", "Heal Zone", "Health", "Deal Damage", "Heal", "Over Time", "Bleed" }));
            Assert.That(rows.Select(r => r.Depth), Is.EqualTo(new[] { 0, 0, 0, 0, 1, 0, 1, 1, 1, 2 }));
            Assert.That(rows[0].Kind, Is.EqualTo(PickerRowKind.None));
            Assert.That(rows.Where(r => r.Kind == PickerRowKind.Heading).Select(r => r.Path), Is.EqualTo(new[] { "Areas", "Health", "Health/Over Time" }));
            Assert.That(rows.Where(r => r.Kind == PickerRowKind.Heading).Select(r => r.Count), Is.EqualTo(new[] { 1, 3, 1 }));
            Assert.That(rows.Single(r => r.Type == typeof(Bleed)).Path, Is.EqualTo("Health/Over Time"));
            Assert.That(rows.Select(r => r.Type), Has.No.Member(typeof(NotSerializable)));
        }

        [Test]
        public void Rows_HideWhatIsUnderClosedHeadings()
        {
            var health = model.Rows(new[] { "Health" }).Select(r => r.Label);
            var overTime = model.Rows(new[] { "Health/Over Time" }).Select(r => r.Label);

            Assert.That(health, Is.EqualTo(new[] { "None", "Alpha Pulse", "Strike", "Areas", "Heal Zone", "Health" }));
            Assert.That(overTime, Has.Member("Over Time"));
            Assert.That(overTime, Has.No.Member("Bleed"));
        }

        [Test]
        public void Rows_LeaveOutNoneWhenTheSlotNeedsABlock()
        {
            var required = new BlockPickerModel(typeof(IPickerTestBlock), false);

            Assert.That(required.Rows(null).Select(r => r.Kind), Has.No.Member(PickerRowKind.None));
        }

        [Test]
        public void TitleAndKey_ComeFromTheSlotType()
        {
            Assert.That(model.Title, Is.EqualTo(BlockCategories.Label(BlockCategory.Other)));
            Assert.That(model.Key, Is.EqualTo(BlockPickerModel.k_KeyPrefix + typeof(IPickerTestBlock).FullName));
            Assert.That(model.Category, Is.EqualTo(BlockCategory.Other));
        }

        [Test]
        public void Search_MatchesNamesAndFoldersAndRanksNameStartsFirst()
        {
            var rows = model.Search("heal");

            Assert.That(rows.Select(r => r.Label), Is.EqualTo(new[] { "Heal", "Heal Zone", "Bleed", "Deal Damage" }));
            Assert.That(rows.Select(r => r.Path), Is.EqualTo(new[] { "Health", "Areas", "Health/Over Time", "Health" }));
            Assert.That(rows.Select(r => r.Kind), Is.All.EqualTo(PickerRowKind.Block));
            Assert.That(rows.Select(r => r.Depth), Is.All.EqualTo(0));
        }

        [Test]
        public void Search_NeedsEveryWord_AndRanksAllWordsInTheNameNext()
        {
            Assert.That(model.Search("zone heal").Select(r => r.Label), Is.EqualTo(new[] { "Heal Zone" }));
            Assert.That(model.Search("over bleed").Select(r => r.Label), Is.EqualTo(new[] { "Bleed" }));
            Assert.That(model.Search("deal  dam").Select(r => r.Label), Is.EqualTo(new[] { "Deal Damage" }));
            Assert.That(model.Search("health s").Select(r => r.Label), Is.Empty);
        }

        [Test]
        public void Search_IgnoresDescriptions_AndBlankQueries()
        {
            Assert.That(model.Search("pipeline"), Is.Empty);
            Assert.That(model.Search("sword"), Is.Empty);
            Assert.That(model.Search("   "), Is.Empty);
            Assert.That(BlockPickerModel.IsSearch("   "), Is.False);
            Assert.That(BlockPickerModel.IsSearch(" a "), Is.True);
        }

        [Test]
        public void PathTo_ListsTheHeadingsThatHoldAType()
        {
            Assert.That(model.PathTo(typeof(Bleed)), Is.EqualTo(new[] { "Health", "Health/Over Time" }));
            Assert.That(model.PathTo(typeof(Strike)), Is.Empty);
            Assert.That(model.PathTo(null), Is.Empty);
            Assert.That(model.PathTo(typeof(NotSerializable)), Is.Empty);
        }

        [Test]
        public void Describe_FillsThePaneForEachKindOfRow()
        {
            var rows = model.Rows(null);

            BlockPickerModel.Describe(rows.Single(r => r.Type == typeof(Strike)), out string title, out string text, out string example);
            Assert.That((title, text, example), Is.EqualTo(("Strike", "Hits once", "Example: One sword swing")));

            BlockPickerModel.Describe(rows.Single(r => r.Type == typeof(AlphaPulse)), out _, out text, out example);
            Assert.That((text, example), Is.EqualTo(("No description", (string)null)));

            BlockPickerModel.Describe(rows.Single(r => r.Path == "Health" && r.Kind == PickerRowKind.Heading), out _, out text, out _);
            Assert.That(text, Is.EqualTo("Health · 3 blocks"));

            BlockPickerModel.Describe(rows.Single(r => r.Path == "Areas" && r.Kind == PickerRowKind.Heading), out _, out text, out _);
            Assert.That(text, Is.EqualTo("Areas · 1 block"));

            BlockPickerModel.Describe(rows[0], out title, out text, out _);
            Assert.That((title, text), Is.EqualTo(("None", BlockPickerModel.k_NoneText)));

            BlockPickerModel.Describe(null, out title, out text, out _);
            Assert.That((title, text), Is.EqualTo(((string)null, BlockPickerModel.k_NoMatchText)));
        }

        [Test]
        public void ForAll_PutsEachCategoryUnderItsOwnHeadingInTheGivenOrder()
        {
            var all = BlockPickerModel.ForAll(new[] { typeof(IRegenRule), typeof(IFireGate) });
            var rows = all.Rows(null);
            var regen = BlockTypes.Candidates(typeof(IRegenRule))[0];

            Assert.That(all.Title, Is.EqualTo(BlockPickerModel.k_AllTitle));
            Assert.That(all.Key, Is.EqualTo(BlockPickerModel.k_KeyPrefix + "All"));
            Assert.That(all.Category, Is.Null);
            Assert.That(rows.Where(r => r.Depth == 0).Select(r => r.Label), Is.EqualTo(new[] { "Regen", "Gate" }));
            Assert.That(rows.Select(r => r.Kind), Has.No.Member(PickerRowKind.None));
            Assert.That(all.PathTo(regen)[0], Is.EqualTo("Regen"));
            Assert.That(all.Rows(new[] { "Regen" }).Select(r => r.Type), Has.No.Member(regen));
        }

        [Test]
        public void Window_OpensOnTheCurrentType_WithItsHeadingsOpen()
        {
            StudioPrefs.Store(model.Key, "Health\nHealth/Over Time");

            window = BlockPickerWindow.Create(model, typeof(Bleed), null);

            Assert.That(window.Highlighted?.Type, Is.EqualTo(typeof(Bleed)));
            Assert.That(window.Shown.Select(r => r.Label), Has.Member("Deal Damage"));
        }

        [Test]
        public void Window_KeysMoveTheHighlightAndCloseAndOpenHeadings()
        {
            window = BlockPickerWindow.Create(model, typeof(Heal), null);

            Assert.That(window.Press(KeyCode.LeftArrow), Is.True);
            Assert.That(window.Highlighted.Path, Is.EqualTo("Health"));
            window.Press(KeyCode.LeftArrow);
            Assert.That(window.Shown.Select(r => r.Label), Has.No.Member("Heal"));
            Assert.That(StudioPrefs.GetString(model.Key), Is.EqualTo("Health"));
            window.Press(KeyCode.RightArrow);
            Assert.That(window.Shown.Select(r => r.Label), Has.Member("Heal"));
            Assert.That(StudioPrefs.GetString(model.Key), Is.Empty);
            window.Press(KeyCode.DownArrow);
            Assert.That(window.Highlighted.Label, Is.EqualTo("Deal Damage"));
            window.Press(KeyCode.UpArrow);
            window.Press(KeyCode.UpArrow);
            Assert.That(window.Highlighted.Label, Is.EqualTo("Heal Zone"));
            Assert.That(window.Press(KeyCode.A), Is.False);
        }

        [Test]
        public void Window_SearchHighlightsTheFirstResult_AndClearingRestoresTheList()
        {
            window = BlockPickerWindow.Create(model, null, null);

            window.Search("dam");
            Assert.That(window.Shown.Select(r => r.Label), Is.EqualTo(new[] { "Deal Damage" }));
            Assert.That(window.Highlighted.Type, Is.EqualTo(typeof(DealDamage)));
            Assert.That(window.Press(KeyCode.LeftArrow), Is.False);

            window.Search("");
            Assert.That(window.Shown.Count, Is.EqualTo(model.Rows(null).Count));
            Assert.That(window.Highlighted.Type, Is.EqualTo(typeof(DealDamage)));
        }

        [Test]
        public void Window_PicksTheHighlightedBlockOnce()
        {
            var picks = new List<Type>();
            window = BlockPickerWindow.Create(model, null, picks.Add);

            window.Search("strike");
            window.Press(KeyCode.Return);

            Assert.That(picks, Is.EqualTo(new[] { typeof(Strike) }));
            Assert.IsTrue(window == null);
        }

        [Test]
        public void Window_EscapeClosesWithoutPicking()
        {
            var picks = new List<Type>();
            window = BlockPickerWindow.Create(model, null, picks.Add);

            Assert.That(window.Press(KeyCode.Escape), Is.True);

            Assert.That(picks, Is.Empty);
            Assert.IsTrue(window == null);
        }

        [Test]
        public void Window_EnterOnAHeadingOpensOrClosesIt_AndNonePicksNull()
        {
            var picks = new List<Type>();
            window = BlockPickerWindow.Create(model, null, picks.Add);
            int health = window.Shown.ToList().FindIndex(r => r.Kind == PickerRowKind.Heading && r.Path == "Health");

            window.Activate(health);
            Assert.That(window.Shown.Select(r => r.Label), Has.No.Member("Heal"));
            Assert.That(picks, Is.Empty);
            window.Activate(health);
            Assert.That(window.Shown.Select(r => r.Label), Has.Member("Heal"));

            window.Activate(0);
            Assert.That(picks, Is.EqualTo(new Type[] { null }));
        }
    }
}
