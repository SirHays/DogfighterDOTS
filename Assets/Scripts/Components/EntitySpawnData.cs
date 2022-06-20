using Unity.Entities;

namespace Components
{
    //holds entity prefabs used for instantitation
    [GenerateAuthoringComponent]
    public struct EntitySpawnData : IComponentData
    {
        public Entity shipEntity;
        public Entity projectileEntity;
        public Entity playerProjectileEntity;
    }
}
