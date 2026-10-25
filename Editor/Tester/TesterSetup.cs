using System;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal enum RollMode { Random, AlwaysSucceed, AlwaysFail }

    [Serializable]
    internal struct StatValue
    {
        public StatType Stat;
        public float Value;
    }

    [Serializable]
    internal struct ResourceValue
    {
        public ResourceType Resource;
        public float Current;
    }

    [Serializable]
    internal sealed class SandboxSetup
    {
        [Tooltip("Optional unit prefab. Its stat profile, faction and starting tags are used unless set below")]
        public GameObject Unit;
        [Tooltip("Overrides the unit's stat profile")]
        public UnitStatProfile Profile;
        [Tooltip("Overrides the unit's faction")]
        public Faction Faction;
        [Tooltip("Added on top of the unit's starting tags")]
        public CombatTag[] Tags = Array.Empty<CombatTag>();
        [Tooltip("Base values that replace the profile's")]
        public StatValue[] Stats = Array.Empty<StatValue>();
        [Tooltip("Starting amounts, e.g. how much shield is left")]
        public ResourceValue[] Resources = Array.Empty<ResourceValue>();

        public UnitStatProfile EffectiveProfile => Profile != null ? Profile : UnitLibrary.ProfileOf(Unit);
        public Faction EffectiveFaction => Faction != null ? Faction : UnitLibrary.FactionOf(Unit);
    }

    [Serializable]
    internal sealed class TesterSettings
    {
        [Tooltip("Leave empty to use the defender's own pipeline")]
        public DamagePipeline Pipeline;
        [Min(0f)] public float Amount = 20f;
        public DamageType DamageType;
        public DamageFlags Flags;
        [Tooltip("Optional definition passed as the cause, e.g. the firable that hit")]
        public GameDefinition Cause;
        [Tooltip("Hit a hitbox so Hitbox Multiplier steps have something to read")]
        public bool UseHitbox;
        [Min(0f)] public float HitboxMultiplier = 2f;
        public CombatTag HitZone;
        [Tooltip("The defender hits itself, for self damage rules")]
        public bool SelfHit;
        public RollMode Rolls = RollMode.Random;
        public int Seed = 1;
        [Range(10, 20000)] public int Samples = 1000;
        public SandboxSetup Attacker = new();
        public SandboxSetup Defender = new();

        public DamagePipeline EffectivePipeline => Pipeline != null ? Pipeline : UnitLibrary.PipelineOf(Defender.Unit);
    }
}
