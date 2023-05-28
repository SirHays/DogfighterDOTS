# DogfighterDOTS
 Dogfighting game made using Unity DOTS and Unity ML-Agents

Scripts located in Assets/Scripts


**If you have any questions or suggestions feel free to contact me via email as this was my first crack at Unity DOTS and I understand this code isn't exactly perfect.**
<br />
<br />
<br />
<br />

Initial game setup starts with converting the prefabs for the enemies, projectiles, and obstacles to entities at runtime using the ConvertAuthoring script, adding all the ML-agents components to the agent entities as Hybrid Components* and instantiating them using the SpawnEntitiesSystem.

<br />

Systems like RaycastObservationSystem and AddRewardSystem feed raycast info and reward feedback into the agent hybrid component and systems like SetRotationDataSystem take the decision output by the neural network
and feed it into the systems for moving and controlling the agent opponents.

<br />
<br />

Adding of the hybrid components in the Convert method of ConvertAuthoring that converts the agent gameobject into an entity and allows me to modify it using the provided EntityManager and GameObjectConversionSystem instances.

```C#
public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
{
  //add ML components to enemy entity
  conversionSystem.AddHybridComponent(GetComponent<PilotAgent>());
  conversionSystem.AddHybridComponent(GetComponent<DecisionRequester>());
  conversionSystem.AddHybridComponent(GetComponent<BehaviorParameters>());
  ...
}

```

<br />
<br />

Since ML-Agents' own RayPerceptionSensor class which previously handled raycast observations for the agent script sent normal raycasts that didn't collide with Entities and their provided Physics Shape and Physics Body components, I had to raycast using the Unity Physics package raycasts, normalize the values, and pass them into the agent script manually.

<br />
<br />

if statement from RaycastObservationSystem calculating the normalized distance outputed by the forward raycast and putting it in the observations component attached to every agent entity. 
```C#
if (Raycast(rayStart, fRayEnd, out var hit))
{
  observations.fDist = distance(hit.Position, pos) / visibleDistance;
}

```
Raycast method used above. Has to be run on the main thread.
```C#
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
```
ApplyObservationsSystem accessing the hybrid PilotAgent component and putting in the calculated observations.
```C#
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

```

The PilotAgent script takes in the observations and the rewards applied by the AssignPenalty and AssignReward systems and outputs rotation values for the agent spacecraft.

```C#
public override void OnActionReceived(ActionBuffers actions)
        {
            //sets pitch and roll values according to agent decision
            pitch = actions.ContinuousActions[0];
            roll = actions.ContinuousActions[1];

        }
```

SetRotationDataSystem grabs the pitch and roll values from the agent script and puts them into the EnemyRotationData component which is then used by the EnemyRotationSystem to rotate the entities by the appropriate values.

```C#
//runs for all enemies
            Entities.ForEach((Entity e, PilotAgent pilotAgent,ref EnemyRotationData data) =>
            {
                //sets rotation data to be the ML script's decision
                data.vertical = pilotAgent.Pitch;
                data.horizontal = pilotAgent.Roll;
            }).WithoutBurst().Run();

```

<br >

Positive reward is derived from the dot product of the agent ship's forward vector and the vector to their target found by the FindTargetSystem.

<br >
<br >

the below code from AddRewardSystem runs on all non-disabled agent entities that have a target.
```C#
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
        
        //if ang is large enough and enough time passed since last shot, shoot projectiles.
        if (ang > 0.8f && shootingTimer.timer > 0.2f)
        {
            ecb.AddComponent<NeedToShoot>(entityInQueryIndex, entity);
            shootingTimer.timer = 0f;
        }
}
```

the reward is then passed onto the PilotAgent script by the AssignReward system

```C#
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
```

When ships collide with an obstacle or projectile whether they are the main player or an agent they are awarded with a disabled tag and are handled by the respawn foreach function in SpawnEntitiesSystem.
This function gives the agents a penalty component which gets picked up by the AssignPenalty system and passes a negative reward into the PilotAgent script for that entity.

```C#
public class AssignPenalty : SystemBase
{
    protected override void OnUpdate()
    {
        //runs for enemies with below components
        Entities.ForEach((Entity entity, int entityInQueryIndex, ref PenaltyComponent penaltyComponent, in PilotAgent pilotAgent) =>
        {
            //penalty assigned to rewardcomponent
            float reward = penaltyComponent.penalty;
            
            if(reward == 0) return;
            //reset reward
            penaltyComponent.penalty = 0;
            //add reward to agent script
            pilotAgent.AssignReward(reward);
            //end episode
            if (penaltyComponent.endEpisode)
            {
                pilotAgent.EndEp();
                penaltyComponent.endEpisode = false;
            }
                
        }).WithoutBurst().Run();
    }

```


<br />



Other systems such as UpdateUISystem, SpawnProjectilesSystem, and EntityTrackerSystem deal with aspects that aren't related to the machine learning model and are just pure Unity DOTS so I chose to not expand on them here but feel free to peruse as they are all documented.

<br />

* Hybrid components which have been removed past Entities 0.17 create a companion gameobject for the entity and allows it to have components that can't be attached to an entity like the pilotagent script and all the other
 ML-Agents components such as the decision requester, Agent superclass, and the behaviour parameters script.

The use of hybrid components comes with a few drawbacks such the need for additional sync points in each frame in order to access the agent script and make use of its information.

Project uses paid assets:

https://assetstore.unity.com/packages/3d/vehicles/space/spacecraft-1-103179 <br />
https://assetstore.unity.com/packages/2d/gui/sci-fi-ui-pack-pro-149421

