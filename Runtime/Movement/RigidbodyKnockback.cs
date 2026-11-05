using UnityEngine;

namespace CG.CombatFramework
{
    [UnitPart("Rigidbody Knockback", Description = "Knockback and teleports through a Rigidbody, which is added for you.", Color = "#4C8DF6", Icon = "modifier", Order = -30)]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/ Rigidbody Knockback")]
    public sealed class RigidbodyKnockback : MonoBehaviour, IKnockbackReceiver, ITeleportReceiver
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

        public void Teleport(Vector3 position, in EffectContext ctx)
        {
            if(body != null)
            {
                body.position = position;
                body.linearVelocity = Vector3.zero;
            }
            transform.position = position;
        }
    }
}