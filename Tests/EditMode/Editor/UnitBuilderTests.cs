using System.Linq;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    public sealed class UnitBuilderTests
    {
        private const string k_Folder = "Assets/CGUnitBuilderTests";
        private const string k_Missing = "Missing.Part, Nowhere";

        [SetUp]
        public void SetUp()
        {
            Looks.Clear();
            UnitParts.UseCustom(UnitParts.Discover(new[] { typeof(TestBrain), typeof(TestBob), typeof(TestArrival) }));
            if(!AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.CreateFolder("Assets", "CGUnitBuilderTests");
        }

        [TearDown]
        public void TearDown()
        {
            UnitParts.UseCustom(null);
            Looks.Clear();
            if(AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.DeleteAsset(k_Folder);
        }

        private static PartInfo Part<T>() => UnitParts.OfType(typeof(T));

        private static UnitBlueprint Blueprint(string name)
        {
            return new UnitBlueprint
            {
                FileName = name,
                Folder = k_Folder,
                TypesFolder = k_Folder,
                OwnFolder = false,
                Body = BodyShape.None,
                Collider = false,
                Pipeline = PipelineMode.None,
                Parts = UnitPart.None,
                HealthResource = string.Empty
            };
        }

        [Test]
        public void Build_AddsCustomPartsAndDespawnOnDeath()
        {
            var blueprint = Blueprint("Built");
            blueprint.Parts = UnitParts.Toggle(UnitPart.None, UnitPart.DespawnOnDeath, true);
            UnitParts.ToggleCustom(blueprint, Part<TestBrain>(), true);
            UnitParts.ToggleCustom(blueprint, Part<TestBob>(), true);
            UnitParts.ToggleCustom(blueprint, Part<TestArrival>(), true);
            blueprint.CustomParts.Add(k_Missing);

            var prefab = UnitBuilder.Build(blueprint);
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<Unit>());
            Assert.IsNotNull(prefab.GetComponent<ResourceComponent>());
            Assert.AreEqual(1, prefab.GetComponents<HealthComponent>().Length);
            Assert.IsNotNull(prefab.GetComponent<DespawnOnDeath>());
            Assert.IsNotNull(prefab.GetComponent<TestBrain>());
            Assert.IsNotNull(prefab.GetComponent<TestBob>());
            Assert.IsNotNull(prefab.GetComponent<TestArrival>());
        }

        [Test]
        public void MissingCustomParts_AreSkippedWithAWarning()
        {
            var blueprint = Blueprint("Missing");
            blueprint.CustomParts.Add(k_Missing);
            CollectionAssert.Contains(UnitBuilder.Warnings(blueprint), "The custom part Part no longer exists and will be skipped");
            Assert.IsEmpty(UnitBuilder.CustomTypes(blueprint));
            Assert.IsNotNull(UnitBuilder.Build(blueprint));
        }

        [Test]
        public void CopyToNewUnit_CarriesCustomPartsAndDespawnOnDeath()
        {
            var root = new GameObject("Source", typeof(Unit), typeof(ResourceComponent), typeof(HealthComponent), typeof(DespawnOnDeath), typeof(TestBrain));
            var body = new GameObject("Body", typeof(TestArrival));
            body.transform.SetParent(root.transform, false);
            var source = PrefabUtility.SaveAsPrefabAsset(root, $"{k_Folder}/Source.prefab");
            Object.DestroyImmediate(root);

            var blueprint = new UnitBlueprint();
            UnitArchetypes.CopyFrom(source, blueprint);
            Assert.IsTrue(blueprint.Has(UnitPart.DespawnOnDeath | UnitPart.Health | UnitPart.Resources));
            CollectionAssert.AreEquivalent(new[] { UnitParts.NameOf(typeof(TestBrain)), UnitParts.NameOf(typeof(TestArrival)) }, blueprint.CustomParts);

            blueprint.FileName = "Copy";
            blueprint.Folder = k_Folder;
            blueprint.OwnFolder = false;
            blueprint.Body = BodyShape.None;
            blueprint.Collider = false;
            var copy = UnitBuilder.Build(blueprint);
            Assert.IsNotNull(copy.GetComponent<TestBrain>());
            Assert.IsNotNull(copy.GetComponent<TestArrival>());
            Assert.IsNotNull(copy.GetComponent<DespawnOnDeath>());
        }

        [Test]
        public void ClearContent_EmptiesCustomParts()
        {
            var blueprint = Blueprint("Clear");
            blueprint.CustomParts.Add(UnitParts.NameOf(typeof(TestBob)));
            blueprint.ClearContent();
            Assert.IsEmpty(blueprint.CustomParts);
        }

        [TestCase("Enemy", true)]
        [TestCase("Destructible", true)]
        [TestCase("Summon", true)]
        [TestCase("Player", false)]
        [TestCase("Boss", false)]
        [TestCase("Training Dummy", false)]
        public void Archetypes_DespawnOnDeath(string archetype, bool despawns)
        {
            var blueprint = new UnitBlueprint();
            UnitArchetypes.Find(archetype).Apply(blueprint);
            Assert.AreEqual(despawns, blueprint.Has(UnitPart.DespawnOnDeath));
            Assert.AreEqual(despawns, UnitArchetypes.Contents(UnitArchetypes.Find(archetype)).Contains("Despawn On Death"));
        }
    }
}
