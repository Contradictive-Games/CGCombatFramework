using UnityEngine;

namespace CG.CombatFramework
{
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Aiming/Transform Aim")]
    public sealed class TransformAimProvider : MonoBehaviour, IAimProvider
    {
        [SerializeField, Tooltip("Muzzle or hand, otherwise defaults to this transforms")] private Transform origin;
        [SerializeField, Min(0f)] private float defaultDistance = 20f;
        [SerializeField, Tooltip("Aim this far above target pivot")] private float targetHeight = 1f;

        private UnitHandle target;
        public Transform Muzzle => origin != null ? origin : transform;
        public Vector3 Origin => Muzzle.position;
        public Vector3 AimPoint
        {
            get
            {
                var unit = target.Get();
                return unit != null ?
                    unit.Transform.position + Vector3.up * targetHeight
                    : Muzzle.position + Muzzle.forward * defaultDistance;
            }
        }

        public Vector3 Direction
        {
            get
            {
                Vector3 dir = AimPoint - Origin;
                return dir.sqrMagnitude > 0.001f ? dir.normalized : Muzzle.forward;
            }
        }

        public void SetTarget(IUnit unit) => target = new UnitHandle(unit);
        public void ClearTarget() => target = default;
    }
}
