using System.Collections.Generic;
using UnityEngine;

namespace GhostWard
{
    // Записанные звуки из Assets/Resources/Sfx/<ключ>. Если файла нет, играет синтез с тем же ключом.
    // Длинные дубли обрезаются, а громкость выравнивается, чтобы их можно было заменять без правки кода.
    public static class SoundBank
    {
        static readonly Dictionary<string, float> MaxSeconds = new()
        {
            ["whisper"] = 4f,
            ["breath"] = 5f,
            ["steps"] = 5f,
            ["knock"] = 4f,
            ["sob"] = 7f,
            ["screech"] = 3f,
        };

        const float Peak = 0.9f, FadeSeconds = 0.2f;
        static readonly Dictionary<string, AudioClip> Loaded = new();

        public static bool Has(string key) => Get(key) != null;

        public static void Preload()
        {
            foreach (var key in MaxSeconds.Keys) Get(key);
        }

        public static AudioClip Get(string key)
        {
            if (Loaded.TryGetValue(key, out var ready)) return ready;
            AudioClip clip = null;
            if (MaxSeconds.TryGetValue(key, out float seconds))
            {
                var file = Resources.Load<AudioClip>("Sfx/" + key);
                if (file != null) clip = Prepare(file, key, seconds);
            }
            return Loaded[key] = clip;
        }

        static AudioClip Prepare(AudioClip file, string key, float seconds)
        {
            if (file.loadState != AudioDataLoadState.Loaded && !file.LoadAudioData()) return file;
            int channels = file.channels;
            var all = new float[file.samples * channels];
            if (!file.GetData(all, 0)) return file;

            int frames = Mathf.Min(file.samples, Mathf.RoundToInt(seconds * file.frequency));
            var data = new float[frames * channels];
            System.Array.Copy(all, data, data.Length);

            float max = 1e-4f;
            foreach (var v in data) max = Mathf.Max(max, Mathf.Abs(v));
            float gain = Peak / max;
            int fade = Mathf.Min(frames, Mathf.RoundToInt(FadeSeconds * file.frequency));
            for (int f = 0; f < frames; f++)
            {
                float tail = frames - f <= fade ? (frames - f - 1) / (float)Mathf.Max(fade, 1) : 1f;
                for (int c = 0; c < channels; c++) data[f * channels + c] *= gain * tail;
            }

            var clip = AudioClip.Create("sfx_" + key, frames, channels, file.frequency, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
