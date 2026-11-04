using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// El mercader encapuchado de RE4. Junto a su farol se puede hablar con el [E] para mejorar las armas.
    /// Si no tiene modelo, se construye uno sencillo por codigo (abrigo, capucha, ojos brillantes y farol).
    /// </summary>
    public class Merchant : MonoBehaviour
    {
        public static readonly List<Merchant> All = new List<Merchant>();

        public float talkRange = 2.8f;
        [Tooltip("Modelo del mercader en Resources (convertido de 'The Shrewd Scoundrel'). Vacio = figura sencilla.")]
        public string modelResource = "RE4Modelos/Mercader/Mercader";
        [Tooltip("Giro del modelo si no mira de frente al jugador.")]
        public float modelYaw = 0f;

        private Light _lantern;
        private float _baseIntensity;
        private static Material _modelMat;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start()
        {
            if (GetComponentsInChildren<Renderer>().Length == 0 && !LoadModel()) BuildModel();
            _lantern = GetComponentInChildren<Light>();
            if (_lantern != null) _baseIntensity = _lantern.intensity;
        }

        private void Update()
        {
            if (_lantern != null)
            {
                float n = Mathf.PerlinNoise(Time.time * 3f, transform.position.x);
                _lantern.intensity = _baseIntensity * (0.8f + n * 0.4f);
            }

            // Mira al jugador cuando esta cerca
            Transform player = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;
            if (player == null) return;
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            if (to.magnitude < 9f && to.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 2f);
            }
        }

        public bool InRange(Vector3 playerPos)
        {
            Vector3 to = transform.position - playerPos;
            to.y = 0f;
            return to.magnitude <= talkRange;
        }

        public void Talk(GameObject player)
        {
            WeaponUpgradeUI ui = player.GetComponent<WeaponUpgradeUI>();
            if (ui == null) ui = player.AddComponent<WeaponUpgradeUI>();
            ui.Open();
        }

        // ------------------------------------------------------------ Modelo descargado

        /// <summary>
        /// Pone el modelo del mercader (Resources). Mide 1,80 m y ya viene con los pies en el suelo.
        /// Se le añaden la caja de mercancia, el farol azul y la colision.
        /// </summary>
        private bool LoadModel()
        {
            if (string.IsNullOrEmpty(modelResource)) return false;
            GameObject prefab = Resources.Load<GameObject>(modelResource);
            if (prefab == null) return false;

            GameObject model = Instantiate(prefab, transform);
            model.name = "Modelo_Mercader";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);

            if (_modelMat == null)
            {
                _modelMat = new Material(Shader.Find("Standard")) { name = "Mercader" };
                Texture2D color = Resources.Load<Texture2D>(modelResource.Substring(0, modelResource.LastIndexOf('/') + 1) + "Mercader_Color");
                Texture2D normal = Resources.Load<Texture2D>(modelResource.Substring(0, modelResource.LastIndexOf('/') + 1) + "Mercader_Normal");
                if (color != null) _modelMat.mainTexture = color;
                if (normal != null)
                {
                    _modelMat.SetTexture("_BumpMap", normal);
                    _modelMat.EnableKeyword("_NORMALMAP");
                }
                _modelMat.SetFloat("_Glossiness", 0.22f);
                _modelMat.SetFloat("_Metallic", 0f);
            }
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = _modelMat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            BuildCrateAndLantern(new Vector3(0.95f, 0f, 0.25f));
            return true;
        }

        /// <summary>Caja de mercancia con el farol azul encima, y la colision del mercader.</summary>
        private void BuildCrateAndLantern(Vector3 crate)
        {
            Part(PrimitiveType.Cube, crate + new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.6f, 0.5f), new Vector3(0f, 15f, 0f), new Color(0.35f, 0.24f, 0.13f));
            GameObject lamp = Part(PrimitiveType.Cylinder, crate + new Vector3(0f, 0.72f, 0f), new Vector3(0.14f, 0.12f, 0.14f), Vector3.zero, new Color(1f, 0.7f, 0.3f));
            Renderer lr = lamp.GetComponent<Renderer>();
            lr.material.EnableKeyword("_EMISSION");
            lr.material.SetColor("_EmissionColor", new Color(0.35f, 0.55f, 1f) * 2.5f);
            Light l = new GameObject("Farol").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = crate + new Vector3(0f, 0.95f, 0f);
            l.type = LightType.Point;
            l.color = new Color(0.45f, 0.62f, 1f); // farol azul: se ve de lejos en la noche
            l.range = 9f;
            l.intensity = 2.2f;
            l.shadows = LightShadows.Soft;

            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.4f;
        }

        // ------------------------------------------------------------ Modelo procedimental (si falta el descargado)

        private void BuildModel()
        {
            Color coat = new Color(0.16f, 0.1f, 0.2f);
            Color hood = new Color(0.12f, 0.07f, 0.15f);

            Part(PrimitiveType.Capsule, new Vector3(0f, 0.85f, 0f), new Vector3(0.62f, 0.85f, 0.5f), Vector3.zero, coat);
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0.02f), new Vector3(0.38f, 0.42f, 0.4f), Vector3.zero, hood);
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.56f, 0.14f), new Vector3(0.22f, 0.24f, 0.16f), Vector3.zero, new Color(0.02f, 0.02f, 0.02f));
            GameObject eyeL = Part(PrimitiveType.Sphere, new Vector3(-0.05f, 1.6f, 0.22f), Vector3.one * 0.03f, Vector3.zero, new Color(1f, 0.85f, 0.4f));
            GameObject eyeR = Part(PrimitiveType.Sphere, new Vector3(0.05f, 1.6f, 0.22f), Vector3.one * 0.03f, Vector3.zero, new Color(1f, 0.85f, 0.4f));
            foreach (var eye in new[] { eyeL, eyeR })
            {
                Renderer r = eye.GetComponent<Renderer>();
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.3f) * 3f);
            }
            // Bandolera y mochila
            Part(PrimitiveType.Cube, new Vector3(0f, 1.05f, 0.05f), new Vector3(0.66f, 0.08f, 0.54f), new Vector3(0f, 0f, 35f), new Color(0.3f, 0.2f, 0.1f));
            Part(PrimitiveType.Cube, new Vector3(0f, 1.1f, -0.33f), new Vector3(0.5f, 0.6f, 0.25f), Vector3.zero, new Color(0.28f, 0.2f, 0.12f));
            // Caja de mercancia
            Part(PrimitiveType.Cube, new Vector3(0.75f, 0.3f, 0.2f), new Vector3(0.7f, 0.6f, 0.5f), new Vector3(0f, 15f, 0f), new Color(0.35f, 0.24f, 0.13f));

            // Farol
            GameObject lamp = Part(PrimitiveType.Cylinder, new Vector3(0.75f, 0.72f, 0.2f), new Vector3(0.14f, 0.12f, 0.14f), Vector3.zero, new Color(1f, 0.7f, 0.3f));
            Renderer lr = lamp.GetComponent<Renderer>();
            lr.material.EnableKeyword("_EMISSION");
            lr.material.SetColor("_EmissionColor", new Color(0.35f, 0.55f, 1f) * 2.5f);
            Light l = new GameObject("Farol").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = new Vector3(0.75f, 0.95f, 0.2f);
            l.type = LightType.Point;
            l.color = new Color(0.45f, 0.62f, 1f); // farol azul: se ve de lejos en la noche
            l.range = 9f;
            l.intensity = 2.2f;
            l.shadows = LightShadows.Soft;

            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.4f;
        }

        private GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Color color)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.GetComponent<Renderer>().material.color = color;
            return g;
        }

        /// <summary>Calcula la distancia horizontal en el plano XZ hasta la posición indicada.</summary>
        public float PlanarDistanceTo(Vector3 pos)
        {
            Vector3 d = transform.position - pos;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
