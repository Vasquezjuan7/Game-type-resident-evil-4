using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gameplay.RE4;
using UnityEditor;
using UnityEngine;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Prepara los modelos descargados de Sketchfab (Assets/RE4/Importados/Casas y /Animales):
    ///  - casas: separa las del OBJ "classic house09" en prefabs sueltos (con sus vigas), crea materiales
    ///    Standard con sus texturas (MTL o por nombre), las escala a tamaño real y pone el pivote en el suelo;
    ///  - animales: orienta (de pie, mirando a +Z), escala, materiales, colision y el script FarmAnimal.
    /// Los prefabs quedan en Assets/RE4/Prefabs/Importados.
    /// </summary>
    public static class RE4ImportBuilder
    {
        private const string Src = "Assets/RE4/Importados/";
        public const string OutFolder = "Assets/RE4/Prefabs/Importados";
        private const string MatFolder = "Assets/RE4/Materials/Importados";

        public const string CowPrefab = OutFolder + "/Vaca.prefab";
        public const string PigPrefab = OutFolder + "/Cerdo.prefab";

        /// <summary>Prefabs de casas generados (los que existan).</summary>
        public static List<string> HousePrefabs()
        {
            var list = new List<string>();
            if (!AssetDatabase.IsValidFolder(OutFolder)) return list;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab Casa_", new[] { OutFolder }))
                list.Add(AssetDatabase.GUIDToAssetPath(guid));
            return list;
        }

        public static void BuildAll()
        {
            RE4EditorUtil.EnsureFolder(OutFolder);
            RE4EditorUtil.EnsureFolder(MatFolder);
            Safe("casas clasicas", BuildClassicHouses);
            Safe("casa de madera", () => BuildHouse("Casa_Madera", Src + "Casas/Casa_Madera/Casa_Madera.fbx", 13f, new[]
            {
                ("leaf", "dif_leafs_2_png.png"), ("hoja", "dif_leafs_2_png.png"), ("roof", "TEXTURE_ROOF_png.png"), ("dak", "TEXTURE_ROOF_png.png"),
                ("wall", "T_walls_uv_png.png"), ("muur", "T_walls_uv_png.png"),
            }, "T_unique_UV2_png.png"));
            Safe("casa del veterano", () => BuildHouse("Casa_Veterano", Src + "Casas/Casa_Veterano/Finished_House_2.fbx", 14f, new[]
            {
                ("brick", "Brick_512_color_text_1.png"), ("roof", "RoofTop_seamLess_Dark_1.png"), ("wall", "walls_seamless_old_light.png"),
            }, "The_Veterans_House_assets_2_0002.png"));
            Safe("vaca", () => BuildAnimal("Vaca", Src + "Animales/Vaca/COW_L.3DS", CowPrefab, FarmAnimal.Kind.Cow, 2.4f, new[]
            {
                ("eye", "COWEYE.JPG"), ("ojo", "COWEYE.JPG"), ("cuer", "COWCUER.JPG"), ("horn", "COWCUER.JPG"),
            }, "COWSKIN.JPG"));
            Safe("cerdo", () => BuildAnimal("Cerdo", Src + "Animales/Cerdo/pig2.fbx", PigPrefab, FarmAnimal.Kind.Pig, 1.45f,
                new (string, string)[0], "Tex_1244_0.jpg"));
            AssetDatabase.SaveAssets();
        }

        private static void Safe(string name, System.Action a)
        {
            try { a(); }
            catch (System.Exception e) { Debug.LogError($"[RE4] No se pudo preparar {name}: {e}"); }
        }

        // ------------------------------------------------------------ Casas

        /// <summary>El OBJ trae varias casas: cada una (con las vigas que la tocan) es un prefab.</summary>
        private static void BuildClassicHouses()
        {
            const string folder = Src + "Casas/Casas_Clasicas";
            string objPath = folder + "/classic house09.obj";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            if (src == null) { Debug.LogWarning("[RE4] Falta " + objPath); return; }
            Dictionary<string, string> mtl = ReadMtl(folder + "/classic house09.mtl");

            GameObject inst = Object.Instantiate(src);
            try
            {
                FixMaterials(inst, "Clasica", folder, mtl, new (string, string)[0], "house.jpg");
                var parts = inst.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.transform).ToList();
                var houses = parts.Where(t => !t.name.StartsWith("Plane") && !t.name.StartsWith("poutre")).ToList();
                var beams = parts.Where(t => t.name.StartsWith("poutre")).ToList();
                if (houses.Count == 0) return;

                // misma escala para todas: la casa mas grande mide ~13 m de planta
                float biggest = houses.Max(h => Footprint(Bounds(h)));
                float scale = biggest > 0.01f ? 13f / biggest : 1f;

                var seen = new List<Vector3>();
                int n = 0;
                foreach (Transform h in houses)
                {
                    Bounds hb = Bounds(h);
                    // las copias ".001" suelen ser identicas: se descartan si miden lo mismo
                    Vector3 sig = new Vector3(Mathf.Round(hb.size.x * 10f), Mathf.Round(hb.size.y * 10f), Mathf.Round(hb.size.z * 10f));
                    if (seen.Contains(sig)) continue;
                    seen.Add(sig);

                    var group = new List<Transform> { h };
                    Bounds grow = hb;
                    grow.Expand(1f);
                    foreach (Transform b in beams) if (grow.Intersects(Bounds(b))) group.Add(b);

                    GameObject root = new GameObject($"Casa_Clasica_{++n}");
                    GameObject model = new GameObject("Modelo");
                    model.transform.SetParent(root.transform, false);
                    foreach (Transform p in group)
                    {
                        GameObject copy = Object.Instantiate(p.gameObject);
                        copy.name = p.name;
                        foreach (Transform c in copy.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
                        copy.transform.SetPositionAndRotation(p.position, p.rotation);
                        copy.transform.localScale = p.lossyScale;
                        copy.transform.SetParent(model.transform, true);
                    }
                    model.transform.localScale = Vector3.one * scale;
                    GroundPivot(root, model);
                    Save(root, $"{OutFolder}/Casa_Clasica_{n}.prefab");
                }
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        private static void BuildHouse(string name, string path, float footprint, (string key, string tex)[] rules, string fallbackTex)
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { Debug.LogWarning("[RE4] Falta " + path); return; }
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            GameObject root = new GameObject(name);
            GameObject model = Object.Instantiate(src, root.transform);
            model.name = "Modelo";
            foreach (var c in model.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (var l in model.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
            FixMaterials(model, name, folder, null, rules, fallbackTex);

            // De pie (por si viene con Z arriba) y a tamaño real
            Bounds b = RE4EditorUtil.WorldBounds(model);
            if (b.size.y > Mathf.Max(b.size.x, b.size.z) * 1.6f) model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * model.transform.localRotation;
            b = RE4EditorUtil.WorldBounds(model);
            float f = Footprint(b);
            if (f > 0.01f) model.transform.localScale *= footprint / f;
            GroundPivot(root, model);
            Save(root, $"{OutFolder}/{name}.prefab");
        }

        // ------------------------------------------------------------ Animales

        private static void BuildAnimal(string name, string path, string outPath, FarmAnimal.Kind kind, float length,
            (string key, string tex)[] rules, string fallbackTex)
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { Debug.LogWarning("[RE4] Falta " + path); return; }
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');

            GameObject root = new GameObject(name);
            GameObject model = new GameObject("Modelo");
            model.transform.SetParent(root.transform, false);
            GameObject inst = Object.Instantiate(src, model.transform);
            inst.name = src.name;
            foreach (var c in inst.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (var l in inst.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
            foreach (var a in inst.GetComponentsInChildren<Animator>(true)) a.applyRootMotion = false;
            FixMaterials(inst, name, folder, null, rules, fallbackTex);

            // 1) De pie: la altura de un cuadrupedo es su medida intermedia (largo > alto > ancho).
            //    Si "arriba" es la mas pequeña, el modelo viene tumbado (Z arriba).
            Bounds b = RE4EditorUtil.WorldBounds(inst);
            if (b.size.y < b.size.x && b.size.y < b.size.z) inst.transform.rotation = Quaternion.Euler(-90f, 0f, 0f) * inst.transform.rotation;
            // patas abajo: la mitad inferior (patas) tiene menos vertices que la superior (cuerpo)
            if (CountVerts(inst, true) > CountVerts(inst, false) * 1.3f) inst.transform.rotation = Quaternion.Euler(0f, 0f, 180f) * inst.transform.rotation;

            // 2) Largo hacia +Z
            b = RE4EditorUtil.WorldBounds(inst);
            if (b.size.x > b.size.z) inst.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * inst.transform.rotation;

            // 3) Cabeza hacia +Z: los puntos mas altos (cuernos, orejas, cabeza) estan en el extremo de la cabeza
            if (HeadSign(inst) < 0f) inst.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * inst.transform.rotation;

            // 4) Tamaño real y pivote en las patas
            b = RE4EditorUtil.WorldBounds(inst);
            if (b.size.z > 0.01f) model.transform.localScale = Vector3.one * (length / b.size.z);
            b = RE4EditorUtil.WorldBounds(model);
            model.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

            b = RE4EditorUtil.WorldBounds(model);
            var box = root.AddComponent<BoxCollider>();
            box.center = b.center - root.transform.position;
            box.size = new Vector3(b.size.x * 0.85f, b.size.y * 0.9f, b.size.z * 0.9f);

            var fa = root.AddComponent<FarmAnimal>();
            fa.kind = kind;
            fa.visual = model.transform;
            fa.health = kind == FarmAnimal.Kind.Cow ? 90f : 45f;
            fa.walkSpeed = kind == FarmAnimal.Kind.Cow ? 0.9f : 1.2f;
            fa.fleeSpeed = kind == FarmAnimal.Kind.Cow ? 4f : 5f;
            Save(root, outPath);
        }

        private static int CountVerts(GameObject go, bool lowerHalf)
        {
            Bounds b = RE4EditorUtil.WorldBounds(go);
            int n = 0;
            foreach (Vector3 v in WorldVerts(go))
                if ((v.y < b.center.y) == lowerHalf) n++;
            return n;
        }

        private static float HeadSign(GameObject go)
        {
            Bounds b = RE4EditorUtil.WorldBounds(go);
            var verts = WorldVerts(go).ToList();
            if (verts.Count == 0) return 1f;
            float top = b.max.y - b.size.y * 0.15f;
            var high = verts.Where(v => v.y >= top).ToList();
            if (high.Count == 0) return 1f;
            float z = high.Average(v => v.z);
            return Mathf.Sign(z - b.center.z);
        }

        private static IEnumerable<Vector3> WorldVerts(GameObject go)
        {
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                foreach (Vector3 v in mf.sharedMesh.vertices) yield return mf.transform.TransformPoint(v);
            }
            foreach (SkinnedMeshRenderer sm in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sm.sharedMesh == null) continue;
                foreach (Vector3 v in sm.sharedMesh.vertices) yield return sm.transform.TransformPoint(v);
            }
        }

        // ------------------------------------------------------------ Materiales

        /// <summary>
        /// Material Standard por hueco: textura del MTL (OBJ), la que Unity ya encontro, o la que indique
        /// el nombre del material; si no, la textura principal del modelo.
        /// </summary>
        private static void FixMaterials(GameObject model, string prefix, string folder, Dictionary<string, string> mtl,
            (string key, string tex)[] rules, string fallbackTex)
        {
            var cache = new Dictionary<string, Material>();
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string slot = mats[i] != null ? mats[i].name : r.name;
                    if (cache.TryGetValue(slot, out Material done)) { mats[i] = done; continue; }

                    Texture2D tex = null;
                    string lower = slot.ToLowerInvariant();
                    if (mtl != null && mtl.TryGetValue(Norm(slot), out string file)) tex = FindTex(folder, file);
                    if (tex == null && mats[i] != null && mats[i].HasProperty("_MainTex")) tex = mats[i].GetTexture("_MainTex") as Texture2D;
                    bool leaves = false;
                    if (tex == null)
                    {
                        foreach (var rule in rules)
                        {
                            if (!lower.Contains(rule.key)) continue;
                            tex = FindTex(folder, rule.tex);
                            leaves = rule.tex.Contains("leaf");
                            break;
                        }
                    }
                    if (tex == null) tex = FindTex(folder, fallbackTex);

                    Material m = SaveMat($"{prefix}_{Sanitize(slot)}", tex, leaves ? MakeLeaves(folder) : null);
                    cache[slot] = m;
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private static Material SaveMat(string name, Texture2D tex, Texture2D cutout)
        {
            string path = $"{MatFolder}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("Standard");
            m.color = new Color(0.92f, 0.9f, 0.86f); // un poco mas apagado: casas viejas en la noche
            m.SetTexture("_MainTex", cutout != null ? cutout : tex);
            m.SetFloat("_Glossiness", 0.08f);
            m.SetFloat("_Metallic", 0f);
            if (cutout != null)
            {
                m.SetFloat("_Mode", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = 2450;
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.doubleSidedGI = true;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Hojas de la casa de madera: color + mapa de opacidad en una textura con alfa.</summary>
        private static Texture2D MakeLeaves(string folder)
        {
            string outPath = MatFolder + "/Hojas_Casa_Madera.png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            if (existing != null) return existing;
            Color[] col = ReadPixels(folder + "/Textures/dif_leafs_2_png.png", out int w, out int h);
            Color[] op = ReadPixels(folder + "/Textures/OPACITY_leafs_2_png.png", out int w2, out int h2);
            if (col == null) return null;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            for (int i = 0; i < col.Length; i++)
            {
                float a = 1f;
                if (op != null && w2 == w && h2 == h) a = op[i].grayscale;
                col[i].a = a;
            }
            tex.SetPixels(col);
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(outPath);
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        private static Color[] ReadPixels(string path, out int w, out int h)
        {
            w = h = 0;
            if (!File.Exists(path)) return null;
            var tmp = new Texture2D(2, 2);
            if (!tmp.LoadImage(File.ReadAllBytes(path))) return null;
            w = tmp.width;
            h = tmp.height;
            Color[] px = tmp.GetPixels();
            Object.DestroyImmediate(tmp);
            return px;
        }

        private static Texture2D FindTex(string folder, string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            file = Path.GetFileName(file.Trim());
            foreach (string p in new[] { folder + "/Textures/" + file, folder + "/" + file })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t != null) return t;
            }
            // el MTL dice .jpeg y el archivo es .jpg (o al reves)
            string stem = Path.GetFileNameWithoutExtension(file);
            foreach (string guid in AssetDatabase.FindAssets(stem + " t:Texture2D", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == stem) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            return null;
        }

        private static Dictionary<string, string> ReadMtl(string path)
        {
            var map = new Dictionary<string, string>();
            if (!File.Exists(path)) return map;
            string current = null;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("newmtl ")) current = line.Substring(7).Trim();
                else if (line.StartsWith("map_Kd ") && current != null) map[Norm(current)] = line.Substring(7).Trim();
            }
            return map;
        }

        private static string Norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        private static string Sanitize(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        // ------------------------------------------------------------ Utilidades

        private static Bounds Bounds(Transform t)
        {
            Renderer r = t.GetComponent<Renderer>();
            return r != null ? r.bounds : new Bounds(t.position, Vector3.zero);
        }

        private static float Footprint(Bounds b) => Mathf.Max(b.size.x, b.size.z);

        /// <summary>Centra el modelo en X/Z y apoya su base en y = 0.</summary>
        private static void GroundPivot(GameObject root, GameObject model)
        {
            root.transform.position = Vector3.zero;
            Bounds b = RE4EditorUtil.WorldBounds(model);
            model.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
        }

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log("[RE4] Modelo preparado: " + path);
        }
    }
}
