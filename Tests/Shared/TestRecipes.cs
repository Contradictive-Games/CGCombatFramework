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
            TestAssets.Set(channel, "keepChannelingWhile", keepChannelingWhile);
            return channel;
        }

        public static HasTagCondition HasTag(CombatTag tag, Subject subject = Subject.Target)
        {
            var condition = new HasTagCondition();
            TestAssets.Set(condition, "tag", tag);
            TestAssets.Set(condition, "subject", subject);
            return condition;
        }

        public static NotCondition Not(ICondition nested)
        {
            var condition = new NotCondition();
            TestAssets.Set(condition, "condition", nested);
            return condition;
        }

        public static DamagePayload Damage(float amount, DamageFlags flags = DamageFlags.None)
        {
            var payload = new DamagePayload();
            TestAssets.Set(payload, "amount", new ScalableValue(amount));
            TestAssets.Set(payload, "flags", flags);
            return payload;
        }

        public static PayloadsBehavior OnTick(params IPayload[] payloads)
        {
            var behavior = new PayloadsBehavior();
            TestAssets.Set(behavior, "when", PayloadEvent.Tick);
            TestAssets.Set(behavior, "payloads", payloads);
            return behavior;
        }

        public static StatModifiersBehavior Modifier(StatType stat, float value, StatModifierType type, bool perStack = false)
        {
            var behavior = new StatModifiersBehavior();
            TestAssets.Set(behavior, "modifiers", new[]
            {
                new StatModifiersBehavior.Entry { Stat = stat, Value = value, Type = type, PerStack = perStack }
            });
            return behavior;
        }

        public static Delivery Deliver(IHitQuery query, IPayload[] payloads, params ICondition[] filters)
        {
            var delivery = new Delivery();
            TestAssets.Set(delivery, "query", query);
            TestAssets.Set(delivery, "payloads", payloads);
            TestAssets.Set(delivery, "filters", filters);
            return delivery;
        }

        public static SphereQuery Sphere(float radius, QueryCenter center = QueryCenter.Origin, bool includeSource = false)
        {
            var query = new SphereQuery();
            TestAssets.Set(query, "radius", radius);
            TestAssets.Set(query, "center", center);
            TestAssets.Set(query, "includeSource", includeSource);
            return query;
        }

        public static RaycastQuery Raycast(float range, int pierce = 0, bool blockedByWorld = true)
        {
            var query = new RaycastQuery();
            TestAssets.Set(query, "range", range);
            TestAssets.Set(query, "pierce", pierce);
            TestAssets.Set(query, "blockedByWorld", blockedByWorld);
            return query;
        }

        public static RelationCondition Relation(bool hostile = true, bool friendly = false, bool self = false, bool unknown = false)
        {
            var condition = new RelationCondition();
            TestAssets.Set(condition, "hostile", hostile);
            TestAssets.Set(condition, "friendly", friendly);
            TestAssets.Set(condition, "self", self);
            TestAssets.Set(condition, "unknown", unknown);
            return condition;
        }

        public static ChargesGate Charges(int max, float rechargeSeconds, StatType rechargeRate = null)
        {
            var gate = new ChargesGate();
            TestAssets.Set(gate, "maxCharges", max);
            TestAssets.Set(gate, "rechargeSeconds", rechargeSeconds);
            TestAssets.Set(gate, "rechargeRate", rechargeRate);
            return gate;
        }

        public static ProjectileEmission Shoot(Projectile prefab, float speed, IEmission onHitUnit, int pierce = 0)
        {
            var emission = new ProjectileEmission();
            TestAssets.Set(emission, "prefab", prefab);
            TestAssets.Set(emission, "speed", speed);
            TestAssets.Set(emission, "onHitUnit", new[] { onHitUnit });
            TestAssets.Set(emission, "pierce", pierce);
            return emission;
        }

        public static RepeatEmission Repeat(int count, params IEmission[] emissions) =>
            Configure(new RepeatEmission(), ("count", count), ("emissions", emissions));

        /// <summary>Sets several private fields and returns the object: TestRecipes.Configure(new ClampScaler(), ("maximum", 50f)).</summary>
        public static T Configure<T>(T target, params (string field, object value)[] fields)
        {
            foreach (var (field, value) in fields) TestAssets.Set(target, field, value);
            return target;
        }

        /// <summary>Every built-in step, in the recommended order, on a hit that goes all the way through.</summary>
        public static DamagePipeline FullPipeline(TestAssets assets, FakeUnit target)
        {
            var stat = assets.Stat("Any");
            var shieldType = assets.Make<ResourceType>("Shield");
            var resources = new FakeResources();
            target.Add(resources);
            resources.Add(shieldType, 1e9f, 1e9f); // never runs out during a measurement

            var pipeline = assets.Make<DamagePipeline>();
            pipeline.Steps = new IDamageStep[]
            {
                new FriendlyFireStep(),
                TestRecipes.Configure(new ImmuneWithTagStep(), ("tag", assets.Tag("State.Invulnerable"))),
                TestRecipes.Configure(new EvadeStep(), ("evadeChance", stat)),
                new CritStrikeStep(),
                new HitboxMultiplierStep(),
                TestRecipes.Configure(new ArmorStep(), ("armorStat", stat)),
                new DamageResistanceStep(),
                TestRecipes.Configure(new FlatReductionStep(), ("reduction", stat)),
                new MinimumDamageStep(),
                TestRecipes.Configure(new ShieldAbsorbStep(), ("shield", shieldType), ("absorbShare", 0.5f))
            };
            return pipeline;
        }

        public static Projectile PrefabOf(ProjectileEmission emission) =>
            (Projectile)typeof(ProjectileEmission)
                .GetField("prefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(emission);
    }
}
