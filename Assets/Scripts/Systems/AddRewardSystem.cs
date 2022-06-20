
using Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;
using UnityEngine;

namespace Components
{
    public struct RewardComponent : IComponentData
    {
        public float reward;
    }

    public struct NeedToShoot : IComponentData {}
}


namespace Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AssignPenalty))]
    public class AddRewardSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem ecbSystem;

        protected override void OnCreate()
        {
            ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        //implement condition for removing hastarget component
        protected override void OnUpdate()
        {
            //used for playing back changes
            EntityCommandBuffer.ParallelWriter ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();
            //stores all translation components. 
            var allTranslations = GetComponentDataFromEntity<Translation>(true);
            var disabledTagged = GetComponentDataFromEntity<DisabledTag>(true);
            //time since last update call.
            var deltaTime = Time.DeltaTime;
            //goes over all entities with below components.
            Entities
                .WithReadOnly(allTranslations)
                .WithReadOnly(disabledTagged)
                .ForEach((Entity entity,int entityInQueryIndex,ref HasTarget hasTarget,ref ShootingTimer shootingTimer, in LocalToWorld localToWorld) =>
                {
                    //add deltaTime to timer
                    shootingTimer.timer += deltaTime;
                    //set current target to variable
                    Entity targetEntity = hasTarget.targetEntity;
                    //if target exists in the world
                    if (allTranslations.HasComponent(targetEntity) && !disabledTagged.HasComponent(targetEntity))
                    {
                        float3 unitPosition = allTranslations[entity].Value;
                        float3 unitForward = localToWorld.Forward;
                        float3 targetPosition = allTranslations[targetEntity].Value;
                        //calc angle to the target
                        float ang = dot(unitForward, normalize(targetPosition - unitPosition));
                        //check if distance and ang satisfy target requirements
                        if (distance(unitPosition, targetPosition) < 400f && ang > -0.2f)
                        {
                            //set reward appropriate to angle
                            ecb.SetComponent(entityInQueryIndex,entity, new RewardComponent
                            {
                                reward = ang * 0.1f
                            });
                            //nice
                            //if ang is large enough and enough time passed since last shot, shoot projectiles.
                            if (ang > 0.8f && shootingTimer.timer > 0.2f)
                            {
                                ecb.AddComponent<NeedToShoot>(entityInQueryIndex, entity);
                                shootingTimer.timer = 0f;
                            }
                            return;
                        }
                    }
                    //if requirements were not met, remove hastarget component and let FindTargetSystem find another target.
                    ecb.RemoveComponent<HasTarget>(entityInQueryIndex, entity);
            }).ScheduleParallel();
            
            //if left click is pressed, shoot player projectiles.
            if (Input.GetMouseButtonDown(0))
            {
                double timeSinceInit = Time.ElapsedTime;
                
                Entities.WithAll<PlayerComponent>().ForEach((Entity e, int entityInQueryIndex,ref ShootingTimer shootingTimer) =>
                {
                    double playerDeltaTime = timeSinceInit - shootingTimer.timer;
                    if (playerDeltaTime > 0.2f)
                    {
                        ecb.AddComponent<NeedToShoot>(entityInQueryIndex, e);
                        shootingTimer.timer = (float) timeSinceInit;
                    }
                }).ScheduleParallel();
            } 
            //set dependency for ecb
            ecbSystem.AddJobHandleForProducer(Dependency);
        
        }
    }
    
    [UpdateAfter(typeof(AddRewardSystem))]
    public class AssignReward : SystemBase
    {
        protected override void OnUpdate()
        {
            //goes over all entities with a pilotAgent component and adds appropriate reward stored in rewardComponent.
            Entities.WithAll<HasTarget>().ForEach((Entity entity, PilotAgent pilotAgent, in RewardComponent rewardComponent) =>
            {
                pilotAgent.AssignReward(rewardComponent.reward);
            }).WithoutBurst().Run();
        }
    }
}
