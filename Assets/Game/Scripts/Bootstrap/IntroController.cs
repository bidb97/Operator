using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace Operator.Bootstrap
{
    public class IntroController : MonoBehaviour
    {
        public static bool SuppressAutoRun { get; set; }

        [SerializeField] Image slideImage;
        [SerializeField] Image backdropImage;
        [SerializeField] Object sourceFolder;
        [SerializeField] Sprite[] slides;
        [SerializeField] string gameSceneName = "Game";

        [Header("Timing")]
        [SerializeField] float startInterval = 1.2f;
        [SerializeField] float minInterval = 0.08f;
        [SerializeField] float intervalDecay = 0.92f;
        [SerializeField] float minIntroDuration = 6f;
        [SerializeField] int minFullLoops = 2;

        [Header("Audio")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip slideChangeClip;
        [SerializeField] AudioClip slidesEndClip;

        float _interval;
        float _timer;
        float _introStartedAt;
        int _index;
        int _completedLoops;
        bool _running;

        void Awake()
        {
            DisableIntroEventSystem();

#if UNITY_EDITOR
            EnsureSlidesLoaded();
#endif
        }

        void Start()
        {
            if (SuppressAutoRun)
                return;

            if (!TryPrepareSlides())
            {
                SceneManager.LoadScene(gameSceneName);
                return;
            }

            StartCoroutine(RunIntroWithGameLoad());
        }

        public void BeginFromPreload()
        {
            if (_running)
                return;

            if (!TryPrepareSlides())
            {
                Debug.LogWarning($"{nameof(IntroController)}: нет слайдов, сразу в Game.", this);
                CompletePreloadedIntro();
                return;
            }

            StartCoroutine(RunSlidesAndEnterGame());
        }

        bool TryPrepareSlides()
        {
            if (slideImage == null || slides == null || slides.Length == 0)
            {
                Debug.LogError($"{nameof(IntroController)}: slideImage or slides not set.", this);
                return false;
            }

            EnsureBlackBackdrop();
            EnsureIntroCameraBlack();
            GameScenePresentation.SetEnabled(gameSceneName, false);

            slideImage.enabled = true;

            _running = true;
            _introStartedAt = Time.unscaledTime;
            _interval = startInterval;
            _index = 0;
            _timer = 0f;
            _completedLoops = 0;

            Canvas.ForceUpdateCanvases();
            ShowSlide(_index);
            return true;
        }

        IEnumerator RunSlidesAndEnterGame()
        {
            yield return RunSlides();
            yield return RunSlidesEndSequence();
            CompletePreloadedIntro();
            _running = false;
        }

        IEnumerator RunIntroWithGameLoad()
        {
            _running = true;

            var load = SceneManager.LoadSceneAsync(gameSceneName);
            load.allowSceneActivation = false;

            while (true)
            {
                yield return RunSlideFrame();

                var slidesDone = AreSlidesDone();
                var loadReady = load.progress >= 0.9f;

                if (slidesDone && loadReady)
                    break;
            }

            yield return RunSlidesEndSequence();

            load.allowSceneActivation = true;

            while (!load.isDone)
                yield return null;

            _running = false;
        }

        IEnumerator RunSlidesEndSequence()
        {
            if (audioSource == null || slidesEndClip == null)
                yield break;

            audioSource.PlayOneShot(slidesEndClip);

            yield return new WaitForSecondsRealtime(slidesEndClip.length);
        }

        IEnumerator RunSlides()
        {
            while (true)
            {
                yield return RunSlideFrame();

                if (AreSlidesDone())
                    break;
            }
        }

        IEnumerator RunSlideFrame()
        {
            _timer += Time.unscaledDeltaTime;

            if (_timer >= _interval)
            {
                _timer = 0f;
                _interval = Mathf.Max(minInterval, _interval * intervalDecay);

                if (_index == slides.Length - 1)
                    _completedLoops++;

                if (!AreSlidesDone())
                {
                    _index = (_index + 1) % slides.Length;
                    ShowSlide(_index);
                }
            }

            yield return null;
        }

        bool AreSlidesDone() =>
            _completedLoops >= minFullLoops
            && Time.unscaledTime - _introStartedAt >= minIntroDuration;

        void CompletePreloadedIntro()
        {
            Debug.Log("[IntroController] интро завершено, переход в Game");

            GameScenePresentation.SetEnabled(gameObject.scene, false);
            GameScenePresentation.SetEnabled(gameSceneName, true);
            NewGameLoadFlow.IntroFinished = true;
        }

        void DisableIntroEventSystem()
        {
            foreach (var eventSystem in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (eventSystem.gameObject.scene == gameObject.scene)
                    eventSystem.enabled = false;
            }
        }

        void EnsureBlackBackdrop()
        {
            if (backdropImage == null)
            {
                var canvas = slideImage.canvas;
                if (canvas == null)
                    return;

                var go = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                go.transform.SetAsFirstSibling();
                backdropImage = go.GetComponent<Image>();
            }

            backdropImage.color = Color.black;
            backdropImage.raycastTarget = false;

            var rect = backdropImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        void EnsureIntroCameraBlack()
        {
            foreach (var camera in GetComponentsInChildren<Camera>(true))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }
        }

        void ShowSlide(int index)
        {
            slideImage.sprite = slides[index];
            slideImage.preserveAspect = true;

            if (audioSource != null && slideChangeClip != null)
                audioSource.PlayOneShot(slideChangeClip);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (sourceFolder != null && (slides == null || slides.Length == 0))
                EnsureSlidesLoaded();
        }

        void EnsureSlidesLoaded()
        {
            if (sourceFolder == null || (slides != null && slides.Length > 0))
                return;

            var folderPath = AssetDatabase.GetAssetPath(sourceFolder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return;

            slides = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                .Where(sprite => sprite != null)
                .Distinct()
                .OrderBy(SpriteOrderKey)
                .ToArray();

            if (slides.Length > 0)
                EditorUtility.SetDirty(this);
        }

        static int SpriteOrderKey(Sprite sprite)
        {
            var name = sprite.name;
            var i = 0;
            while (i < name.Length && !char.IsDigit(name[i]))
                i++;

            if (i >= name.Length)
                return 0;

            var end = i;
            while (end < name.Length && char.IsDigit(name[end]))
                end++;

            return int.TryParse(name.Substring(i, end - i), out var index) ? index : 0;
        }
#endif
    }
}
