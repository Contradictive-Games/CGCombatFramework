using System;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    public static class BlockTypePicker
    {
        public static void Show(Rect rect, Type baseType, bool allowNone, Action<Type> picked) => Show(rect, baseType, allowNone, null, picked);

        public static void Show(Rect rect, Type baseType, bool allowNone, Type current, Action<Type> picked)
        {
            if(baseType == null) return;
            BlockPickerWindow.Show(rect, new BlockPickerModel(baseType, allowNone), current, picked);
        }
    }
}
