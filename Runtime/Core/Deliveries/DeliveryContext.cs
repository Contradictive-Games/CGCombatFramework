using Vector3 = UnityEngine.Vector3;

namespace CG.CombatFramework
{
    public readonly struct DeliveryContext
    {
        public readonly EffectContext Effect;
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        public readonly Vector3 AimPoint;

        public DeliveryContext(in EffectContext effect, Vector3 origin, Vector3 direction, Vector3 aimPoint)
        {
            Effect = effect;
            Origin = origin;
            Direction = direction;
            AimPoint = aimPoint;
        }
    }
}