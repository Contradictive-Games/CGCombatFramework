using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class ScalerTests
    {
        private TestAssets assets;
        private FakeUnit source;
        private FakeUnit target;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            source = new FakeUnit("Source");
            target = new FakeUnit("Target");
        }

        [TearDown]
        public void TearDown()
        {
            source.Dispose();
            target.Dispose();
            assets.Dispose();
        }

        private float Evaluate(float baseValue, IValueScaler[] scalers, float power = 1f, int stacks = 1, IGameRandom random = null)
        {
            var value = new ScalableValue(baseValue) { Scalers = scalers };
            return value.Evaluate(new EffectContext(source, null, target, default, power, stacks, 0, random));
        }

        private float Evaluate(float baseValue, IValueScaler scaler, float power = 1f, int stacks = 1) =>
            Evaluate(baseValue, new[] { scaler }, power, stacks);

        private StatType SourceStat(string name, float value)
        {
            var stat = assets.Stat(name);
            source.Stats.GetOrCreate(stat).SetBaseValue(value);
            return stat;
        }

        [Test]
        public void Stat_Multiply()
        {
            var scaler = TestRecipes.Configure(new StatScaler(), ("stat", SourceStat("DamageMultiplier", 1.5f)));
            Assert.AreEqual(15f, Evaluate(10f, scaler), 0.001f);
        }

        [Test]
        public void Stat_AddPerPoint()
        {
            var scaler = TestRecipes.Configure(new StatScaler(),
                ("stat", SourceStat("SpellPower", 20f)), ("mode", StatScaleMode.AddPerPoint), ("perPoint", 0.5f));
            Assert.AreEqual(20f, Evaluate(10f, scaler), 0.001f);
        }

        [Test]
        public void Stat_Missing_LeavesTheValueAlone()
        {
            var scaler = TestRecipes.Configure(new StatScaler(), ("stat", assets.Stat("DamageMultiplier")));
            Assert.AreEqual(10f, Evaluate(10f, scaler), 0.001f);
        }

        [Test]
        public void Power_IsWeighted()
        {
            var scaler = TestRecipes.Configure(new PowerScaler(), ("weight", 0.5f));
            Assert.AreEqual(20f, Evaluate(10f, scaler, power: 3f), 0.001f);
        }

        [TestCase(1f, 30f)]
        [TestCase(0.5f, 20f)]
        public void Stacks_AddPerExtraStack(float perExtraStack, float expected)
        {
            var scaler = TestRecipes.Configure(new StacksScaler(), ("perExtraStack", perExtraStack));
            Assert.AreEqual(expected, Evaluate(10f, scaler, stacks: 3), 0.001f);
        }

        [Test]
        public void PercentOfResource_Missing()
        {
            var healthType = assets.Make<ResourceType>("Health");
            var resources = new FakeResources();
            target.Add(resources);
            resources.Add(healthType, 100f, 40f);
            var scaler = TestRecipes.Configure(new PercentOfResourceScaler(),
                ("resource", healthType), ("part", ResourcePart.Missing), ("percent", 0.1f));
            Assert.AreEqual(16f, Evaluate(10f, scaler), 0.001f);
        }

        [Test]
        public void DistanceFalloff_IsLinearBetweenStartAndEnd()
        {
            target.Transform.position = new Vector3(6f, 0f, 0f);
            var scaler = TestRecipes.Configure(new DistanceFalloffScaler(), ("start", 2f), ("end", 10f), ("minimum", 0.25f));
            Assert.AreEqual(6.25f, Evaluate(10f, scaler), 0.001f);
        }

        [Test]
        public void RandomVariance_StaysInRange_AndRepeatsWithTheSameSeed()
        {
            var scalers = new IValueScaler[] { TestRecipes.Configure(new RandomVarianceScaler(), ("variance", 0.1f)) };
            var first = new List<float>();
            var second = new List<float>();
            var a = new SeededGameRandom(7);
            var b = new SeededGameRandom(7);
            for (int i = 0; i < 100; i++)
            {
                first.Add(Evaluate(10f, scalers, random: a));
                second.Add(Evaluate(10f, scalers, random: b));
            }
            Assert.That(first, Is.All.InRange(9f, 11f));
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Order_Matters()
        {
            var stacks = TestRecipes.Configure(new StacksScaler(), ("perExtraStack", 1f));
            var clamp = TestRecipes.Configure(new ClampScaler(), ("minimum", 0f), ("maximum", 25f));
            Assert.AreEqual(25f, Evaluate(10f, new IValueScaler[] { stacks, clamp }, stacks: 3), 0.001f);
            Assert.AreEqual(30f, Evaluate(10f, new IValueScaler[] { clamp, stacks }, stacks: 3), 0.001f);
        }
    }
}