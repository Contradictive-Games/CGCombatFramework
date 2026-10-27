using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class TriggerTests
    {
        private TestAssets assets;
        private FakeUnit host;
        private FakeUnit enemy;
        private FakeHealth health;
        private StatusEffectsComponent effects;
        private CountingPayload probe;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            host = new FakeUnit("Host");
            enemy = new FakeUnit("Enemy");
            health = new FakeHealth(50f) { Owner = host };
            host.Add(health);
            effects = host.AddUnitComponent<StatusEffectsComponent>();
            probe = new CountingPayload();
        }

        [TearDown]
        public void TearDown()
        {
            host.Dispose();
            enemy.Dispose();
            assets.Dispose();
        }

        private StatusEffectHandle Wear(IStatusEffectBehavior behavior, IUnit appliedBy = null) =>
            effects.Apply(assets.Effect("Item", duration: 0f, behaviors: behavior), appliedBy ?? host);

        private OnDamageDealtTrigger OnHit(params (string field, object value)[] settings)
        {
            var trigger = TestRecipes.Configure(new OnDamageDealtTrigger(), ("payloads", new IPayload[] { probe }));
            foreach (var (field, value) in settings) TestAssets.Set(trigger, field, value);
            return trigger;
        }

        private DamageResult Result(float amount, HitResult result = HitResult.Hit, int procDepth = 0, DamageFlags flags = DamageFlags.None)
        {
            var ctx = new EffectContext(host, null, enemy, default, 1f, 1, procDepth);
            var request = new DamageRequest(amount, null, in ctx, flags);
            return new DamageResult(in request, result, result == HitResult.Hit ? amount : 0f, 0f, 0f, false, false);
        }

        private void Dealt(float amount, int procDepth = 0, DamageFlags flags = DamageFlags.None) =>
            host.Events.Raise(new DamageDealtEvent(host, enemy, Result(amount, HitResult.Hit, procDepth, flags)));

        [Test]
        public void OnDamageDealt_PassesTheEventsNumbers()
        {
            Wear(OnHit());
            Dealt(20f);
            Assert.AreEqual(1, probe.Count);
            Assert.AreEqual(20f, probe.Last.Amount);
            Assert.AreSame(host, probe.Last.SourceUnit);
            Assert.AreSame(enemy, probe.Last.TargetUnit);
            Assert.AreEqual(1, probe.Last.ProcDepth);
        }

        [Test]
        public void IgnoresDamageCausedByTriggers()
        {
            Wear(OnHit());
            Dealt(20f, procDepth: 1);
            Assert.AreEqual(0, probe.Count);
        }

        [Test]
        public void IgnoresDamageOverTime_ByDefault()
        {
            Wear(OnHit());
            Dealt(20f, flags: DamageFlags.Periodic);
            Assert.AreEqual(0, probe.Count);
        }

        [Test]
        public void Cooldown_RunsOnEffectTime()
        {
            Wear(OnHit(("cooldown", 1f)));
            Dealt(10f);
            Dealt(10f);
            Assert.AreEqual(1, probe.Count);
            effects.Tick(1f);
            Dealt(10f);
            Assert.AreEqual(2, probe.Count);
        }

        [Test]
        public void EveryNth_CountsQualifyingEvents()
        {
            Wear(OnHit(("everyNth", 3)));
            for (int i = 0; i < 7; i++) Dealt(10f);
            Assert.AreEqual(2, probe.Count);
        }

        [Test]
        public void StopsListening_WhenTheEffectEnds()
        {
            var handle = Wear(OnHit());
            effects.Remove(handle);
            Dealt(10f);
            Assert.AreEqual(0, probe.Count);
        }

        [Test]
        public void HostTarget_AimsAtTheWearer_ApplierCreditPaysTheCaster()
        {
            Wear(OnHit(("credit", TriggerCredit.Applier), ("target", TriggerTarget.Host)), appliedBy: enemy);
            Dealt(10f);
            Assert.AreSame(enemy, probe.Last.SourceUnit);
            Assert.AreSame(host, probe.Last.TargetUnit);
        }

        [Test]
        public void OnDamageTaken_Avoided_FiresOnlyForEvades()
        {
            Wear(TestRecipes.Configure(new OnDamageTakenTrigger(), ("when", TakenWhen.Avoided), ("payloads", new IPayload[] { probe })));
            host.Events.Raise(new DamageReceivedEvent(enemy, host, Result(20f)));
            Assert.AreEqual(0, probe.Count);
            host.Events.Raise(new DamageReceivedEvent(enemy, host, Result(20f, HitResult.Evaded)));
            Assert.AreEqual(1, probe.Count);
            Assert.AreSame(enemy, probe.Last.TargetUnit, "the target is whoever attacked");
        }

        [Test]
        public void Lifesteal_HealsAShareOfTheDamage()
        {
            var heal = TestRecipes.Configure(new HealPayload(), ("subject", Subject.Source),
                ("amount", new ScalableValue(0.25f) { Scalers = new IValueScaler[] { new TriggeredAmountScaler() } }));
            Wear(TestRecipes.Configure(new OnDamageDealtTrigger(), ("payloads", new IPayload[] { heal })));

            Dealt(20f);
            Assert.AreEqual(55f, health.Current, 0.001f, "a quarter of the 20 dealt");
        }
    }
}
