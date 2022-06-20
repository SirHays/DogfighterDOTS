using Unity.Entities;
using Unity.Mathematics;

namespace Components
{
    //tag used to identify an enemy entity
    [GenerateAuthoringComponent]
    public struct Unit : IComponentData
    {
        public float3 posOfDeath;
    }
}