using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/Health Component")]
    public sealed class HealthComponent : MonoBehaviour, IHealth, IUnitComponent
    {
        [Header("Settings")]
        [SerializeField] private ResourceType healthType;
        [SerializeField] private DamagePipeline pipeline;
        [SerializeField, Tooltip("Optional tag granted while dead")] private CombatTag deadTag;


        private IUnit owner;
        private IResource resource;
        private bool applyingDamage;
        private bool deathPending;

        public IReadonlyResource Resource => resource;
        public bool IsDead { get; private set; }

        public event Action<DamageResult> OnDamaged;
        public event Action<HealResult> OnHealed;
        public event Action<DeathInfo> OnDied;
        public event Action OnRevived;

        public void Initialize(IUnit owner) => this.owner = owner;

        public void Bind()
        {
            if (!owner.TryGet<IResourceProvider>(out var provider) || !provider.TryGet(healthType, out resource))
            {
                Debug.LogError($"[Unit:HealthComponent] '{owner.DisplayName}' has no '{(healthType ? healthType : "null")}' resource in its profile", this);
                return;
            }

            resource.OnDepleted += OnDepleted;
        }

        public void Unbind()
        {
            if (resource != null) resource.OnDepleted -= OnDepleted;
        }


        public DamageResult ApplyDamage(in DamageRequest request)
        {
            if (IsDead || resource == null || !owner.IsSpawned) return DamageResult.Ignored(in request);

            var calc = pipeline != null ? pipeline.Run(in request, owner) : new DamageCalculation(in request, owner);

            applyingDamage = true;
            float dealt = resource.Remove(calc.Amount, ChangeReason.Damage, request.Context);
            applyingDamage = false;

            var result = new DamageResult(in request, calc.Result, dealt, calc.Absorbed, calc.Amount - dealt, calc.IsCrit, deathPending);

            var source = request.Context.SourceUnit;
            OnDamaged?.Invoke(result);
            GameEvents.Raise(owner, new DamageReceivedEvent(source, owner, in result));
            GameEvents.Raise(source, new DamageDealtEvent(source, owner, in result));

            if (deathPending)
            {
                deathPending = false;
                Die(in request.Context, result);
            }
            return result;
        }

        public HealResult ApplyHeal(float amount, in EffectContext ctx)
        {
            if (IsDead || resource == null || !owner.IsSpawned) return HealResult.Ignored();
            float healed = resource.Add(amount, ChangeReason.Heal, ctx);
            if(healed <= 0f) return HealResult.Ignored();
            
            var result = new HealResult(healed, amount - healed, false, in ctx);
            var source = ctx.SourceUnit;
            OnHealed?.Invoke(result);
            GameEvents.Raise(owner, new HealReceivedEvent(source, owner, in result));
            GameEvents.Raise(source, new HealDealtEvent(source, owner, in result));
            return result;
        }


        public void Kill(in EffectContext ctx)
        {
            if (IsDead || resource == null) return;
            if (resource.IsDepleted) Die(in ctx, null);
            else resource.Set(0f, ChangeReason.Damage, in ctx);
        }

        public void Revive(float normalized = 1f)
        {
            if (!IsDead || resource == null) return;
            IsDead = false;
            resource.RegenPaused = false;
            if (deadTag != null) owner.Tags.Remove(deadTag);
            resource.Set(resource.Max * Mathf.Clamp01(normalized), ChangeReason.Heal);
            OnRevived?.Invoke();
        }

        private void OnDepleted(ResourceChange change)
        {
            if (applyingDamage)
            {
                deathPending = true;
                return;
            }
            Die(in change.Context, null);
        }

        private void Die(in EffectContext ctx, DamageResult? finalBlow)
        {
            if (IsDead) return;
            IsDead = true;
            resource.RegenPaused = true;
            if (deadTag != null) owner.Tags.Add(deadTag);
            
            var info = new DeathInfo(owner, ctx.SourceUnit?.Root(), in ctx, finalBlow);
            OnDied?.Invoke(info);
            GameEvents.Raise(owner, new DiedEvent(in info));
            if(info.Killer != null && !ReferenceEquals(info.Killer, owner))
            {
                GameEvents.Raise(info.Killer, new KilledUnitEvent(in info));
            }
        }

        public void OnSpawned(in SpawnData data)
        {
            IsDead = false;
            applyingDamage = false;
            deathPending = false;
            if (resource != null) resource.RegenPaused = false;
        }

        public void OnDespawned(DespawnReason reason) { }
    }
}