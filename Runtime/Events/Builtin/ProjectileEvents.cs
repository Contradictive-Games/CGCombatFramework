using Vector3 = UnityEngine.Vector3;

namespace CG.CombatFramework
{
    public enum ProjectileStopReason { HitWorld, LastUnit, Expired, Released }

    public readonly struct ProjectileLaunchedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Projectile Projectile;
        public readonly Vector3 Origin;
        public readonly Vector3 Velocity;
        public readonly IGameDefinition Cause;
        public ProjectileLaunchedEvent(IUnit unit, Projectile projectile, Vector3 origin, Vector3 velocity, IGameDefinition cause)
        {
            Unit = unit;
            Projectile = projectile;
            Origin = origin;
            Velocity = velocity;
            Cause = cause;
        }
    }

    public readonly struct ProjectileBouncedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Projectile Projectile;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public ProjectileBouncedEvent(IUnit unit, Projectile projectile, Vector3 point, Vector3 normal)
        {
            Unit = unit;
            Projectile = projectile;
            Point = point;
            Normal = normal;
        }
    }

    public readonly struct ProjectileStoppedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Projectile Projectile;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly ProjectileStopReason Reason;
        public ProjectileStoppedEvent(IUnit unit, Projectile projectile, Vector3 point, Vector3 normal, ProjectileStopReason reason)
        {
            Unit = unit;
            Projectile = projectile;
            Point = point;
            Normal = normal;
            Reason = reason;
        }
    }
}
