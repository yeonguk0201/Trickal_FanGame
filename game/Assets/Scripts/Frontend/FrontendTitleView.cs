using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class FrontendTitleView : MonoBehaviour
    {
        public const string ReadyMessage = "닉네임 등록과 홈 화면은 준비 중입니다.";
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text statusText;
        public Button StartButton => startButton;
        public TMP_Text StatusText => statusText;

        public void Configure(Button button, TMP_Text status)
        {
            startButton = button;
            statusText = status;
        }

        private void OnEnable()
        {
            if (startButton != null) startButton.onClick.AddListener(RequestStart);
        }

        private void Start() => FocusStart();

        private void Update()
        {
            if (Keyboard.current?.tabKey.wasPressedThisFrame == true) FocusStart();
        }

        private void OnDisable()
        {
            if (startButton != null) startButton.onClick.RemoveListener(RequestStart);
        }

        public void FocusStart()
        {
            if (startButton != null && startButton.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        public void RequestStart()
        {
            // Profile registration and home routing are a later slice. Never start a Run here.
            if (statusText != null) statusText.text = ReadyMessage;
        }
    }
}
