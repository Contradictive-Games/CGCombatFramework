using UnityEngine;

namespace CG.CombatFramework
{
    public sealed class TestingMonoBehavior : MonoBehaviour
    {
        [SerializeReference, SelectType] public BaseGenericType[] BaseGenericTypes;
        [SerializeReference, SelectType] public IBaseInterface[] BaseInterfaces;
    }
}