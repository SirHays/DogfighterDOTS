using System.Collections;
using Components;
using Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NonECS
{
    public class PauseMenu : MonoBehaviour
    {
        private bool gamePaused;
        public GameObject pauseMenu;
        public GameObject loadingScreen;
        public GameObject WinScreen;
        public GameObject LoseScreen;
        private EntityManager entityManager;

        public GameObject explosion;
        
        private void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        private IEnumerator CleanExplosion(GameObject obj)
        {
            yield return new WaitForSeconds(2f);
            Destroy(obj);
        }
        
        
        //checks for pause and handles outcome.
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (gamePaused) Resume();
                else Pause();
            }

            if (Time.frameCount % 3 == 0)
            {
                var explosionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ExplosionComponent>());
                int length = explosionQuery.CalculateEntityCount();
                if (length > 0)
                {
                    var dueForExplosionArray = explosionQuery.ToEntityArray(Allocator.Temp);
                    var posArray = explosionQuery.ToComponentDataArray<ExplosionComponent>(Allocator.Temp);

                    for (int i = 0; i < length; i++)
                    {
                        GameObject exp = Instantiate(explosion, posArray[i].pos, Quaternion.identity);
                        StartCoroutine(CleanExplosion(exp));
                        entityManager.RemoveComponent<ExplosionComponent>(dueForExplosionArray[i]);
                    }

                    dueForExplosionArray.Dispose();
                    posArray.Dispose();
                }
                
                

                EntityQuery outcomeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<OutcomeComponent>());
                if(outcomeQuery.CalculateEntityCount() > 0)
                {
                    NativeArray<OutcomeComponent> outcomeArray =
                    outcomeQuery.ToComponentDataArray<OutcomeComponent>(Allocator.Temp);
                
                
                    var outcome = outcomeArray[0];
                    if (outcome.win)
                    {
                        WinScreen.SetActive(true);
                        entityManager.World.QuitUpdate = true;
                        gamePaused = true;
                    }
                    else
                    {
                        LoseScreen.SetActive(true);
                        entityManager.World.QuitUpdate = true;
                        gamePaused = true;
                    }
                }
            }
        }
        //pauses game
        public void Pause()
        {
            pauseMenu.SetActive(true);
            entityManager.World.QuitUpdate = true;
            gamePaused = true;
        }
        //resumes game
        public void Resume()
        {
            pauseMenu.SetActive(false);
            entityManager.World.QuitUpdate = false;
            gamePaused = false;
        }
        //restarts game
        public void Restart()
        {
            loadingScreen.SetActive(true);
            entityManager.World.QuitUpdate = false;

            entityManager.CompleteAllJobs();
            entityManager.DestroyEntity(entityManager.UniversalQuery);
            Scene scene = SceneManager.GetActiveScene();
            AsyncOperation async = SceneManager.LoadSceneAsync(scene.name);
            async.allowSceneActivation = true;

            if (async.isDone)
            {
                loadingScreen.SetActive(false);
            }
        }

        //quits game
        public void QuitGame()
        {
            
            entityManager.CompleteAllJobs();
            entityManager.DestroyEntity(entityManager.UniversalQuery);
            loadingScreen.SetActive(true);
            
            Scene activeScene = SceneManager.GetActiveScene();
            AsyncOperation async = SceneManager.LoadSceneAsync("MainMenu");
            async.allowSceneActivation = true;
            
            if (async.isDone)
            {
                SceneManager.SetActiveScene(SceneManager.GetSceneByName("MainMenu"));
                SceneManager.UnloadSceneAsync(activeScene);
                loadingScreen.SetActive(false);
            }
        }
    
        
    }
}
