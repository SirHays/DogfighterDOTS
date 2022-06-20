using Unity.Entities;
using UnityEngine;
using Components;

namespace Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(EnemyRotationSystem))]
    public class SetRotationDataSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            //runs for all enemies
            Entities.ForEach((Entity e, PilotAgent pilotAgent,ref EnemyRotationData data) =>
            {
                //sets rotation data to be the ML script's decision
                data.vertical = pilotAgent.Pitch;
                data.horizontal = pilotAgent.Roll;
            }).WithoutBurst().Run();
            
            //axis inputs
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            //runs for the player
            Entities.WithAll<PlayerComponent>().ForEach((Entity e, int entityInQueryIndex,ref EnemyRotationData data)=>
            {
                //sets rotation data to be the axis inputs
                data.vertical = vertical;
                data.horizontal = horizontal;
            }).ScheduleParallel();
        }
    }
}