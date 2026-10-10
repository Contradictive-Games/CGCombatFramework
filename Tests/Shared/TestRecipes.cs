using System.Collections.Generic;

namespace CG.CombatFramework.Tests
{
    /// <summary>Configured built-ins, so the reflection lives in one place and tests read like the inspector.</summary>
    public static class TestRecipes
    {
        public static CooldownGate Cooldown(float seconds)
        {
            var gate = new CooldownGate();
            TestAssets.Set(gate, "seconds", seconds);
            return gate;
        }

        public static AmmoGate Ammo(int magazine, bool autoReload = false)
        {
            var gate = new AmmoGate();
            TestAssets.Set(gate, "magazineSize", magazine);
            TestAssets.Set(gate, "autoReloadWhenEmpty", autoReload);
            return gate;
        }

        public static ChannelFire Channel(float pulseInterval = 0.25f, float maxDuration = 3f,
            ChannelCommit commit = ChannelCommit.OnStart, params ICondition[] keepChannelingWhile)
        {
            var channel = new ChannelFire();
            TestAssets.Set(channel, "pulseInterval", pulseInterval);
            TestAssets.Set(channel, "maxDuration", maxDuration);
            TestAssets.Set(channel, "commit", commit);
            TestAssets.Set(channel, "keepChannelingWhile", new List<ICondition>(keepChannelingWhile));
            return channel;
        }

        public static HasTagCondition HasTag(CombatTag tag, Subject subject = Subject.Target, bool invert = false)
        {
            var condition = new HasTagCondition();
            TestAssets.Set(condition, "tag", tag);
            TestAssets.Set(condition, "subject", subject);
            TestAssets.Set(condition, "invert", invert);
            return condition;
        }

        public static DamagePayload Damage(float amount, DamageFlags flags = DamageFlags.None)
        {
            var payload = new DamagePayload();
            TestAssets.Set(payload, "amount", new ScalableValue(amount));
            TestAssets.Set(payload, "flags", flags);
            return payload;
        }

        public static TickPayloadsBehavior OnTick(params IPayload[] payloads)
        {
            var behavior = new TickPayloadsBehavior();
            TestAssets.Set(behavior, "payloads", payloads);
            return behavior;
        }

        public static StatModifiersBehavior Modifier(StatType stat, float value, StatModifierType type, bool perStack = false)
        {
            var behavior = new StatModifiersBehavior();
            TestAssets.Set(behavior, "modifiers", new []
            {
                new StatModifiersBehavior.Entry { Stat = stat, Value = value, Type = type, PerStack = perStack }
            });
            return behavior;
        }
    }
}