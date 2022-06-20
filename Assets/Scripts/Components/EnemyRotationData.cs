using Unity.Entities;

namespace Components
{
    //holds rotation values for ship entities.
    [GenerateAuthoringComponent]
    public struct EnemyRotationData : IComponentData
    {
        public float vertical, horizontal;
    }
}