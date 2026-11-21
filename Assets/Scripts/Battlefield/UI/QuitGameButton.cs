using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    // Independent of faction, tutorial, wave, and resource state.
    [RequireComponent(typeof(Button))]
    public class QuitGameButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(1f)] private float confirmationSeconds = 5f;

        private Button button;
        private bool awaitingConfirmation;
        private float confirmUntil;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        }

        private void OnEnable()
        {
            ResetConfirmation();
            button.onClick.AddListener(RequestQuit);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(RequestQuit);
            ResetConfirmation();
        }

        private void Update()
        {
            if (awaitingConfirmation && Time.unscaledTime >= confirmUntil)
                ResetConfirmation();
        }

        private void RequestQuit()
        {
            if (awaitingConfirmation && Time.unscaledTime < confirmUntil)
            {
                ResetConfirmation();
                QuitApplication();
                return;
            }

            awaitingConfirmation = true;
            confirmUntil = Time.unscaledTime + confirmationSeconds;
            if (label != null) label.text = "Quit game?";
        }

        private void ResetConfirmation()
        {
            awaitingConfirmation = false;
            if (label != null) label.text = "Quit";
        }

        protected virtual void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
