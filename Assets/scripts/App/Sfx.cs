using UnityEngine;

namespace CozySudoku
{
    public enum Sound { Tap, Select, Note, Correct, Wrong, Win, Lose, Whoosh, Pop }

    /// <summary>
    /// Ses dosyası gerektirmeyen, kodla üretilmiş yumuşak "cozy" efektler.
    /// Tüm klipler açılışta bir kez üretilir ve tek AudioSource üzerinden çalınır.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 22050;
        static AudioSource source;
        static AudioClip[] clips;

        public static void Init(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            clips = new AudioClip[9];
            clips[(int)Sound.Tap] = Tone("tap", 0.07f, new[] { 660f }, 0.25f, 30f);
            clips[(int)Sound.Select] = Tone("select", 0.06f, new[] { 880f }, 0.16f, 40f);
            clips[(int)Sound.Note] = Tone("note", 0.06f, new[] { 1175f }, 0.12f, 45f);
            clips[(int)Sound.Correct] = Arp("correct", new[] { 784f, 1047f }, 0.07f, 0.22f);
            clips[(int)Sound.Wrong] = Tone("wrong", 0.22f, new[] { 220f, 207f }, 0.3f, 9f);
            clips[(int)Sound.Win] = Arp("win", new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.11f, 0.24f);
            clips[(int)Sound.Lose] = Arp("lose", new[] { 392f, 330f, 262f }, 0.16f, 0.22f);
            clips[(int)Sound.Whoosh] = Noise("whoosh", 0.35f, 0.08f);
            clips[(int)Sound.Pop] = Tone("pop", 0.09f, new[] { 520f }, 0.22f, 25f, 2.2f);
        }

        public static void Play(Sound s, float volume = 1f)
        {
            if (source == null || clips == null || !Settings.Sound) return;
            source.pitch = 1f;
            source.PlayOneShot(clips[(int)s], volume);
        }

        // Yumuşak sinüs + hafif üst harmonik, üstel sönümlü
        static AudioClip Tone(string name, float length, float[] freqs, float amp, float decay, float sweep = 1f)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            for (int f = 0; f < freqs.Length; f++)
            {
                float phase = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / Rate;
                    float freq = freqs[f] * Mathf.Lerp(sweep, 1f, Mathf.Clamp01(t / length));
                    phase += 2f * Mathf.PI * freq / Rate;
                    float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400f);
                    data[i] += (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f)) * env * amp / freqs.Length;
                }
            }
            return Make(name, data);
        }

        static AudioClip Arp(string name, float[] notes, float step, float amp)
        {
            float length = step * notes.Length + 0.35f;
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Mathf.RoundToInt(k * step * Rate);
                float phase = 0f;
                for (int i = start; i < n; i++)
                {
                    float t = (float)(i - start) / Rate;
                    phase += 2f * Mathf.PI * notes[k] / Rate;
                    float env = Mathf.Exp(-t * 7f) * Mathf.Clamp01(t * 300f);
                    data[i] += (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f) + 0.1f * Mathf.Sin(phase * 3f)) * env * amp;
                }
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -0.9f, 0.9f);
            return Make(name, data);
        }

        static AudioClip Noise(string name, float length, float amp)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            var rng = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Sin(t * Mathf.PI); // yumuşak giriş/çıkış
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * Mathf.Lerp(0.05f, 0.25f, t); // alçak geçiren filtre → "vuuş"
                data[i] = lp * env * amp * 4f;
            }
            return Make(name, data);
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
