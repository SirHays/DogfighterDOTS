
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Components;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using ReadOnly = Unity.Collections.ReadOnlyAttribute;
namespace Systems
{
    public class CollisionSystem : JobComponentSystem {

        [BurstCompile]
        private struct CollisionJob : ICollisionEventsJob
        {
            private EntityCommandBuffer ecb;
            [ReadOnly] private ComponentDataFromEntity<HitCounter> hitCounter;
            [ReadOnly] private ComponentDataFromEntity<ProjectileComponent> projectileComponents;
            [ReadOnly] private ComponentDataFromEntity<ObstacleComponent> obstacleComponents;
            [ReadOnly] private ComponentDataFromEntity<PlayerProjectile> playerProjectiles;
            public CollisionJob(EntityCommandBuffer ecb,
                ComponentDataFromEntity<HitCounter> hitCounter,
                ComponentDataFromEntity<ProjectileComponent> projectileComponents,
                ComponentDataFromEntity<ObstacleComponent> obstacleComponents,
                ComponentDataFromEntity<PlayerProjectile> playerProjectiles)
            {
                this.ecb = ecb;
                this.hitCounter = hitCounter;
                this.projectileComponents = projectileComponents;
                this.obstacleComponents = obstacleComponents;
                this.playerProjectiles = playerProjectiles;
            }
            public void Execute(CollisionEvent collisionEvent)
            {
                Entity e1 = collisionEvent.EntityA;
                Entity e2 = collisionEvent.EntityB;
                
                //returns entity that is not the obstacle provided an obstacle is present in the collision.
                Entity notObstacleEntity = Contains(obstacleComponents, e1, e2);
                if (notObstacleEntity != Entity.Null)
                {
                    //obstacle collision with a ship.
                    if (hitCounter.HasComponent(notObstacleEntity))
                    {
                        ObstacleShipCollision(notObstacleEntity);
                        return;
                    }

                    //obstacle collision with a projectile.
                    if (projectileComponents.HasComponent(notObstacleEntity))
                    {
                        ObstacleProjectileCollision(notObstacleEntity);
                        return;
                    }
                }

                //ship on ship collision.
                if (hitCounter.HasComponent(e1) && hitCounter.HasComponent(e2))
                {
                    ShipShipCollision(e1,e2);
                    return;
                }

                Entity notProjectileEntity = Contains(projectileComponents, e1, e2);
                if (notProjectileEntity != Entity.Null)
                {
                    //ship projectile collision.
                    if (hitCounter.HasComponent(notProjectileEntity))
                    {
                        ProjectileShipCollision(notProjectileEntity, projectileComponents.HasComponent(e1) ? e1 : e2);
                        return;
                    }

                    //projectile on projectile collision
                    if (projectileComponents.HasComponent(notProjectileEntity))
                    {
                        ecb.DestroyEntity(e1);
                        ecb.DestroyEntity(e2);
                    }
                }
            }
            private Entity Contains<T>(ComponentDataFromEntity<T> component, Entity e1, Entity e2) where T : struct, IComponentData
            {
                //returns entity that doesn't contain component.
                //if both of them don't contain the component, returns Entity.Null.
                if (component.HasComponent(e1)) return e2;
                if (component.HasComponent(e2)) return e1;
                return Entity.Null;
            }
            private void ProjectileShipCollision(Entity ship, Entity projectile)
            {
                //destroys projectile
                ecb.DestroyEntity(projectile);
                //hits suffered so far
                int hits = hitCounter[ship].counter;
                int hitsNeededToKill = hitCounter[ship].hitsToKill;
                //disable ship
                if (hits > 1)
                {
                    //if ship was killed by a projectile shot by a player
                    //set killedByPlayer to be true, else set to false.
                    ecb.AddComponent(ship, playerProjectiles.HasComponent(projectile) ? 
                        new DisabledTag {killedByPlayer = true} :
                        new DisabledTag());
                }
                //increment hits in counter component.
                else {ecb.SetComponent(ship,new HitCounter
                {
                    hitsToKill = hitsNeededToKill,
                    counter = hits+1
                });}
            }

            private void ShipShipCollision(Entity ship1, Entity ship2)
            {
                //disable the two ships.
                ecb.AddComponent<DisabledTag>(ship1);
                ecb.AddComponent<DisabledTag>(ship2);
            }
            
            private void ObstacleProjectileCollision(Entity projectile)
            {
                //destroy projectile
                ecb.DestroyEntity(projectile);
            }
            
            private void ObstacleShipCollision(Entity ship)
            {
                //disable ship
                ecb.AddComponent<DisabledTag>(ship);
            }
        }

        private BuildPhysicsWorld buildPhysicsWorldSystem;
        private StepPhysicsWorld stepPhysicsWorldSystem;
        private BeginSimulationEntityCommandBufferSystem beginSimulationEntityCommandBufferSystem;

        protected override void OnCreate() {
            buildPhysicsWorldSystem = World.GetExistingSystem<BuildPhysicsWorld>();
            stepPhysicsWorldSystem = World.GetExistingSystem<StepPhysicsWorld>();
            beginSimulationEntityCommandBufferSystem = World.GetExistingSystem<BeginSimulationEntityCommandBufferSystem>();
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            //declaration and scheduling of job with its fields.
            JobHandle jobHandle = new CollisionJob(
                beginSimulationEntityCommandBufferSystem.CreateCommandBuffer(),
                GetComponentDataFromEntity<HitCounter>(true),
                GetComponentDataFromEntity<ProjectileComponent>(true),
                GetComponentDataFromEntity<ObstacleComponent>(true),
                GetComponentDataFromEntity<PlayerProjectile>()
            ).Schedule(stepPhysicsWorldSystem.Simulation, ref buildPhysicsWorldSystem.PhysicsWorld, inputDeps);
            
            //set dependency for ecb
            beginSimulationEntityCommandBufferSystem.AddJobHandleForProducer(jobHandle);
            return jobHandle;    
        }
    }
    
}
