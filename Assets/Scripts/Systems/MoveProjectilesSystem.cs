using Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Systems
{
    [UpdateAfter(typeof(SpawnProjectilesSystem))]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class MoveProjectilesSystem : SystemBase
    {
        //used for playing back changes.
        private BeginPresentationEntityCommandBufferSystem beginPresentationEntityCommandBufferSystem;

        protected override void OnCreate()
        {
            beginPresentationEntityCommandBufferSystem =
                World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecb = beginPresentationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            //bullet speed
            float firePower = 150f;
            //time since last update call
            float deltaTime = Time.DeltaTime;
            //runs for enemy projectiles.
            Entities.ForEach((Entity e, int entityInQueryIndex,ref Translation translation,ref ShootingTimer shootingTimer,in LocalToWorld ltw,in ProjectileComponent projectileComponent) =>
            {
                var originLocalToWorld = GetComponent<LocalToWorld>(projectileComponent.origin);
                
                //adding to time since instantiation
                shootingTimer.timer += deltaTime;
                //if 1 second passed since instantiation, destroy projectile
                if (shootingTimer.timer > 2f)
                {
                    ecb.DestroyEntity(entityInQueryIndex,e);
                    return;
                }
                //move projectile forward the appropriate amount
                translation.Value += originLocalToWorld.Forward * firePower * deltaTime;
                
            }).ScheduleParallel();
            
            //set dependency for ecb
            beginPresentationEntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}