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
        [SerializeField] Image slideImage;
        [SerializeField] Object sourceFolder;
        [SerializeField] Sprite[] slides;
        [SerializeField] string gameSceneName = "Game";

        [Header("Timing")]
        [SerializeField] float startInterval = 1.2f;
        [SerializeField] float minInterval = 0.08f;
        [SerializeField] float intervalDecay = 0.92f;
        [SerializeField] float minIntroDuration = 6f;
        [SerializeField] int minFullLoops = 2;

        [Header("Zoom")]
        [SerializeField] float startScale = 0.2f;
        [SerializeField] float endScale = 1f;

        Vector2 _slideFullSize;
        float _interval;
        float _timer;
        float _introStartedAt;
        int _index;
        int _completedLoops;

        void Awake()
        {
            DisableIntroEventSystem();

#if UNITY_EDITOR
            EnsureSlidesLoaded();
#endif
        }

        void Start()
        {
            if (slideImage == null || slides == null || slides.Length == 0)
            {
                Debug.LogError($"{nameof(IntroController)}: slideImage or slides not set.", this);
                SceneManager.LoadScene(gameSceneName);
                return;
            }

            _introStartedAt = Time.unscaledTime;
            _interval = startInterval;
            _index = 0;
            _timer = 0f;

            Canvas.ForceUpdateCanvases();
            ShowSlide(_index);

            StartCoroutine(RunIntro());
        }

        IEnumerator RunIntro()
        {
            var load = SceneManager.LoadSceneAsync(gameSceneName);
            load.allowSceneActivation = false;

            while (true)
            {
                _timer += Time.unscaledDeltaTime;

                if (_timer >= _interval)
                {
                    _timer = 0f;
                    _interval = Mathf.Max(minInterval, _interval * intervalDecay);

                    var prev = _index;
                    _index = (_index + 1) % slides.Length;
                    if (_index == 0 && prev == slides.Length - 1)
                        _completedLoops++;

                    ShowSlide(_index);
                }

                ApplyGlobalZoom();

                var slidesDone = _completedLoops >= minFullLoops
                    && Time.unscaledTime - _introStartedAt >= minIntroDuration;
                var loadReady = load.progress >= 0.9f;

                if (slidesDone && loadReady)
                    break;

                yield return null;
            }

            load.allowSceneActivation = true;

            while (!load.isDone)
                yield return null;
        }

        void ApplyGlobalZoom()
        {
            if (_slideFullSize.sqrMagnitude <= 0f)
                CacheSlideFullSize();

            var total = minFullLoops * slides.Length;
            var linear = _completedLoops * slides.Length + _index + Mathf.Clamp01(_timer / _interval);
            var progress = total > 0 ? Mathf.Clamp01(linear / total) : 1f;
            var scale = Mathf.Lerp(startScale, endScale, progress);

            slideImage.rectTransform.localScale = Vector3.one;
            slideImage.rectTransform.sizeDelta = _slideFullSize * scale;
        }

        void DisableIntroEventSystem()
        {
            foreach (var eventSystem in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (eventSystem.gameObject.scene == gameObject.scene)
                    eventSystem.enabled = false;
            }
        }

        void ShowSlide(int index)
        {
            slideImage.sprite = slides[index];
            slideImage.preserveAspect = true;
            CacheSlideFullSize();
        }

        void CacheSlideFullSize()
        {
            var sprite = slideImage.sprite;
            if (sprite == null)
                return;

            var canvasRect = slideImage.canvas != null
                ? slideImage.canvas.GetComponent<RectTransform>().rect
                : new Rect(0f, 0f, Screen.width, Screen.height);

            var width = canvasRect.width > 1f ? canvasRect.width : Screen.width;
            var aspect = sprite.rect.height / sprite.rect.width;
            _slideFullSize = new Vector2(width, width * aspect);
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
