using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Genera los efectos de sonido por codigo (el proyecto no trae sonidos de armas).
    /// Los clips se crean una sola vez y se cachean.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int Rate = 44100;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();
        private static System.Random _rng = new System.Random(1234);

        private static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        private static AudioClip Make(string key, float seconds, System.Func<int, float, float> generator)
        {
            if (Cache.TryGetValue(key, out AudioClip cached) && cached != null) return cached;

            int count = Mathf.Max(1, Mathf.CeilToInt(seconds * Rate));
            float[] data = new float[count];
            float peak = 0.0001f;
            for (int i = 0; i < count; i++)
            {
                data[i] = generator(i, i / (float)Rate);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float norm = 0.9f / peak;
            for (int i = 0; i < count; i++) data[i] *= norm;

            AudioClip clip = AudioClip.Create(key, count, 1, Rate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }

        /// <summary>Disparo: chasquido de ruido + golpe grave + cola de reverberacion.</summary>
        public static AudioClip Gunshot(float pitch, float body, float length)
        {
            string key = $"shot_{pitch:F2}_{body:F2}_{length:F2}";
            float lp = 0f, lp2 = 0f;
            float[] echo = new float[(int)(0.045f * Rate)];
            int echoIdx = 0;
            return Make(key, length, (i, t) =>
            {
                float crack = Noise() * Mathf.Exp(-t * 55f * pitch);
                // ruido filtrado (cuerpo de la explosion)
                lp += (Noise() - lp) * Mathf.Lerp(0.08f, 0.25f, pitch * 0.5f);
                float boom = lp * Mathf.Exp(-t * 9f / body) * 1.6f;
                float thump = Mathf.Sin(2f * Mathf.PI * (55f + 40f * pitch) * t * (1f - t)) * Mathf.Exp(-t * 18f) * body;
                float s = crack * 0.9f + boom + thump;
                lp2 += (s - lp2) * 0.35f;
                float e = echo[echoIdx];
                echo[echoIdx] = lp2 * 0.55f + e * 0.45f;
                echoIdx = (echoIdx + 1) % echo.Length;
                return lp2 + e * 0.5f;
            });
        }

        public static AudioClip Explosion()
        {
            float lp = 0f;
            return Make("explosion", 2.2f, (i, t) =>
            {
                lp += (Noise() - lp) * 0.04f;
                float rumble = lp * 4f * Mathf.Exp(-t * 1.6f);
                float crack = Noise() * Mathf.Exp(-t * 20f);
                float sub = Mathf.Sin(2f * Mathf.PI * 38f * t) * Mathf.Exp(-t * 3f);
                return rumble + crack * 0.6f + sub;
            });
        }

        /// <summary>Clic metalico (cargadores, correderas, gatillo sin balas).</summary>
        public static AudioClip Click(float freq, float decay = 140f)
        {
            return Make($"click_{freq:F0}_{decay:F0}", 0.09f, (i, t) =>
            {
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * freq * 2.7f * t);
                return (tone * 0.6f + Noise() * 0.4f) * Mathf.Exp(-t * decay);
            });
        }

        /// <summary>Corredera / bombeo: dos golpes metalicos con roce.</summary>
        public static AudioClip Rack()
        {
            return Make("rack", 0.35f, (i, t) =>
            {
                float a = t < 0.12f ? Mathf.Exp(-t * 60f) : 0f;
                float bT = t - 0.18f;
                float b = bT > 0f ? Mathf.Exp(-bT * 50f) : 0f;
                float scrape = (t > 0.02f && t < 0.17f) ? Noise() * 0.15f : 0f;
                float tone = Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.5f + Noise() * 0.5f;
                return tone * (a + b) + scrape;
            });
        }

        public static AudioClip ShellDrop()
        {
            return Make("shell", 0.5f, (i, t) =>
            {
                float env = 0f;
                float[] hits = { 0f, 0.09f, 0.16f, 0.21f };
                for (int h = 0; h < hits.Length; h++)
                {
                    float d = t - hits[h];
                    if (d > 0f) env += Mathf.Exp(-d * 90f) * (1f - h * 0.22f);
                }
                return Mathf.Sin(2f * Mathf.PI * 4200f * t) * env * 0.7f + Noise() * env * 0.2f;
            });
        }

        public static AudioClip Swoosh()
        {
            float lp = 0f;
            return Make("swoosh", 0.28f, (i, t) =>
            {
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.28f) * Mathf.PI);
                lp += (Noise() - lp) * Mathf.Lerp(0.05f, 0.4f, t / 0.28f);
                return lp * env;
            });
        }

        public static AudioClip Impact(float weight)
        {
            float lp = 0f;
            return Make($"impact_{weight:F1}", 0.35f, (i, t) =>
            {
                lp += (Noise() - lp) * 0.12f;
                float thud = Mathf.Sin(2f * Mathf.PI * (70f / weight + 40f) * t) * Mathf.Exp(-t * 22f);
                return (thud * weight + lp * 1.2f) * Mathf.Exp(-t * 14f);
            });
        }

        /// <summary>Gemido / gruñido de aldeano.</summary>
        public static AudioClip Growl(int variant)
        {
            float baseF = 85f + variant * 17f;
            float len = 0.9f + (variant % 3) * 0.35f;
            float lp = 0f, ph = 0f;
            return Make($"growl_{variant}", len, (i, t) =>
            {
                float vib = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * (5f + variant) * t);
                float f = baseF * vib * (1f + 0.25f * Mathf.Sin(Mathf.PI * t / len));
                ph += f / Rate;
                float saw = (ph % 1f) * 2f - 1f;
                lp += (saw + Noise() * 0.6f - lp) * 0.09f;
                float env = Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI);
                return lp * env;
            });
        }

        public static AudioClip Scream()
        {
            float lp = 0f, ph = 0f;
            return Make("scream", 1.1f, (i, t) =>
            {
                float f = 220f + 160f * Mathf.Sin(Mathf.PI * t / 1.1f);
                ph += f / Rate;
                float saw = (ph % 1f) * 2f - 1f;
                lp += (saw + Noise() * 0.8f - lp) * 0.2f;
                return lp * Mathf.Sin(Mathf.Clamp01(t / 1.1f) * Mathf.PI);
            });
        }

        public static AudioClip Pickup()
        {
            return Make("pickup", 0.35f, (i, t) =>
            {
                float f = t < 0.12f ? 880f : 1320f;
                return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-((t % 0.12f)) * 12f) * 0.5f;
            });
        }

        /// <summary>Mugido: tono grave nasal que sube y cae, con formantes y algo de aire.</summary>
        public static AudioClip Moo(int variant)
        {
            float phase = 0f, lp = 0f;
            float len = 1.3f + variant * 0.25f;
            float baseF = 92f + variant * 9f;
            return Make($"moo_{variant}", len, (i, t) =>
            {
                float u = t / len;
                float f = baseF * (1f + 0.25f * Mathf.Sin(Mathf.Clamp01(u * 1.3f) * Mathf.PI) - 0.12f * u);
                phase += f / 44100f;
                float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
                float formant = Mathf.Sin(2f * Mathf.PI * phase * 4f) * 0.35f + Mathf.Sin(2f * Mathf.PI * phase * 7f) * 0.15f;
                lp += ((saw + formant) - lp) * 0.18f;
                float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((len - t) / 0.35f);
                return (lp + Noise() * 0.04f) * env;
            });
        }

        /// <summary>Gruñido de cerdo: dos o tres golpes nasales cortos.</summary>
        public static AudioClip Oink(int variant)
        {
            float phase = 0f, lp = 0f;
            int grunts = 2 + variant % 2;
            float len = 0.22f * grunts + 0.1f;
            return Make($"oink_{variant}", len, (i, t) =>
            {
                float local = t % 0.22f;
                float env = Mathf.Sin(Mathf.Clamp01(local / 0.16f) * Mathf.PI);
                float f = 170f + variant * 25f + Mathf.Sin(local * 40f) * 30f;
                phase += f / 44100f;
                float pulse = (phase % 1f) < 0.3f ? 1f : -0.4f;
                lp += ((pulse + Noise() * 0.6f) - lp) * 0.3f;
                return lp * env;
            });
        }

        /// <summary>Chillido de animal herido o asustado.</summary>
        public static AudioClip Squeal()
        {
            float phase = 0f;
            return Make("squeal", 0.7f, (i, t) =>
            {
                float f = 650f + Mathf.Sin(t * 22f) * 120f - t * 200f;
                phase += f / 44100f;
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 3f);
                return (Mathf.Sin(2f * Mathf.PI * phase) * 0.7f + Mathf.Sin(4f * Mathf.PI * phase) * 0.25f + Noise() * 0.15f) * env;
            });
        }

        /// <summary>Trueno lejano: chasquido inicial (si es cercano) y retumbo grave que rueda varios segundos.</summary>
        public static AudioClip Thunder(int variant)
        {
            float lp = 0f, lp2 = 0f, lp3 = 0f;
            float len = 4.2f + variant * 0.6f;
            float crack = variant == 0 ? 0.6f : 0.15f;
            var rng = new System.Random(77 + variant);
            float[] bumps = new float[6];
            for (int k = 0; k < bumps.Length; k++) bumps[k] = (float)rng.NextDouble() * (len - 1.2f);
            return Make($"thunder_{variant}", len, (i, t) =>
            {
                float n = Noise();
                lp += (n - lp) * 0.02f;
                lp2 += (lp - lp2) * 0.05f;
                lp3 += (n - lp3) * 0.25f;
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-t * 0.65f);
                float roll = 0.6f;
                for (int k = 0; k < bumps.Length; k++)
                {
                    float d = t - bumps[k];
                    if (d > 0f) roll += Mathf.Exp(-d * 2.2f) * (1f - d * 0.05f) * 0.7f;
                }
                float c = Mathf.Exp(-t * 9f) * crack;
                return lp2 * 6f * env * roll + lp3 * c;
            });
        }

        /// <summary>Golpe de tension: zumbido grave disonante que crece y se corta (apariciones).</summary>
        public static AudioClip Stinger()
        {
            float lp = 0f;
            return Make("stinger", 2.6f, (i, t) =>
            {
                float env = Mathf.Pow(Mathf.Clamp01(t / 2.1f), 2.2f) * (t > 2.1f ? Mathf.Exp(-(t - 2.1f) * 9f) : 1f);
                float trem = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.8f * Mathf.Sin(2f * Mathf.PI * 58.3f * t)
                             + 0.45f * Mathf.Sin(2f * Mathf.PI * 116.5f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 233f * t + Mathf.Sin(t * 3f));
                lp += (Noise() - lp) * 0.06f;
                return (tone * 0.5f + lp * 1.5f) * env * trem;
            });
        }

        /// <summary>Susurro: ruido "sibilante" con silabas irregulares.</summary>
        public static AudioClip Whisper(int variant)
        {
            float hp = 0f, prev = 0f, lp = 0f;
            float len = 1.6f + variant * 0.3f;
            float rate = 5.5f + variant * 1.3f;
            return Make($"whisper_{variant}", len, (i, t) =>
            {
                float n = Noise();
                hp = 0.85f * (hp + n - prev); // paso alto: sibilancia
                prev = n;
                lp += (hp - lp) * 0.45f;
                float syll = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * rate * t + Mathf.Sin(t * 4.3f) * 2f));
                float env = Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI);
                return lp * syll * syll * env;
            });
        }

        /// <summary>Jadeo de cansancio: inspiracion corta + espiracion de ruido filtrado.</summary>
        public static AudioClip Breath(int variant)
        {
            float lp = 0f, lp2 = 0f;
            float len = 0.9f + variant * 0.12f;
            return Make($"breath_{variant}", len, (i, t) =>
            {
                float inhale = t < 0.3f ? Mathf.Sin(t / 0.3f * Mathf.PI) * 0.45f : 0f;
                float et = t - 0.38f;
                float exhale = et > 0f ? Mathf.Sin(Mathf.Clamp01(et / (len - 0.38f)) * Mathf.PI) : 0f;
                lp += (Noise() - lp) * (0.08f + variant * 0.02f);
                lp2 += (lp - lp2) * 0.3f;
                return lp2 * 3f * (inhale + exhale);
            });
        }

        public static AudioClip Heartbeat()
        {
            return Make("heart", 0.8f, (i, t) =>
            {
                float a = Mathf.Exp(-t * 25f);
                float bt = t - 0.22f;
                float b = bt > 0 ? Mathf.Exp(-bt * 25f) * 0.7f : 0f;
                return Mathf.Sin(2f * Mathf.PI * 50f * t) * (a + b);
            });
        }

        public static AudioClip Bell()
        {
            return Make("bell", 4f, (i, t) =>
            {
                float env = Mathf.Exp(-t * 1.1f);
                float s = Mathf.Sin(2f * Mathf.PI * 196f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 392.7f * t)
                          + 0.35f * Mathf.Sin(2f * Mathf.PI * 523f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 784f * t * 1.003f);
                return s * env;
            });
        }

        public static AudioClip CaseOpen()
        {
            return Make("case", 0.3f, (i, t) =>
            {
                float a = Mathf.Exp(-t * 70f);
                float bt = t - 0.12f;
                float b = bt > 0 ? Mathf.Exp(-bt * 70f) : 0f;
                return (Mathf.Sin(2f * Mathf.PI * 600f * t) * 0.5f + Noise() * 0.5f) * (a + b);
            });
        }

        /// <summary>Reproduce un clip 3D en una posicion con variacion de tono.</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariance = 0.06f, float maxDistance = 60f)
        {
            if (clip == null) return;
            GameObject go = new GameObject("SFX_" + clip.name);
            go.transform.position = position;
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;
            src.maxDistance = maxDistance;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, src.pitch) + 0.1f);
        }
    }
}
