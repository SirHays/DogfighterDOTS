using Unity.Entities;

namespace Components
{
    //holds the observations for the ML model
    public struct ObservationsComponent : IComponentData
    {
        public float fDist;
        public float lDist;
        public float rDist;
        public float flDist;
        public float frDist;
    }
}