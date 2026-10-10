using System;
using System.Collections.Generic;
using Codice.CM.Client.Differences.Graphic;
using UnityEngine;

namespace CG.CombatFramework
{
    public sealed class FirableLoadout : MonoBehaviour, IUnitComponent, ITickable
    {
        [System.Serializable]
        public struct SlotEntry
        {
            public FirableSlot Slot;
            public FirableDefinition Firable;
        }

        [SerializeField] private List<SlotEntry> startingLoadout = new();

        private readonly Dictionary<FirableSlot, Firable> equipped = new();
        private readonly List<Firable> tickBuffer = new();
        private readonly List<FirableSlot> slotBuffer = new();
        private IUnit owner;

        public IReadOnlyDictionary<FirableSlot, Firable> Equipped => equipped;

        public event Action<FirableSlot, Firable> OnEquipped;
        public event Action<FirableSlot, Firable> OnUnequipped;

        public void Initialize(IUnit owner) => this.owner = owner;
        public void Bind(){ }
        public void Unbind(){ }

        public void OnSpawned(in SpawnData data)
        {
            if(equipped.Count == 0)
            {
                EquipStartingLoadout();
                return;
            }
            foreach(var firable in equipped.Values)
            {
                firable.Reset();
            }
        }

        public void OnDespawned(DespawnReason reason)
        {
            foreach(var firable in equipped.Values)
            {
                firable.Cancel();
            }
        }

        public void EquipStartingLoadout()
        {
            UnequipAll();
            foreach(var entry in startingLoadout)
            {
                if(entry.Slot != null && entry.Firable != null)
                {
                    Equip(entry.Slot, entry.Firable);
                }
            }
        }

        public Firable Equip(FirableSlot slot, FirableDefinition definition)
        {
            if(slot == null) throw new ArgumentNullException(nameof(slot));
            Unequip(slot);
            if(definition == null) return null;

            var firable = new Firable(definition, owner);
            equipped.Add(slot, firable);
            OnEquipped?.Invoke(slot, firable);
            return firable;
        }

        public void Unequip(FirableSlot slot)
        {
            if(slot == null || !equipped.Remove(slot, out var firable)) return;
            firable.Cancel();
            OnUnequipped?.Invoke(slot, firable);
        }

        public void UnequipAll()
        {
            slotBuffer.Clear();
            slotBuffer.AddRange(equipped.Keys);
            foreach(var slot in slotBuffer)
            {
                Unequip(slot);
            }
        }

        public bool TryGet(FirableSlot slot, out Firable firable)
        {
            firable = null;
            return slot != null && equipped.TryGetValue(slot, out firable);
        }

        public void Press(FirableSlot slot)
        {
            if(TryGet(slot, out var firable)) firable.Press();
        }
        public void Release(FirableSlot slot)
        {
            if(TryGet(slot, out var firable)) firable.Release();
        }
        public bool Reload(FirableSlot slot)
        {
            return TryGet(slot, out var firable) && firable.TryGetGate<IReloadable>(out var ammo) && ammo.Reload();
        }

        public void Tick(float deltaTime)
        {
            tickBuffer.Clear();
            tickBuffer.AddRange(equipped.Values);
            for(int i = 0; i < tickBuffer.Count; i++)
            {
                tickBuffer[i].Tick(deltaTime);
            }
        }

    }
}