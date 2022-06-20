using System;
using System.Collections;
using Components;
using Systems;
using Unity.Entities;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SetRotationDataSystem))]
public class PilotAgent : Agent
{   
    
    public float fDist { get; set; } = -1f;
    public float lDist { get; set; } = -1f;
    public float rDist { get; set; } = -1f;
    public float frDist { get; set; } = -1f;
    public float flDist { get; set; } = -1f;

        private float pitch;
        private float roll;

        public float Pitch => pitch;
        public float Roll => roll;
        
        
        public void AssignReward(float reward)
        {
            //Debug.Log("Reward: " + reward);
            AddReward(reward);
        }

        public void EndEp()
        {
            //Debug.Log("Ending Episode");
            EndEpisode();
        }
        /// <summary>
        /// runs when an episode starts
        /// </summary>
        public override void OnEpisodeBegin()
        {
        }
        
        /// <summary>
        /// adds the observations to the brain
        /// </summary>
        /// <param name="sensor"></param>
        public override void CollectObservations(VectorSensor sensor)
        {
            //1
            //same as fdist != -1f
            //fixed to be precise when comparing floating point values.
            if (fDist > -1f)
            {
                
                sensor.AddObservation(fDist);
                sensor.AddObservation(1f);
            }
            else
            {
                sensor.AddObservation(1f);
                sensor.AddObservation(0f);
            }
            //2
            if (rDist > -1f)
            {
                sensor.AddObservation(rDist);
                sensor.AddObservation(1f);
            }
            else
            {
                sensor.AddObservation(1f);
                sensor.AddObservation(0f);
            }

            //3
            if (lDist > -1f)
            {
                sensor.AddObservation(lDist);
                sensor.AddObservation(1f);
            }
            else
            {
                sensor.AddObservation(1f);
                sensor.AddObservation(0f);
            }

            //4
            if (frDist > -1f)
            {
                sensor.AddObservation(frDist);
                sensor.AddObservation(1f);
            }
            else
            {
                sensor.AddObservation(1f);
                sensor.AddObservation(0f);
            }

            //5
            if (flDist > -1f)
            {
                sensor.AddObservation(flDist);
                sensor.AddObservation(1f);
            }
            else
            {
                sensor.AddObservation(1f);
                sensor.AddObservation(0f);
            }

        }

        /// <summary>
        /// runs when the agent makes a decision
        /// </summary>
        /// <param name="actions"></param>
        public override void OnActionReceived(ActionBuffers actions)
        {
            //sets pitch and roll values according to agent decision
            pitch = actions.ContinuousActions[0];
            roll = actions.ContinuousActions[1];

        }
        //disabled to not interfere with agent movement while moving the player.
        // public override void Heuristic(in ActionBuffers actionsOut)
        // {
        //     ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        //     continuousActions[0] = Input.GetAxisRaw("Vertical");
        //     continuousActions[1] = Input.GetAxisRaw("Horizontal");
        // }

    
}

namespace Components
{
    public struct PenaltyComponent : IComponentData
    {
        public float penalty;
        public bool endEpisode;
    }
}
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
    }

