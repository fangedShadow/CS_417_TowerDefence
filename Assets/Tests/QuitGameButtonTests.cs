#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TMPro;
using TowerDefence.Battlefield;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TowerDefence.Tests
{
    public sealed class RecordingQuitGameButton : QuitGameButton
    {
        public int QuitRequests;
        protected override void QuitApplication() => QuitRequests++;
    }

    public sealed class QuitGameButtonTests
    {
        private GameObject root;
        private Button button;
        private TMP_Text label;
        private RecordingQuitGameButton quit;
        private float previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            root = new GameObject("Quit", typeof(RectTransform), typeof(Button));
            root.SetActive(false);
            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(root.transform, false);
            label = text.GetComponent<TMP_Text>();
            button = root.GetComponent<Button>();
            quit = root.AddComponent<RecordingQuitGameButton>();
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = previousTimeScale;
            Object.DestroyImmediate(root);
        }

        [Test]
        public void OnlySecondPressRequestsQuit()
        {
            button.onClick.Invoke();
            Assert.That(quit.QuitRequests, Is.Zero);
            Assert.That(label.text, Is.EqualTo("Quit game?"));
            button.onClick.Invoke();
            Assert.That(quit.QuitRequests, Is.EqualTo(1));
            Assert.That(label.text, Is.EqualTo("Quit"));
        }

        [Test]
        public void ClosingAndReopeningPanelCancelsConfirmationWithoutDuplicatingListeners()
        {
            button.onClick.Invoke();
            root.SetActive(false);
            root.SetActive(true);
            Assert.That(label.text, Is.EqualTo("Quit"));
            button.onClick.Invoke();
            Assert.That(quit.QuitRequests, Is.Zero);
            button.onClick.Invoke();
            Assert.That(quit.QuitRequests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ConfirmationExpiresEvenWhenGameTimeIsPaused()
        {
            Time.timeScale = 0;
            button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(5.1f);
            Assert.That(label.text, Is.EqualTo("Quit"));
            button.onClick.Invoke();
            Assert.That(quit.QuitRequests, Is.Zero);
        }

        [Test]
        public void SavedPanelContainsWiredQuitButtonWithoutReplacingHelp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Shared/UI/GamePanel.prefab");
            var savedQuit = prefab.transform.Find("PanelContent/Quit");
            Assert.That(savedQuit, Is.Not.Null);
            Assert.That(savedQuit.GetComponent<QuitGameButton>(), Is.Not.Null);
            var serialized = new SerializedObject(savedQuit.GetComponent<QuitGameButton>());
            Assert.That(serialized.FindProperty("label").objectReferenceValue, Is.Not.Null);
            Assert.That(prefab.GetComponent<GamePanelController>().TutorialHelp,
                Is.EqualTo(prefab.transform.Find("PanelContent/Help").GetComponent<Button>()));
        }
    }
}
#endif
