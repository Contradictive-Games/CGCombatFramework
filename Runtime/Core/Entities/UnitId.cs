using System;

namespace CG.CombatFramework
{
    public readonly struct UnitId : IEquatable<UnitId>
    {
        public readonly ulong Value;
        public UnitId(ulong value) => Value = value;

        public static readonly UnitId None = default;
        public bool IsValid => Value != 0;

        public bool Equals(UnitId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is UnitId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(UnitId a, UnitId b) => a.Value == b.Value;
        public static bool operator !=(UnitId a, UnitId b) => a.Value != b.Value;
        public override string ToString() => IsValid ? $"Unit-{Value}" : "Unit-None";
    }
}
