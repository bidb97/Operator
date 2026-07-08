using UnityEngine;

namespace Operator.Managers
{
    /// <summary>
    /// Музыка и SFX по id из AssetManager.
    /// </summary>
    public class AudioManager
    {
        readonly AssetManager _assets;
        readonly AudioSource _musicSource;
        readonly AudioSource _sfxSource;

        public AudioManager(AssetManager assets, GameObject host)
        {
            _assets = assets;
            _musicSource = host.AddComponent<AudioSource>();
            
            _musicSource.loop = true;
            _sfxSource = host.AddComponent<AudioSource>();
        }

        public void PlayMusic(string assetPackName, string assetId, float volume = 1f)
        {
            var clip = _assets.GetAudio(assetPackName, assetId);
            
            if (clip == null)
            {
                 return;
            }

            _musicSource.clip = clip;
            _musicSource.volume = volume;
            _musicSource.Play();
        }

        public void PlaySfx(string assetPackName, string assetId, float volume = 1f)
        {
            var clip = _assets.GetAudio(assetPackName, assetId);

            if (clip == null)
            {
                 return;
            }

            _sfxSource.PlayOneShot(clip, volume);
        }
    }
}
