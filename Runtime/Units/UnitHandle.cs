using System;

namespace CG.CombatFramework
{
    /// <summary>
    /// A reference to one life of a Unit and will not resolve when Unit life ends even if GameObject
    /// is recycled
    /// </summary>
    public readonly struct UnitHandle : IEquatable<UnitHandle>
    {
        public readonly IUnit Unit;
        public readonly UnitId Id;

        public UnitHandle(IUnit unit)
        {
            Unit = unit;
            Id = unit != null ? unit.Id : UnitId.None;
        }

        public bool IsValid => Unit != null && Id.IsValid && Unit.Id == Id;

        public IUnit Get() => IsValid ? Unit : null;

        public bool Equals(UnitHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is UnitHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => IsValid ? $"{Unit.DisplayName} ({Id})" : "None";
    }
}
