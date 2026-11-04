using System.Collections;
using DeadEcho.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Gameplay.RE4
{
    /// <summary>
    /// Atmosfera de terror del pueblo. Se crea sola en cualquier escena con Leon (FirstPersonController):
    ///  - Niebla: la de Unity (lejana) + niebla de altura a ras de suelo con jirones (HorrorLens).
    ///  - Cielo y luna mas oscuros y frios.
    ///  - Tormenta lejana: rayos que iluminan la niebla y truenos que llegan segundos despues.
    ///  - Polvo / ceniza flotando alrededor de la camara (se ve con la linterna).
    ///  - Miedo: sube con enemigos alerta cerca, poca vida y el asedio; aprieta la viñeta, quita color,
    ///    distorsiona los bordes y hace sonar el corazon.
    ///  - "Observadores": a veces una figura inmovil aparece en la niebla mirando a Leon y desaparece
    ///    cuando la miras o te acercas (con susurros).
    /// "Reducir movimiento" suaviza los destellos y quita el latido de los bordes.
    /// </summary>
    public class HorrorAtmosphere : MonoBehaviour
    {
        public static HorrorAtmosphere Instance { get; private set; }

        [Header("Niebla de Unity (lejana)")]
        public float unityFogDensity = 0.016f;
        public Color unityFogColor = new Color(0.07f, 0.078f, 0.09f);

        [Header("Tormenta")]
        public Vector2 lightningEvery = new Vector2(35f, 85f);
        public float lightningMoonIntensity = 2.2f;

        [Header("Miedo")]
        public float fearRange = 24f;

        [Header("Observadores en la niebla")]
        public bool watchers = true;
        public Vector2 watcherEvery = new Vector2(70f, 150f);
        public Vector2 watcherDistance = new Vector2(20f, 32f);

        /// <summary>Miedo actual 0..1.</summary>
        public float Fear { get; private set; }

        private FirstPersonController _fps;
        private Camera _cam;
        private HorrorLens _lens;
        private Light _moon;
        private float _moonBase;
        private Color _ambientSky, _ambientEq, _ambientGround;
        private float _nextLightning;
        private float _flash;
        private AudioSource _thunderSrc;
        private ParticleSystem _dust;
        private float _heartTimer;
        private float _heartPulse;
        private float _nextWatcher;
        private GameObject _watcher;
        private float _watcherSeen;
        private float _watcherBorn;
        private float _whisperTimer;
        private float _fogBreath;

        // ------------------------------------------------------------ Arranque automatico

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => TryCreate();

        private static void TryCreate()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<FirstPersonController>() == null) return;
            new GameObject("Atmosfera_Terror").AddComponent<HorrorAtmosphere>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_watcher != null) Destroy(_watcher);
        }

        private IEnumerator Start()
        {
            // esperar a que Leon y su camara esten listos
            while ((_fps = FirstPersonController.Instance) == null || _fps.ViewCamera == null) yield return null;
            _cam = _fps.ViewCamera;

            _lens = _cam.GetComponent<HorrorLens>();
            if (_lens == null) _lens = _cam.gameObject.AddComponent<HorrorLens>();
            _lens.fogBase = _fps.transform.position.y - 0.4f;

            SetupWorldLighting();
            _dust = CreateDust(_cam.transform);

            _thunderSrc = gameObject.AddComponent<AudioSource>();
            _thunderSrc.playOnAwake = false;
            _thunderSrc.spatialBlend = 0f;

            _nextLightning = Time.time + Random.Range(12f, 25f);
            _nextWatcher = Time.time + Random.Range(45f, 80f);
        }

        private void SetupWorldLighting()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = unityFogDensity;
            RenderSettings.fogColor = unityFogColor;

            _ambientSky = RenderSettings.ambientSkyColor;
            _ambientEq = RenderSettings.ambientEquatorColor;
            _ambientGround = RenderSettings.ambientGroundColor;

            _moon = RenderSettings.sun;
            if (_moon == null)
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { _moon = l; break; }
            }
            if (_moon != null)
            {
                _moon.color = Color.Lerp(_moon.color, new Color(0.5f, 0.6f, 0.9f), 0.5f);
                _moonBase = _moon.intensity;
            }

            // Cielo: copia (no tocar el material del proyecto) mas oscuro y del tono de la niebla
            Material sky = RenderSettings.skybox;
            if (sky != null)
            {
                sky = new Material(sky) { name = sky.name + " (Terror)" };
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", sky.GetFloat("_Exposure") * 0.65f);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", Color.Lerp(sky.GetColor("_SkyTint"), unityFogColor * 2f, 0.5f));
                RenderSettings.skybox = sky;
            }
        }

        // ------------------------------------------------------------ Bucle

        private void Update()
        {
            if (_fps == null || _lens == null) return;
            bool reduce = SettingsService.ReduceMotion;
            float dt = Time.deltaTime;

            // La niebla de altura sigue el suelo bajo Leon (colinas) y "respira" despacio
            _lens.fogBase = Mathf.Lerp(_lens.fogBase, _fps.transform.position.y - 0.4f, dt * 0.5f);
            _fogBreath = Mathf.PerlinNoise(Time.time * 0.03f, 4.2f);
            _lens.fogDensity = Mathf.Lerp(0.034f, 0.058f, _fogBreath);
            RenderSettings.fogDensity = unityFogDensity * Mathf.Lerp(0.85f, 1.2f, _fogBreath);

            UpdateFear(dt, reduce);
            UpdateLightning(dt, reduce);
            UpdateWatcher(dt);
        }

        // ------------------------------------------------------------ Miedo

        private void UpdateFear(float dt, bool reduce)
        {
            Vector3 p = _fps.transform.position;
            float threat = 0f;
            foreach (ZombieAI z in ZombieAI.All)
            {
                if (z == null || z.IsDead || z.State == GanadoState.Idle) continue;
                float d = Vector3.Distance(p, z.transform.position);
                threat = Mathf.Max(threat, 1f - Mathf.InverseLerp(3f, fearRange, d));
            }
            foreach (ZombieDogAI g in ZombieDogAI.All)
            {
                if (g == null || g.IsDead) continue;
                float d = Vector3.Distance(p, g.transform.position);
                threat = Mathf.Max(threat, (1f - Mathf.InverseLerp(3f, fearRange, d)) * 0.8f);
            }

            float target = threat;
            PlayerHealth ph = PlayerHealth.Instance;
            if (ph != null && !ph.IsDead)
            {
                if (ph.Status == HealthStatus.Danger) target += 0.45f;
                else if (ph.Status == HealthStatus.Caution) target += 0.2f;
            }
            if (HordeWaveManager.Instance != null && HordeWaveManager.Instance.SiegeActive) target += 0.2f;
            if (_watcher != null) target += 0.25f;
            target = Mathf.Clamp01(target);
            Fear = Mathf.MoveTowards(Fear, target, dt * (target > Fear ? 0.8f : 0.25f));

            // Latido: lo pone PlayerHealth en Peligro; aqui solo cuando el miedo es por los enemigos
            bool healthHeart = ph != null && ph.Status == HealthStatus.Danger;
            if (Fear > 0.45f && !healthHeart && (ph == null || !ph.IsDead))
            {
                _heartTimer -= dt;
                if (_heartTimer <= 0f)
                {
                    _heartTimer = Mathf.Lerp(1.15f, 0.62f, Fear);
                    _heartPulse = 1f;
                    ProceduralAudio.PlayAt(ProceduralAudio.Heartbeat(), _cam.transform.position, Mathf.Lerp(0.15f, 0.4f, Fear), 0f, 3f);
                }
            }
            else if (healthHeart)
            {
                // seguir el ritmo del latido de PlayerHealth (0.9 s)
                _heartTimer -= dt;
                if (_heartTimer <= 0f) { _heartTimer = 0.9f; _heartPulse = 1f; }
            }
            _heartPulse = Mathf.MoveTowards(_heartPulse, 0f, dt * 3.5f);

            // Lente
            _lens.chromaticAberration = 0.0035f + Fear * 0.009f + (reduce ? 0f : _heartPulse * Fear * 0.006f);
            _lens.vignette = 0.5f + Fear * 0.3f;
            _lens.desaturate = 0.12f + Fear * 0.33f;
            _lens.pulse = reduce ? 0f : _heartPulse * Fear;
        }

        // ------------------------------------------------------------ Tormenta

        private void UpdateLightning(float dt, bool reduce)
        {
            if (Time.time >= _nextLightning)
            {
                _nextLightning = Time.time + Random.Range(lightningEvery.x, lightningEvery.y);
                StartCoroutine(Lightning(reduce));
            }
            _lens.flash = _flash;

            if (_moon != null) _moon.intensity = Mathf.Lerp(_moonBase, lightningMoonIntensity, _flash);
            float amb = 1f + _flash * 4f;
            RenderSettings.ambientSkyColor = _ambientSky * amb;
            RenderSettings.ambientEquatorColor = _ambientEq * amb;
            RenderSettings.ambientGroundColor = _ambientGround * (1f + _flash * 2f);
        }

        private IEnumerator Lightning(bool reduce)
        {
            bool close = Random.value < 0.3f;
            float peak = (close ? 1f : 0.55f) * (reduce ? 0.35f : 1f);
            if (reduce)
            {
                // un solo destello suave (sin parpadeos)
                yield return Fade(0f, peak, 0.25f);
                yield return Fade(peak, 0f, 0.9f);
            }
            else
            {
                int strikes = Random.Range(2, 4);
                for (int i = 0; i < strikes; i++)
                {
                    _flash = peak * Random.Range(0.6f, 1f);
                    yield return new WaitForSeconds(Random.Range(0.04f, 0.09f));
                    _flash = peak * 0.15f;
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.14f));
                }
                _flash = peak;
                yield return Fade(peak, 0f, 0.6f);
            }
            _flash = 0f;

            // el sonido tarda en llegar
            yield return new WaitForSeconds(close ? Random.Range(0.2f, 0.7f) : Random.Range(1.5f, 4f));
            if (_thunderSrc != null)
            {
                _thunderSrc.pitch = Random.Range(0.85f, 1.05f);
                _thunderSrc.PlayOneShot(ProceduralAudio.Thunder(close ? 0 : Random.Range(1, 3)), close ? 0.85f : 0.5f);
            }
            if (close && _fps != null && !reduce) _fps.AddShake(0.12f);
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                _flash = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            _flash = to;
        }

        // ------------------------------------------------------------ Polvo y ceniza

        private static ParticleSystem CreateDust(Transform cam)
        {
            var go = new GameObject("Polvo_Ceniza");
            go.transform.SetParent(cam, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.72f, 0.65f, 0.45f), new Color(0.55f, 0.55f, 0.55f, 0.25f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 350;
            main.gravityModifier = 0.004f;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 7f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            Shader sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh) { name = "Polvo" };
            mat.mainTexture = SoftDot();
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
            return ps;
        }

        private static Texture2D SoftDot()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PuntoSuave" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------ Observadores

        private void UpdateWatcher(float dt)
        {
            if (!watchers) return;
            if (_watcher == null)
            {
                bool calm = Fear < 0.15f && !_fps.InputLocked &&
                            (HordeWaveManager.Instance == null || !HordeWaveManager.Instance.SiegeActive);
                if (Time.time < _nextWatcher || !calm) return;
                _nextWatcher = Time.time + Random.Range(watcherEvery.x, watcherEvery.y);
                TrySpawnWatcher();
                return;
            }

            Vector3 camPos = _cam.transform.position;
            Vector3 head = _watcher.transform.position + Vector3.up * 1.6f;
            float dist = Vector3.Distance(camPos, head);

            // Siempre mirando a Leon
            Vector3 flat = camPos - _watcher.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                _watcher.transform.rotation = Quaternion.Slerp(_watcher.transform.rotation, Quaternion.LookRotation(flat), dt * 2f);

            bool onScreen = IsVisible(head);
            if (onScreen) _watcherSeen += dt;

            // Susurros mientras esta ahi
            _whisperTimer -= dt;
            if (_whisperTimer <= 0f)
            {
                _whisperTimer = Random.Range(3f, 6f);
                ProceduralAudio.PlayAt(ProceduralAudio.Whisper(Random.Range(0, 3)), head, 0.35f, 0.1f, 45f);
            }

            bool tooClose = dist < 13f;
            bool stared = _watcherSeen > 1.4f;
            bool old = Time.time - _watcherBorn > 45f;
            if (tooClose || stared || old)
            {
                if (stared || tooClose)
                    ProceduralAudio.PlayAt(ProceduralAudio.Stinger(), camPos, 0.5f, 0f, 5f);
                Destroy(_watcher);
                _watcher = null;
            }
        }

        private bool IsVisible(Vector3 point)
        {
            Vector3 vp = _cam.WorldToViewportPoint(point);
            if (vp.z <= 0f || vp.x < 0.1f || vp.x > 0.9f || vp.y < 0.05f || vp.y > 0.95f) return false;
            Vector3 from = _cam.transform.position;
            Vector3 to = point - from;
            return !Physics.Raycast(from, to.normalized, to.magnitude - 0.8f, ~0, QueryTriggerInteraction.Ignore);
        }

        private void TrySpawnWatcher()
        {
            GameObject prefab = DeadEcho.UI.MenuShotsConfig.Current != null ? DeadEcho.UI.MenuShotsConfig.Current.shot1Main : null;
            if (prefab == null) return;
            Vector3 origin = _fps.transform.position;
            float yaw = _fps.transform.eulerAngles.y;

            for (int i = 0; i < 14; i++)
            {
                // a un lado o detras: al girarse, Leon lo encuentra alli
                float side = Random.value < 0.5f ? -1f : 1f;
                float a = yaw + side * Random.Range(65f, 150f);
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Vector3 pos = origin + dir * Random.Range(watcherDistance.x, watcherDistance.y);
                if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
                Vector3 head = hit.position + Vector3.up * 1.6f;
                Vector3 eye = _cam.transform.position;
                if (Physics.Raycast(eye, (head - eye).normalized, Vector3.Distance(eye, head) - 0.8f, ~0, QueryTriggerInteraction.Ignore))
                    continue; // tapado por una pared: no se veria
                SpawnWatcher(prefab, hit.position);
                return;
            }
        }

        private void SpawnWatcher(GameObject prefab, Vector3 pos)
        {
            var holder = new GameObject("tmp");
            holder.SetActive(false);
            GameObject inst = Instantiate(prefab, holder.transform);
            DeadEcho.UI.MenuCinematics.StripToActor(inst);
            foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Destroy(c);
            inst.transform.SetParent(null, false);
            Destroy(holder);

            inst.name = "Observador";
            inst.transform.position = pos;
            Vector3 flat = _fps.transform.position - pos;
            flat.y = 0f;
            inst.transform.rotation = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : Quaternion.identity;
            foreach (Animator an in inst.GetComponentsInChildren<Animator>(true))
            {
                an.applyRootMotion = false;
                an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            inst.SetActive(true);

            _watcher = inst;
            _watcherSeen = 0f;
            _watcherBorn = Time.time;
            _whisperTimer = Random.Range(1f, 2.5f);
        }
    }
}
