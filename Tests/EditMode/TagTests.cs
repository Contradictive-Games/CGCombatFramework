using System.Collections.Generic;
using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class TagTests
    {
        private TestAssets assets;
        private FakeUnit unit;
        private readonly List<TagAddedEvent> added = new();
        private readonly List<TagRemovedEvent> removed = new();
        private GameEventHandler<TagAddedEvent> onAdded;
        private GameEventHandler<TagRemovedEvent> onRemoved;
        private GameEventHandler<TagAddedEvent> onGlobalAdded;
        private int globalAdded;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            unit = new FakeUnit("Tagged");
            added.Clear();
            removed.Clear();
            globalAdded = 0;
            onAdded = (in TagAddedEvent e) => added.Add(e);
            onRemoved = (in TagRemovedEvent e) => removed.Add(e);
            onGlobalAdded = (in TagAddedEvent e) => globalAdded++;
            unit.Events.Subscribe(onAdded);
            unit.Events.Subscribe(onRemoved);
            CombatServices.Events.Subscribe(onGlobalAdded);
        }

        [TearDown]
        public void TearDown()
        {
            CombatServices.Events.Unsubscribe(onGlobalAdded);
            unit.Dispose();
            assets.Dispose();
        }

        [Test]
        public void Tags_AreReferenceCounted()
        {
            var tags = new TagContainer(null);
            var stunned = assets.Tag("State.Stunned");
            tags.Add(stunned);
            tags.Add(stunned);
            tags.Remove(stunned);
            Assert.IsTrue(tags.HasExact(stunned));
            tags.Remove(stunned);
            Assert.IsFalse(tags.HasExact(stunned));
        }

        [Test]
        public void Has_MatchesChildTags()
        {
            var state = assets.Tag("State");
            var tags = new TagContainer(null);
            tags.Add(assets.Tag("State.Stunned", state));
            Assert.IsTrue(tags.Has(state));
        }

        [Test]
        public void Owned_RaisesOnFirstAddAndLastRemove_OnBothBuses()
        {
            var stunned = assets.Tag("State.Stunned");
            unit.Tags.Add(stunned);
            unit.Tags.Add(stunned);
            Assert.AreEqual(1, added.Count);
            Assert.AreEqual(1, globalAdded);
            Assert.AreSame(unit, added[0].Unit);
            Assert.AreSame(stunned, added[0].Tag);

            unit.Tags.Remove(stunned);
            Assert.AreEqual(0, removed.Count, "one holder is left");
            unit.Tags.Remove(stunned);
            Assert.AreEqual(1, removed.Count);
            Assert.AreSame(stunned, removed[0].Tag);
        }

        [Test]
        public void Clear_RaisesOneRemovalPerTag()
        {
            var stunned = assets.Tag("State.Stunned");
            var burning = assets.Tag("State.Burning");
            unit.Tags.Add(stunned);
            unit.Tags.Add(burning);
            unit.Tags.Add(burning);

            unit.Tags.Clear();

            Assert.AreEqual(2, removed.Count);
            Assert.IsFalse(unit.Tags.HasExact(stunned));
            Assert.IsFalse(unit.Tags.HasExact(burning));
        }

        [Test]
        public void Clear_LetsHandlersChangeTags()
        {
            var stunned = assets.Tag("State.Stunned");
            var dazed = assets.Tag("State.Dazed");
            unit.Tags.Add(stunned);
            GameEventHandler<TagRemovedEvent> daze = (in TagRemovedEvent e) => unit.Tags.Add(dazed);
            unit.Events.Subscribe(daze);

            Assert.DoesNotThrow(() => unit.Tags.Clear());
            unit.Events.Unsubscribe(daze);
            Assert.IsTrue(unit.Tags.HasExact(dazed));
            Assert.IsFalse(unit.Tags.HasExact(stunned));
        }

        [Test]
        public void Unowned_RaisesNothing()
        {
            var tags = new TagContainer(null);
            tags.Add(assets.Tag("State.Stunned"));
            Assert.AreEqual(0, globalAdded);
        }
    }
}
