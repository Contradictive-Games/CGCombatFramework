using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace CG.CombatFramework
{
    public sealed class PooledUnitSpawner : IUnitSpawner
    {
        private readonly Dictionary<Unit, ObjectPool<Unit>> pools = new();
        private readonly Dictionary<Unit, ObjectPool<Unit>> active = new();
        private readonly List<Unit> buffer = new();
        private readonly Transform root;

        public PooledUnitSpawner(Transform root)
        {
            this.root = root;
        }

        public IUnit Spawn(Unit prefab, in SpawnData data)
        {
            var pool = GetPool(prefab);
            var unit = pool.Get();
            active[unit] = pool;
            unit.Spawn(in data);
            return unit;
        }

        public void Despawn(IUnit unit, DespawnReason reason)
        {
            if(unit is not Unit u) return;
            if(u == null)
            {
                active.Remove(u);
                return;
            }
            u.Despawn(reason);
            if(active.Remove(u, out var pool)) pool.Release(u);
            else Object.Destroy(u.gameObject);
        }

        public void DespawnAll(DespawnReason reason)
        {
            buffer.Clear();
            buffer.AddRange(active.Keys);
            foreach(var unit in buffer)
            {
                Despawn(unit, reason);
            }
        }

        public void Prewarm(Unit prefab, int count)
        {
            var pool = GetPool(prefab);
            buffer.Clear();
            for(int i =0; i < count; i++)
            {
                buffer.Add(pool.Get());
            }
            foreach(var unit in buffer)
            {
                pool.Release(unit);
            }
        }

        private ObjectPool<Unit> GetPool(Unit prefab)
        {
            if(pools.TryGetValue(prefab, out var pool)) return pool;

            pool = new ObjectPool<Unit>(
                createFunc: () =>
                {
                    var unit = Object.Instantiate(prefab, root);
                    unit.gameObject.SetActive(false);
                    return unit;
                },
                actionOnGet: null,
                actionOnRelease: unit => unit.gameObject.SetActive(false),
                actionOnDestroy: unit =>
                {
                    if(unit != null)
                    {
                        Object.Destroy(unit.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: 1024
            );
            pools.Add(prefab, pool);
            return pool;
        }
    }
}