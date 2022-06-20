using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Components;
using Unity.Physics;

namespace Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class EnemyRotationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            //movement parameters.
            float pitchPower = 25f;
            float rollPower = 40f;
            float enginePower = 55f;
            
            //time since last update call
            float deltaTime = Time.DeltaTime;
            
            //goes over all entities with below components
            Entities.WithAll<Unit>().ForEach((Entity e, ref Rotation rotation,ref Translation translation,ref PhysicsVelocity physicsVelocity,in LocalToWorld ltw, in EnemyRotationData enemyRotationData) =>
            {
                physicsVelocity.Linear = new float3(0, 0, 0);
                physicsVelocity.Angular = new float3(0, 0, 0);
                
                //current rotation data
                float activePitch = enemyRotationData.vertical * pitchPower * deltaTime;
                float activeRoll = enemyRotationData.horizontal * rollPower * deltaTime;
                //rotation to reach
                quaternion targetRotation = Quaternion.Euler(activePitch * pitchPower*deltaTime,0,-activeRoll* rollPower * deltaTime);
                //sets the rotation to the result of transforming the quaternion b by the quaternion a.
                rotation.Value = math.mul(rotation.Value, targetRotation);
                //sets the position to be the engine power multiplied by the forward vector of the ship and the deltaTime for smoothing
                translation.Value += ltw.Forward * enginePower * deltaTime;
            }).ScheduleParallel();
            
            //movement parameters
            float rollSpeed = 200f;
            float lookRateSpeed = 80f;
            float playerEnginePower = 40f;
            //inputs
            float2 screenCenter = new float2(Screen.width * .5f, Screen.height * .5f);
            float2 lookInput = new float2(Input.mousePosition.x, Input.mousePosition.y);
            
            //runs for the player entity
            Entities.WithAll<PlayerComponent>().ForEach(
                (Entity e, ref Translation translation, ref Rotation rotation, ref PhysicsVelocity physicsVelocity, in LocalToWorld ltw, in EnemyRotationData rotationData) =>
                {
                    
                    physicsVelocity.Linear = new float3(0, 0, 0);
                    physicsVelocity.Angular = new float3(0, 0, 0);
                    
                    //distance from the screen center
                    float2 mouseDistance;
                    mouseDistance.x = (lookInput.x-screenCenter.x) / screenCenter.y;
                    mouseDistance.y = (lookInput.y-screenCenter.y) / screenCenter.y;
                    
                    //caps the magnitude of the distance vector in order to limit the speed of rotation 
                    //if the player takes the mouse off screen.
                    mouseDistance =  Vector2.ClampMagnitude(mouseDistance, 2f);
                    
                    //input for the horizontal axis.
                     var rollInput = rotationData.horizontal;
                    
                     //target rotation
                    quaternion targetRotation = Quaternion.Euler(-mouseDistance.y * lookRateSpeed * deltaTime,
                        mouseDistance.x * lookRateSpeed * deltaTime,
                        rollInput* rollSpeed * deltaTime);
                    
                    //raw value of vertical axis.
                    var vertical = rotationData.vertical;
                    //speed of ship moving forwards
                    var activeForwardSpeed = vertical<0f ? 0f : vertical * playerEnginePower;
                    //sets the rotation to the result of transforming the quaternion b by the quaternion a.
                    rotation.Value = math.mul(rotation.Value, targetRotation);
                        //sets the position to be the activeForwardSpeed multiplied by the forward vector of the ship and the deltaTime for smoothing
                    translation.Value += ltw.Forward * activeForwardSpeed * deltaTime;
                }).ScheduleParallel();
        }
    }
}