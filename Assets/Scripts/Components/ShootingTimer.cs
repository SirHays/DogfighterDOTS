using Unity.Entities;

namespace Components
{
    [GenerateAuthoringComponent]
    public struct ShootingTimer : IComponentData
    {
        public float timer;
    }
}