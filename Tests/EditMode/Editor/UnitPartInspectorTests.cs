using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    public sealed class UnitPartInspectorTests
    {
        private GameObject probe;
        private UnityEditor.Editor editor;

        [SetUp]
        public void SetUp()
        {
            if(!StudioPrefs.Get(CombatInspector.k_Enabled, true)) Assert.Ignore("Enhanced inspectors are off");
            probe = new GameObject("Probe");
        }

        [TearDown]
        public void TearDown()
        {
            if(editor != null) Object.DestroyImmediate(editor);
            if(probe != null) Object.DestroyImmediate(probe);
            UnitParts.UseCustom(null);
        }

        [Test]
        public void PartsWithoutTheirOwnEditor_ShowTheirDescription()
        {
            editor = UnityEditor.Editor.CreateEditor(probe.AddComponent<DespawnOnDeath>());
            Assert.IsInstanceOf<UnitPartInspector>(editor);
            var root = editor.CreateInspectorGUI();
            Assert.IsTrue(root.ClassListContains("cg-inspector"));
            Assert.AreEqual(UnitParts.Of(UnitPart.DespawnOnDeath).Description, root.Q<Label>(className: "cg-inspector__about")?.text);
        }

        [Test]
        public void CustomParts_ShowTheirDescriptionAndColor()
        {
            UnitParts.UseCustom(UnitParts.Discover(new[] { typeof(TestBrain) }));
            editor = UnityEditor.Editor.CreateEditor(probe.AddComponent<TestBrain>());
            var root = editor.CreateInspectorGUI();
            var about = root.Q<Label>(className: "cg-inspector__about");
            Assert.AreEqual("Thinks", about?.text);
            Assert.IsTrue(Looks.TryColor("#F78166", out var color));
            Assert.AreEqual(color, about.style.borderLeftColor.value);
            Assert.IsTrue(root.ClassListContains(PartInfo.k_CustomStyle));
        }

        [Test]
        public void PlainScripts_KeepTheDefaultInspector()
        {
            UnitParts.UseCustom(UnitParts.Discover(new[] { typeof(TestBrain) }));
            editor = UnityEditor.Editor.CreateEditor(probe.AddComponent<TestBob>());
            var root = editor.CreateInspectorGUI();
            Assert.IsFalse(root.ClassListContains("cg-inspector"));
            Assert.IsNull(root.Q(className: "cg-inspector__about"));
        }
    }
}
