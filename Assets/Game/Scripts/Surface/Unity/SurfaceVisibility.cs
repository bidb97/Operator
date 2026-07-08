using UnityEngine;

namespace Operator.Surface.Unity
{
    /// <summary>
    /// Вешать на Surface (родитель). Сам Surface всегда active; гасит/включает детей (parallax, Ярмо).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class SurfaceVisibility : MonoBehaviour
    {
        [SerializeField] float hideWhenCameraTopBelowY;
        [SerializeField] float showWhenCameraTopAboveY = 0.5f;

        bool _contentVisible = true;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var cameraTopY = cam.transform.position.y + cam.orthographicSize;

            if (_contentVisible && cameraTopY < hideWhenCameraTopBelowY)
            {
                SetContentActive(false);
                _contentVisible = false;
                return;
            }

            if (!_contentVisible && cameraTopY > showWhenCameraTopAboveY)
            {
                SetContentActive(true);
                _contentVisible = true;
            }
        }

        void SetContentActive(bool active)
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(active);
            }
        }
    }
}
