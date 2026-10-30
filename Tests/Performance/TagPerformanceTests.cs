using NUnit.Framework;
using Unity.PerformanceTesting;

namespace CG.CombatFramework.Tests
{
    public class TagPerformanceTests
    {
        public enum TagQuery { HeldTag, ParentOfAHeldTag, NotHeld, AnyOfThreeNotHeld }

        private TestAssets assets;
        private TagContainer tags;
        private CombatTag held;
        private CombatTag parent;
        private CombatTag missing;
        private CombatTag[] blockedBy;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            tags = new TagContainer(null);

            var state = assets.Tag("State");
            var control = assets.Tag("State.Control", state);
            var movement = assets.Tag("State.Movement", state);
            var element = assets.Tag("Element");
            var fire = assets.Tag("Element.Fire", element);
            var frost = assets.Tag("Element.Frost", element);
            var rank = assets.Tag("Rank");
            var buff = assets.Tag("Status.Buff", assets.Tag("Status"));

            tags.Add(assets.Tag("State.Control.Stunned", control));
            tags.Add(assets.Tag("State.Movement.Rooted", movement));
            tags.Add(assets.Tag("State.Movement.Slowed", movement));
            tags.Add(assets.Tag("Element.Fire.Burning", fire));
            tags.Add(assets.Tag("Element.Frost.Chilled", frost));
            tags.Add(assets.Tag("Rank.Elite", rank));
            tags.Add(assets.Tag("Faction.Undead", assets.Tag("Faction")));
            held = assets.Tag("Status.Buff.Haste", buff);
            tags.Add(held);

            parent = buff;
            missing = assets.Tag("State.Invulnerable", state);
            blockedBy = new[] { missing, assets.Tag("State.Silenced", state), assets.Tag("Element.Fire.Immune", fire) };
        }

        [TearDown] public void TearDown() => assets.Dispose();

        [Test, Performance]
        public void Tags_QueryEightHeldTags([Values] TagQuery query)
        {
            int hits = 0;
            Measure.Method(() =>
                {
                    bool found = query switch
                    {
                        TagQuery.HeldTag => tags.Has(held),
                        TagQuery.ParentOfAHeldTag => tags.Has(parent),
                        TagQuery.NotHeld => tags.Has(missing),
                        _ => tags.HasAny(blockedBy)
                    };
                    if (found) hits++;
                })
                .SampleGroup(new SampleGroup("Tags", SampleUnit.Microsecond))
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(10_000)
                .GC()
                .Run();
            bool shouldFind = query == TagQuery.HeldTag || query == TagQuery.ParentOfAHeldTag;
            Assert.AreEqual(shouldFind, hits > 0);
        }
    }
}
