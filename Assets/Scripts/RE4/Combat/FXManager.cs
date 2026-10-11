using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Efectos de combate con pools: fogonazo, trazadoras, sangre, polvo, casquillos, explosiones y manchas.
    /// </summary>
    public class FXManager : MonoBehaviour
    {
        public static FXManager Instance { get; private set; }

        public CombatFXLibrary library;
        public int maxDecals = 80;
        public int maxShells = 40;

        private ParticleSystem _blood;
        private ParticleSystem _bloodMist;
        private ParticleSystem _dust;
        private ParticleSystem _sparks;
        private ParticleSystem _smoke;
        private ParticleSystem _fire;

        private readonly Queue<Transform> _decals = new Queue<Transform>();
        private readonly Queue<Rigidbody> _shells = new Queue<Rigidbody>();
        private readonly List<LineRenderer> _tracers = new List<LineRenderer>();
        private Mesh _quadMesh;

        public static FXManager Get()
        {
            if (Instance == null)
            {
                Instance = FindAnyObjectByType<FXManager>();
                if (Instance == null)
                {
                    Instance = new GameObject("FXManager").AddComponent<FXManager>();
                }
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildSystems();
        }

        private Material Mat(Material m, Color fallback)
        {
            if (m != null) return m;
            Material created = new Material(Shader.Find("Sprites/Default"));
            created.color = fallback;
            return created;
        }

        private void BuildSystems()
        {
            CombatFXLibrary lib = library;
            _blood = CreateSystem("FX_Blood", Mat(lib ? lib.blood : null, new Color(0.45f, 0f, 0f)), 0.6f, 0.05f, 1.6f, new Color(0.5f, 0.02f, 0.02f, 1f), 0.9f);
            _bloodMist = CreateSystem("FX_BloodMist", Mat(lib ? lib.blood : null, new Color(0.4f, 0f, 0f)), 0.35f, 0.25f, 0.1f, new Color(0.45f, 0.03f, 0.03f, 0.7f), 0.1f, true);
            _dust = CreateSystem("FX_Dust", Mat(lib ? lib.dust : null, new Color(0.5f, 0.45f, 0.4f)), 0.8f, 0.18f, 0.2f, new Color(0.55f, 0.5f, 0.42f, 0.8f), 0.1f, true);
            _sparks = CreateSystem("FX_Sparks", Mat(lib ? lib.spark : null, new Color(1f, 0.8f, 0.3f)), 0.25f, 0.025f, 1f, new Color(1f, 0.85f, 0.4f, 1f), 1f);
            _smoke = CreateSystem("FX_Smoke", Mat(lib ? lib.smoke : null, new Color(0.2f, 0.2f, 0.2f)), 3f, 1.4f, -0.05f, new Color(0.18f, 0.17f, 0.16f, 0.7f), 0.05f, true);
            _fire = CreateSystem("FX_Fire", Mat(lib ? lib.fire : null, new Color(1f, 0.5f, 0.1f)), 0.6f, 1.2f, -0.2f, new Color(1f, 0.55f, 0.15f, 1f), 0.1f, true);

            _quadMesh = new Mesh();
            _quadMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            _quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            _quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quadMesh.RecalculateNormals();
        }

        private ParticleSystem CreateSystem(string name, Material mat, float lifetime, float size, float gravity, Color color, float drag, bool grow = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = 3000;
            main.startSpeed = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = drag > 0f;
            limit.dampen = drag;
            limit.limit = 0.5f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            if (grow)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
            }

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            ps.Play();
            return ps;
        }

        private static void Burst(ParticleSystem ps, Vector3 pos, Vector3 dir, int count, float speed, float cone)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 v = Vector3.Slerp(dir.normalized, Random.onUnitSphere, cone).normalized * speed * Random.Range(0.4f, 1f);
                ep.position = pos;
                ep.velocity = v;
                ep.applyShapeToPosition = false;
                ps.Emit(ep, 1);
            }
        }

        // ----------------------------------------------------------------- API

        public void MuzzleFlash(Transform muzzle, float scale)
        {
            if (muzzle == null) return;
            StartCoroutine(FlashRoutine(muzzle, scale));
            Burst(_smoke, muzzle.position, muzzle.forward, 2, 0.8f, 0.3f);
        }

        private IEnumerator FlashRoutine(Transform muzzle, float scale)
        {
            GameObject root = new GameObject("MuzzleFlash");
            root.transform.SetParent(muzzle, false);
            root.layer = muzzle.gameObject.layer;
            Material mat = Mat(library ? library.muzzleFlash : null, new Color(1f, 0.8f, 0.4f));

            for (int i = 0; i < 3; i++)
            {
                GameObject q = new GameObject("Q");
                q.layer = root.layer;
                q.transform.SetParent(root.transform, false);
                q.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
                MeshRenderer mr = q.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (i == 0)
                {
                    q.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                    q.transform.localScale = Vector3.one * 0.16f * scale;
                }
                else
                {
                    q.transform.localRotation = Quaternion.Euler(0f, 90f, i * 90f);
                    q.transform.localPosition = new Vector3(0f, 0f, 0.08f * scale);
                    q.transform.localScale = new Vector3(0.3f * scale, 0.1f * scale, 1f);
                }
            }

            Light l = root.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.38f);
            l.range = 9f * scale;
            l.intensity = 4.5f;
            l.shadows = LightShadows.None;

            yield return null;
            yield return new WaitForSeconds(0.035f);
            Destroy(root);
        }

        public void Tracer(Vector3 from, Vector3 to)
        {
            LineRenderer lr = null;
            foreach (var t in _tracers) if (!t.enabled) { lr = t; break; }
            if (lr == null)
            {
                GameObject go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.sharedMaterial = Mat(library ? library.tracer : null, new Color(1f, 0.9f, 0.6f));
                lr.startWidth = 0.02f;
                lr.endWidth = 0.008f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _tracers.Add(lr);
            }
            // Trazadora corta que viaja: solo un segmento del recorrido
            Vector3 dir = (to - from);
            float len = dir.magnitude;
            Vector3 start = from + dir * Random.Range(0.05f, 0.25f);
            Vector3 end = start + dir.normalized * Mathf.Min(len * 0.6f, 9f);
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.enabled = true;
            StartCoroutine(DisableNext(lr, 0.03f));
        }

        private IEnumerator DisableNext(LineRenderer lr, float t)
        {
            yield return new WaitForSeconds(t);
            if (lr != null) lr.enabled = false;
        }

        public void FleshImpact(Vector3 point, Vector3 shotDir, bool heavy)
        {
            int n = heavy ? 26 : 12;
            Burst(_blood, point, -shotDir + Random.insideUnitSphere * 0.3f, n, heavy ? 3.2f : 2.2f, 0.45f);
            Burst(_blood, point, shotDir, n / 2, 2.5f, 0.3f); // salida
            Burst(_bloodMist, point, -shotDir, heavy ? 6 : 3, 0.6f, 0.6f);

            // Mancha en la superficie detras del objetivo
            if (Physics.Raycast(point + shotDir * 0.2f, (shotDir + Vector3.down * 0.4f).normalized, out RaycastHit hit, 4f, RE4Layers.ShotMask, QueryTriggerInteraction.Ignore))
            {
                SpawnDecal(hit.point, hit.normal, Random.Range(0.35f, 0.8f) * (heavy ? 1.4f : 1f));
            }
        }

        public void SurfaceImpact(Vector3 point, Vector3 normal)
        {
            Burst(_dust, point + normal * 0.02f, normal, 5, 1.2f, 0.4f);
            Burst(_sparks, point + normal * 0.02f, normal, 6, 4f, 0.5f);
        }

        public void SpawnDecal(Vector3 point, Vector3 normal, float size)
        {
            Transform d;
            if (_decals.Count >= maxDecals)
            {
                d = _decals.Dequeue();
            }
            else
            {
                GameObject go = new GameObject("BloodDecal");
                go.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mat(library ? library.bloodDecal : null, new Color(0.3f, 0f, 0f, 0.9f));
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d = go.transform;
            }
            d.position = point + normal * 0.012f;
            d.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            d.localScale = new Vector3(size, size * Random.Range(0.7f, 1.2f), 1f);
            _decals.Enqueue(d);
        }

        public void EjectShell(Vector3 position, Vector3 right, Vector3 up, float size)
        {
            Rigidbody rb;
            if (_shells.Count >= maxShells)
            {
                rb = _shells.Dequeue();
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Shell";
                Destroy(go.GetComponent<Collider>());
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(1f, 1f, 1f);
                go.GetComponent<MeshRenderer>().sharedMaterial = Mat(library ? library.shellCasing : null, new Color(0.8f, 0.6f, 0.2f));
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.01f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                go.layer = 2; // Ignore Raycast
            }
            rb.transform.localScale = new Vector3(0.012f, 0.014f, 0.012f) * size;
            rb.transform.position = position;
            rb.transform.rotation = Random.rotation;
            rb.gameObject.SetActive(true);
            rb.linearVelocity = right * Random.Range(1.8f, 2.6f) + up * Random.Range(1.2f, 2f);
            rb.angularVelocity = Random.insideUnitSphere * 25f;
            _shells.Enqueue(rb);
            StartCoroutine(ShellSound(rb.transform));
        }

        private IEnumerator ShellSound(Transform shell)
        {
            yield return new WaitForSeconds(0.45f);
            if (shell != null) ProceduralAudio.PlayAt(ProceduralAudio.ShellDrop(), shell.position, 0.25f, 0.15f, 12f);
        }

        public void Explosion(Vector3 point, float radius)
        {
            Burst(_fire, point, Vector3.up, 60, radius * 1.4f, 0.9f);
            Burst(_smoke, point, Vector3.up, 40, radius * 0.6f, 0.8f);
            Burst(_sparks, point, Vector3.up, 60, radius * 3f, 1f);
            Burst(_dust, point, Vector3.up, 30, radius, 0.9f);
            AudioClip boom = library != null && library.sounds != null ? library.sounds.explosion : null;
            ProceduralAudio.PlayAt(boom != null ? boom : ProceduralAudio.Explosion(), point, 1f, 0.05f, 150f);
            StartCoroutine(ExplosionLight(point, radius));

            FirstPersonController fps = FirstPersonController.Instance;
            if (fps != null)
            {
                float d = Vector3.Distance(fps.transform.position, point);
                fps.AddShake(Mathf.Clamp01(1f - d / (radius * 6f)) * 1.4f);
            }
        }

        private IEnumerator ExplosionLight(Vector3 point, float radius)
        {
            GameObject go = new GameObject("ExplosionLight");
            go.transform.position = point + Vector3.up;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.25f);
            l.range = radius * 5f;
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                l.intensity = Mathf.Lerp(8f, 0f, t / 0.6f);
                yield return null;
            }
            Destroy(go);
        }
    }
}
