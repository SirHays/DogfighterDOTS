using System;
using UnityEngine;
using UnityEngine.UI;

namespace NonECS
{
    [RequireComponent(typeof(Button))]
    public class ToggleSpriteSwap: MonoBehaviour
    {
        [SerializeField] private Image targetImage;

        [SerializeField] private Sprite onSprite;
        [SerializeField] private Sprite offSprite;
        [SerializeField] private bool isOn;

        public bool IsOn { 
            get => isOn;
            set { isOn = value; UpdateValue(); }
        }
    
        public event Action<bool> onValueChanged;

        private Button button;

        // to set initial value and skip onValueChanged notification
        public void Initialize(bool value)
        {
            isOn = value;
            //dont call subscriber methods since this is for init
            UpdateValue(false);
        }

        // Use this for initialization
        void Start ()
        {
            button = GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(OnClick);
        }
        //when clicked
        void OnClick()
        {
            isOn = !isOn;
            UpdateValue();
        }
        //when value is updated
        //sets true as default value of notifySubscribers when it is not specified on method call
        //can be overridden with value on method call
        private void UpdateValue(bool notifySubscribers = true)
        {
            //call
            if(notifySubscribers && onValueChanged != null)
                onValueChanged(isOn);

            if (targetImage == null)
                return;

            targetImage.sprite = isOn ? onSprite : offSprite;
        }
    }
}