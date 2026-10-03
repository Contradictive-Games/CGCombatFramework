namespace CG.CombatFramework
{
    public delegate void GameEventHandler<T>(in T evt) where T : struct;
}