using System;
using System.Collections.Generic;
using System.Reflection;
using DeadEcho.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Tomas cinematicas del menu principal (F2, 01_DISENO_VISUAL.md y referencias/LEEME.md):
    ///  1. Figura en la niebla blanca: empuje de camara muy lento, bancos de niebla y ceniza.
    ///  2. Bosque de noche: luz de linterna que titila, neblina baja, el zombie respira.
    ///  3. Calle en llamas: brillo naranja que palpita, brasas subiendo y paneo lateral.
    /// Cada toma es un pequeño decorado 3D (modelos del juego sin IA) con su camara, que dibuja en una
    /// RenderTexture; la interfaz las muestra con fundido cruzado (10 s por toma, 1.5 s de fundido).
    /// Se detienen si la ventana pierde el foco y respetan "Reducir movimiento".
    /// </summary>
    public class MenuCinematics : MonoBehaviour
    {
        private class Shot
        {
            public string name;
            public GameObject set;
            public Camera cam;
            public RenderTexture rt;
            public CanvasGroup group;
            public RawImage image;
            public Transform model;
            public Vector3 camStart, camEnd, lookAt;
            public Color fogColor, ambient;
            public float fogDensity;
            public readonly List<Light> lights = new List<Light>();
            public readonly List<float> lightBase = new List<float>();
            public Image glow;
            public float t;
        }

        private readonly List<Shot> _shots = new List<Shot>();
        private int _current;
        private float _shotTime;
        private int _fadingTo = -1;
        private float _fadeT;
        private readonly List<Animator> _animators = new List<Animator>();
        private bool _focused = true;

        /// <summary>Mantener viva la toma 1 aunque no se vea (la usa el panel derecho de Opciones).</summary>
        public bool keepFirstShotAlive;

        public static MenuCinematics Instance { get; private set; }

        /// <summary>Imagen de la toma 1 (zombie en la niebla) para reutilizarla en Opciones.</summary>
        public Texture FogShotTexture => _shots.Count > 0 ? _shots[0].rt : null;

        // ------------------------------------------------------------ Creacion

        /// <summary>Crea las tomas y sus imagenes dentro de 'uiParent' (detras del menu).</summary>
        public static MenuCinematics Create(Transform uiParent)
        {
            var go = new GameObject("[TomasCinematicas]");
            var mc = go.AddComponent<MenuCinematics>();
            Instance = mc;
            RectTransform layer = UIFx.Stretch(UIFx.Rect(uiParent, "Tomas"));
            mc.Build(layer);
            return mc;
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= OnCameraPreCull;
            foreach (var s in _shots)
                if (s.rt != null) { s.rt.Release(); Destroy(s.rt); }
            if (Instance == this) Instance = null;
        }

        private void Build(RectTransform layer)
        {
            MenuShotsConfig cfg = MenuShotsConfig.Current;
            _shots.Add(BuildFogShot(layer, cfg));
            _shots.Add(BuildForestShot(layer, cfg));
            _shots.Add(BuildFireShot(layer, cfg));
            Camera.onPreCull += OnCameraPreCull;

            for (int i = 0; i < _shots.Count; i++)
            {
                _shots[i].group.alpha = i == 0 ? 1f : 0f;
                _shots[i].cam.enabled = i == 0;
            }
            _current = 0;
            _shotTime = 0f;
        }

        /// <summary>Niebla y luz ambiente propias de cada camara (el render Built-in las lee de RenderSettings).</summary>
        private void OnCameraPreCull(Camera cam)
        {
            foreach (var s in _shots)
            {
                if (s.cam != cam) continue;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = s.fogColor;
                RenderSettings.fogDensity = s.fogDensity;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = s.ambient;
                return;
            }
        }

        // ------------------------------------------------------------ Tomas

        private Shot NewShot(RectTransform layer, string name, int index, Color fog, float density, Color ambient)
        {
            var s = new Shot { name = name, fogColor = fog, fogDensity = density, ambient = ambient };
            s.set = new GameObject("Decorado_" + name);
            s.set.transform.SetParent(transform, false);
            s.set.transform.position = new Vector3(index * 400f, -300f, 0f); // lejos de todo y entre si

            // suelo
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Suelo";
            Destroy(ground.GetComponent<Collider>());
            ground.transform.SetParent(s.set.transform, false);
            ground.transform.localScale = new Vector3(12f, 1f, 12f);
            Renderer gr = ground.GetComponent<Renderer>();
            gr.material.color = new Color(0.16f, 0.16f, 0.15f);

            // camara → RenderTexture
            int w = Mathf.Clamp(Screen.width, 640, 1920);
            int h = Mathf.Clamp(Mathf.RoundToInt(w / Mathf.Max(1f, (float)Screen.width / Mathf.Max(1, Screen.height))), 360, 1080);
            s.rt = new RenderTexture(w, h, 24) { name = "RT_" + name, antiAliasing = 2 };
            var camGo = new GameObject("Camara_" + name);
            camGo.transform.SetParent(s.set.transform, false);
            s.cam = camGo.AddComponent<Camera>();
            s.cam.targetTexture = s.rt;
            s.cam.clearFlags = CameraClearFlags.SolidColor;
            s.cam.backgroundColor = fog;
            s.cam.fieldOfView = 38f;
            s.cam.nearClipPlane = 0.05f;
            s.cam.farClipPlane = 70f;
            s.cam.depth = -10;

            // imagen en la interfaz (con su grupo para el fundido, que incluye las capas de niebla/ceniza)
            RectTransform g = UIFx.Stretch(UIFx.Rect(layer, "Toma_" + name));
            s.group = g.gameObject.AddComponent<CanvasGroup>();
            s.group.blocksRaycasts = false;
            s.group.interactable = false;
            s.image = UIFx.Stretch(UIFx.Rect(g, "Imagen")).gameObject.AddComponent<RawImage>();
            s.image.texture = s.rt;
            s.image.raycastTarget = false;
            return s;
        }

        private Shot BuildFogShot(RectTransform layer, MenuShotsConfig cfg)
        {
            Shot s = NewShot(layer, "Niebla", 0, new Color(0.80f, 0.80f, 0.78f), 0.11f, new Color(0.62f, 0.62f, 0.6f));
            s.model = SpawnFigure(cfg.shot1Main, s.set.transform, new Vector3(0f, 0f, 0f), 196f);
            // figuras al fondo, medio borradas por la niebla
            SpawnFigure(cfg.shot1Background != null ? cfg.shot1Background : cfg.shot1Main, s.set.transform, new Vector3(-3.2f, 0f, 7.5f), 170f);
            SpawnFigure(cfg.shot1Main, s.set.transform, new Vector3(2.6f, 0f, 12f), 210f);
            Frame(s, 1.65f, 0.55f, 0.65f); // plano medio con empuje lento

            AddGrade(s.cam, 0.08f, 1.18f, Color.white, 1.02f);
            AddFog(s.group.transform, 3, 0.2f, new Color(1f, 1f, 1f, 1f), 0f, 1f);
            FallingAsh.Create(s.group.transform, 28, new Color(0.22f, 0.22f, 0.22f, 0.55f));
            return s;
        }

        private Shot BuildForestShot(RectTransform layer, MenuShotsConfig cfg)
        {
            Shot s = NewShot(layer, "Bosque", 1, new Color(0.035f, 0.045f, 0.06f), 0.085f, new Color(0.05f, 0.06f, 0.08f));
            s.model = SpawnFigure(cfg.shot2Main, s.set.transform, Vector3.zero, 172f);

            // troncos de arboles (siluetas en la oscuridad)
            var rnd = new System.Random(7);
            for (int i = 0; i < 46; i++)
            {
                float x = (float)(rnd.NextDouble() * 34 - 17), z = (float)(rnd.NextDouble() * 30 - 6);
                if (Mathf.Abs(x) < 1.6f && z < 3f) continue;       // no tapar al protagonista
                if (z < -2f && Mathf.Abs(x) < 4f) continue;          // ni a la camara
                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(trunk.GetComponent<Collider>());
                trunk.transform.SetParent(s.set.transform, false);
                float r = (float)(0.16 + rnd.NextDouble() * 0.28);
                trunk.transform.localPosition = new Vector3(x, 6f, z);
                trunk.transform.localScale = new Vector3(r, 6f, r);
                trunk.transform.localRotation = Quaternion.Euler((float)(rnd.NextDouble() * 6 - 3), 0f, (float)(rnd.NextDouble() * 6 - 3));
                trunk.GetComponent<Renderer>().material.color = new Color(0.06f, 0.06f, 0.055f);
            }
            Frame(s, 2.0f, 0.62f, 0.25f);

            // linterna / luna: foco que titila de forma irregular
            Transform setT = s.set.transform;
            Light spot = AddLight(s, LightType.Spot, setT.InverseTransformPoint(s.camStart + new Vector3(0.6f, 0.9f, 0f)), new Color(0.78f, 0.86f, 1f), 3.2f, 26f);
            spot.spotAngle = 34f;
            spot.transform.LookAt(s.lookAt + Vector3.right * 0.8f);
            spot.shadows = LightShadows.Soft;
            AddLight(s, LightType.Point, setT.InverseTransformPoint(s.lookAt + new Vector3(-1f, 4f, 6f)), new Color(0.35f, 0.42f, 0.6f), 0.8f, 14f);

            AddGrade(s.cam, 0.3f, 1.12f, new Color(0.88f, 0.94f, 1.05f), 1.1f);
            AddFog(s.group.transform, 2, 0.16f, new Color(0.7f, 0.75f, 0.85f, 1f), 0f, 0.42f); // neblina baja
            return s;
        }

        private Shot BuildFireShot(RectTransform layer, MenuShotsConfig cfg)
        {
            Shot s = NewShot(layer, "Fuego", 2, new Color(0.10f, 0.045f, 0.02f), 0.06f, new Color(0.10f, 0.05f, 0.03f));
            s.model = SpawnFigure(cfg.shot3Main, s.set.transform, Vector3.zero, 185f);

            // calle: fachadas oscuras a los dos lados
            var rnd = new System.Random(3);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = -4f;
                while (z < 34f)
                {
                    float depth = (float)(4 + rnd.NextDouble() * 4), height = (float)(4 + rnd.NextDouble() * 7);
                    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(b.GetComponent<Collider>());
                    b.transform.SetParent(s.set.transform, false);
                    b.transform.localPosition = new Vector3(side * (5.5f + (float)rnd.NextDouble()), height * 0.5f, z + depth * 0.5f);
                    b.transform.localScale = new Vector3(3f, height, depth - 0.3f);
                    b.GetComponent<Renderer>().material.color = new Color(0.09f, 0.08f, 0.075f);
                    z += depth;
                }
            }
            Frame(s, 2.3f, 0.6f, 0.2f);

            // fuego: luces naranjas que palpitan detras y a los lados
            AddLight(s, LightType.Point, new Vector3(-3.8f, 1.2f, 4f), new Color(1f, 0.45f, 0.12f), 3.2f, 13f);
            AddLight(s, LightType.Point, new Vector3(4.2f, 2.5f, 9f), new Color(1f, 0.38f, 0.08f), 3.6f, 15f);
            AddLight(s, LightType.Point, new Vector3(0.5f, 0.6f, -1.5f), new Color(1f, 0.5f, 0.2f), 1.4f, 6f);

            AddGrade(s.cam, 0.65f, 1.12f, new Color(1.05f, 0.95f, 0.88f), 1.05f);
            // brillo naranja sobre el fondo y brasas subiendo
            s.glow = UIFx.Image(s.group.transform, "BrilloFuego", UITheme.Current.vineta, new Color(1f, 0.48f, 0.12f, 0.1f));
            UIFx.Stretch(s.glow.rectTransform);
            s.glow.rectTransform.localScale = new Vector3(-1f, -1f, 1f);
            FallingAsh.Create(s.group.transform, 34, new Color(1f, 0.55f, 0.18f, 0.85f), true);
            return s;
        }

        // ------------------------------------------------------------ Ayudas de decorado

        /// <summary>Encuadre: protagonista en el tercio derecho (el menu queda a la izquierda).</summary>
        private static void Frame(Shot s, float distanceFactor, float eyeFactor, float push)
        {
            float h = 1.8f;
            Vector3 center = s.set.transform.position;
            if (s.model != null && TryBounds(s.model, out Bounds b))
            {
                h = Mathf.Clamp(b.size.y, 1f, 4f);
                center = new Vector3(b.center.x, s.set.transform.position.y, b.center.z);
            }
            float dist = h * distanceFactor;
            Vector3 target = center + Vector3.up * h * 0.74f;
            s.camStart = center + new Vector3(0f, h * eyeFactor + 0.2f, -dist);
            s.camEnd = s.camStart + new Vector3(0f, 0f, dist * push * 0.35f);
            // mirar un poco a la izquierda del modelo → el modelo aparece a la derecha
            s.lookAt = target + Vector3.left * dist * 0.24f;
            s.cam.transform.position = s.camStart;
            s.cam.transform.LookAt(s.lookAt);
        }

        private static bool TryBounds(Transform root, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }

        private static Light AddLight(Shot s, LightType type, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject("Luz");
            go.transform.SetParent(s.set.transform, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            s.lights.Add(l);
            s.lightBase.Add(intensity);
            return l;
        }

        private static void AddGrade(Camera cam, float saturation, float contrast, Color tint, float exposure)
        {
            ScreenGrade g = ScreenGrade.On(cam);
            g.followSettings = false; // el brillo de Opciones no cambia los menus
            g.saturation = saturation;
            g.contrast = contrast;
            g.tint = tint;
            g.exposure = exposure;
        }

        /// <summary>Capas de niebla 2D que cruzan la toma (entre yMin e yMax de la pantalla).</summary>
        private static void AddFog(Transform parent, int layers, float alpha, Color color, float yMin, float yMax)
        {
            for (int i = 0; i < layers; i++)
            {
                RawImage fog = UIFx.Rect(parent, "Niebla" + i).gameObject.AddComponent<RawImage>();
                fog.rectTransform.anchorMin = new Vector2(0f, yMin);
                fog.rectTransform.anchorMax = new Vector2(1f, yMax);
                fog.rectTransform.offsetMin = fog.rectTransform.offsetMax = Vector2.zero;
                fog.texture = DEBuild.FogTexture(256, 128, 2.2f + i * 1.4f, 40 + i * 9, 1.3f);
                fog.uvRect = new Rect(0f, 0f, 1f + i * 0.35f, 1f);
                fog.color = color;
                fog.raycastTarget = false;
                var d = fog.gameObject.AddComponent<FogDrift>();
                d.speed = new Vector2(0.005f + i * 0.004f, 0f) * (i % 2 == 0 ? 1f : -1f);
                d.baseAlpha = alpha + i * 0.05f;
                d.alphaPulse = 0.05f;
            }
        }

        // ------------------------------------------------------------ Modelos sin IA

        /// <summary>
        /// Instancia un modelo del juego como "actor" quieto: se crea desactivado (no se ejecuta ningun Awake),
        /// se le quitan scripts, IA, navegacion y sonidos, y la fisica queda cinematica. Solo queda el Animator
        /// en su estado por defecto (reposo). Sin modelo, se pone una silueta sencilla.
        /// </summary>
        private Transform SpawnFigure(GameObject prefab, Transform parent, Vector3 localPos, float yaw)
        {
            GameObject inst;
            if (prefab != null)
            {
                var holder = new GameObject("tmp");
                holder.SetActive(false);
                inst = Instantiate(prefab, holder.transform);
                StripToActor(inst);
                inst.transform.SetParent(parent, false);
                Destroy(holder);
            }
            else inst = Silhouette(parent);

            inst.name = "Figura";
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (Animator a in inst.GetComponentsInChildren<Animator>(true))
            {
                a.applyRootMotion = false;
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // cada figura empieza en un punto distinto del reposo
                a.Update(UnityEngine.Random.Range(0f, 3f));
                _animators.Add(a);
            }
            inst.SetActive(true);
            return inst.transform;
        }

        internal static void StripToActor(GameObject root)
        {
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            // varias pasadas: un componente solo se quita cuando ya nadie lo requiere ([RequireComponent])
            for (int pass = 0; pass < 6; pass++)
            {
                bool removed = false;
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || !IsRemovable(c)) continue;
                    if (IsRequiredByOthers(c)) continue;
                    DestroyImmediate(c);
                    removed = true;
                }
                if (!removed) break;
            }
        }

        private static bool IsRemovable(Component c)
        {
            return c is MonoBehaviour || c is AudioSource || c is UnityEngine.AI.NavMeshAgent || c is UnityEngine.AI.NavMeshObstacle
                   || c is CharacterController;
        }

        private static bool IsRequiredByOthers(Component target)
        {
            Type t = target.GetType();
            foreach (Component other in target.GetComponents<Component>())
            {
                if (other == null || other == target) continue;
                foreach (RequireComponent req in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if (Requires(req.m_Type0, t) || Requires(req.m_Type1, t) || Requires(req.m_Type2, t)) return true;
                }
            }
            return false;
        }

        private static bool Requires(Type required, Type actual) => required != null && required.IsAssignableFrom(actual);

        /// <summary>Figura de reserva: capsula y cabeza oscuras (se lee como silueta en la niebla).</summary>
        private static GameObject Silhouette(Transform parent)
        {
            var root = new GameObject("Silueta");
            root.transform.SetParent(parent, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.8f, 0.35f);
            body.transform.localRotation = Quaternion.Euler(6f, 0f, 4f);
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0.05f, 1.62f, 0.06f);
            head.transform.localScale = Vector3.one * 0.26f;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) r.material.color = new Color(0.05f, 0.05f, 0.05f);
            return root;
        }

        // ------------------------------------------------------------ Animacion

        private void Update()
        {
            // se pausan si la ventana pierde el foco
            bool focused = Application.isFocused;
            if (focused != _focused)
            {
                _focused = focused;
                foreach (Animator a in _animators) if (a != null) a.speed = focused ? 1f : 0f;
            }
            if (!focused) return;

            float dt = Time.unscaledDeltaTime;
            MenuShotsConfig cfg = MenuShotsConfig.Current;
            bool reduce = SettingsService.ReduceMotion;

            // rotacion de tomas con fundido cruzado
            _shotTime += dt;
            if (_fadingTo < 0 && _shotTime >= cfg.shotSeconds) StartFade((_current + 1) % _shots.Count);
            if (_fadingTo >= 0)
            {
                _fadeT += dt / Mathf.Max(0.01f, reduce ? 0.4f : cfg.crossfadeSeconds);
                float k = UIFx.Ease(Mathf.Clamp01(_fadeT));
                _shots[_fadingTo].group.alpha = k;
                _shots[_current].group.alpha = 1f - k;
                if (_fadeT >= 1f)
                {
                    _shots[_current].group.alpha = 0f;
                    _current = _fadingTo;
                    _fadingTo = -1;
                    _shotTime = 0f;
                }
            }

            for (int i = 0; i < _shots.Count; i++)
            {
                Shot s = _shots[i];
                bool visible = i == _current || i == _fadingTo;
                s.cam.enabled = visible || (i == 0 && keepFirstShotAlive);
                if (!s.cam.enabled) continue;
                s.t += dt;
                Animate(i, s, reduce);
            }
        }

        private void StartFade(int next)
        {
            _fadingTo = next;
            _fadeT = 0f;
            _shots[next].t = 0f; // la toma entrante empieza su movimiento desde el principio
        }

        private void Animate(int index, Shot s, bool reduce)
        {
            float shotLen = Mathf.Max(1f, MenuShotsConfig.Current.shotSeconds + MenuShotsConfig.Current.crossfadeSeconds);
            float k = reduce ? 0f : Mathf.Clamp01(s.t / shotLen);
            Transform cam = s.cam.transform;
            switch (index)
            {
                case 0: // empuje de camara muy lento
                    cam.position = Vector3.Lerp(s.camStart, s.camEnd, k);
                    cam.LookAt(s.lookAt);
                    break;
                case 1: // el zombie respira (±2 % en 5 s) y la linterna titila
                    cam.position = Vector3.Lerp(s.camStart, s.camEnd, k * 0.5f);
                    cam.LookAt(s.lookAt);
                    if (s.model != null)
                    {
                        float breath = reduce ? 1f : 1f + Mathf.Sin(s.t * Mathf.PI * 2f / 5f) * 0.02f;
                        s.model.localScale = new Vector3(1f, breath, 1f);
                    }
                    if (s.lights.Count > 0)
                    {
                        float n = Mathf.PerlinNoise(s.t * 3.1f, 0.3f);
                        float drop = n < 0.28f ? 0.35f : 1f; // caidas bruscas de luz
                        s.lights[0].intensity = s.lightBase[0] * (reduce ? 1f : Mathf.Lerp(0.75f, 1.1f, n) * drop);
                    }
                    break;
                case 2: // paneo lateral y fuego que palpita
                    Vector3 side = Vector3.right * Mathf.Lerp(-0.9f, 0.9f, k);
                    cam.position = s.camStart + side;
                    cam.LookAt(s.lookAt + side * 0.6f);
                    for (int l = 0; l < s.lights.Count; l++)
                    {
                        float n = Mathf.PerlinNoise(s.t * (2.2f + l), l * 7.7f);
                        s.lights[l].intensity = s.lightBase[l] * (reduce ? 1f : Mathf.Lerp(0.6f, 1.25f, n));
                    }
                    if (s.glow != null)
                        s.glow.color = new Color(1f, 0.48f, 0.12f, reduce ? 0.1f : Mathf.Lerp(0.05f, 0.16f, Mathf.PerlinNoise(s.t * 1.7f, 2f)));
                    break;
            }
        }
    }
}
