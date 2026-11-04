using System.Linq;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    public sealed class UnitLibraryTests
    {
        private const string k_Folder = "Assets/CGUnitLibraryTests";

        [SetUp]
        public void SetUp()
        {
            if(!AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.CreateFolder("Assets", "CGUnitLibraryTests");
        }

        [TearDown]
        public void TearDown()
        {
            if(AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.DeleteAsset(k_Folder);
        }

        private static string Save(GameObject root)
        {
            string path = $"{k_Folder}/{root.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }

        [Test]
        public void NewUnits_ShowUpWithoutARescan()
        {
            int before = UnitLibrary.All.Count;
            string path = Save(new GameObject("Fresh", typeof(Unit)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CollectionAssert.Contains(UnitLibrary.All, prefab);
            Assert.AreEqual(before + 1, UnitLibrary.All.Count);
            Assert.IsTrue(UnitLibrary.Touches(path));
        }

        [Test]
        public void PrefabsThatStopBeingUnits_Leave()
        {
            string path = Save(new GameObject("Leaving", typeof(Unit)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CollectionAssert.Contains(UnitLibrary.All, prefab);

            var root = PrefabUtility.LoadPrefabContents(path);
            Object.DestroyImmediate(root.GetComponent<Unit>());
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            CollectionAssert.DoesNotContain(UnitLibrary.All, prefab);
            Assert.IsTrue(UnitLibrary.Touches(path));
        }

        [Test]
        public void DeletedUnits_Leave()
        {
            int before = UnitLibrary.All.Count;
            string path = Save(new GameObject("Deleted", typeof(Unit)));
            Assert.AreEqual(before + 1, UnitLibrary.All.Count);
            AssetDatabase.DeleteAsset(path);
            Assert.AreEqual(before, UnitLibrary.All.Count);
            Assert.IsFalse(UnitLibrary.All.Any(unit => unit == null));
        }

        [Test]
        public void OtherPrefabs_AreNotUnits()
        {
            string path = Save(new GameObject("Prop", typeof(BoxCollider)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CollectionAssert.DoesNotContain(UnitLibrary.All, prefab);
            Assert.IsFalse(UnitLibrary.Touches(path));
        }

        [Test]
        public void ARescan_OnlyRechecksChangedPrefabs()
        {
            UnitLibrary.Clear();
            _ = UnitLibrary.All;
            UnitLibrary.Clear();
            _ = UnitLibrary.All;
            Assert.AreEqual(0, UnitLibrary.LastChecked);
        }

        [Test]
        public void Read_UsesTheField_AndTurnsMissingReferencesIntoNull()
        {
            var probe = new GameObject("Probe", typeof(Unit), typeof(StatsComponent));
            var profile = ScriptableObject.CreateInstance<UnitStatProfile>();
            try
            {
                using(var serialized = new SerializedObject(probe.GetComponent<StatsComponent>()))
                {
                    serialized.FindProperty("profile").objectReferenceValue = profile;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                Assert.AreSame(profile, UnitLibrary.ProfileOf(probe));
                Object.DestroyImmediate(profile);
                Assert.IsTrue(ReferenceEquals(UnitLibrary.Read<UnitStatProfile>(probe.GetComponent<StatsComponent>(), "profile"), null));
                Assert.IsNull(UnitLibrary.Read<UnitStatProfile>(probe.GetComponent<StatsComponent>(), "noSuchField"));
                Assert.IsNull(UnitLibrary.Read<UnitStatProfile>(null, "profile"));
            }
            finally
            {
                Object.DestroyImmediate(probe);
                if(profile != null) Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ReadArray_SkipsEmptyEntries()
        {
            var probe = new GameObject("Probe", typeof(Unit));
            var tag = ScriptableObject.CreateInstance<CombatTag>();
            try
            {
                using(var serialized = new SerializedObject(probe.GetComponent<Unit>()))
                {
                    var tags = serialized.FindProperty("startingTags");
                    tags.arraySize = 2;
                    tags.GetArrayElementAtIndex(0).objectReferenceValue = tag;
                    tags.GetArrayElementAtIndex(1).objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                CollectionAssert.AreEqual(new[] { tag }, UnitLibrary.TagsOf(probe));
            }
            finally
            {
                Object.DestroyImmediate(probe);
                Object.DestroyImmediate(tag);
            }
        }
    }
}
