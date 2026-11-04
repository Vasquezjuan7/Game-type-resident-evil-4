using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Lente de terror en la camara del juego (render Built-in): niebla de altura con jirones, aberracion
    /// cromatica, viñeta, tono frio/calido y efectos de miedo. Los valores los mueve HorrorAtmosphere.
    /// Shader: Resources/DeadEcho/Shaders/HorrorLens. Si no esta disponible, la imagen pasa sin cambios.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class HorrorLens : MonoBehaviour
    {
        [Header("Niebla de altura")]
        public bool heightFog = true;
        public Color fogColor = new Color(0.075f, 0.082f, 0.095f);
        public float fogDensity = 0.045f;
        public float fogFalloff = 0.32f;
        public float fogBase = 0f;
        [Range(0f, 1f)] public float fogMax = 0.93f;
        [Range(0f, 1f)] public float fogScatter = 0.22f;
        public float skyDistance = 140f;
        public Vector2 wind = new Vector2(0.35f, 0.12f);
        public float wispScale = 0.09f;
        [Range(0f, 1f)] public float wispStrength = 0.75f;

        [Header("Lente")]
        public float chromaticAberration = 0.004f;
        [Range(0f, 1f)] public float vignette = 0.55f;
        public float vignetteRadius = 0.95f;
        [Range(0f, 1f)] public float desaturate = 0.12f;
        public Color shadowTint = new Color(0.86f, 0.97f, 1.08f);
        public Color highlightTint = new Color(1.08f, 1.0f, 0.9f);

        [Header("Dinamico (lo fija HorrorAtmosphere)")]
        [Range(0f, 1f)] public float flash;
        [Range(0f, 1f)] public float pulse;

        private static Shader _shader;
        private Material _mat;
        private Camera _cam;
        private readonly Vector3[] _corners = new Vector3[4];

        private static readonly int IdRayBL = Shader.PropertyToID("_RayBL");
        private static readonly int IdRayBR = Shader.PropertyToID("_RayBR");
        private static readonly int IdRayTL = Shader.PropertyToID("_RayTL");
        private static readonly int IdRayTR = Shader.PropertyToID("_RayTR");
        private static readonly int IdCamPos = Shader.PropertyToID("_CamPos");
        private static readonly int IdFogOn = Shader.PropertyToID("_FogOn");
        private static readonly int IdFogColor = Shader.PropertyToID("_FogColor");
        private static readonly int IdFogDensity = Shader.PropertyToID("_FogDensity");
        private static readonly int IdFogFalloff = Shader.PropertyToID("_FogFalloff");
        private static readonly int IdFogBase = Shader.PropertyToID("_FogBase");
        private static readonly int IdFogMax = Shader.PropertyToID("_FogMax");
        private static readonly int IdFogScatter = Shader.PropertyToID("_FogScatter");
        private static readonly int IdSkyDistance = Shader.PropertyToID("_SkyDistance");
        private static readonly int IdWind = Shader.PropertyToID("_Wind");
        private static readonly int IdFlash = Shader.PropertyToID("_Flash");
        private static readonly int IdCA = Shader.PropertyToID("_CA");
        private static readonly int IdVignette = Shader.PropertyToID("_Vignette");
        private static readonly int IdVignetteRadius = Shader.PropertyToID("_VignetteRadius");
        private static readonly int IdDesat = Shader.PropertyToID("_Desat");
        private static readonly int IdPulse = Shader.PropertyToID("_Pulse");
        private static readonly int IdShadowTint = Shader.PropertyToID("_ShadowTint");
        private static readonly int IdHighTint = Shader.PropertyToID("_HighTint");

        private void OnEnable()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null) _cam.depthTextureMode |= DepthTextureMode.Depth;
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        private bool EnsureMaterial()
        {
            if (_mat != null) return true;
            if (_shader == null) _shader = Resources.Load<Shader>("DeadEcho/Shaders/HorrorLens");
            if (_shader == null || !_shader.isSupported) return false;
            _mat = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_cam == null || !EnsureMaterial())
            {
                Graphics.Blit(src, dst);
                return;
            }

            // Rayos de las esquinas: BL, TL, TR, BR (orden de CalculateFrustumCorners)
            _cam.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), _cam.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, _corners);
            Transform t = _cam.transform;
            _mat.SetVector(IdRayBL, t.TransformVector(_corners[0]));
            _mat.SetVector(IdRayTL, t.TransformVector(_corners[1]));
            _mat.SetVector(IdRayTR, t.TransformVector(_corners[2]));
            _mat.SetVector(IdRayBR, t.TransformVector(_corners[3]));
            _mat.SetVector(IdCamPos, t.position);

            _mat.SetFloat(IdFogOn, heightFog ? 1f : 0f);
            _mat.SetColor(IdFogColor, fogColor);
            _mat.SetFloat(IdFogDensity, fogDensity);
            _mat.SetFloat(IdFogFalloff, Mathf.Max(0.01f, fogFalloff));
            _mat.SetFloat(IdFogBase, fogBase);
            _mat.SetFloat(IdFogMax, fogMax);
            _mat.SetFloat(IdFogScatter, fogScatter);
            _mat.SetFloat(IdSkyDistance, skyDistance);
            _mat.SetVector(IdWind, new Vector4(wind.x, wind.y, wispScale, wispStrength));
            _mat.SetFloat(IdFlash, flash);
            _mat.SetFloat(IdCA, chromaticAberration);
            _mat.SetFloat(IdVignette, vignette);
            _mat.SetFloat(IdVignetteRadius, vignetteRadius);
            _mat.SetFloat(IdDesat, desaturate);
            _mat.SetFloat(IdPulse, pulse);
            _mat.SetColor(IdShadowTint, shadowTint);
            _mat.SetColor(IdHighTint, highlightTint);
            Graphics.Blit(src, dst, _mat);
        }
    }
}
