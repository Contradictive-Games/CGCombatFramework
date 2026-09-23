using System.Collections.Generic;
using UnityEngine;

namespace CG.RGS.CombatFramework
{
    public sealed class Ticker : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Min(0f)] private float tickSpeed = 0.02f;

        private HashSet<ITickable> tickables = new();
        private float tickTimer;

        
        private void Update()
        {
            
        }
    }
}