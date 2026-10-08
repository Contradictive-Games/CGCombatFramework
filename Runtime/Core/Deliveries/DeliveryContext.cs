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

        public static DeliveryContext FromUnit(IUnit source, IGameDefinition cause, float power = 1f, IGameRandom random = null)
        {
            var effect = new EffectContext(source, cause, null, default, power, 1, 0, random);
            if(source.TryGet<IAimProvider>(out var aim))
            {
                return new DeliveryContext(in effect, aim.Origin, aim.Direction, aim.AimPoint);
            }
            var tf = source.Transform;
            return new DeliveryContext(in effect, tf.position, tf.forward, tf.position + tf.forward * 10f);
        }
    }
}