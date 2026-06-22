using UnityEngine;

namespace AngelGuardian.Enemies
{
    /// <summary>
    /// 音效管理器 - 管理游戏音效和BGM
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private float defaultSFXVolume = 1f;
        [SerializeField] private float defaultBGMVolume = 0.7f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // 自动创建AudioSource
                if (sfxSource == null)
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                    sfxSource.playOnAwake = false;
                    sfxSource.volume = defaultSFXVolume;
                }

                if (bgmSource == null)
                {
                    bgmSource = gameObject.AddComponent<AudioSource>();
                    bgmSource.playOnAwake = false;
                    bgmSource.loop = true;
                    bgmSource.volume = defaultBGMVolume;
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void PlaySFX(AudioClip clip, Vector3 position)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip);
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || bgmSource == null) return;

            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            if (bgmSource != null)
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
        }

        public void SetSFXVolume(float volume)
        {
            if (sfxSource != null)
                sfxSource.volume = Mathf.Clamp01(volume);
        }

        public void SetBGMVolume(float volume)
        {
            if (bgmSource != null)
                bgmSource.volume = Mathf.Clamp01(volume);
        }
    }
}
