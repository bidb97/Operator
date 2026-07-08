using Operator.Depth.Core;
using Operator.Managers;
using UnityEngine;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace Operator.Drone.Unity
{
    /// <summary>
    /// Огонь сопла: всегда полный цикл кадров; drive / rotate / idle отличаются FPS и «утоплением» под дрон.
    /// В гараже на стоянке — затухание и off.
    /// </summary>
    public class DroneThrusterFx : MonoBehaviour
    {
        enum ThrusterMode
        {
            Drive,
            Rotate,
            Idle,
        }

        [SerializeField] DroneController drone;
        [SerializeField] SpriteRenderer thrusterRenderer;
        [SerializeField] Object sourceFolder;
        [SerializeField] Sprite[] frames;
        [SerializeField] float driveFps = 24f;
        [SerializeField] float rotateFps = 20f;
        [SerializeField] float idleFps = 8f;
        [SerializeField] float fadeDuration = 0.35f;
        [SerializeField] float driveScale = 1f;
        [SerializeField] float rotateScale = 0.9f;
        [SerializeField] float idleScale = 0.75f;
        [SerializeField] float rotateSink = 0.35f;
        [SerializeField] float idleSink = 0.55f;
        [SerializeField] float visualSmooth = 12f;

        float _alpha;
        float _timer;
        int _frameIndex;
        Vector3 _baseLocalPosition;
        Vector3 _baseLocalScale;
        Transform _thrusterTransform;

        void Awake()
        {
            if (drone == null)
            {
                drone = GetComponent<DroneController>();
            }

            if (thrusterRenderer == null)
            {
                thrusterRenderer = FindThrusterRenderer();
            }

            if (thrusterRenderer != null)
            {
                _thrusterTransform = thrusterRenderer.transform;
                _baseLocalPosition = _thrusterTransform.localPosition;
                _baseLocalScale = _thrusterTransform.localScale;
                thrusterRenderer.enabled = false;
            }

#if UNITY_EDITOR
            TryLoadFramesFromFolder();
#endif
        }

#if UNITY_EDITOR
        void TryLoadFramesFromFolder()
        {
            if (frames != null && frames.Length > 0)
            {
                return;
            }

            if (sourceFolder == null)
            {
                return;
            }

            var folderPath = AssetDatabase.GetAssetPath(sourceFolder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            frames = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                .Where(sprite => sprite != null)
                .Distinct()
                .OrderBy(SpriteOrderKey)
                .ToArray();

            if (frames.Length > 0)
            {
                EditorUtility.SetDirty(this);
            }
        }

        static int SpriteOrderKey(Sprite sprite)
        {
            var name = sprite.name;
            var separator = name.LastIndexOf('_');
            if (separator >= 0 && int.TryParse(name.Substring(separator + 1), out var index))
            {
                return index;
            }

            return 0;
        }
#endif

        void Update()
        {
            if (thrusterRenderer == null || drone == null || frames == null || frames.Length == 0)
            {
                return;
            }

            var inGarage = IsInGarage();
            var working = drone.IsMoving || drone.IsDrilling || drone.IsRotating;
            var mode = ResolveMode(inGarage);
            var targetAlpha = !inGarage || working ? 1f : 0f;
            var fadeStep = fadeDuration > 0f ? Time.deltaTime / fadeDuration : 1f;
            _alpha = Mathf.MoveTowards(_alpha, targetAlpha, fadeStep);

            thrusterRenderer.enabled = _alpha > 0.001f;
            if (!thrusterRenderer.enabled)
            {
                return;
            }

            var color = thrusterRenderer.color;
            color.a = _alpha;
            thrusterRenderer.color = color;

            ApplyVisualMode(mode);

            var fps = ResolveFps(mode);
            if (fps <= 0f)
            {
                ApplySprite();
                return;
            }

            _timer += Time.deltaTime;
            var frameDuration = 1f / fps;
            while (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _frameIndex = (_frameIndex + 1) % frames.Length;
            }

            ApplySprite();
        }

        ThrusterMode ResolveMode(bool inGarage)
        {
            if (inGarage)
            {
                return ThrusterMode.Idle;
            }

            if (drone.IsMoving || drone.IsDrilling)
            {
                return ThrusterMode.Drive;
            }

            if (drone.IsRotating)
            {
                return ThrusterMode.Rotate;
            }

            return ThrusterMode.Idle;
        }

        float ResolveFps(ThrusterMode mode)
        {
            return mode switch
            {
                ThrusterMode.Drive => driveFps,
                ThrusterMode.Rotate => rotateFps,
                _ => idleFps,
            };
        }

        void ApplyVisualMode(ThrusterMode mode)
        {
            if (_thrusterTransform == null)
            {
                return;
            }

            var targetScale = mode switch
            {
                ThrusterMode.Drive => driveScale,
                ThrusterMode.Rotate => rotateScale,
                _ => idleScale,
            };

            var sink = mode switch
            {
                ThrusterMode.Drive => 0f,
                ThrusterMode.Rotate => rotateSink,
                _ => idleSink,
            };

            var targetPosition = Vector3.Lerp(_baseLocalPosition, _baseLocalPosition * (1f - sink), sink);
            var targetScaleVector = _baseLocalScale * targetScale;
            var t = 1f - Mathf.Exp(-visualSmooth * Time.deltaTime);

            _thrusterTransform.localPosition = Vector3.Lerp(_thrusterTransform.localPosition, targetPosition, t);
            _thrusterTransform.localScale = Vector3.Lerp(_thrusterTransform.localScale, targetScaleVector, t);
        }

        void ApplySprite()
        {
            if (thrusterRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            var index = Mathf.Clamp(_frameIndex, 0, frames.Length - 1);
            var sprite = frames[index];
            if (sprite != null)
            {
                thrusterRenderer.sprite = sprite;
            }
        }

        bool IsInGarage()
        {
            var cell = WorldGrid.WorldToCell(drone.transform.position);
            var world = GameManager.Instance?.World;
            if (world != null)
            {
                cell.x = world.WrapX(cell.x);
            }

            return GarageBounds.Contains(cell.x, cell.y);
        }

        SpriteRenderer FindThrusterRenderer()
        {
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.gameObject.name == "Thruster")
                {
                    return renderer;
                }
            }

            return null;
        }
    }
}
