using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [AddComponentMenu("CG Combat Framework/Health")]
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
            if(!owner.TryGet<IResourceProvider>(out var provider) || !provider.TryGet(healthType, out resource))
            {
                Debug.LogError($"[Unit:HealthComponent] '{owner.DisplayName}' has no '{(healthType ? healthType : "null")}' resource in its profile", this);
                return;
            }

            resource.OnDepleted += OnDepleted;
            IsDead = resource.IsDepleted;
        }

        public void Unbind()
        {
            if(resource != null) resource.OnDepleted -= OnDepleted;
        }


        public DamageResult ApplyDamage(in DamageRequest request)
        {
            if(IsDead || resource == null) return DamageResult.Ignored(in request);

            var calc = pipeline != null ? pipeline.Run(in request, owner) : new DamageCalculation(in request, owner);

            applyingDamage = true;
            float dealt = resource.Remove(calc.Amount, ChangeReason.Damage, request.Context);
            applyingDamage = false;

            var result = new DamageResult(in request, calc.Result, dealt, calc.Absorbed, calc.Amount - dealt, calc.IsCrit, deathPending);

            OnDamaged?.Invoke(result);

            if (deathPending)
            {
                deathPending = false;
                Die(request.Context, result);
            }
            return result;
        }

        public HealResult ApplyHeal(float amount, in EffectContext ctx)
        {
            Debug.LogWarning($"Heal currently does not take in a HealRequest and doesn't allow for crits");
            if(IsDead || resource == null) return HealResult.Ignored();
            float healed = resource.Add(amount, ChangeReason.Heal, ctx);
            if(healed > 0f) {
                HealResult result = new HealResult(healed, amount - healed, false, in ctx);
                OnHealed?.Invoke(result);
                return result;
            }
            return HealResult.Ignored();
        }


        public void Kill(in EffectContext ctx)
        {
            if(IsDead || resource == null) return;
            if(resource.IsDepleted) Die(ctx, null);
            else resource.Set(0f, ChangeReason.Damage, ctx);
        }

        public void Revive(float normalized = 1f)
        {
            if(!IsDead || resource == null) return;
            IsDead = false;
            resource.RegenPaused = false;
            if(deadTag != null) owner.Tags.Remove(deadTag);
            resource.Set(resource.Max * Mathf.Clamp01(normalized), ChangeReason.Heal);
            OnRevived?.Invoke();
        }

        private void OnDepleted(ResourceChange change)
        {
            if(applyingDamage)
            {
                deathPending = true;
                return;
            }
            Die(change.Context, null);
        }

        private void Die(in EffectContext ctx, DamageResult? finalBlow)
        {
            if(IsDead) return;
            IsDead = true;
            resource.RegenPaused = true;
            if(deadTag != null) owner.Tags.Add(deadTag);
            OnDied?.Invoke(new DeathInfo(owner, ctx.SourceUnit, in ctx, finalBlow));
        }

        public void OnSpawned(in SpawnData data)
        {
            throw new NotImplementedException();
        }

        public void OnDespawned(DespawnReason reason)
        {
            throw new NotImplementedException();
        }
    }
}