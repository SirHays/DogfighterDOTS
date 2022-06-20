using Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
namespace Systems
{
    public struct HasTarget : IComponentData
    {
        public Entity targetEntity;
    }
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AddRewardSystem))]
    public class FindTargetSystem : SystemBase
    {
        //used for playing back changes
        private BeginSimulationEntityCommandBufferSystem beginSimulationEntityCommandBufferSystem;
        //player
        private EntityQuery playerQuery;


        protected override void OnStartRunning()
        {
            beginSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            
        }

    
        protected override void OnUpdate()
        {
            playerQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new []{ComponentType.ReadOnly<Translation>()},
                Any = new []{ComponentType.ReadOnly<Unit>(), ComponentType.ReadOnly<PlayerComponent>() },
                None = new []{ComponentType.ReadOnly<DisabledTag>()}
            });
            EntityCommandBuffer.ParallelWriter entityCommandBuffer = beginSimulationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            
            //async means running independently of the main program. 
            
            //potential targets
            NativeArray<Entity> entities = playerQuery.ToEntityArray(Allocator.TempJob);
            //potential target positions.
            NativeArray<Translation> translations = playerQuery.ToComponentDataArray<Translation>(Allocator.TempJob);

            
            
            //runs for enemies with no target
               Entities.WithAll<Unit>()
                   .WithNone<HasTarget>()
                   .WithReadOnly(entities)
                   .WithReadOnly(translations)
                   .WithDisposeOnCompletion(entities)
                   .WithDisposeOnCompletion(translations)
                    .ForEach((Entity entity, int entityInQueryIndex, ref Translation translation, in LocalToWorld ltw) => 
                    {
                        float3 unitPosition = translation.Value;
                        float3 unitForward = ltw.Forward;
                        Entity closestTargetEntity = Entity.Null;
                        float3 closestTargetPosition = float3.zero;
                        
                        
                        for (int i = 0; i < entities.Length; i++) 
                        {
                            //info for new target candidate.
                            var newTargetEntity = entities[i];
                            var newTargetTranslation = translations[i];
                            //distance to new target
                            var distanceToTarget = distancesq(unitPosition, newTargetTranslation.Value);
                            
                            //safety check in case the new target is the current entity.
                            if(distanceToTarget == 0f) continue;
                            
                            //if no target is considered
                            if (closestTargetEntity == Entity.Null) 
                            {
                                closestTargetEntity = newTargetEntity;
                                closestTargetPosition = newTargetTranslation.Value;
                            } 
                            else
                            {
                                float targetAng = dot(unitForward, normalize(closestTargetPosition - unitPosition));
                                float newTargetAng = dot(unitForward, normalize(newTargetTranslation.Value - unitPosition));
                                
                                //if distance is optimal and the new target angle is bigger than the old target, set the new target to the current target.
                                if (distanceToTarget < 160000f && newTargetAng > targetAng)
                                {
                                    closestTargetEntity = newTargetEntity;
                                    closestTargetPosition = newTargetTranslation.Value;
                                }
                            }
                        }
                        //if a suitable target was found.
                        if (closestTargetEntity != Entity.Null) 
                        {
                            //mark the entity as one with a target and the target entity the component.
                            entityCommandBuffer.AddComponent(entityInQueryIndex,entity, new HasTarget{targetEntity = closestTargetEntity});
                        }
                        //combined job handle ensures conversion jobs are completed before running targeting system.
                    }).ScheduleParallel();
           
               
            //set dependency for ecb
            beginSimulationEntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
