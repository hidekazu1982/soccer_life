using UnityEngine;

namespace SoccerClub.UI
{
    /// <summary>
    /// メイン画面の BGM と試合用 BGM をループ再生し、画面に合わせて切り替える。
    /// 試合用は試合前の選手紹介からリザルトまで同じ再生を続ける。
    /// </summary>
    public sealed class ScreenBgm
    {
        const float FadeSeconds = 0.4f;
        const string VolumeKey = "SoccerClub.BgmVolume";

        AudioSource _source;
        AudioClip _main;
        AudioClip _match;
        AudioClip _current;
        AudioClip _queued;
        float _volume = 1f;
        float _userVolume = 1f;
        int _fade;

        public float UserVolume => _userVolume;

        public void Ensure(GameObject host)
        {
            if (_source != null) return;
            _main = Resources.Load<AudioClip>("BGM/main");
            _match = Resources.Load<AudioClip>("BGM/match");
            if (_main == null)
                Debug.LogWarning("SoccerClub: BGM/main を読み込めませんでした。");
            if (_match == null)
                Debug.LogWarning("SoccerClub: BGM/match を読み込めませんでした。");
            _source = host.AddComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _userVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            ApplyOutput();
        }

        public void SetUserVolume(float volume)
        {
            _userVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(VolumeKey, _userVolume);
            ApplyOutput();
        }

        public void Tick(bool matchScreen, float deltaTime)
        {
            if (_source == null) return;
            var desired = matchScreen ? _match : _main;
            if (desired == null) return;

            if (_current == null)
            {
                SwitchTo(desired);
                _volume = 1f;
                _fade = 0;
                ApplyOutput();
                return;
            }

            if (desired == _current)
                _queued = null;
            else
                _queued = desired;

            if (_queued != null && _fade == 0)
                _fade = -1;
            else if (_queued == null && _fade < 0)
                _fade = 1;

            if (_fade == 0) return;

            float step = FadeSeconds <= 0f ? 1f : deltaTime / FadeSeconds;
            if (_fade < 0)
            {
                _volume = Mathf.Max(0f, _volume - step);
                if (_volume <= 0f && _queued != null)
                {
                    SwitchTo(_queued);
                    _queued = null;
                    _fade = 1;
                }
            }
            else
            {
                _volume = Mathf.Min(1f, _volume + step);
                if (_volume >= 1f)
                    _fade = 0;
            }

            ApplyOutput();
        }

        void ApplyOutput()
        {
            if (_source != null)
                _source.volume = _volume * _userVolume;
        }

        void SwitchTo(AudioClip clip)
        {
            _current = clip;
            _source.clip = clip;
            _source.loop = true;
            _source.Play();
        }
    }
}
