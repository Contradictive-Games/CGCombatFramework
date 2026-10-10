using UnityEngine;

namespace CG.CombatFramework
{
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/ Rigidbody Knockback")]
    public sealed class RigidbodyKnock : MonoBehaviour, IKnockbackReceiver
    {
        [SerializeField, Tooltip("On = heavier rigidbodies move less, Off = all rigidbodies gets same speed chance")]
        private bool useMass;

        private Rigidbody body;

        private void Awake() => body = GetComponent<Rigidbody>();

        public void ApplyKnockback(Vector3 impulse, in EffectContext ctx)
        {
            if(body == null || body.isKinematic) return;
            body.AddForce(impulse, useMass ? ForceMode.Impulse : ForceMode.VelocityChange);
        }
    }
}