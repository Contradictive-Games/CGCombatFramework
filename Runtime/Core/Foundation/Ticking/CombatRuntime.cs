using UnityEngine;

namespace CG.CombatFramework
{
    public static class CombatRuntime
    {
        private static ITickSource tickSource;
        private static ICombatRandom random;

        public static ITickSource TickSource
        {
            get
            {
                if(tickSource is Object unityObject && unityObject == null) tickSource = null;

                if(tickSource == null)
                {
                    var go = new GameObject("[CG Combat Ticker]");
                    Object.DontDestroyOnLoad(go);
                    tickSource = go.AddComponent<CombatTicker>();
                }
                return tickSource;
            }
            set => tickSource = value;
        }

        public static ICombatRandom Random
        {
            get => random ??= new UnityCombatRandom();
            set => random = value;
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            tickSource = null;
            random = null;
        }
    }

}