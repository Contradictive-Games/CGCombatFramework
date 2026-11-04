using System.Linq;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    public sealed class UnitEditsTests
    {
        private const string k_Folder = "Assets/CGUnitEditsTests";

        private GameObject prefab;
        private string path;

        [SetUp]
        public void SetUp()
        {
            Looks.Clear();
            UnitParts.UseCustom(UnitParts.Discover(new[] { typeof(TestBrain), typeof(TestNeedsBrain), typeof(TestBob), typeof(TestArrival) }));
            if(!AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.CreateFolder("Assets", "CGUnitEditsTests");
            prefab = Save(new GameObject("Unit", typeof(Unit)), "Unit");
        }

        [TearDown]
        public void TearDown()
        {
            UnitParts.UseCustom(null);
            Looks.Clear();
            if(AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.DeleteAsset(k_Folder);
        }

        private GameObject Save(GameObject root, string name)
        {
            path = $"{k_Folder}/{name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private GameObject Reload() => prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static PartInfo Part<T>() => UnitParts.OfType(typeof(T));

        [Test]
        public void AddingACustomPart_AddsWhatItNeeds()
        {
            UnitEdits.AddPart(prefab, Part<TestNeedsBrain>());
            Reload();
            Assert.IsNotNull(prefab.GetComponent<TestNeedsBrain>());
            Assert.IsNotNull(prefab.GetComponent<TestBrain>());
            Assert.IsNotNull(prefab.GetComponent<HealthComponent>());
            Assert.IsNotNull(prefab.GetComponent<ResourceComponent>());
            Assert.AreEqual(1, prefab.GetComponents<HealthComponent>().Length);
        }

        [Test]
        public void AddingABuiltInPart_AddsWhatItNeeds()
        {
            UnitEdits.AddPart(prefab, UnitParts.Of(UnitPart.DespawnOnDeath));
            Reload();
            Assert.IsNotNull(prefab.GetComponent<DespawnOnDeath>());
            Assert.IsNotNull(prefab.GetComponent<HealthComponent>());
            Assert.IsNotNull(prefab.GetComponent<ResourceComponent>());
        }

        [Test]
        public void Dependents_ListEveryPartThatNeedsIt()
        {
            UnitEdits.AddPart(prefab, Part<TestNeedsBrain>());
            UnitEdits.AddPart(Reload(), UnitParts.Of(UnitPart.DespawnOnDeath));
            UnitEdits.AddPart(Reload(), Part<TestBob>());
            Reload();
            CollectionAssert.AreEquivalent(new[] { "Health", "Despawn On Death", "Test Brain", "Test Needs Brain" },
                UnitParts.Dependents(prefab, UnitParts.Of(UnitPart.Resources)).Select(p => p.Label));
            CollectionAssert.AreEquivalent(new[] { "Test Needs Brain" }, UnitParts.Dependents(prefab, Part<TestBrain>()).Select(p => p.Label));
        }

        [Test]
        public void RemovingAPart_RemovesWhatNeedsIt()
        {
            UnitEdits.AddPart(prefab, Part<TestNeedsBrain>());
            UnitEdits.AddPart(Reload(), UnitParts.Of(UnitPart.DespawnOnDeath));
            UnitEdits.RemovePart(Reload(), UnitParts.Of(UnitPart.Health));
            Reload();
            Assert.IsNull(prefab.GetComponent<HealthComponent>());
            Assert.IsNull(prefab.GetComponent<DespawnOnDeath>());
            Assert.IsNull(prefab.GetComponent<TestBrain>());
            Assert.IsNull(prefab.GetComponent<TestNeedsBrain>());
            Assert.IsNotNull(prefab.GetComponent<ResourceComponent>());
        }

        [Test]
        public void RemovingACustomPart_KeepsWhatItNeeded()
        {
            UnitEdits.AddPart(prefab, Part<TestNeedsBrain>());
            UnitEdits.RemovePart(Reload(), Part<TestBrain>());
            Reload();
            Assert.IsNull(prefab.GetComponent<TestBrain>());
            Assert.IsNull(prefab.GetComponent<TestNeedsBrain>());
            Assert.IsNotNull(prefab.GetComponent<HealthComponent>());
        }

        [Test]
        public void PresentCustomParts_AreCounted()
        {
            UnitEdits.AddPart(prefab, Part<TestBrain>());
            Reload();
            CollectionAssert.AreEquivalent(new[] { Part<TestBrain>() }, UnitParts.PresentCustom(prefab));
            Assert.AreEqual(3, UnitParts.CountAll(prefab));
            CollectionAssert.AreEquivalent(new[] { "Resources", "Health", "Test Brain" }, UnitParts.Labels(prefab));
        }

        [Test]
        public void Sequences_AreFoundOnChildren_AndRemovedEverywhere()
        {
            var root = new GameObject("Dropper", typeof(Unit));
            var body = new GameObject("Body", typeof(TestArrival));
            body.transform.SetParent(root.transform, false);
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            var head = new GameObject("Head", typeof(TestArrival));
            head.transform.SetParent(model.transform, false);
            prefab = Save(root, "Dropper");

            var arrival = Part<TestArrival>();
            Assert.IsTrue(UnitParts.Has(prefab, arrival));
            CollectionAssert.AreEquivalent(new[] { "Body", "Model/Head" }, UnitParts.Locations(prefab, arrival));
            CollectionAssert.Contains(UnitParts.PresentCustom(prefab), arrival);

            UnitEdits.AddPart(prefab, arrival);
            Assert.IsNull(Reload().GetComponent<TestArrival>());
            Assert.AreEqual(2, prefab.GetComponentsInChildren<TestArrival>(true).Length);

            UnitEdits.RemovePart(prefab, arrival);
            Assert.AreEqual(0, Reload().GetComponentsInChildren<TestArrival>(true).Length);
            Assert.IsFalse(UnitParts.Has(prefab, arrival));
        }

        [Test]
        public void UnitComponentOnAChild_IsFlagged()
        {
            var root = new GameObject("Turret Base", typeof(Unit));
            var child = new GameObject("Barrel", typeof(StatsComponent));
            child.transform.SetParent(root.transform, false);
            var pet = new GameObject("Pet", typeof(Unit), typeof(StatsComponent));
            pet.transform.SetParent(root.transform, false);
            try
            {
                var flagged = UnitChecks.Of(root).Where(issue => issue.Message.Contains("only set up unit components on their root")).ToList();
                Assert.AreEqual(1, flagged.Count);
                Assert.AreEqual("Stats is on Barrel. Units only set up unit components on their root, so it never runs", flagged[0].Message);
                Assert.AreEqual(UnityEngine.UIElements.HelpBoxMessageType.Warning, flagged[0].Severity);
                Assert.IsNull(flagged[0].Fix);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
