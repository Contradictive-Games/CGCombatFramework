using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CG.CombatFramework.Tests
{
    public sealed class UnitPartsTests
    {
        [AddComponentMenu("Tests/Menu Label")]
        private sealed class TestMenuPart : MonoBehaviour, IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        [UnitPart(Hidden = true)]
        private sealed class TestHiddenPart : MonoBehaviour, IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        [AddComponentMenu("")]
        private sealed class TestMenuHidden : MonoBehaviour, IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        [UnitPart("Base Label", Group = "AI")]
        private abstract class TestAiBase : MonoBehaviour, IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        private sealed class TestAiChild : TestAiBase
        {
        }

        private sealed class TestGenericPart<T> : MonoBehaviour, IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        private sealed class PlainComponent : IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        [UnitPart("Not a behaviour")]
        private sealed class NotABehaviour
        {
        }

        [UnitPart("Zed", Group = "Ordered", Order = -5)]
        private sealed class TestOrderedFirst : MonoBehaviour, IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        [UnitPart("Alpha", Group = "Ordered")]
        private sealed class TestOrderedPlain : MonoBehaviour, IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        [UnitPart("Beta", Group = "Ordered", Order = 5)]
        private sealed class TestOrderedLast : MonoBehaviour, IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        [UnitPart(Group = "Ordered", Order = -10)]
        private abstract class TestOrderedBase : MonoBehaviour, IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        private sealed class TestOrderedChild : TestOrderedBase
        {
        }

        [UnitPart("Keeper", Group = "Aviary", Order = -100)]
        private sealed class TestAviaryKeeper : MonoBehaviour, IUnitComponent
        {
            public void Initialize(IUnit owner) { }
        }

        private static readonly Type[] tested =
        {
            typeof(TestBrain), typeof(TestNeedsBrain), typeof(TestBob), typeof(TestArrival), typeof(TestDeparture), typeof(TestFade),
            typeof(TestMenuPart), typeof(TestHiddenPart), typeof(TestMenuHidden), typeof(TestAiBase), typeof(TestAiChild), typeof(TestGenericPart<>),
            typeof(PlainComponent), typeof(HealthComponent), typeof(DespawnOnDeath), typeof(Unit), typeof(TestBrain)
        };

        private IReadOnlyList<PartInfo> parts;

        [SetUp]
        public void SetUp()
        {
            Looks.Clear();
            parts = UnitParts.Discover(tested);
            UnitParts.UseCustom(parts);
        }

        [TearDown]
        public void TearDown()
        {
            UnitParts.UseCustom(null);
            Looks.Clear();
        }

        private PartInfo Part<T>() => parts.First(p => p.Type == typeof(T));

        [Test]
        public void FindsUnitComponents_AttributesAndSequences()
        {
            CollectionAssert.AreEquivalent(new[]
            {
                typeof(TestBrain), typeof(TestNeedsBrain), typeof(TestBob), typeof(TestArrival), typeof(TestDeparture), typeof(TestFade), typeof(TestMenuPart), typeof(TestAiChild)
            }, parts.Select(p => p.Type));
            Assert.IsTrue(parts.All(p => p.IsCustom && p.Part == UnitPart.None));
        }

        [Test]
        public void SkipsHiddenAbstractGenericBuiltInAndNonBehaviourTypes()
        {
            var types = parts.Select(p => p.Type).ToList();
            CollectionAssert.DoesNotContain(types, typeof(TestHiddenPart));
            CollectionAssert.DoesNotContain(types, typeof(TestMenuHidden));
            CollectionAssert.DoesNotContain(types, typeof(TestAiBase));
            CollectionAssert.DoesNotContain(types, typeof(TestGenericPart<>));
            CollectionAssert.DoesNotContain(types, typeof(PlainComponent));
            CollectionAssert.DoesNotContain(types, typeof(HealthComponent));
            CollectionAssert.DoesNotContain(types, typeof(DespawnOnDeath));
            CollectionAssert.DoesNotContain(types, typeof(Unit));
        }

        [Test]
        public void UnitPartOnANonBehaviour_IsIgnoredWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("NotABehaviour has \\[UnitPart\\] but isn't a MonoBehaviour"));
            Assert.IsEmpty(UnitParts.Discover(new[] { typeof(NotABehaviour) }));
        }

        [Test]
        public void Labels_FromTheAttribute_ThenTheComponentMenu_ThenTheTypeName()
        {
            Assert.AreEqual("Test Brain", Part<TestBrain>().Label);
            Assert.AreEqual("Menu Label", Part<TestMenuPart>().Label);
            Assert.AreEqual("Test Bob", Part<TestBob>().Label);
            Assert.AreEqual("Test Ai Child", Part<TestAiChild>().Label);
        }

        [Test]
        public void Descriptions_AndSequenceDefaults()
        {
            Assert.AreEqual("Thinks", Part<TestBrain>().Description);
            Assert.IsNull(Part<TestBob>().Description);
            Assert.AreEqual("Plays when the unit arrives", Part<TestArrival>().Description);
            Assert.AreEqual("Plays when the unit leaves", Part<TestDeparture>().Description);
            Assert.AreEqual("Plays when the unit arrives and leaves", Part<TestFade>().Description);
        }

        [Test]
        public void Groups_UngroupedFirst_ThenNamedGroups_ThenSequences()
        {
            CollectionAssert.AreEqual(new[]
            {
                "Menu Label", "Test Needs Brain", "Test Ai Child", "Test Brain", "Test Bob", "Test Arrival", "Test Departure", "Test Fade"
            }, parts.Select(p => p.Label));
            Assert.AreEqual("AI", Part<TestAiChild>().Group);
            Assert.AreEqual("Presentation", Part<TestBob>().Group);
            Assert.AreEqual(UnitParts.k_Sequences, Part<TestFade>().Group);
            CollectionAssert.AreEqual(new string[] { null, "AI", "Presentation", UnitParts.k_Sequences }, UnitParts.Groups(parts).Select(g => g.Key));
        }

        [Test]
        public void Order_SortsPartsWithinAGroup_ThenLabels()
        {
            var ordered = UnitParts.Discover(new[] { typeof(TestOrderedLast), typeof(TestOrderedPlain), typeof(TestOrderedChild), typeof(TestAviaryKeeper), typeof(TestOrderedFirst), typeof(TestBob) });

            CollectionAssert.AreEqual(new[] { "Keeper", "Zed", "Alpha", "Test Ordered Child", "Beta", "Test Bob" }, ordered.Select(p => p.Label));
            CollectionAssert.AreEqual(new[] { "Aviary", "Ordered", "Presentation" }, UnitParts.Groups(ordered).Select(g => g.Key));
            Assert.AreEqual(0, ordered.Single(p => p.Type == typeof(TestOrderedChild)).Order);
        }

        [Test]
        public void BuiltInParts_TakeLabelDescriptionOrderAndStyleFromTheirAttributeAndTheStyleMap()
        {
            foreach(var info in UnitParts.All)
            {
                var attribute = Looks.Own<UnitPartAttribute>(info.Type);
                Assert.AreEqual(attribute.Label, info.Label);
                Assert.AreEqual(attribute.Description, info.Description);
                Assert.AreEqual(attribute.Order, info.Order);
                Assert.AreEqual(BuiltInStyles.Of(info.Type), info.Style);
                Assert.IsFalse(info.IsCustom);
            }
            CollectionAssert.AreEqual(new[]
            {
                "Stats", "Resources", "Health", "Despawn On Death", "Status Effects", "Firable Loadout", "View Binder", "Rigidbody Knockback", "Transform Aim", "Ground Plane Aim"
            }, UnitParts.All.Select(p => p.Label));
        }

        [Test]
        public void Needs_FromRequireComponent()
        {
            CollectionAssert.AreEqual(new[] { typeof(HealthComponent) }, Part<TestBrain>().Needs);
            CollectionAssert.AreEqual(new[] { typeof(TestBrain) }, Part<TestNeedsBrain>().Needs);
            Assert.IsEmpty(Part<TestBob>().Needs);
            Assert.AreEqual("Needs Health", UnitParts.NeedsLabel(Part<TestBrain>()));
            Assert.AreEqual("Needs Test Brain", UnitParts.NeedsLabel(Part<TestNeedsBrain>()));
            Assert.IsNull(UnitParts.NeedsLabel(Part<TestBob>()));
        }

        [Test]
        public void Requirements_CoverBuiltInAndCustomParts()
        {
            CollectionAssert.AreEqual(new[] { UnitParts.Of(UnitPart.Health) }, UnitParts.Requirements(Part<TestBrain>()));
            CollectionAssert.AreEqual(new[] { Part<TestBrain>() }, UnitParts.Requirements(Part<TestNeedsBrain>()));
            CollectionAssert.AreEqual(new[] { UnitParts.Of(UnitPart.Health) }, UnitParts.Requirements(UnitParts.Of(UnitPart.DespawnOnDeath)));
            Assert.IsTrue(UnitParts.DependsOn(Part<TestBrain>(), UnitParts.Of(UnitPart.Health)));
            Assert.IsFalse(UnitParts.DependsOn(UnitParts.Of(UnitPart.Health), Part<TestBrain>()));
        }

        [Test]
        public void Sequences_AreFoundInChildren_OthersOnTheRoot()
        {
            Assert.IsTrue(Part<TestArrival>().InChildren);
            Assert.IsTrue(Part<TestFade>().InChildren);
            Assert.IsFalse(Part<TestBrain>().InChildren);
            Assert.AreEqual(PartInfo.k_SequenceStyle, Part<TestArrival>().Style);
            Assert.AreEqual(PartInfo.k_CustomStyle, Part<TestBob>().Style);
        }

        [Test]
        public void Look_ComesFromTheAttribute()
        {
            Assert.IsTrue(Looks.TryColor("#F78166", out var color));
            Assert.AreEqual(color, Part<TestBrain>().Look.Color);
            Assert.AreEqual("query", Part<TestBrain>().Look.Icon.Glyph);
            Assert.IsNull(Part<TestBob>().Look.Color);
            Assert.IsNull(UnitParts.Of(UnitPart.Health).Look.Color);
        }

        [Test]
        public void TestAssemblies_AreSkipped()
        {
            Assert.IsTrue(UnitParts.IsTestAssembly(typeof(UnitPartsTests).Assembly));
            Assert.IsTrue(UnitParts.IsTestAssembly(typeof(TestBrain).Assembly));
            Assert.IsFalse(UnitParts.IsTestAssembly(typeof(Unit).Assembly));
            Assert.IsFalse(UnitParts.IsTestAssembly(typeof(UnitParts).Assembly));
        }

        [Test]
        public void ProjectDiscovery_SkipsTestAssemblies_WithoutWarnings()
        {
            UnitParts.UseCustom(null);
            var types = UnitParts.Custom.Select(p => p.Type).ToList();
            CollectionAssert.DoesNotContain(types, typeof(TestBrain));
            CollectionAssert.DoesNotContain(types, typeof(TestArrival));
            CollectionAssert.DoesNotContain(types, typeof(TestMenuPart));
            Assert.IsFalse(types.Any(t => UnitParts.IsTestAssembly(t.Assembly)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DespawnOnDeath_IsABuiltInPartThatNeedsHealth()
        {
            var info = UnitParts.Of(UnitPart.DespawnOnDeath);
            Assert.AreEqual(typeof(DespawnOnDeath), info.Type);
            Assert.AreEqual("Despawn On Death", info.Label);
            Assert.AreEqual(UnitPart.Health, info.Requires);
            Assert.AreSame(info, UnitParts.OfType(typeof(DespawnOnDeath)));
        }

        [Test]
        public void Toggle_TurnsOnWhatAPartNeeds_AndOffWhatNeedsIt()
        {
            var on = UnitParts.Toggle(UnitPart.None, UnitPart.DespawnOnDeath, true);
            Assert.AreEqual(UnitPart.DespawnOnDeath | UnitPart.Health | UnitPart.Resources, on);
            Assert.AreEqual(UnitPart.None, UnitParts.Toggle(on, UnitPart.Resources, false));
            Assert.AreEqual(UnitPart.Resources, UnitParts.Toggle(on, UnitPart.Health, false));
        }

        [Test]
        public void Blueprint_CustomPartsTurnOnWhatTheyNeed()
        {
            var blueprint = new UnitBlueprint { Parts = UnitPart.None };
            UnitParts.ToggleCustom(blueprint, Part<TestNeedsBrain>(), true);
            CollectionAssert.AreEquivalent(new[] { UnitParts.NameOf(typeof(TestNeedsBrain)), UnitParts.NameOf(typeof(TestBrain)) }, blueprint.CustomParts);
            Assert.IsTrue(blueprint.Has(UnitPart.Health | UnitPart.Resources));
        }

        [Test]
        public void Blueprint_TurningOffANeededPart_TurnsOffWhatNeedsIt()
        {
            var blueprint = new UnitBlueprint { Parts = UnitPart.None };
            UnitParts.ToggleCustom(blueprint, Part<TestNeedsBrain>(), true);
            UnitParts.ToggleCustom(blueprint, Part<TestBob>(), true);
            UnitParts.ToggleCustom(blueprint, Part<TestBrain>(), false);
            CollectionAssert.AreEqual(new[] { UnitParts.NameOf(typeof(TestBob)) }, blueprint.CustomParts);

            UnitParts.ToggleCustom(blueprint, Part<TestNeedsBrain>(), true);
            blueprint.Parts = UnitParts.Toggle(blueprint.Parts, UnitPart.Resources, false);
            UnitParts.Prune(blueprint);
            CollectionAssert.AreEqual(new[] { UnitParts.NameOf(typeof(TestBob)) }, blueprint.CustomParts);
            Assert.AreEqual(UnitPart.None, blueprint.Parts);
        }

        [Test]
        public void Blueprint_MissingTypesAreKeptForTheReviewWarning()
        {
            var blueprint = new UnitBlueprint { Parts = UnitPart.None };
            blueprint.CustomParts.Add("Missing.Part, Nowhere");
            UnitParts.Prune(blueprint);
            CollectionAssert.AreEqual(new[] { "Missing.Part, Nowhere" }, blueprint.CustomParts);
        }

        [Test]
        public void Names_RoundTrip()
        {
            Assert.AreEqual(typeof(TestBrain), UnitParts.Resolve(UnitParts.NameOf(typeof(TestBrain))));
            Assert.IsNull(UnitParts.Resolve("Missing.Part, Nowhere"));
            Assert.IsNull(UnitParts.Resolve(null));
            Assert.AreEqual("Test Brain", UnitParts.ShortName("Some.Space.TestBrain, Some.Assembly"));
        }
    }
}
