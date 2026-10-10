using System.Runtime.CompilerServices;

// Tests can see internal members (the status effect pool, instance versions) without making them public.
[assembly: InternalsVisibleTo("CG.CombatFramework.TestUtils")]
[assembly: InternalsVisibleTo("CG.CombatFramework.Tests")]
[assembly: InternalsVisibleTo("CG.CombatFramework.Tests.PlayMode")]
[assembly: InternalsVisibleTo("CG.CombatFramework.Tests.Performance")]