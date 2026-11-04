using System.Collections;
using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Tests
{
    public sealed class AssetPreviewsTests
    {
        private const string k_Folder = "Assets/CGAssetPreviewsTests";

        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            if(!AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.CreateFolder("Assets", "CGAssetPreviewsTests");
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{k_Folder}/Cube.prefab");
            Object.DestroyImmediate(root);
        }

        [TearDown]
        public void TearDown()
        {
            if(AssetDatabase.IsValidFolder(k_Folder)) AssetDatabase.DeleteAsset(k_Folder);
        }

        [Test]
        public void NoAsset_ReportsNoTexture_AndDoesNotWait()
        {
            var image = new Image();
            int before = AssetPreviews.Waiting;
            bool called = false;
            AssetPreviews.Show(image, null, texture =>
            {
                called = true;
                Assert.IsNull(texture);
            });
            Assert.IsTrue(called);
            Assert.AreEqual(before, AssetPreviews.Waiting);
        }

        [Test]
        public void Cancel_StopsWaiting()
        {
            var image = new Image();
            int before = AssetPreviews.Waiting;
            AssetPreviews.Show(image, prefab, null);
            AssetPreviews.Cancel(image);
            Assert.AreEqual(before, AssetPreviews.Waiting);
        }

        [Test]
        public void ShowingAgain_ReplacesTheOldRequest()
        {
            var image = new Image();
            int before = AssetPreviews.Waiting;
            AssetPreviews.Show(image, prefab, null);
            AssetPreviews.Show(image, prefab, null);
            Assert.LessOrEqual(AssetPreviews.Waiting, before + 1);
            AssetPreviews.Cancel(image);
        }

        [UnityTest]
        public IEnumerator ImagesNotOnScreen_StopWaiting()
        {
            var image = new Image();
            AssetPreviews.Show(image, prefab, null);
            double end = EditorApplication.timeSinceStartup + 2.0;
            while(AssetPreviews.Waiting > 0 && EditorApplication.timeSinceStartup < end)
            {
                yield return null;
            }
            Assert.AreEqual(0, AssetPreviews.Waiting);
        }
    }
}
