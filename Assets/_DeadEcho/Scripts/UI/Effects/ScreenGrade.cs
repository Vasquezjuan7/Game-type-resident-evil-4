using DeadEcho.Core;
using UnityEngine;

namespace DeadEcho.UI
{
    /// <summary>
    /// Ajuste de imagen en la camara (render Built-in, OnRenderImage): brillo, grano de pelicula y modo
    /// daltonico de Opciones, y ademas saturacion / contraste / tinte para las tomas cinematicas.
    /// Si el shader no esta disponible, la imagen pasa sin cambios.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ScreenGrade : MonoBehaviour
    {
        [Tooltip("Lee brillo, grano y modo daltonico de SettingsService.")]
        public bool followSettings = true;
        [Tooltip("Grano con Grano de pelicula = 100 (en el juego mas suave que en los menus).")]
        public float grainAtMax = 0.09f;

        [Header("Look (tomas cinematicas)")]
        [Range(0f, 1.5f)] public float saturation = 1f;
        [Range(0.5f, 1.8f)] public float contrast = 1f;
        public Color tint = Color.white;
        [Tooltip("Brillo propio, se multiplica por el de Opciones.")]
        public float exposure = 1f;
        [Tooltip("Grano propio (se suma al de Opciones).")]
        public float extraGrain;

        private static Shader _shader;
        private Material _mat;
        private float _seed;
        private float _nextSeed;

        private static readonly int IdBrightness = Shader.PropertyToID("_Brightness");
        private static readonly int IdGamma = Shader.PropertyToID("_Gamma");
        private static readonly int IdSaturation = Shader.PropertyToID("_Saturation");
        private static readonly int IdContrast = Shader.PropertyToID("_Contrast");
        private static readonly int IdGrain = Shader.PropertyToID("_Grain");
        private static readonly int IdSeed = Shader.PropertyToID("_GrainSeed");
        private static readonly int IdTint = Shader.PropertyToID("_Tint");
        private static readonly int IdRow0 = Shader.PropertyToID("_Row0");
        private static readonly int IdRow1 = Shader.PropertyToID("_Row1");
        private static readonly int IdRow2 = Shader.PropertyToID("_Row2");

        /// <summary>Añade (o devuelve) el ajuste de imagen de una camara.</summary>
        public static ScreenGrade On(Camera cam)
        {
            if (cam == null) return null;
            var g = cam.GetComponent<ScreenGrade>();
            return g != null ? g : cam.gameObject.AddComponent<ScreenGrade>();
        }

        /// <summary>¿Hace falta el efecto? (si todo esta neutro se ahorra el pase a pantalla completa)</summary>
        public bool IsNeutral()
        {
            GameSettings s = SettingsService.Current;
            bool settingsNeutral = !followSettings || (s.brightness == 50 && (s.filmGrain == 0 || s.reduceMotion) && s.colorBlindMode == 0);
            return settingsNeutral && Mathf.Approximately(saturation, 1f) && Mathf.Approximately(contrast, 1f)
                   && tint == Color.white && Mathf.Approximately(exposure, 1f) && extraGrain <= 0f;
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        private bool EnsureMaterial()
        {
            if (_mat != null) return true;
            if (_shader == null) _shader = Resources.Load<Shader>("DeadEcho/Shaders/ScreenGrade");
            if (_shader == null || !_shader.isSupported) return false;
            _mat = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (IsNeutral() || !EnsureMaterial())
            {
                Graphics.Blit(src, dst);
                return;
            }

            GameSettings s = SettingsService.Current;
            float b = followSettings ? (s.brightness - 50f) / 50f : 0f; // -1..1
            _mat.SetFloat(IdBrightness, (1f + b * 0.35f) * exposure);
            _mat.SetFloat(IdGamma, 1f - b * 0.22f);
            _mat.SetFloat(IdSaturation, saturation);
            _mat.SetFloat(IdContrast, contrast);
            _mat.SetColor(IdTint, tint);

            // grano: 24 imagenes por segundo; quieto (apagado) con Reducir movimiento
            float grain = s.reduceMotion ? 0f : (followSettings ? s.filmGrain / 100f * grainAtMax : 0f) + extraGrain;
            if (Time.unscaledTime >= _nextSeed)
            {
                _nextSeed = Time.unscaledTime + 1f / 24f;
                _seed = Random.Range(0f, 1000f);
            }
            _mat.SetFloat(IdGrain, grain);
            _mat.SetFloat(IdSeed, _seed);

            Matrix4x4 m = ColorBlindMatrix(followSettings ? s.colorBlindMode : 0);
            _mat.SetVector(IdRow0, m.GetRow(0));
            _mat.SetVector(IdRow1, m.GetRow(1));
            _mat.SetVector(IdRow2, m.GetRow(2));
            Graphics.Blit(src, dst, _mat);
        }

        // ------------------------------------------------------------ Daltonismo

        /// <summary>
        /// Correccion "daltonize": se simula la deficiencia (matrices de Machado et al. 2009, severidad 1)
        /// y la informacion de color que se pierde se desplaza a canales que si se distinguen.
        /// </summary>
        public static Matrix4x4 ColorBlindMatrix(int mode)
        {
            if (mode <= 0) return Matrix4x4.identity;
            Matrix4x4 sim = Matrix4x4.identity;
            switch (mode)
            {
                case 1: // protanopia
                    sim = Rows(0.152286f, 1.052583f, -0.204868f, 0.114503f, 0.786281f, 0.099216f, -0.003882f, -0.048116f, 1.051998f);
                    break;
                case 2: // deuteranopia
                    sim = Rows(0.367322f, 0.860646f, -0.227968f, 0.280085f, 0.672501f, 0.047413f, -0.011820f, 0.042940f, 0.968881f);
                    break;
                default: // tritanopia
                    sim = Rows(1.255528f, -0.076749f, -0.178779f, -0.078411f, 0.930809f, 0.147602f, 0.004733f, 0.691367f, 0.303900f);
                    break;
            }
            Matrix4x4 shift = Rows(0f, 0f, 0f, 0.7f, 1f, 0f, 0.7f, 0f, 1f);
            // resultado = I + shift * (I - sim)
            Matrix4x4 err = Sub(Matrix4x4.identity, sim);
            Matrix4x4 corr = Add(Matrix4x4.identity, shift * err);
            corr.m33 = 1f;
            return corr;
        }

        private static Matrix4x4 Rows(float a, float b, float c, float d, float e, float f, float g, float h, float i)
        {
            var m = Matrix4x4.identity;
            m.SetRow(0, new Vector4(a, b, c, 0f));
            m.SetRow(1, new Vector4(d, e, f, 0f));
            m.SetRow(2, new Vector4(g, h, i, 0f));
            m.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
            return m;
        }

        private static Matrix4x4 Sub(Matrix4x4 a, Matrix4x4 b)
        {
            var r = new Matrix4x4();
            for (int i = 0; i < 16; i++) r[i] = a[i] - b[i];
            return r;
        }

        private static Matrix4x4 Add(Matrix4x4 a, Matrix4x4 b)
        {
            var r = new Matrix4x4();
            for (int i = 0; i < 16; i++) r[i] = a[i] + b[i];
            return r;
        }
    }
}
