using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace CG.CombatFramework
{
    [DisallowMultipleComponent]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Projectile")]
    public sealed class Projectile : MonoBehaviour, ITickable
    {
        private const float k_RetargetInterval = 0.25f;

        private readonly HashSet<IUnit> alreadyHit = new();
        private readonly List<HitCandidate> impacts = new();
        private ObjectPool<Projectile> pool;
        private DeliveryContext shot;
        private ProjectileFlight flight;
        private Vector3 velocity;
        private UnitHandle homingTarget;
        private float age;
        private float travelled;
        private float retargetTimer;
        private int unitsLeft;
        private int bouncesLeft;

        public bool IsInAir { get; private set; }
        public DeliveryContext Shot => shot;
        public Vector3 Velocity => velocity;

        internal void SetPool(ObjectPool<Projectile> owner) => pool = owner;

        internal void Launch(in DeliveryContext ctx, in ProjectileFlight settings)
        {
            shot = ctx;
            flight = settings;
            age = 0f;
            travelled = 0f;
            retargetTimer = 0f;
            unitsLeft = settings.Pierce + 1;
            bouncesLeft = settings.Bounces;
            alreadyHit.Clear();

            homingTarget = new UnitHandle(ctx.Effect.TargetUnit);

            velocity = settings.ArcToAimPoint && settings.Gravity > 0f
                ? ArcVelocity(ctx.Origin, ctx.AimPoint, ctx.Direction, settings.Speed, settings.Gravity)
                : ctx.Direction.normalized * settings.Speed;

            transform.SetPositionAndRotation(ctx.Origin, Facing(velocity, ctx.Direction));
            gameObject.SetActive(true);
            IsInAir = true;
            CombatServices.TickSource.Register(this);
        }

        public void Tick(float deltaTime)
        {
            using var sample = CombatProfiler.Projectiles.Auto();
            if (!IsInAir) return;

            age += deltaTime;
            Steer(deltaTime);
            if (flight.Gravity > 0f) velocity += Vector3.down * (flight.Gravity * deltaTime);

            Vector3 from = transform.position;
            Vector3 move = velocity * deltaTime;
            float step = move.magnitude;

            Vector3 dir = step > 0.00001f ? move / step : shot.Direction;

            var buffer = PhysicsBuffers.Hits;
            int count = step <= 0.00001f ? 0 : flight.Radius > 0f
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
                if (ReferenceEquals(unit, source) || !unit.IsAlive || !alreadyHit.Add(unit)) continue;
                impacts.Add(new HitCandidate(unit, new HitData(point, hit.normal, dir, hitbox, travelled + hit.distance), hit.distance));
            }

            for (int i = 0; i < impacts.Count && IsInAir; i++)
            {
                HitUnit(impacts[i]);
            }

            if (!IsInAir) return;

            if (hitWorld)
            {
                if (bouncesLeft > 0) Bounce(stopPoint, stopNormal);
                else Stop(stopPoint, stopNormal);
                return;
            }

            transform.SetPositionAndRotation(from + move, Facing(velocity, dir));
            travelled += step;
            if (age >= flight.Lifetime) Stop(transform.position, -dir);
        }

        private void Steer(float deltaTime)
        {
            if (flight.HomingTurnRate <= 0f) return;
            var target = homingTarget.Get();
            if (target == null || !target.IsAlive)
            {
                retargetTimer -= deltaTime;
                if (retargetTimer > 0f) return;
                retargetTimer = k_RetargetInterval;
                target = FindTarget();
                homingTarget = new UnitHandle(target);
                if (target == null) return;
            }

            Vector3 to = target.Transform.position + Vector3.up * flight.HomingHeight - transform.position;
            if (to.sqrMagnitude < 0.0001f) return;
            float speed = velocity.magnitude;
            velocity = Vector3.RotateTowards(velocity, to.normalized * speed, flight.HomingTurnRate * Mathf.Deg2Rad * deltaTime, 0f);
        }

        private IUnit FindTarget()
        {
            var source = shot.Effect.SourceUnit;
            if (source == null) return null;

            Vector3 position = transform.position;
            Vector3 forward = velocity;

            IUnit best = null;
            float bestSqr = flight.HomingRadius * flight.HomingRadius;
            var all = CombatServices.Units.All;
            for(int i = 0; i < all.Count; i++)
            {
                var unit = all[i];
                Vector3 to = unit.Transform.position - position;
                float sqr = to.sqrMagnitude;
                if(sqr > bestSqr || Vector3.Dot(to, forward) < 0f) continue;
                if(!unit.IsAlive || alreadyHit.Contains(unit) || source.RelationTo(unit) != FactionRelation.Hostile) continue;
                bestSqr = sqr;
                best = unit;
            }
            return best;
        }


        private void Bounce(Vector3 point, Vector3 normal)
        {
            bouncesLeft--;
            travelled += Vector3.Distance(transform.position, point);
            velocity = Vector3.Reflect(velocity, normal) * flight.BounceSpeedKept;
            transform.SetPositionAndRotation(point + normal * 0.01f, Facing(velocity, normal));
            alreadyHit.Clear();
        }


        private void HitUnit(in HitCandidate impact)
        {
            unitsLeft--;
            transform.position = impact.Hit.Point;

            if(flight.OnHitUnit != null)
            {
                var effect = shot.Effect.Retarget(impact.Unit, impact.Hit);
                flight.OnHitUnit.Emit(new DeliveryContext(in effect, impact.Hit.Point, impact.Hit.Direction, impact.Hit.Point));
            }

            if(unitsLeft > 0) return;
            if(flight.StopAfterLastUnit) Stop(impact.Hit.Point, impact.Hit.Normal);
        }

        private void Stop(Vector3 point, Vector3 normal)
        {
            if(flight.OnStop != null)
            {
                Vector3 dir = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : shot.Direction;
                var effect = shot.Effect.Retarget(null, new HitData(point, normal, dir, null, travelled));
                flight.OnStop.Emit(new DeliveryContext(in effect, point, dir, point));
            }
            Release();
        }

        public void Release()
        {
            if(!IsInAir) return;
            IsInAir = false;
            CombatServices.TickSource.Unregister(this);
            ProjectilePool.MarkReleased(this);
            if(pool != null) pool.Release(this);
            else Destroy(gameObject);
        }

        private static Vector3 ArcVelocity(Vector3 from, Vector3 to, Vector3 fallback, float speed, float gravity)
        {
            Vector3 flat = to - from;
            float height = flat.y;
            flat.y = 0f;
            float distance = flat.magnitude;
            if(distance < 0.01f || speed <= 0f) return fallback.normalized * speed;
            float time = distance / speed;
            float vertical = height / time + 0.5f * gravity * time;
            return flat / distance * speed + Vector3.up * vertical;
        }

        private static Quaternion Facing(Vector3 velocity, Vector3 fallback)
        {
            Vector3 forward = velocity.sqrMagnitude > 0.0001f ? velocity : fallback;
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        }
    }
}
