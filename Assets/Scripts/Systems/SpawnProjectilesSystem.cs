
using Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using static Unity.Mathematics.math;
using Unity.Mathematics;
using quaternion = Unity.Mathematics.quaternion;

namespace Components
{
    public struct PlayerProjectile : IComponentData {}
}

namespace Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class SpawnProjectilesSystem : SystemBase
    {
       //helper entity
        private EntityQuery spawnerEntityQuery;
        //used for playing back changes
        private BeginPresentationEntityCommandBufferSystem beginPresentationEntityCommandBufferSystem;
        
        protected override void OnStartRunning()
        {
            spawnerEntityQuery = GetEntityQuery(ComponentType.ReadOnly<EntitySpawnData>());
            beginPresentationEntityCommandBufferSystem =
                World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecb = beginPresentationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            //entityspawndata component
            var spawnerDataArray = spawnerEntityQuery.ToComponentDataArray<EntitySpawnData>(Allocator.TempJob);
            var dataComponent = spawnerDataArray[0];
            //grabbing entities to instantiate from spawndata component
            var projEntity = dataComponent.projectileEntity;
            var playerProjEntity = dataComponent.playerProjectileEntity;
            spawnerDataArray.Dispose();
            
            //runs for enemies that need to shoot
            Entities
                .WithAll<NeedToShoot>()
                .WithNone<PlayerComponent>()
                .ForEach((Entity e, int entityInQueryIndex,in Rotation rotation, in LocalToWorld localToWorld) =>
                {
                    float4x4 ltw = localToWorld.Value;
                    //sets positions for projectile instantiation
                    float3 posL = transform(ltw, new float3(10,0,10));
                    float3 posR = transform(ltw, new float3(-10, 0, 10));
                    
                    //instantiates projectiles
                    Entity proj1 = ecb.Instantiate(entityInQueryIndex, projEntity);
                    Entity proj2 = ecb.Instantiate(entityInQueryIndex, projEntity);
                    
                    
                    ecb.SetComponent(entityInQueryIndex,proj1,new ProjectileComponent{origin = e});
                    ecb.SetComponent(entityInQueryIndex,proj2,new ProjectileComponent{origin = e});
                    //sets positions and rotations for projectiles
                    ecb.SetComponent(entityInQueryIndex,proj1,new Translation{Value = posL});
                    ecb.SetComponent(entityInQueryIndex,proj2,new Translation{Value = posR});
                    
                    var rot = new Rotation
                    {
                        Value = mul(rotation.Value, quaternion.Euler(80, 0, 0))
                    };
                    
                    ecb.SetComponent(entityInQueryIndex,proj1,rot);
                    ecb.SetComponent(entityInQueryIndex,proj2,rot);
                    //removes the needtoshot component
                    ecb.RemoveComponent<NeedToShoot>(entityInQueryIndex,e);
                }).ScheduleParallel();
                
            //runs for the main player when they need to shot
            Entities
                .WithAll<PlayerComponent, NeedToShoot>()
                .ForEach((Entity e, int entityInQueryIndex,in Rotation rotation, in LocalToWorld localToWorld) =>
                {
                    float4x4 ltw = localToWorld.Value;
                    
                    //sets positions for projectile instantiation
                    float3 posTL = transform(ltw, new float3(10,3,10));
                    float3 posTR = transform(ltw, new float3(-10, 3, 10));
                    float3 posBL = transform(ltw, new float3(10,-3,10));
                    float3 posBR = transform(ltw, new float3(-10, -3, 10));
                    
                    //instantiates projectiles
                    Entity proj1 = ecb.Instantiate(entityInQueryIndex, playerProjEntity);
                    Entity proj2 = ecb.Instantiate(entityInQueryIndex, playerProjEntity);
                    Entity proj3 = ecb.Instantiate(entityInQueryIndex, playerProjEntity);
                    Entity proj4 = ecb.Instantiate(entityInQueryIndex, playerProjEntity);
                    
                    ecb.SetComponent(entityInQueryIndex,proj1,new ProjectileComponent{origin = e});
                    ecb.SetComponent(entityInQueryIndex,proj2,new ProjectileComponent{origin = e});
                    ecb.SetComponent(entityInQueryIndex,proj3,new ProjectileComponent{origin = e});
                    ecb.SetComponent(entityInQueryIndex,proj4,new ProjectileComponent{origin = e});
                    
                    //sets positions and rotations for projectiles
                    ecb.SetComponent(entityInQueryIndex,proj1,new Translation{Value = posTL});
                    ecb.SetComponent(entityInQueryIndex,proj2,new Translation{Value = posTR});
                    ecb.SetComponent(entityInQueryIndex,proj3,new Translation{Value = posBL});
                    ecb.SetComponent(entityInQueryIndex,proj4,new Translation{Value = posBR});

                    var rot = new Rotation
                    {
                        Value = mul(rotation.Value, quaternion.Euler(80, 0, 0))
                    };
                    
                    ecb.SetComponent(entityInQueryIndex,proj1,rot);
                    ecb.SetComponent(entityInQueryIndex,proj2,rot);
                    ecb.SetComponent(entityInQueryIndex,proj3,rot);
                    ecb.SetComponent(entityInQueryIndex,proj4,rot);
                    
                    //removes the needtoshot component
                    ecb.RemoveComponent<NeedToShoot>(entityInQueryIndex,e);
                }).ScheduleParallel();
            
            //set dependency for ecb
            beginPresentationEntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}