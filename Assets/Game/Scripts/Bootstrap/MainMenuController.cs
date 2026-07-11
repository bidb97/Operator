using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Operator.Bootstrap
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] Button _newGameButton;
        [SerializeField] Button _continueGameButton;
        [SerializeField] string _introSceneName = "Intro";
        [SerializeField] string _gameSceneName = "Game";
        [SerializeField] CanvasGroup _overlay;
        [SerializeField] Image _overlayBackdrop;
        [SerializeField] Transform _overlayControlsRoot;
        [SerializeField] GameObject _overlayTitle;
        [SerializeField] Canvas _menuCanvas;
        [SerializeField] Camera _menuCamera;
        [SerializeField] AudioSource _menuMusic;
        [SerializeField] GameObject _loader;
        [SerializeField] CanvasGroup _prologueGroup;
        [SerializeField] Button _continueButton;
        [SerializeField] float _overlayFadeDuration = 0.35f;
        [SerializeField] float _transitionToIntroDuration = 0.6f;
        [SerializeField] float _continueButtonDelay = 5f;
        [SerializeField] float _autoContinueTimeout = 15f;
        [SerializeField] float _minLoaderDuration = 1f;
        [SerializeField] float _prologueFadeDuration = 0.5f;

        bool _loading;

        void Awake()
        {
            var hasSave = GameBootstrap.Instance != null && GameBootstrap.Instance.HasSave;

            if (hasSave && _continueGameButton != null)
            {
                _newGameButton.gameObject.SetActive(false);
                _continueGameButton.gameObject.SetActive(true);
                _continueGameButton.onClick.AddListener(StartNewGame); // TODO: продолжить сохранённую игру без интро
            }
            else
            {
                _newGameButton.gameObject.SetActive(true);
                _continueGameButton?.gameObject.SetActive(false);
                _newGameButton.onClick.AddListener(StartNewGame);
            }
        }

        void StartNewGame()
        {
            if (_loading)
                return;

            _loading = true;
            NewGameLoadFlow.ResetForNewGame();

            StartCoroutine(StartNewGameHandler());
        }

        IEnumerator StartNewGameHandler()
        {
            Debug.Log("[MainMenu] Загрузка: старт");

            _overlay.interactable = false;
            _overlay.blocksRaycasts = false;

            float time = 0f;
            float startAlpha = _overlay.alpha;

            while (time < _overlayFadeDuration)
            {
                time += Time.unscaledDeltaTime;
                _overlay.alpha = Mathf.Lerp(startAlpha, 0f, time / _overlayFadeDuration);
                yield return null;
            }

            _overlay.alpha = 0f;
            _overlay.gameObject.SetActive(false);
            _loader.SetActive(true);
            StartCoroutine(FadeInPrologue());

            LevelBootstrap.SuppressAutoBegin = true;
            IntroController.SuppressAutoRun = true;

            var introLoad = SceneManager.LoadSceneAsync(_introSceneName, LoadSceneMode.Additive);
            introLoad.allowSceneActivation = false;

            var gameLoad = SceneManager.LoadSceneAsync(_gameSceneName, LoadSceneMode.Additive);
            gameLoad.allowSceneActivation = false;

            float startedAt = Time.unscaledTime;
            float lastLogAt = startedAt;

            while (true)
            {
                bool timeOk = Time.unscaledTime - startedAt >= _minLoaderDuration;
                bool introReady = introLoad.progress >= 0.9f;
                bool gameReady = gameLoad.progress >= 0.9f;

                if (Time.unscaledTime - lastLogAt >= 0.5f)
                {
                    lastLogAt = Time.unscaledTime;
                    Debug.Log(
                        $"[MainMenu] Загрузка: intro={introLoad.progress:F2}, game={gameLoad.progress:F2}, timeOk={timeOk}");
                }

                if (timeOk && introReady && gameReady)
                    break;

                yield return null;
            }

            introLoad.allowSceneActivation = true;
            gameLoad.allowSceneActivation = true;

            while (!introLoad.isDone)
                yield return null;

            while (!gameLoad.isDone)
                yield return null;

            GameScenePresentation.SetEnabled(_introSceneName, false);
            GameScenePresentation.SetEnabled(_gameSceneName, false);

            Debug.Log("[MainMenu] Сцены скрыты, сигнал LevelBootstrap");

            NewGameLoadFlow.BuildRequested = true;

            float buildLogAt = Time.unscaledTime;

            while (!NewGameLoadFlow.BuildFinished)
            {
                if (Time.unscaledTime - buildLogAt >= 0.5f)
                {
                    buildLogAt = Time.unscaledTime;
                    Debug.Log($"[MainMenu] Загрузка: {NewGameLoadFlow.BuildProgress:P0} (мир)");
                }

                yield return null;
            }

            Debug.Log("[MainMenu] Мир готов, лоадер ещё немного");

            yield return WaitForContinue();

            var menuScene = gameObject.scene;
            var introScene = SceneManager.GetSceneByName(_introSceneName);

            yield return TransitionToIntro(menuScene, introScene);

            Debug.Log("[MainMenu] Intro завершён, выгрузка Main Menu");

            yield return SceneManager.UnloadSceneAsync(menuScene);
        }

        IEnumerator TransitionToIntro(Scene menuScene, Scene introScene)
        {
            PrepareOverlayBlackout();

            var musicStartVolume = _menuMusic != null ? _menuMusic.volume : 0f;
            var time = 0f;

            while (time < _transitionToIntroDuration)
            {
                time += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(time / _transitionToIntroDuration);
                _overlay.alpha = t;

                if (_menuMusic != null)
                    _menuMusic.volume = Mathf.Lerp(musicStartVolume, 0f, t);

                yield return null;
            }

            _overlay.alpha = 1f;

            if (_menuMusic != null)
            {
                _menuMusic.volume = 0f;
                _menuMusic.Stop();
            }

            _loader.SetActive(false);

            if (_menuCanvas != null)
                _menuCanvas.enabled = false;

            if (_menuCamera != null)
                _menuCamera.enabled = false;

            GameScenePresentation.SetEnabled(_gameSceneName, false);
            GameScenePresentation.SetEnabled(introScene, true);

            var introController = FindIntroController(introScene);
            if (introController == null)
            {
                Debug.LogError("[MainMenu] IntroController не найден");
                GameScenePresentation.SetEnabled(introScene, false);
                GameScenePresentation.SetEnabled(_gameSceneName, true);
                NewGameLoadFlow.IntroFinished = true;
            }
            else
            {
                introController.BeginFromPreload();
            }

            GameScenePresentation.SetEnabled(menuScene, false);

            while (!NewGameLoadFlow.IntroFinished)
                yield return null;
        }

        IEnumerator WaitForContinue()
        {
            var pressed = false;
            void OnPressed() => pressed = true;

            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnPressed);

            var elapsed = 0f;
            var buttonShown = false;

            while (!pressed && elapsed < _autoContinueTimeout)
            {
                if (!buttonShown && _continueButton != null && elapsed >= _continueButtonDelay)
                {
                    buttonShown = true;
                    ShowContinueButton();
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveListener(OnPressed);
                HideContinueButton();
            }
        }

        void ShowContinueButton()
        {
            _continueButton.gameObject.SetActive(true);
            _loader.SetActive(false);

            if (_prologueGroup != null)
            {
                _prologueGroup.interactable = true;
                _prologueGroup.blocksRaycasts = true;
            }
        }

        void HideContinueButton()
        {
            _continueButton.gameObject.SetActive(false);

            if (_prologueGroup != null)
            {
                _prologueGroup.interactable = false;
                _prologueGroup.blocksRaycasts = false;
            }
        }

        IEnumerator FadeInPrologue()
        {
            if (_prologueGroup == null)
                yield break;

            _prologueGroup.gameObject.SetActive(true);
            _prologueGroup.alpha = 0f;

            yield return FadeCanvasGroup(_prologueGroup, 0f, 1f, _prologueFadeDuration);
        }

        static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                group.alpha = to;
                yield break;
            }

            var time = 0f;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, time / duration);
                yield return null;
            }

            group.alpha = to;
        }

        void PrepareOverlayBlackout()
        {
            _overlay.gameObject.SetActive(true);
            _overlay.alpha = 0f;
            _overlay.interactable = false;
            _overlay.blocksRaycasts = true;

            StretchToScreen((RectTransform)_overlay.transform);

            if (_overlayControlsRoot is RectTransform controlsRect)
                StretchToScreen(controlsRect);

            if (_overlayBackdrop != null)
            {
                StretchToScreen(_overlayBackdrop.rectTransform);
                _overlayBackdrop.type = Image.Type.Simple;
                _overlayBackdrop.color = Color.black;
                _overlayBackdrop.raycastTarget = false;
            }

            if (_overlayControlsRoot != null)
            {
                for (var i = 0; i < _overlayControlsRoot.childCount; i++)
                    _overlayControlsRoot.GetChild(i).gameObject.SetActive(false);
            }

            if (_overlayTitle != null)
                _overlayTitle.SetActive(false);

            Canvas.ForceUpdateCanvases();
        }
        

        static void StretchToScreen(RectTransform rect)
        {
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        static IntroController FindIntroController(Scene introScene)
        {
            if (!introScene.IsValid())
                return null;

            foreach (var root in introScene.GetRootGameObjects())
            {
                var intro = root.GetComponentInChildren<IntroController>(true);
                if (intro != null)
                    return intro;
            }

            return null;
        }
    }
}
