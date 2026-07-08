namespace Operator.Parallax.Core
{
    public class ParallaxMotion
    {
        float[] _speeds;

        public void Init(float[] speeds)
        {
            _speeds = speeds;
        }

        public int LayerCount => _speeds != null ? _speeds.Length : 0;

        public float GetLayerOffset(int layerIndex, float cameraX)
        {
            if (_speeds == null || layerIndex < 0 || layerIndex >= _speeds.Length)
            {
                return 0f;
            }

            return cameraX * (1f - _speeds[layerIndex]);
        }
    }
}
