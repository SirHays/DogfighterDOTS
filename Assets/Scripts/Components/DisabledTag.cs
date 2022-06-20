using Unity.Entities;
using Unity.Mathematics;

namespace Components
{
    //added to disabled entities
    public struct DisabledTag : IComponentData
    {
        public bool killedByPlayer;
    }
}
//maybe do effect instantiation in pausemenu frame check.