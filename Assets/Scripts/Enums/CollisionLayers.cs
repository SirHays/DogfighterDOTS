using System;

namespace Enums
{
    [Flags]
    public enum CollisionLayers
    {
        //sets layer ids to appropriate layer name.
        Wall,
        Obstacle,
        Player,
        Enemy,
        Raycast,
    }
}