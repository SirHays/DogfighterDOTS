using Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;

namespace Systems
{
    public class EntityTrackerSystem : MonoBehaviour {}
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(CopyTransformToGameObjectSystem))]
    public class SynchronizeGameObjectTransformsWithEntities : SystemBase
    {
        //main camera
        EntityQuery cameraQuery;
        //player
        private EntityQuery playerQuery;

        protected override void OnStartRunning()
        {
            cameraQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    typeof(EntityTrackerSystem),
                    typeof(Transform),
                }
            });

            playerQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    typeof(PlayerComponent),
                    typeof(LocalToWorld)
                }
            });
        }

        protected override void OnUpdate()
        {
            //if player doesn't exist return
            if(playerQuery.CalculateEntityCount() == 0) return;
            //get localToWorld of player
            var localToWorlds = playerQuery.ToComponentDataArrayAsync<LocalToWorld>(Allocator.TempJob, out var jobHandle);
            //declare and schedule job.
            Dependency = new SyncTransforms
            {
                LocalToWorlds = localToWorlds,
                offset = new float3(0,5,-25),
                velocity = new Vector3(0,0,0),
                dt = Time.DeltaTime
            }.Schedule(cameraQuery.GetTransformAccessArray(), JobHandle.CombineDependencies(Dependency, jobHandle));
        }

        [BurstCompile]
        struct SyncTransforms : IJobParallelForTransform
        {
            [DeallocateOnJobCompletion]
            [ReadOnly] public NativeArray<LocalToWorld> LocalToWorlds;
            [ReadOnly] public float3 offset;
            public Vector3 velocity;
            public float dt;
            public void Execute(int index, TransformAccess transform)
            {
                var entityLocalToWorld = LocalToWorlds[0];
                
                //transforms pos + offset to world space. same as doing Transform.TransformPoint()
                //transform.position = ((Matrix4x4) entityLocalToWorld.Value).MultiplyPoint3x4(offset);
                
                //smooth camera position to be the player position + offset
                transform.position = Vector3.SmoothDamp(transform.position,
                    math.transform(entityLocalToWorld.Value, offset), ref velocity, 0.01f,Mathf.Infinity,dt);
                
                //set the camera rotation to be aligned with the player forward vector
                transform.rotation = quaternion.LookRotationSafe(entityLocalToWorld.Forward,entityLocalToWorld.Up);
            }
        }
    }
}