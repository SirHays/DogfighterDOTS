using System.Collections.Generic;
using Components;
using NonECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.Transforms;
using UnityEngine;

namespace Systems
{
    [UpdateAfter(typeof(GameObjectConversionGroup))]
    public class ConvertAuthoring : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        private Entity spawnerEntity;
        private Entity playerEntity;
        public GameObject projectileGameObject;
        public GameObject playerProjectileGameObject;
        public GameObject[] obstaclePrefabs;
        private bool difficulty;
        
        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            //declare prefabs that need to be converted.
            referencedPrefabs.Add(projectileGameObject);
            referencedPrefabs.Add(playerProjectileGameObject);
            foreach (var p in obstaclePrefabs)
            {
                referencedPrefabs.Add(p);
            }
        }
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            //if difficulty is null, set it to false, and if it isn't null leave it.
            difficulty = MapSelectMenu.Difficulty ?? default(bool);
            
            //get helper entity.
            var spawnerEntityArray = dstManager.CreateEntityQuery(typeof(EntitySpawnData)).ToEntityArray(Allocator.Temp);
            spawnerEntity = spawnerEntityArray[0];
            spawnerEntityArray.Dispose();

            
            //add ML components to enemy entity
            conversionSystem.AddHybridComponent(GetComponent<PilotAgent>());
            conversionSystem.AddHybridComponent(GetComponent<DecisionRequester>());
            conversionSystem.AddHybridComponent(GetComponent<BehaviorParameters>());
            
            //add needed components to enemy entity.
            dstManager.AddComponent<EnemyRotationData>(entity);
            dstManager.AddComponent<ShootingTimer>(entity);
            dstManager.AddComponent<HitCounter>(entity);
            dstManager.SetComponentData(entity, new HitCounter
            {
                hitsToKill = difficulty ? 2 : 1
            });
            dstManager.AddComponent<ObservationsComponent>(entity);
            dstManager.AddComponent<RewardComponent>(entity);
            
            #region ConvertProjectile
            //get converted projectile entity
            Entity projectileEntity = conversionSystem.GetPrimaryEntity(projectileGameObject);
            
            //add projectile components.    
            dstManager.AddComponent<ProjectileComponent>(projectileEntity);
            dstManager.AddComponent<ShootingTimer>(projectileEntity);
            
            //get converted player projectile entity.
            Entity playerProjectileEntity = conversionSystem.GetPrimaryEntity(playerProjectileGameObject);
            
            //add player projectile components.    
            dstManager.AddComponent<ProjectileComponent>(playerProjectileEntity);
            dstManager.AddComponent<ShootingTimer>(playerProjectileEntity);
            dstManager.AddComponent<PlayerProjectile>(playerProjectileEntity);
            
            #endregion
            
            #region ConvertObstacles
            //add the buffer containing the converted prefabs to the helper entity.
            DynamicBuffer<PrefabSpawnerBufferElement> buffer = dstManager.AddBuffer<PrefabSpawnerBufferElement>(spawnerEntity);
            //used to play back changes at the end of for loop since addComponent calls invalidate all buffers.
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            //adding each converted prefab to the buffer.
            foreach (var t in obstaclePrefabs)
            {
                Entity e = conversionSystem.GetPrimaryEntity(t);
                //this call invalidates the buffer. replace with ecb.addcomponent.
                ecb.AddComponent<ObstacleComponent>(e);
                buffer.Add(new PrefabSpawnerBufferElement{Prefab = e});
            }
            //playing back the changes.
            ecb.Playback(dstManager);
            //disposing of ecb to prevent a leak.
            ecb.Dispose();
            #endregion
            //adding the entitySpawnData component to the helper entity containing the entities for instantiation.
            dstManager.SetComponentData(spawnerEntity,new EntitySpawnData
            {
                shipEntity = entity,
                projectileEntity = projectileEntity,
                playerProjectileEntity = playerProjectileEntity,
            });
        }
    }
}
