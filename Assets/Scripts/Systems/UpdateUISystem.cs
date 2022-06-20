using Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Systems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class UpdateUISystem : SystemBase
    {
        //time since last update call used to limit update calls to every 2 seconds
        private float timePassed;
        
        protected override void OnUpdate()
        {
            timePassed += Time.DeltaTime;
            //doesnt run if timer is less than 2
            if(timePassed < 1f) return;
            //runs for the main player and references the ui
            Entities.ForEach((UIComponent ui, in PlayerComponent playerComponent,in HitCounter hitCounter,in Translation translation)=>
            {
                string shipHealth;
                //updates the ui text to the appropriate values
                ui.PointsData.text = "Score: " + playerComponent.score;
                
                if (hitCounter.counter == hitCounter.hitsToKill)
                {
                    shipHealth = "0%";
                }
                else
                {
                    shipHealth = hitCounter.hitsToKill == 2 ? hitCounter.counter > 0 ? "50" : "100%" :
                        hitCounter.counter == 0 ? "100%" :
                        hitCounter.counter == 1 ? "66%" : "33%";
                }

                ui.HealthData.text = "Ship Health: " + shipHealth;
                
            }).WithoutBurst().Run();
            //reset the timer
            timePassed = 0f;
        }
    }
}