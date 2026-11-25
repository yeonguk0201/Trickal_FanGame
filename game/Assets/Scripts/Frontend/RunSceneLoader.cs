using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Frontend
{
    // A single persistent overlay spans both scenes. Network profile/save requests never hold scene activation.
    [DefaultExecutionOrder(32000), DisallowMultipleComponent]
    public sealed class RunSceneLoader : MonoBehaviour
    {
        public const float MinimumVisibleSeconds = 0.6f;
        public const float CompletedVisibleSeconds = 0.15f;
        private static RunSceneLoader instance;
        // Balance-1: a scene loaded ahead in the background (the result scene during GAME OVER), held before
        // activation. Unity queues every later async load behind a held one, so only the same scene may load next.
        private static AsyncOperation preload;
        private static int preloadIndex = -1;
        private RunLoadingScreenView view;
        private float previousTimeScale;
        private GameObject previousSelection;
        private Action failed;
        public static bool IsLoading => instance != null && instance.busy;
        public static RunSceneLoader Current => instance;
        public RunLoadingScreenView View => view;
        private bool busy;
        private EventSystem blockedEvents;
        private bool eventsWereEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            preload = null;
            preloadIndex = -1;
        }

        public static bool IsPreloading => preload != null;
        public static int PreloadedBuildIndex => preload != null ? preloadIndex : -1;
        // The held scene has finished loading and only waits for activation.
        public static bool IsPreloadReady => preload != null && preload.progress >= 0.9f;

        public static bool CanLoad(int buildIndex) => !IsLoading && buildIndex >= 0 &&
            buildIndex < SceneManager.sceneCountInBuildSettings &&
            (preload == null || preloadIndex == buildIndex);

        // Starts loading a scene in the background without showing anything or activating it. A later TryLoad of
        // the same scene activates it and skips the loading screen when it is ready. Returns whether that scene is
        // now preloading.
        public static bool Preload(int buildIndex)
        {
            if (preload != null) return preloadIndex == buildIndex;
            if (!Application.isPlaying || !CanLoad(buildIndex)) return false;
            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
                if (operation == null) return false;
                operation.allowSceneActivation = false;
                preload = operation;
                preloadIndex = buildIndex;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        public static bool TryLoad(int buildIndex, float completedTimeScale = 1f, Action onFailure = null)
        {
            if (!Application.isPlaying || !CanLoad(buildIndex)) return false;
            try
            {
                if (instance == null)
                {
                    GameObject root = new("Run Scene Loading");
                    instance = root.AddComponent<RunSceneLoader>();
                    DontDestroyOnLoad(root);
                }
                instance.EnsureView();
                instance.previousTimeScale = Time.timeScale;
                instance.previousSelection = EventSystem.current != null
                    ? EventSystem.current.currentSelectedGameObject : null;
                instance.failed = onFailure;
                instance.busy = true;
                AsyncOperation preloaded = preload;
                preload = null;
                preloadIndex = -1;
                // A finished preload switches straight over; the loading screen shows only while loading remains.
                bool quiet = preloaded != null && preloaded.progress >= 0.9f;
                instance.view.SetVisible(!quiet);
                instance.view.SetProgress(0f);
                Time.timeScale = 0f;
                instance.BlockEvents();
                instance.StartCoroutine(instance.Load(buildIndex, completedTimeScale, preloaded, quiet));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (instance != null && instance.busy) instance.Fail();
                return false;
            }
        }

        private void EnsureView()
        {
            if (view != null) return;
            RunLoadingScreenStyle style = Resources.Load<RunLoadingScreenStyle>(RunLoadingScreenStyle.ResourcePath);
            if (style == null) throw new InvalidOperationException("Missing RunLoadingScreen/Style resource.");
            style.ReadLayout();
            view = gameObject.AddComponent<RunLoadingScreenView>();
            view.Build(style);
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!busy) return;
            Time.timeScale = 0f;
            BlockEvents();
        }

        private void LateUpdate()
        {
            if (!busy) return;
            Time.timeScale = 0f;
            BlockEvents();
        }

        // Blocking the EventSystem also blocks keyboard/controller submit, unlike a raycast-only backdrop.
        private void BlockEvents()
        {
            EventSystem current = EventSystem.current;
            if (current == null || current == blockedEvents) return;
            RestoreEvents();
            blockedEvents = current;
            eventsWereEnabled = current.enabled;
            current.enabled = false;
        }

        private void RestoreEvents()
        {
            if (blockedEvents != null) blockedEvents.enabled = eventsWereEnabled;
            blockedEvents = null;
        }

        private IEnumerator Load(int buildIndex, float completedTimeScale, AsyncOperation preloaded, bool quiet)
        {
            AsyncOperation operation = preloaded;
            if (operation == null)
            {
                // Give the overlay a rendered frame before any heavy scene deserialization starts.
                yield return null;
                yield return null;
                Exception error = null;
                try { operation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single); }
                catch (Exception exception) { error = exception; }
                if (error != null || operation == null)
                {
                    Debug.LogError($"[RunSceneLoader] Scene load failed: {error?.Message ?? "no operation"}");
                    Fail();
                    yield break;
                }

                operation.allowSceneActivation = false;
            }

            float started = Time.realtimeSinceStartup;
            while (operation.progress < 0.9f ||
                   (!quiet && Time.realtimeSinceStartup - started < MinimumVisibleSeconds))
            {
                view.SetProgress(Mathf.Min(0.9f, operation.progress));
                yield return null;
            }
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;
            // Awake, OnEnable and Start all run behind the overlay (or the last frame) before it reaches 100%.
            yield return null;
            if (!quiet)
            {
                view.SetProgress(1f);
                float completed = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - completed < CompletedVisibleSeconds) yield return null;
            }
            failed = null;
            busy = false;
            view.SetVisible(false);
            RestoreEvents();
            Time.timeScale = completedTimeScale;
        }

        private void Fail()
        {
            Action callback = failed;
            failed = null;
            busy = false;
            view?.SetVisible(false);
            RestoreEvents();
            Time.timeScale = previousTimeScale;
            if (EventSystem.current != null && previousSelection != null)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            callback?.Invoke();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            if (busy)
            {
                RestoreEvents();
                Time.timeScale = previousTimeScale;
            }
            instance = null;
        }
    }
}
