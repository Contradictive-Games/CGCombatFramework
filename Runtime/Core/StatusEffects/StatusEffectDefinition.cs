using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public enum EffectStackingMode { Refresh, AddDuration, Independent }

    [CreateAssetMenu(fileName = "StatusEffectDefinition", menuName = Globals.k_PACKAGE_NAME + "/Status Effect Definition")]
    public sealed class StatusEffectDefinition : GameDefinition
    {
        protected override string IdPrefix => "effect";

        [Header("Status Effect Settings")]
        public string Name;
        public Sprite Icon;
        [TextArea] public string Description;
        [Tooltip("General use, good for ui grouping")] 
        public CombatTag[] Categories = Array.Empty<CombatTag>();

        [Header("Timing")]
        [Min(0f), Tooltip("Duration in seconds, 0 = permanent until removed")] 
        public float Duration = 5f;
        [Min(0f), Tooltip("Seconds between OnTick calls, 0 = no ticks")] 
        public float TickInterval = 1f;
        public bool TickOnApplication = false;

        [Header("Stacking")]
        public EffectStackingMode Stacking  = EffectStackingMode.Refresh;
        [Min(1)] public int MaxStacks = 1;
        [Min(0f), Tooltip("For Stacking = AddDuration. Cap on highest duratino allowed, 0 = no cap")] 
        public float MaxDuration;
        [Tooltip("If each applier gets their own instance. Two different units can have their effects tick separately and each is credited")] 
        public bool StackPerSource;

        [Header("Rules")]
        [Tooltip("Survives death and only ends on despawn/dispel/expiration")] 
        public bool KeepOnDeath;
        [Tooltip("Tags given while effect is active")] 
        public CombatTag[] GrantedTags = Array.Empty<CombatTag>();
        [Tooltip("Effect refused while host has any of these tags")]
        public CombatTag[] BlockedBy = Array.Empty<CombatTag>();

        [Header("Behavior")]
        [SerializeReference, SelectType] 
        public IStatusEffectBehavior[] Behaviors = Array.Empty<IStatusEffectBehavior>();

        public bool IsPermanent => Duration <= 0f;

        public bool HasCategory(CombatTag category)
        {
            if(category == null || Categories == null || Categories.Length == 0) return false;
            for(int i = 0; i < Categories.Length; i++)
            {
                if(Categories[i] != null && Categories[i].IsOrDescendedFrom(category)) return true;
            }
            return false;
        }    
    }
}