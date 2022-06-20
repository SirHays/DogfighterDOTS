using Unity.Entities;

namespace Components
{
    //tag used to identify a player entity
    [GenerateAuthoringComponent]
    public struct PlayerComponent : IComponentData
    {
        public int score;
        public int pointsNeededToWin;
    }
}