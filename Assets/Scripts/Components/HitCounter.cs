using Unity.Entities;

namespace Components
{
    //holds the number of hits a ship has suffered
    [GenerateAuthoringComponent]
    public struct HitCounter : IComponentData
    {
        public int counter;
        public int hitsToKill;
    }
}