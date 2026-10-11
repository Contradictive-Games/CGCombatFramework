using UnityEngine;

namespace CG.CombatFramework
{
    internal static class PhysicsSort
    {
        public static void ByDistance(RaycastHit[] hits, int count)
        {
            for(int i = 1; i < count; i++)
            {
                var current = hits[i];
                int j = i - 1;
                while(j >= 0 && hits[j].distance > current.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }
                hits[j + 1] = current;
            }
        }
    }
}