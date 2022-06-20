using System.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NonECS
{
    public class MainMenu : MonoBehaviour
    {
        private string sceneToLoad;
        public GameObject optionsMenu;
        public GameObject mapMenu;
        public GameObject loadingScreen;
    
        public GameObject startButton;
        public GameObject quitButton;
        public GameObject mapMenuButton;
        public GameObject optionsMenuButton;

        private Button[] buttons;
        public Button[] Buttons => buttons;
    
        public GameObject difficultyWarning;
        public GameObject mapWarning;
        private bool canRun;
        
        private void Awake()
        {
            sceneToLoad = null;
            canRun = true;
            buttons = new[]
            {
                optionsMenuButton.GetComponent<Button>(),
                mapMenuButton.GetComponent<Button>(),
                startButton.GetComponent<Button>(),
                quitButton.GetComponent<Button>()
            };
        }
        //when enabled
        private void OnEnable()
        {
            if (mapWarning.activeSelf) mapWarning.SetActive(false);
            if(difficultyWarning.activeSelf) difficultyWarning.SetActive(false);
            canRun = true;
        }
        //sets scene to load
        public void SetSceneToLoad(string scene)
        {
            sceneToLoad = scene;
        }
        
        public void StartGame()
        {
            //loads scene and starts loading screen. handles warnings
            if (sceneToLoad != null && MapSelectMenu.Difficulty != null)
            {
                if (World.DefaultGameObjectInjectionWorld.QuitUpdate)
                    World.DefaultGameObjectInjectionWorld.QuitUpdate = false;
                
                loadingScreen.SetActive(true);
                StartCoroutine(WaitForLoad());
                
                AsyncOperation async = SceneManager.LoadSceneAsync(sceneToLoad);
                async.allowSceneActivation = true;
                if (async.isDone)
                {
                    SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneToLoad));
                    SceneManager.UnloadSceneAsync("MainMenu");
                }
                //pop up loading screen and load scene according to difficulty 
            }
            else if (sceneToLoad == null)
            {
                if(canRun) StartCoroutine(Fade(mapWarning));
            }
            else
            {
                if(canRun) StartCoroutine(Fade(difficultyWarning));
            }

        }
        //waits to turn off loading screen
        IEnumerator WaitForLoad()
        {
            yield return new WaitForSeconds(4f);
            if(loadingScreen.activeSelf) loadingScreen.SetActive(false);
        }
        //for warnings
        private IEnumerator Fade(GameObject obj)
        {
            canRun = false;
            obj.SetActive(true);
            yield return new WaitForSeconds(2f);
            obj.SetActive(false);
            canRun = true;
        }
        //opens map menu
        public void OpenMapMenu()
        {
            mapMenu.SetActive(true);
            gameObject.SetActive(false);
        }
        // opens options
        public void OpenOptions()
        {
            optionsMenu.SetActive(true);
            foreach (Button button in buttons)
            {
                button.interactable = false;
            }
        }
        // quits game
        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
