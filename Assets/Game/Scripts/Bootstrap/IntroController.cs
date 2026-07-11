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
        [SerializeField] float startInterval = 0.7f;
        [SerializeField] float minInterval = 0.08f;
        [SerializeField] float intervalDecay = 0.9f;
        [SerializeField] float minIntroDuration = 6f;
        [SerializeField] int minFullLoops = 3;

        [Header("End")]
        [SerializeField] float blackHoldDuration = 1f;
        [SerializeField] AudioClip wakeAlarmClip;

        [Header("Scale")]
        [SerializeField] float startScale = 0.05f;
        [SerializeField] float endOverflow = 1.4f;
        [SerializeField] float scaleCurve = 1f;

        [Header("Audio")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip slideChangeClip;
        [SerializeField] AudioClip slidesEndClip;

        float _interval;
        float _timer;
        float _introStartedAt;
        int _index;
        int _completedLoops;
        int _slidesShown;
        bool _running;
        float _resolvedEndScale;
        bool _wakeAlarmStarted;

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
            EnsureIntroCanvas();
            EnsureIntroCameraBlack();
            GameScenePresentation.SetEnabled(gameObject.scene, true);
            GameScenePresentation.SetEnabled(gameSceneName, false);

            slideImage.enabled = false;

            _running = true;
            _introStartedAt = Time.unscaledTime;
            _interval = startInterval;
            _index = 0;
            _timer = 0f;
            _completedLoops = 0;
            _slidesShown = 0;
            _resolvedEndScale = 0f;
            _wakeAlarmStarted = false;

            return true;
        }

        IEnumerator ShowFirstSlide()
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            WarmupEndScale();
            ShowSlide(_index);
            _timer = 0f;
        }

        IEnumerator WaitForSceneLoad(AsyncOperation load)
        {
            while (load.progress < 0.9f)
                yield return null;
        }

        IEnumerator RunSlidesAndEnterGame()
        {
            yield return ShowFirstSlide();
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

            yield return WaitForSceneLoad(load);
            yield return ShowFirstSlide();

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
            CutToBlackAndStartWakeAlarm();

            if (audioSource != null && _wakeAlarmStarted)
            {
                while (audioSource.isPlaying)
                    yield return null;
            }
            else if (blackHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(blackHoldDuration);
            }
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
            _timer += Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            if (_timer >= _interval)
            {
                _timer = 0f;
                _interval = Mathf.Max(minInterval, _interval * intervalDecay);

                if (_index == slides.Length - 1)
                    _completedLoops++;

                if (!AreLoopsDone())
                {
                    _index = (_index + 1) % slides.Length;
                    ShowSlide(_index);
                }
                else
                    CutToBlackAndStartWakeAlarm();
            }

            yield return null;
        }

        bool AreLoopsDone() => _completedLoops >= minFullLoops;

        bool AreSlidesDone() =>
            AreLoopsDone()
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

        void CutToBlackAndStartWakeAlarm()
        {
            slideImage.enabled = false;

            if (_wakeAlarmStarted || audioSource == null || wakeAlarmClip == null)
                return;

            _wakeAlarmStarted = true;
            audioSource.PlayOneShot(wakeAlarmClip);
        }

        void EnsureIntroCanvas()
        {
            var canvas = slideImage.canvas?.rootCanvas;
            if (canvas == null)
                return;

            var rect = canvas.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
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
            ApplySlideScale();
            slideImage.enabled = true;

            if (audioSource != null && slideChangeClip != null)
                audioSource.PlayOneShot(slideChangeClip);
        }

        void ApplySlideScale()
        {
            var total = minFullLoops * slides.Length;
            var progress = total > 1 ? (float)_slidesShown / (total - 1) : 0f;
            progress = Mathf.Clamp01(progress);

            if (scaleCurve > 1f)
                progress = Mathf.Pow(progress, scaleCurve);

            var endScale = ResolveScreenOverflowScale();
            var scale = Mathf.Lerp(startScale, endScale, progress);
            slideImage.rectTransform.localScale = Vector3.one * scale;
            _slidesShown++;
        }

        void WarmupEndScale()
        {
            _resolvedEndScale = 0f;
            ResolveScreenOverflowScale();
        }

        float ResolveScreenOverflowScale()
        {
            if (_resolvedEndScale > 0f)
                return _resolvedEndScale;

            var rect = slideImage.rectTransform;
            var baseSize = rect.sizeDelta;
            if (baseSize.x <= 0f || baseSize.y <= 0f)
                baseSize = new Vector2(800f, 450f);

            var canvasSize = GetCanvasPixelSize();
            var fillScale = Mathf.Max(canvasSize.x / baseSize.x, canvasSize.y / baseSize.y);
            if (fillScale <= 0f)
                fillScale = 1f;

            _resolvedEndScale = fillScale * endOverflow;
            return _resolvedEndScale;
        }

        Vector2 GetCanvasPixelSize()
        {
            var canvas = slideImage.canvas?.rootCanvas;
            if (canvas != null)
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    return scaler.referenceResolution;

                var canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    var size = canvasRect.rect.size;
                    if (size.x > 1f && size.y > 1f)
                        return size;
                }
            }

            return new Vector2(Screen.width, Screen.height);
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
