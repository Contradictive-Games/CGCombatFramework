using System.Collections.Generic;

namespace CG.CombatFramework
{
    public interface IArrivalSequence
    {
        public float Begin(IUnit unit, IReadOnlyList<CombatTag> tags);
        public void Stop() { }
    }
}
