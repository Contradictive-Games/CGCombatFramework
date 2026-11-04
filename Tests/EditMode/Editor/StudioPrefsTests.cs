using CG.CombatFramework.Editor;
using NUnit.Framework;
using UnityEditor;

namespace CG.CombatFramework.Tests
{
    public sealed class StudioPrefsTests
    {
        private const string k_Prefix = "CG.CombatStudio.";
        private const string k_Key = "Tests.StudioPrefs.Probe";

        [SetUp]
        public void SetUp() => StudioPrefs.Remove(k_Key);

        [TearDown]
        public void TearDown()
        {
            StudioPrefs.Remove(k_Key);
            StudioPrefs.Forget();
        }

        [Test]
        public void StoredValues_AreReadFromMemory()
        {
            StudioPrefs.Store(k_Key, 5);
            EditorPrefs.SetInt(k_Prefix + k_Key, 9);
            Assert.AreEqual(5, StudioPrefs.GetInt(k_Key, 0));
            StudioPrefs.Forget();
            Assert.AreEqual(9, StudioPrefs.GetInt(k_Key, 0));
        }

        [Test]
        public void Stores_WriteThrough()
        {
            StudioPrefs.Store(k_Key, "hello");
            Assert.AreEqual("hello", EditorPrefs.GetString(k_Prefix + k_Key));
        }

        [Test]
        public void MissingKeys_UseTheFallback()
        {
            Assert.AreEqual(3, StudioPrefs.GetInt(k_Key, 3));
            Assert.IsTrue(StudioPrefs.Get(k_Key, true));
            Assert.AreEqual("none", StudioPrefs.GetString(k_Key, "none"));
            StudioPrefs.Store(k_Key, 1.5f);
            Assert.AreEqual(1.5f, StudioPrefs.GetFloat(k_Key, 0f));
        }

        [Test]
        public void Remove_ForgetsTheValue()
        {
            StudioPrefs.Store(k_Key, true);
            StudioPrefs.Remove(k_Key);
            Assert.IsFalse(StudioPrefs.Get(k_Key, false));
            Assert.IsFalse(EditorPrefs.HasKey(k_Prefix + k_Key));
        }

        [Test]
        public void Set_RaisesChanged()
        {
            int raised = 0;
            void Count() => raised++;
            StudioPrefs.Changed += Count;
            try
            {
                StudioPrefs.Set(k_Key, true);
                StudioPrefs.SetInt(k_Key, 2);
                StudioPrefs.Store(k_Key, false);
            }
            finally
            {
                StudioPrefs.Changed -= Count;
            }
            Assert.AreEqual(2, raised);
        }
    }
}
