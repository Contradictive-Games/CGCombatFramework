using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
