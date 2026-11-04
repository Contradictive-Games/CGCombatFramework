using CG.CombatFramework.Editor;
using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public sealed class EditorSkinScopeTests
    {
        [Test]
        public void Begin_MakesTheEditorSkinCurrent_AndDisposeRestoresIt()
        {
            var before = EditorSkinScope.Current;
            using(EditorSkinScope.Begin())
            {
                var inside = EditorSkinScope.Current;
                Assert.IsNotNull(inside);
                Assert.AreEqual(EditorSkinScope.Skin(), inside);
                Assert.IsNotNull(inside.FindStyle("ArrowNavigationRight"));
                Assert.IsNotNull(inside.FindStyle("DD ItemStyle"));
            }
            Assert.AreEqual(before, EditorSkinScope.Current);
        }
    }
}
