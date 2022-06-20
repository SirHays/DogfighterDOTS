using Components;
using NonECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = Unity.Mathematics.Random;


namespace Components
{
    //contains obstacle prefabs
    [InternalBufferCapacity(3)]
    public struct PrefabSpawnerBufferElement : IBufferElementData
    {
        public Entity Prefab;
    }

    public struct ExplosionComponent : IComponentData
    {
        //position to instantiate explosion
        public float3 pos;
    }
}

namespace Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(SetRotationDataSystem))]
    public class SpawnEntitiesSystem : SystemBase
    {
        //used for playing back changes
        private EndInitializationEntityCommandBufferSystem endInitializationEntityCommandBufferSystem;
        //player
        private EntityQuery playerQuery;
        
        protected override void OnCreate()
        {
            endInitializationEntityCommandBufferSystem =
                World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();

        }
        protected override void OnStartRunning()
        {
            //used for random value generation
            var random = new Random((uint)new System.Random().Next());
            playerQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerComponent>());
            
            var ecb = endInitializationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            //instantiation parameters
            var entityCount = 50;
            var obstacleCount = 100;
            bool difficulty = MapSelectMenu.Difficulty ?? default(bool);
            
            var playerArray = playerQuery.ToEntityArray(Allocator.Temp);
            var playerEntity = playerArray[0];
            playerArray.Dispose();

            bool loadObstacles = SceneManager.GetActiveScene().Equals(SceneManager.GetSceneByName("DistantPlanet"));
            
            //runs once for the helper entity
            Entities.ForEach((Entity e, int entityInQueryIndex, ref DynamicBuffer<PrefabSpawnerBufferElement> buffer,in EntitySpawnData entitySpawnData) =>
            {
                
                ecb.SetComponent(entityInQueryIndex,playerEntity, new PlayerComponent
                {
                    pointsNeededToWin = difficulty ? 20 : 10
                });
                ecb.SetComponent(entityInQueryIndex,playerEntity, new HitCounter
                {
                    hitsToKill = difficulty ? 3 : 2
                });
                
                //ship entity pulled from the data component
                var shipEntity = entitySpawnData.shipEntity;
                
                //spawning the enemies
                for (int i = 0; i < entityCount; i++)
                {
                    Entity ship = ecb.Instantiate(entityInQueryIndex, shipEntity);
                    ecb.SetComponent(entityInQueryIndex,ship,new Translation {Value = random.NextFloat3(-400,400)});
                    ecb.SetComponent(entityInQueryIndex,ship, new Rotation {Value = random.NextQuaternionRotation()});
                }

                if (loadObstacles)
                {
                    //spawning the obstacles
                    for (int i = 0; i < obstacleCount; i++)
                    {
                        Entity prefab = buffer.ElementAt(random.NextInt(0,2)).Prefab;
                        Entity obs = ecb.Instantiate(entityInQueryIndex, prefab);
                        ecb.SetComponent(entityInQueryIndex, obs, new Translation {Value = random.NextFloat3(-750, 750)});
                        ecb.SetComponent(entityInQueryIndex, obs, new Rotation {Value = random.NextQuaternionRotation()});
                    }
                }
            }).ScheduleParallel();
            
            //set dependency for ecb
            endInitializationEntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }

        protected override void OnUpdate()
        {
            //used for random value generation
            var random = new Random((uint)new System.Random().Next());
            
            var ecb = endInitializationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            //playercomponent of the player entity
            var playerArray = playerQuery.ToEntityArray(Allocator.Temp);
            Entity player = playerArray[0];
            playerArray.Dispose();
            //all playercomponents
            var playerComponents = GetComponentDataFromEntity<PlayerComponent>(true);
            bool difficulty = MapSelectMenu.Difficulty ?? default(bool);
            int maxPoints = difficulty ? 20 : 10;
            
            //runs for entities with the disabled tag.
            Entities
                .WithReadOnly(playerComponents)
                .ForEach((Entity e, int entityInQueryIndex, ref Translation translation, in DisabledTag disabledTag) =>
                {
                    //check if entity is the main player
                    if (!playerComponents.HasComponent(e))
                    {
                        //add penalty
                        ecb.AddComponent(entityInQueryIndex, e, new PenaltyComponent
                        {
                            penalty = -3f,
                            endEpisode = true
                        });
                    }
                    else
                    {
                        ecb.AddComponent(entityInQueryIndex,e,new OutcomeComponent
                        {
                            win = false
                        });
                    }

                    var playerComponent = playerComponents[player];
                    //add score if entity was killed by the main player
                    if (disabledTag.killedByPlayer)
                    {
                        int score = playerComponent.score;
                        ecb.SetComponent(entityInQueryIndex,player,new PlayerComponent
                        {
                            score = score + 1
                        });
                    }
                    
                    if (playerComponent.score >= maxPoints)
                    {
                        ecb.AddComponent(entityInQueryIndex,e,new OutcomeComponent
                        {
                            win = true
                        });
                    }
                    
                    //enable entity
                    ecb.RemoveComponent<DisabledTag>(entityInQueryIndex,e);
                    ecb.AddComponent(entityInQueryIndex, e,new ExplosionComponent
                    {
                        pos = translation.Value
                    });
                    //randomize its position and rotation
                    ecb.SetComponent(entityInQueryIndex,e,new Translation
                    {
                        Value = random.NextFloat3(-400,400)
                    });
                    ecb.SetComponent(entityInQueryIndex,e,new Rotation
                    {
                        Value = random.NextQuaternionRotation()
                    });
                    
                    //set its velocities to zero.
                    ecb.SetComponent(entityInQueryIndex,e,new PhysicsVelocity
                    {
                        Linear = new float3(0,0,0),
                        Angular = new float3(0, 0, 0)
                    });
                }).ScheduleParallel();
            
            //set dependency for ecb
            endInitializationEntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}