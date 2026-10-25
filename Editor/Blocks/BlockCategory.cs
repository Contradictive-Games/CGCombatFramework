using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public enum BlockCategory
    {
        Other,
        Payload,
        Emission,
        Query,
        Condition,
        Scaler,
        Gate,
        FireMode,
        DamageStep,
        Regen,
        Behavior,
        Trigger
    }

    public static class BlockCategories
    {
        private static readonly BlockCategory[] all = (BlockCategory[])Enum.GetValues(typeof(BlockCategory));

        public static BlockCategory Of(Type type)
        {
            if(type == null) return BlockCategory.Other;
            if(IsTrigger(type)) return BlockCategory.Trigger;
            if(typeof(IPayload).IsAssignableFrom(type)) return BlockCategory.Payload;
            if(typeof(IEmission).IsAssignableFrom(type)) return BlockCategory.Emission;
            if(typeof(IHitQuery).IsAssignableFrom(type)) return BlockCategory.Query;
            if(typeof(ICondition).IsAssignableFrom(type)) return BlockCategory.Condition;
            if(typeof(IValueScaler).IsAssignableFrom(type)) return BlockCategory.Scaler;
            if(typeof(IFireGate).IsAssignableFrom(type)) return BlockCategory.Gate;
            if(typeof(IFireMode).IsAssignableFrom(type)) return BlockCategory.FireMode;
            if(typeof(IDamageStep).IsAssignableFrom(type)) return BlockCategory.DamageStep;
            if(typeof(IRegenRule).IsAssignableFrom(type)) return BlockCategory.Regen;
            if(typeof(IStatusEffectBehavior).IsAssignableFrom(type)) return BlockCategory.Behavior;
            return BlockCategory.Other;
        }

        public static string Label(BlockCategory category) => ObjectNames.NicifyVariableName(category.ToString());

        public static string ClassName(BlockCategory category) => "cg-" + category.ToString().ToLowerInvariant();

        public static void Apply(VisualElement element, BlockCategory category)
        {
            foreach(var each in all)
            {
                element.RemoveFromClassList(ClassName(each));
            }
            element.AddToClassList(ClassName(category));
        }

        private static bool IsTrigger(Type type)
        {
            for(var current = type; current != null; current = current.BaseType)
            {
                if(current.IsGenericType && current.GetGenericTypeDefinition() == typeof(TriggerBehavior<>)) return true;
            }
            return false;
        }
    }
}
