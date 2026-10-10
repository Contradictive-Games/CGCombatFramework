using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace CG.CombatFramework
{
    public static class ProjectilePool
    {
        private static readonly Dictionary<Projectile, ObjectPool<Projectile>> pools = new();
        private static readonly HashSet<Projectile> inFlight = new();
        private static readonly List<Projectile> buffer = new();
        private static Transform root;

        public static Projectile Get(Projectile prefab)
        {
            var pool = GetPool(prefab);
            var projectile = pool.Get();
            projectile.SetPool(pool);
            inFlight.Add(projectile);
            return projectile;
        }

        public static void Prewarm(Projectile prefab, int count)
        {
            var pool = GetPool(prefab);
            buffer.Clear(); ;
            for (int i = 0; i < count; i++)
            {
                buffer.Add(pool.Get());
            }
            foreach (var projectile in buffer)
            {
                pool.Release(projectile);
            }
            buffer.Clear();
        }

        public static void ReleaseAll()
        {
            buffer.Clear();
            buffer.AddRange(inFlight);
            foreach (var projectile in buffer)
            {
                if (projectile != null) projectile.Release();
            }
            inFlight.Clear();
            buffer.Clear();
        }

        internal static void MarkReleased(Projectile projectile) => inFlight.Remove(projectile);

        private static ObjectPool<Projectile> GetPool(Projectile prefab)
        {
            if (pools.TryGetValue(prefab, out var pool)) return pool;

            pool = new ObjectPool<Projectile>(
                createFunc: () =>
                {
                    var projectile = Object.Instantiate(prefab, Root);
                    projectile.gameObject.SetActive(false);
                    return projectile;
                },
                actionOnRelease: projectile => projectile.gameObject.SetActive(false),
                actionOnDestroy: projectile =>
                {
                    if (projectile != null) Object.Destroy(projectile.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: 32,
                maxSize: 2048
            );
            pools.Add(prefab, pool);
            return pool;
        }

        public static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject($"{Globals.k_PACKAGE_NAME} Projectiles");
                    Object.DontDestroyOnLoad(go);
                    root = go.transform;
                }
                return root;
            }
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pools.Clear();
            inFlight.Clear();
            buffer.Clear();
            root = null;
        }
    }
}