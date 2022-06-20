using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NonECS
{
    public class OptionsMenu : MonoBehaviour
    {
        public MainMenu mainMenu;
        private Button[] buttons;
        private bool activate;

        private Scene currentScene;
        private void Awake()
        {
        
            currentScene  = SceneManager.GetActiveScene();
            //set vars
            if (currentScene.name.Equals("MainMenu"))
            {
                buttons = mainMenu.Buttons;
                activate = true;
            }
        }

        public void CloseMenu()
        {
            //close menu
            gameObject.SetActive(false);
            //activate buttons in background
            if(activate)
            {
                foreach (var button in buttons)
                {
                    button.interactable = true;
                }
            }
        }
    
    }
}
