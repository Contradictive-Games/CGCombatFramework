using UnityEngine;

namespace CG.CombatFramework
{
    public interface IAimProvider
    {
        /// <summary>
        /// The origin position that we are looking from
        /// </summary>
        public Vector3 Origin { get; }
        /// <summary>
        /// The world position that we are aiming at
        /// </summary>
        public Vector3 AimPoint { get; }
        /// <summary>
        /// The direction we are looking
        /// </summary>
        public Vector3 Direction { get; }
    }
}
