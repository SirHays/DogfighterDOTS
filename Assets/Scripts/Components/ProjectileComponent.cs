using Unity.Entities;

namespace Components
{
    //tag used to identify a projectile entity
    public struct ProjectileComponent : IComponentData
    {
        public Entity origin;
    }
}