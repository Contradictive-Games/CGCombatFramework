using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [UnitPart("Firable Loadout", Description = "Slots that hold firables, equipped when the unit spawns.", Color = "#39C5CF", Icon = "firemode", Order = -50)]
    [DisallowMultipleComponent]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/Firable Loadout")]
    public sealed class FirableLoadout : MonoBehaviour, IUnitComponent, ITickable
    {
        [System.Serializable]
        public struct SlotEntry
        {
            public FirableSlot Slot;
            public FirableDefinition Firable;
        }

        [SerializeField] private SlotEntry[] startingLoadout = System.Array.Empty<SlotEntry>();

        private readonly Dictionary<FirableSlot, Firable> equipped = new(ReferenceComparer<FirableSlot>.Instance);
        private readonly List<Firable> tickBuffer = new();
        private readonly List<FirableSlot> slotBuffer = new();
        private IUnit owner;

        public IReadOnlyDictionary<FirableSlot, Firable> Equipped => equipped;

#region IUnitComponent
        void IUnitComponent.Initialize(IUnit owner) => this.owner = owner;

        void IUnitComponent.OnSpawned(in SpawnData data)
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

        void IUnitComponent.OnDespawned(DespawnReason reason)
        {
            foreach(var firable in equipped.Values)
            {
                firable.Cancel();
            }
        }

#endregion


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
            if(slot == null) throw new System.ArgumentNullException(nameof(slot));
            Unequip(slot);
            if(definition == null) return null;

            var firable = new Firable(definition, owner);
            equipped.Add(slot, firable);
            GameEvents.Raise(owner, new FirableEquippedEvent(owner, slot, firable));
            return firable;
        }

        public void Unequip(FirableSlot slot)
        {
            if(slot == null || !equipped.Remove(slot, out var firable)) return;
            firable.Cancel();
            GameEvents.Raise(owner, new FirableUnequippedEvent(owner, slot, firable));
        }

        public void UnequipAll()
        {
            slotBuffer.Clear();
            foreach(var firable in equipped.Keys)
            {
                slotBuffer.Add(firable);
            }
            foreach(var slot in slotBuffer)
            {
                Unequip(slot);
            }
        }

        public bool TryGet(FirableSlot slot, out Firable firable)
        {
            firable = null;
            return !(slot is null) && equipped.TryGetValue(slot, out firable);
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
            using var sample = CombatProfiler.Firables.Auto();

            tickBuffer.Clear();
            foreach(var firable in equipped.Values)
            {
                tickBuffer.Add(firable);
            }
            for(int i = 0; i < tickBuffer.Count; i++)
            {
                tickBuffer[i].Tick(deltaTime);
            }
        }

    }
}
