using System.Collections;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    public sealed class AssetChangesTests
    {
        private const string k_Folder = "Assets/CGAssetChangesTests";

        [SetUp]
        public void SetUp()
        {
            if(!AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.CreateFolder("Assets", "CGAssetChangesTests");
        }

        [TearDown]
        public void TearDown()
        {
            if(AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.DeleteAsset(k_Folder);
        }

        [Test]
        public void Definitions_AreCombatPaths()
        {
            string path = $"{k_Folder}/Stat.asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<StatType>(), path);
            Assert.IsTrue(AssetChanges.IsCombatPath(path));
        }

        [Test]
        public void Pipelines_AreCombatPaths()
        {
            string path = $"{k_Folder}/Pipeline.asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DamagePipeline>(), path);
            Assert.IsTrue(AssetChanges.IsCombatPath(path));
        }

        [Test]
        public void Units_AreCombatPaths_OtherPrefabsAreNot()
        {
            string unit = $"{k_Folder}/Unit.prefab";
            string prop = $"{k_Folder}/Prop.prefab";
            Save(new GameObject("Unit", typeof(Unit)), unit);
            Save(new GameObject("Prop"), prop);
            Assert.IsTrue(AssetChanges.IsCombatPath(unit));
            Assert.IsFalse(AssetChanges.IsCombatPath(prop));
        }

        [Test]
        public void OtherAssets_AreNotCombatPaths()
        {
            string path = $"{k_Folder}/Clip.anim";
            AssetDatabase.CreateAsset(new AnimationClip(), path);
            Assert.IsFalse(AssetChanges.IsCombatPath(path));
            Assert.IsFalse(AssetChanges.IsCombatPath($"{k_Folder}/Gone.png"));
            Assert.IsFalse(AssetChanges.IsCombatPath(null));
        }

        [Test]
        public void MissingAssetFiles_CountInCaseTheyWereDefinitions()
        {
            Assert.IsTrue(AssetChanges.IsCombatPath($"{k_Folder}/Gone.asset"));
            Assert.IsTrue(AssetChanges.AnyCombatPath(new[] { $"{k_Folder}/Gone.png", $"{k_Folder}/Gone.asset" }));
        }

        [UnityTest]
        public IEnumerator SceneObjectEdits_RaiseObjectsChangedOncePerTick()
        {
            var first = new GameObject("First");
            var second = new GameObject("Second");
            int raised = 0;
            void Count() => raised++;
            AssetChanges.ObjectsChanged += Count;
            try
            {
                yield return null;
                raised = 0;
                Undo.RecordObject(first.transform, "Move");
                first.transform.position = Vector3.one;
                Undo.RecordObject(second.transform, "Move");
                second.transform.position = Vector3.one;
                Undo.FlushUndoRecordObjects();

                double end = EditorApplication.timeSinceStartup + 2.0;
                while(raised == 0 && EditorApplication.timeSinceStartup < end)
                {
                    yield return null;
                }
                for(int i = 0; i < 3; i++)
                {
                    yield return null;
                }
                Assert.AreEqual(1, raised);
            }
            finally
            {
                AssetChanges.ObjectsChanged -= Count;
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [UnityTest]
        public IEnumerator DefinitionEdits_DoNotRaiseObjectsChanged()
        {
            string path = $"{k_Folder}/Stat.asset";
            var stat = ScriptableObject.CreateInstance<StatType>();
            AssetDatabase.CreateAsset(stat, path);
            int raised = 0;
            void Count() => raised++;
            yield return null;
            AssetChanges.ObjectsChanged += Count;
            try
            {
                Undo.RecordObject(stat, "Change Default");
                stat.DefaultValue = 3f;
                Undo.FlushUndoRecordObjects();
                for(int i = 0; i < 5; i++)
                {
                    yield return null;
                }
                Assert.AreEqual(0, raised);
            }
            finally
            {
                AssetChanges.ObjectsChanged -= Count;
            }
        }

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
