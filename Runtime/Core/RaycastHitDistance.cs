using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    internal sealed class RaycastHitDistance : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistance Instance = new();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}