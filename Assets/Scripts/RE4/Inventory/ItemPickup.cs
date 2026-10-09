using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Objeto recogible en el mundo (municion, hierbas, armas...). Brilla como en RE4.
    /// </summary>
    public class ItemPickup : MonoBehaviour
    {
        public static readonly List<ItemPickup> All = new List<ItemPickup>();
        /// <summary>Se dispara al recoger un objeto por completo (lo usa la historia).</summary>
        public static event System.Action<ItemDefinition> OnCollected;

        public ItemDefinition item;
        public int count = 1;
        [Tooltip("Balas en el cargador si es un arma.")]
        public int loadedAmmo = -1;
        [Tooltip("Mejoras del arma tirada (se conservan al recogerla).")]
        public WeaponUpgrades upgrades;
        public bool despawns = false;
        public float despawnTime = 60f;

        private Transform _glint;
        private float _spawnTime;
        private Vector3 _glintBase;
        private float _phase;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start()
        {
            _spawnTime = Time.time;
            _phase = Random.Range(0f, 10f);
            CreateGlint();
        }

        private void CreateGlint()
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = "Glint";
            Destroy(g.GetComponent<Collider>());
            g.layer = 2;
            g.transform.SetParent(transform, false);
            Bounds b = WorldBounds();
            g.transform.position = b.center + Vector3.up * (b.extents.y + 0.12f);
            _glintBase = g.transform.localPosition;
            CombatFXLibrary lib = FXManager.Get().library;
            Renderer r = g.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (lib != null && lib.itemGlint != null) r.sharedMaterial = lib.itemGlint;
            else r.material.color = new Color(1f, 1f, 0.8f, 0.8f);
            _glint = g.transform;
        }

        public Bounds WorldBounds()
        {
            var rs = GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(transform.position, Vector3.one * 0.3f);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) if (r.name != "Glint") b.Encapsulate(r.bounds);
            return b;
        }

        private void Update()
        {
            if (_glint != null)
            {
                Camera cam = Camera.main;
                if (cam != null) _glint.rotation = Quaternion.LookRotation(_glint.position - cam.transform.position);
                float pulse = 0.12f + Mathf.Abs(Mathf.Sin(Time.time * 3f + _phase)) * 0.14f;
                _glint.localScale = Vector3.one * pulse / Mathf.Max(0.001f, transform.lossyScale.x);
                _glint.localPosition = _glintBase + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.03f;
            }
            if (despawns && Time.time - _spawnTime > despawnTime) Destroy(gameObject);
        }

        public string Label
        {
            get
            {
                if (item == null) return "";
                if (item.type == ItemType.Money) return $"{count:N0} pesetas";
                return count > 1 ? $"{item.displayName} x{count}" : item.displayName;
            }
        }

        /// <summary>Intenta guardar en el maletin. Devuelve true si se recogio todo.</summary>
        public bool TryCollect(AttacheCase attache)
        {
            if (item == null || attache == null) return false;
            int before = attache.entries.Count;
            attache.TryAdd(item, count, out int leftover);
            if (item.type == ItemType.Weapon && leftover == 0 && loadedAmmo >= 0)
            {
                // conservar balas del cargador del arma recogida
                for (int i = attache.entries.Count - 1; i >= before; i--)
                {
                    if (attache.entries[i].item == item)
                    {
                        if (upgrades != null) attache.entries[i].upgrades = upgrades.Clone();
                        attache.entries[i].loadedAmmo = Mathf.Min(loadedAmmo, attache.entries[i].MagazineSize);
                        break;
                    }
                }
            }
            if (leftover == count) return false;
            ProceduralAudio.PlayAt(ProceduralAudio.Pickup(), transform.position, 0.6f, 0f);
            if (leftover > 0)
            {
                count = leftover;
                return false;
            }
            OnCollected?.Invoke(item);
            Destroy(gameObject);
            return true;
        }

        // ------------------------------------------------------------ Creacion en tiempo de ejecucion

        /// <summary>Crea un objeto recogible con un modelo simple segun su tipo.</summary>
        public static ItemPickup Spawn(ItemDefinition item, int count, Vector3 position, int loadedAmmo = -1)
        {
            if (item == null) return null;
            GameObject root = new GameObject("Pickup_" + item.id);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            if (item.worldModel != null)
            {
                GameObject m = Instantiate(item.worldModel, root.transform);
                m.transform.localPosition = Vector3.zero;
                foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var rb in m.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
                foreach (var mb in m.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
                if (item.type == ItemType.Weapon)
                {
                    // tumbar el arma en el suelo y ajustar tamano
                    m.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    FitToSize(m.transform, item.weapon != null ? item.weapon.viewModelLength * 1.3f : 0.6f);
                }
                else if (item.worldModelSize > 0f)
                {
                    FitToSize(m.transform, item.worldModelSize);
                }
            }
            else
            {
                BuildPrimitiveModel(item, root.transform);
            }

            // Apoyar en el suelo
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, RE4Layers.ShotMask, QueryTriggerInteraction.Ignore))
            {
                root.transform.position = hit.point + Vector3.up * 0.02f;
            }
            Bounds b = BoundsOf(root);
            float lift = root.transform.position.y - b.min.y;
            root.transform.position += Vector3.up * lift;

            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;
            root.layer = 2;

            ItemPickup p = root.AddComponent<ItemPickup>();
            p.item = item;
            p.count = count;
            p.loadedAmmo = loadedAmmo;
            return p;
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static void FitToSize(Transform t, float targetLength)
        {
            Bounds b = BoundsOf(t.gameObject);
            float max = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (max > 0.0001f) t.localScale *= targetLength / max;
        }

        public static void BuildPrimitiveModel(ItemDefinition item, Transform parent)
        {
            Color c = item.tileColor;
            switch (item.type)
            {
                case ItemType.Herb:
                {
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 0.06f, 0f), new Vector3(0.13f, 0.06f, 0.13f), new Color(0.35f, 0.22f, 0.12f));
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        Part(PrimitiveType.Sphere, parent, new Vector3(Mathf.Cos(a) * 0.05f, 0.17f + (i % 2) * 0.03f, Mathf.Sin(a) * 0.05f), new Vector3(0.09f, 0.05f, 0.09f), c);
                    }
                    break;
                }
                case ItemType.Heal:
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.07f, 0.1f, 0.07f), c);
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 0.22f, 0f), new Vector3(0.03f, 0.04f, 0.03f), Color.white);
                    break;
                case ItemType.Document:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 0.005f, 0f), new Vector3(0.21f, 0.004f, 0.29f), new Color(0.9f, 0.86f, 0.74f));
                    Part(PrimitiveType.Cube, parent, new Vector3(0.02f, 0.009f, 0.01f), new Vector3(0.21f, 0.004f, 0.29f), new Color(0.82f, 0.78f, 0.66f));
                    break;
                case ItemType.Resource:
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 0.06f, 0f), new Vector3(0.09f, 0.06f, 0.09f), c);
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 0.125f, 0f), new Vector3(0.095f, 0.01f, 0.095f), c * 0.6f);
                    break;
                case ItemType.Treasure:
                case ItemType.Key:
                    Part(PrimitiveType.Sphere, parent, new Vector3(0f, 0.04f, 0f), new Vector3(0.08f, 0.05f, 0.08f), new Color(0.95f, 0.8f, 0.3f));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 0.01f, 0f), new Vector3(0.12f, 0.005f, 0.12f), new Color(0.7f, 0.55f, 0.2f));
                    break;
                case ItemType.Money:
                    Part(PrimitiveType.Sphere, parent, new Vector3(0f, 0.09f, 0f), new Vector3(0.2f, 0.17f, 0.2f), new Color(0.45f, 0.32f, 0.16f));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 0.19f, 0f), new Vector3(0.07f, 0.03f, 0.07f), new Color(0.3f, 0.2f, 0.1f));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0.12f, 0.01f, 0.05f), new Vector3(0.06f, 0.006f, 0.06f), new Color(0.95f, 0.78f, 0.25f));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(-0.1f, 0.01f, -0.07f), new Vector3(0.06f, 0.006f, 0.06f), new Color(0.95f, 0.78f, 0.25f));
                    break;
                case ItemType.Ammo:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 0.06f, 0f), new Vector3(0.22f, 0.12f, 0.15f), c);
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 0.125f, 0f), new Vector3(0.23f, 0.01f, 0.16f), c * 0.6f);
                    break;
                default:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 0.08f, 0f), new Vector3(0.18f, 0.16f, 0.18f), c);
                    break;
            }
        }

        private static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            Renderer r = g.GetComponent<Renderer>();
            r.material.color = color;
        }
    }
}
