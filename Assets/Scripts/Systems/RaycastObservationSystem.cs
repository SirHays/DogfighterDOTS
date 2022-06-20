using Components;
using Enums;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using static Unity.Mathematics.math;

namespace Systems
{
    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SpawnEntitiesSystem))]
    public class RaycastObservationSystem : SystemBase
    {
        //physics world
        private BuildPhysicsWorld buildPhysicsWorld;
        
        protected override void OnCreate()
        {
            buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        }

        protected override void OnUpdate()
        {
            //detects collisions
            var collisionWorld = buildPhysicsWorld.PhysicsWorld.CollisionWorld;
            //length of raycast
            float visibleDistance = 100f;
            //distance to the sides to create a 45 degree raycast with a length of 100
            float angleDistance = tan(radians(45)) * visibleDistance;
            //runs for all enemies
            Entities.WithReadOnly(collisionWorld).WithAll<Unit>().ForEach((Entity e, int entityInQueryIndex,ref ObservationsComponent observations, in Translation translation, in LocalToWorld ltw ) =>
            {
                //current entity position
                var pos = translation.Value;
                //raycast start
                var rayStart = pos;
                
                //raycast endings for all directions
                var fRayEnd = pos + new float3(0, 0, visibleDistance);
                var lRayEnd = pos + new float3(visibleDistance, 0, 0);
                var rRayEnd = pos + new float3(-visibleDistance, 0, 0);
                var lfRayEnd = pos + new float3(angleDistance, 0, visibleDistance);
                var rfRayEnd = pos + new float3(-angleDistance, 0, visibleDistance);
                
                //sets observations according to the hit values of the raycasts
                #region SetObservations
                
                if (Raycast(rayStart, fRayEnd, out var hit))
                {
                    observations.fDist = distance(hit.Position, pos) / visibleDistance;
                }
                else { observations.fDist = -1f; }
                
                if (Raycast(rayStart, lRayEnd, out hit))
                {
                    observations.lDist = distance(hit.Position, pos) / visibleDistance;
                }
                else { observations.lDist = -1f; }
                
                if (Raycast(rayStart, rRayEnd, out hit))
                {
                    observations.rDist = distance(hit.Position, pos) / visibleDistance;
                }
                else { observations.rDist = -1f; }
                
                if (Raycast(rayStart, lfRayEnd, out hit))
                {
                    observations.flDist = distance(hit.Position, pos) / visibleDistance;
                }
                else { observations.flDist = -1f; }
                
                if (Raycast(rayStart, rfRayEnd, out hit))
                {
                    observations.frDist = distance(hit.Position, pos) / visibleDistance;
                }
                else { observations.frDist = -1f; }
                #endregion
            }).Run();
            
            //function for initializing an ECS raycast.
             bool Raycast(float3 rayStart, float3 rayEnd, out RaycastHit raycastHit)
            {
                var rayCastInput = new RaycastInput
                {
                    //set start and end of raycast
                    Start = rayStart,
                    End = rayEnd,
                    //set collision filter detailing what the ray collides with.
                    Filter = new CollisionFilter
                    {
                        BelongsTo = (uint) CollisionLayers.Raycast,
                        CollidesWith = (uint) (CollisionLayers.Enemy | CollisionLayers.Obstacle | CollisionLayers.Wall |
                                               CollisionLayers.Player)
                    }
                };
                //casts the ray
                return collisionWorld.CastRay(rayCastInput, out raycastHit);
            }
        }
    }
    
    
    
     [UpdateInGroup(typeof(InitializationSystemGroup))]
     [UpdateAfter(typeof(RaycastObservationSystem))]
     public class ApplyObservations : SystemBase
     {
         protected override void OnUpdate()
         {
             //runs on all enemies and applies their observations to the ML script.
             Entities.ForEach((Entity e, int entityInQueryIndex, PilotAgent pilotAgent, ref ObservationsComponent observations) =>
             {
                 pilotAgent.fDist = observations.fDist;
                 pilotAgent.lDist = observations.lDist;
                 pilotAgent.rDist = observations.rDist;
                 pilotAgent.frDist = observations.frDist;
                 pilotAgent.flDist = observations.flDist;
                 
             }).WithoutBurst().Run();
         }
    }
}