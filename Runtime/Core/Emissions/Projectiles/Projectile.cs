using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace CG.CombatFramework
{
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Projectile")]
    public sealed class Projectile : MonoBehaviour, ITickable
    {
        private readonly HashSet<IUnit> alreadyHit = new();
        private readonly List<HitCandidate> impacts = new();
        private ObjectPool<Projectile> pool;
        private DeliveryContext shot;
        private ProjectileFlight flight;
        private float age;
        private int unitsLeft;

        public bool IsInAir { get; private set; }
        public DeliveryContext Shot => shot;

        internal void SetPool(ObjectPool<Projectile> owner) => pool = owner;

        internal void Launch(in DeliveryContext ctx, in ProjectileFlight settings)
        {
            shot = ctx;
            flight = settings;
            age = 0f;
            unitsLeft = settings.Pierce + 1;
            alreadyHit.Clear();

            transform.SetPositionAndRotation(ctx.Origin, Quaternion.LookRotation(ctx.Direction));
            gameObject.SetActive(true);
            IsInAir = true;
            CombatServices.TickSource.Register(this);
        }

        public void Tick(float deltaTime)
        {
            using var sample = CombatProfiler.Projectiles.Auto();
            
            if (!IsInAir) return;
            Vector3 from = transform.position;
            Vector3 dir = shot.Direction;
            float step = flight.Speed * deltaTime;
            var buffer = PhysicsBuffers.Hits;
            int count = flight.Radius > 0f
                ? Physics.SphereCastNonAlloc(from, flight.Radius, dir, buffer, step, flight.Mask, flight.Triggers)
                : Physics.RaycastNonAlloc(from, dir, buffer, step, flight.Mask, flight.Triggers);
            PhysicsSort.ByDistance(buffer, count);

            impacts.Clear();
            bool hitWorld = false;
            Vector3 stopPoint = default;
            Vector3 stopNormal = default;
            var source = shot.Effect.SourceUnit;

            for (int i = 0; i < count && impacts.Count < unitsLeft; i++)
            {
                var hit = buffer[i];
                Vector3 point = hit.distance > 0f ? hit.point : from;
                if (!HitResolver.TryResolve(hit.collider, out var unit, out var hitbox))
                {
                    hitWorld = true;
                    stopPoint = point;
                    stopNormal = hit.normal;
                    break;
                }
                if (ReferenceEquals(unit, source) || !alreadyHit.Add(unit)) continue;
                impacts.Add(new HitCandidate(
                    unit,
                    new HitData(point, hit.normal, dir, hitbox),
                    hit.distance));

            }
            for (int i = 0; i < impacts.Count && IsInAir; i++)
            {
                HitUnit(impacts[i]);
            }

            if (!IsInAir) return;

            if (hitWorld)
            {
                Stop(stopPoint, stopNormal);
                return;
            }

            transform.position = from + dir * step;
            age += deltaTime;
            if (age >= flight.Lifetime) Stop(transform.position, -dir);
        }


        private void HitUnit(in HitCandidate impact)
        {
            unitsLeft--;
            transform.position = impact.Hit.Point;

            if (flight.OnHitUnit != null)
            {
                var e = shot.Effect;
                var effect = new EffectContext(e.SourceUnit, e.Cause, impact.Unit, impact.Hit, e.Power, e.Stacks, e.ProcDepth, e.Random);
                flight.OnHitUnit.Emit(new DeliveryContext(in effect, impact.Hit.Point, shot.Direction, impact.Hit.Point));
            }
            if (unitsLeft <= 0) Release();
        }

        private void Stop(Vector3 point, Vector3 normal)
        {
            if (flight.OnStop != null)
            {
                var e = shot.Effect;
                var effect = new EffectContext(e.SourceUnit, e.Cause, null,
                    new HitData(point, normal, shot.Direction),
                    e.Power, e.Stacks, e.ProcDepth, e.Random);
                flight.OnStop.Emit(new DeliveryContext(in effect, point, Shot.Direction, point));
            }
            Release();
        }


        public void Release()
        {
            if (!IsInAir) return;
            IsInAir = false;
            CombatServices.TickSource.Unregister(this);
            ProjectilePool.MarkReleased(this);
            if (pool != null) pool.Release(this);
            else Destroy(gameObject);
        }
    }
}