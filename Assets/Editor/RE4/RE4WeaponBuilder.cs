using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Prepara los modelos de arma para la vista en primera persona:
    /// - orienta el prefab (canon hacia +Z, arriba +Y) a partir de sus piezas,
    /// - separa la corredera / palanca de carga ("Slide") y el cargador ("Magazine") para animarlos,
    /// - marca la boca del canon ("MuzzlePoint") y la linea de mira ("SightPoint"),
    /// - crea materiales Standard con sus texturas PBR.
    /// </summary>
    public static class RE4WeaponBuilder
    {
        public const string Folder = "Assets/RE4/Prefabs/Weapons";
        private const string MeshFolder = Folder + "/Meshes";
        private const string MatFolder = "Assets/RE4/Materials/Weapons";
        private const string TexFolder = "Assets/RE4/Textures/Weapons";

        public const string Pistol92Source = "Assets/edge resident/92Fq_1.fbx";
        public const string PistolResidentSource = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Arma resident/1.fbx";
        public const string RifleSource = "Assets/FPS Gun Pack 4K/Rifle 1/Prefabs/Rifle 1.prefab";

        public static GameObject Pistol92Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Pistola_92F.prefab");
        public static GameObject PistolResidentPrefab => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Pistola_Resident.prefab");
        public const string RevolverSource = "Assets/RE4/Importados/Revolver/Revolver001.fbx";
        public const string LauncherSource = "Assets/RE4/Importados/Lanzacohetes/RocketLauncher.fbx";
        public const string RevolverCylinder = "A1c_Barrel_center__rotate";
        public static GameObject RiflePrefab => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Rifle_AK47.prefab");

        public static void BuildAll()
        {
            RE4EditorUtil.EnsureFolder(MeshFolder);
            RE4EditorUtil.EnsureFolder(MatFolder);
            RE4EditorUtil.EnsureFolder(TexFolder);
            Safe("92F", BuildPistol92);
            Safe("Resident", BuildPistolResident);
            Safe("AK-47", BuildRifle);
            Safe("Revolver", BuildRevolver);
            Safe("Lanzacohetes", BuildLauncher);
            AssetDatabase.SaveAssets();
        }

        private static void Safe(string name, System.Action build)
        {
            try { build(); }
            catch (System.Exception e) { Debug.LogError($"[RE4] No se pudo preparar el arma {name}: {e}"); }
        }

        // =========================================================== Pistola 92F

        private static void BuildPistol92()
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(Pistol92Source);
            if (src == null) { Debug.LogWarning("[RE4] Falta " + Pistol92Source); return; }

            const string tex = "Assets/edge resident/";
            Material frame1 = PbrMaterial("Pistola92_Frame1", tex + "frame-1_Base_Color.png", tex + "frame-1_Normal_OpenGL.png", tex + "frame-1_Metallic.png", tex + "frame-1_Roughness.png", tex + "frame-1_Mixed_AO.png");
            Material frame2 = PbrMaterial("Pistola92_Frame2", tex + "frame-2_Base_Color.png", tex + "frame-2_Normal_OpenGL.png", tex + "frame-2_Metallic.png", tex + "frame-2_Roughness.png", tex + "frame-2_Mixed_AO.png");
            Material mag = PbrMaterial("Pistola92_Mag", tex + "Mag_Base_Color.png", tex + "Mag_Normal_OpenGL.png", null, tex + "Mag_Roughness.png", null);

            GameObject root = new GameObject("Pistola_92F");
            GameObject model = Wrap(root, src);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : r.name.ToLowerInvariant();
                    mats[i] = n.Contains("mag") ? mag : n.Contains("frame-2") || n.Contains("frame_2") ? frame2 : frame1;
                }
                r.sharedMaterials = mats;
            }

            List<Transform> slide = Find(model, "Cylinder.high.002", "Cylinder.B.high.002");
            List<Transform> magazine = Find(model, "Mag.high.003");
            Finish(root, model, slide, magazine, null, null, Folder + "/Pistola_92F.prefab");
        }

        // =========================================================== Pistola "Arma resident"

        private static void BuildPistolResident()
        {
            ModelImporter mi = AssetImporter.GetAtPath(PistolResidentSource) as ModelImporter;
            if (mi == null) { Debug.LogWarning("[RE4] Falta " + PistolResidentSource); return; }
            if (!mi.isReadable)
            {
                mi.isReadable = true;
                mi.SaveAndReimport();
            }
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(PistolResidentSource);

            const string tex = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Arma resident/texturing_model_";
            Material gun = PbrMaterial("PistolaResident_Gun", tex + "gun_BaseColor.png", tex + "gun_Normal.png", tex + "gun_Metallic.png", tex + "gun_Roughness.png", null);
            Material parts = PbrMaterial("PistolaResident_Parts", tex + "gun_parts_BaseColor.png", tex + "gun_parts_Normal.png", tex + "gun_parts_Metallic.png", tex + "gun_parts_Roughness.png", null);

            GameObject root = new GameObject("Pistola_Resident");
            GameObject model = Wrap(root, src);

            MeshFilter mf = model.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) { Object.DestroyImmediate(root); Debug.LogWarning("[RE4] El modelo de la pistola no tiene malla."); return; }
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            Material[] mats = mr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                mats[i] = n.Contains("parts") ? parts : gun;
            }
            mr.sharedMaterials = mats;

            // La malla viene en una pieza: se separan corredera y cargador por islas de geometria
            Mesh[] split = SplitResident(mf.sharedMesh);
            mf.sharedMesh = split[0];
            Transform slidePiece = AddMeshSibling(mf, "Corredera", split[1], mats);
            Transform magPiece = AddMeshSibling(mf, "CargadorMalla", split[2], mats);

            var slide = new List<Transform>();
            var magazine = new List<Transform>();
            if (slidePiece != null) slide.Add(slidePiece);
            if (magPiece != null) magazine.Add(magPiece);
            Finish(root, model, slide, magazine, null, null, Folder + "/Pistola_Resident.prefab");
        }

        /// <summary>Devuelve [armazon, corredera, cargador].</summary>
        private static Mesh[] SplitResident(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            Bounds b = mesh.bounds;

            // Ejes: largo = mayor, vertical = intermedio, ancho = menor
            int[] axes = { 0, 1, 2 };
            System.Array.Sort(axes, (a, c) => b.size[c].CompareTo(b.size[a]));
            int longA = axes[0], vertA = axes[1], widthA = axes[2];

            // Soldar vertices por posicion (las costuras de UV duplican vertices)
            float q = Mathf.Max(1e-7f, b.size.magnitude * 1e-5f);
            var weld = new Dictionary<Vector3Int, int>();
            int[] id = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(v[i].x / q), Mathf.RoundToInt(v[i].y / q), Mathf.RoundToInt(v[i].z / q));
                if (!weld.TryGetValue(key, out int w)) { w = weld.Count; weld[key] = w; }
                id[i] = w;
            }
            int[] parent = new int[weld.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Root(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }

            var subTris = new List<int[]>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] t = mesh.GetTriangles(s);
                subTris.Add(t);
                for (int i = 0; i < t.Length; i += 3)
                {
                    int a = Root(id[t[i]]), c = Root(id[t[i + 1]]), d = Root(id[t[i + 2]]);
                    parent[c] = a;
                    parent[Root(d)] = Root(a);
                }
            }

            // Bounds de cada isla
            var islandMin = new Dictionary<int, Vector3>();
            var islandMax = new Dictionary<int, Vector3>();
            for (int i = 0; i < v.Length; i++)
            {
                int r = Root(id[i]);
                if (!islandMin.ContainsKey(r)) { islandMin[r] = v[i]; islandMax[r] = v[i]; }
                else { islandMin[r] = Vector3.Min(islandMin[r], v[i]); islandMax[r] = Vector3.Max(islandMax[r], v[i]); }
            }

            // Sentido del eje largo: la empunadura (lo que mas baja) esta en el extremo "1"
            // Sentido vertical: arriba es el extremo cuya franja abarca mas largo (la corredera)
            float Frac(float x, int axis) => (x - b.min[axis]) / Mathf.Max(1e-6f, b.size[axis]);
            float topSpreadMin = float.MaxValue, topSpreadMax = float.MinValue, botSpreadMin = float.MaxValue, botSpreadMax = float.MinValue;
            foreach (var p in v)
            {
                float fv = Frac(p[vertA], vertA);
                float fl = Frac(p[longA], longA);
                if (fv > 0.9f) { topSpreadMin = Mathf.Min(topSpreadMin, fl); topSpreadMax = Mathf.Max(topSpreadMax, fl); }
                if (fv < 0.1f) { botSpreadMin = Mathf.Min(botSpreadMin, fl); botSpreadMax = Mathf.Max(botSpreadMax, fl); }
            }
            bool flipVert = (botSpreadMax - botSpreadMin) > (topSpreadMax - topSpreadMin);
            float lowMinHigh = float.MaxValue, lowMinLow = float.MaxValue;
            foreach (var p in v)
            {
                float fl = Frac(p[longA], longA);
                float fv = Frac(p[vertA], vertA);
                if (flipVert) fv = 1f - fv;
                if (fl > 0.75f) lowMinHigh = Mathf.Min(lowMinHigh, fv);
                if (fl < 0.25f) lowMinLow = Mathf.Min(lowMinLow, fv);
            }
            bool flipLong = lowMinLow < lowMinHigh;

            int Classify(Vector3 mn, Vector3 mx)
            {
                float l0 = Frac(mn[longA], longA), l1 = Frac(mx[longA], longA);
                float v0 = Frac(mn[vertA], vertA), v1 = Frac(mx[vertA], vertA);
                float w0 = Frac(mn[widthA], widthA), w1 = Frac(mx[widthA], widthA);
                if (flipLong) { float t = 1f - l1; l1 = 1f - l0; l0 = t; }
                if (flipVert) { float t = 1f - v1; v1 = 1f - v0; v0 = t; }
                const float e = 0.01f;
                if (l0 >= 0.44f - e && l1 <= 0.955f + e && v0 >= 0.67f - e && v1 <= 1f + e) return 1;
                if (l0 >= 0.77f - e && l1 <= 0.96f + e && v1 <= 0.89f + e && w0 >= 0.15f - e && w1 <= 0.85f + e && v1 - v0 > 0.35f) return 2;
                return 0;
            }
            var islandClass = new Dictionary<int, int>();
            foreach (var kv in islandMin) islandClass[kv.Key] = Classify(kv.Value, islandMax[kv.Key]);

            var result = new Mesh[3];
            string[] names = { "PistolaResident_Armazon", "PistolaResident_Corredera", "PistolaResident_Cargador" };
            for (int g = 0; g < 3; g++)
            {
                var remap = new Dictionary<int, int>();
                var verts = new List<int>();
                var tris = new List<List<int>>();
                foreach (var t in subTris)
                {
                    var list = new List<int>();
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        if (islandClass[Root(id[t[i]])] != g) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            int src = t[i + k];
                            if (!remap.TryGetValue(src, out int dst)) { dst = verts.Count; remap[src] = dst; verts.Add(src); }
                            list.Add(dst);
                        }
                    }
                    tris.Add(list);
                }
                if (verts.Count == 0) { result[g] = null; continue; }
                result[g] = SubMesh(mesh, verts, tris, names[g]);
            }
            int slideTris = result[1] != null ? result[1].triangles.Length / 3 : 0;
            int magTris = result[2] != null ? result[2].triangles.Length / 3 : 0;
            Debug.Log($"[RE4] Pistola Resident separada: corredera {slideTris} tri, cargador {magTris} tri.");
            if (result[0] == null) result[0] = mesh;
            return result;
        }

        private static Mesh SubMesh(Mesh src, List<int> verts, List<List<int>> tris, string name)
        {
            Vector3[] v = src.vertices; Vector3[] n = src.normals; Vector4[] t = src.tangents;
            Vector2[] uv = src.uv; Vector2[] uv2 = src.uv2; Color[] col = src.colors;
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts.ConvertAll(i => v[i]));
            if (n.Length == v.Length) m.SetNormals(verts.ConvertAll(i => n[i]));
            if (t.Length == v.Length) m.SetTangents(verts.ConvertAll(i => t[i]));
            if (uv.Length == v.Length) m.SetUVs(0, verts.ConvertAll(i => uv[i]));
            if (uv2.Length == v.Length) m.SetUVs(1, verts.ConvertAll(i => uv2[i]));
            if (col.Length == v.Length) m.SetColors(verts.ConvertAll(i => col[i]));
            m.subMeshCount = tris.Count;
            for (int s = 0; s < tris.Count; s++) m.SetTriangles(tris[s], s);
            m.RecalculateBounds();

            string path = $"{MeshFolder}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(m, existing);
                Object.DestroyImmediate(m);
                return existing;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Transform AddMeshSibling(MeshFilter original, string name, Mesh mesh, Material[] mats)
        {
            if (mesh == null) return null;
            GameObject go = new GameObject(name);
            go.transform.SetParent(original.transform.parent, false);
            go.transform.localPosition = original.transform.localPosition;
            go.transform.localRotation = original.transform.localRotation;
            go.transform.localScale = original.transform.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            return go.transform;
        }

        // =========================================================== Rifle AK-47

        private static void BuildRifle()
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(RifleSource);
            if (src == null) { Debug.LogWarning("[RE4] Falta " + RifleSource); return; }

            GameObject root = new GameObject("Rifle_AK47");
            GameObject model = Wrap(root, src);

            List<Transform> handle = Find(model, "Shell_Remover");
            List<Transform> magazine = Find(model, "Magazine1");
            List<Transform> muzzle = Find(model, "Muzzle");
            List<Transform> upRef = Find(model, "Receiver_Cover");
            List<Transform> sight = Find(model, "Scope_Rear_Lens");
            Finish(root, model, handle, magazine, muzzle, upRef, Folder + "/Rifle_AK47.prefab", sight);
        }

        // =========================================================== Revolver ("Requiem")

        private static void BuildRevolver()
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(RevolverSource);
            if (src == null) { Debug.LogWarning("[RE4] Falta " + RevolverSource); return; }
            const string tex = "Assets/RE4/Importados/Revolver/";
            Material a1 = PbrMaterial("Revolver_A1", tex + "A1Color.png", tex + "A1_Normal.png", tex + "A1_Metal.png", tex + "A1_Roughness.png", null);
            Material a2 = PbrMaterial("Revolver_A2", tex + "A2Color.png", tex + "A2_Normal.png", tex + "A2_Metal.png", tex + "A2_Roughness.png", null);

            GameObject root = new GameObject("Revolver_Requiem");
            GameObject model = Wrap(root, src);
            AssignByName(model, n => n.Contains("a2") ? a2 : a1);

            List<Transform> grip = Find(model, "A1d");
            List<Transform> barrel = Find(model, "A2");
            List<Transform> cylinder = Find(model, RevolverCylinder);
            if (cylinder.Count == 0) Debug.LogWarning("[RE4] El revolver no tiene la pieza del tambor '" + RevolverCylinder + "'; no girara.");
            Finish(root, model, new List<Transform>(), new List<Transform>(), barrel, cylinder, Folder + "/Revolver_Requiem.prefab", null, grip);
        }

        // =========================================================== Lanzacohetes

        private static void BuildLauncher()
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(LauncherSource);
            if (src == null) { Debug.LogWarning("[RE4] Falta " + LauncherSource); return; }
            const string tex = "Assets/RE4/Importados/Lanzacohetes/Rocket_Launcher_";
            Material body = PbrMaterial("Lanzacohetes_Cuerpo", tex + "Rocket_Launcher_BaseColor.png", tex + "Rocket_Launcher_Normal.png", tex + "Rocket_Launcher_Metalness.png", tex + "Rocket_Launcher_Roughness.png", null);
            Material rocket = PbrMaterial("Lanzacohetes_Cohete", tex + "Rocket4_BaseColor.png", tex + "Rocket4_Normal.png", tex + "Rocket4_Metalness.png", tex + "Rocket4_Roughness.png", null);

            GameObject root = new GameObject("Lanzacohetes");
            GameObject model = Wrap(root, src);
            AssignByName(model, n => n.Contains("rocket4") ? rocket : body);

            // Los cohetes asoman por delante; el gatillo marca la empunadura; el modelo ya viene de pie
            Finish(root, model, new List<Transform>(), new List<Transform>(), Find(model, "Rockets"), null,
                Folder + "/Lanzacohetes.prefab", Find(model, "Aim"), Find(model, "Trigger"), true);
        }

        private static void AssignByName(GameObject model, System.Func<string, Material> pick)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = pick(mats[i] != null ? mats[i].name.ToLowerInvariant() : r.name.ToLowerInvariant());
                r.sharedMaterials = mats;
            }
        }

        // =========================================================== Comun

        /// <summary>Contenedor "Modelo" con la instancia dentro (asi las piezas nuevas giran con el resto).</summary>
        private static GameObject Wrap(GameObject root, GameObject src)
        {
            GameObject model = new GameObject("Modelo");
            model.transform.SetParent(root.transform, false);
            GameObject inst = Object.Instantiate(src, model.transform);
            inst.name = src.name;
            inst.transform.localPosition = Vector3.zero;
            return model;
        }

        private static List<Transform> Find(GameObject model, params string[] names)
        {
            var list = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                foreach (var n in names) if (t.name == n && !list.Contains(t)) list.Add(t);
            }
            return list;
        }

        private static Bounds BoundsOf(IEnumerable<Transform> parts)
        {
            bool has = false;
            Bounds b = new Bounds();
            foreach (var p in parts)
            {
                if (p == null) continue;
                foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                {
                    if (!has) { b = r.bounds; has = true; }
                    else b.Encapsulate(r.bounds);
                }
            }
            return b;
        }

        /// <summary>Orienta, agrupa piezas, crea marcadores y guarda el prefab.</summary>
        private static void Finish(GameObject root, GameObject model, List<Transform> slide, List<Transform> magazine,
            List<Transform> frontRef, List<Transform> upRef, string path, List<Transform> sightRef = null,
            List<Transform> gripRef = null, bool keepUp = false)
        {
            // referencia de la empunadura: el cargador, o una pieza indicada (revolver, lanzacohetes)
            List<Transform> grip = magazine.Count > 0 ? magazine : gripRef ?? new List<Transform>();
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            root.transform.position = Vector3.zero;
            model.transform.localPosition = Vector3.zero;

            Bounds all = RE4EditorUtil.WorldBounds(model);
            Bounds slideB = slide.Count > 0 ? BoundsOf(slide) : all;
            Bounds magB = grip.Count > 0 ? BoundsOf(grip) : new Bounds(all.center - Vector3.up * all.extents.y, Vector3.zero);
            Bounds frontB = frontRef != null && frontRef.Count > 0 ? BoundsOf(frontRef) : slideB;
            Bounds upB = upRef != null && upRef.Count > 0 ? BoundsOf(upRef) : slideB;

            // Eje largo del arma y sentido del canon
            int longAxis = all.size.x >= all.size.y && all.size.x >= all.size.z ? 0 : all.size.y >= all.size.z ? 1 : 2;
            Vector3 axis = Vector3.zero;
            axis[longAxis] = 1f;
            Vector3 toFront = frontB.center - magB.center;
            if (frontRef == null || frontRef.Count == 0)
            {
                // pistola: la boca esta en el extremo de la corredera mas alejado del cargador
                float a0 = slideB.min[longAxis], a1 = slideB.max[longAxis];
                float m = magB.center[longAxis];
                toFront = axis * (Mathf.Abs(a0 - m) > Mathf.Abs(a1 - m) ? a0 - m : a1 - m);
            }
            Vector3 forward = axis * Mathf.Sign(Vector3.Dot(toFront, axis));
            Vector3 up = keepUp ? Vector3.up : upB.center - magB.center;
            up -= forward * Vector3.Dot(up, forward);
            if (up.sqrMagnitude < 1e-8f) up = Vector3.up;
            Quaternion fix = Quaternion.Inverse(Quaternion.LookRotation(forward, up.normalized));
            model.transform.localRotation = fix * model.transform.localRotation;

            // Pivote en la empunadura
            magB = grip.Count > 0 ? BoundsOf(grip) : RE4EditorUtil.WorldBounds(model);
            model.transform.position -= new Vector3(magB.center.x, magB.max.y, magB.center.z);

            all = RE4EditorUtil.WorldBounds(model);
            slideB = slide.Count > 0 ? BoundsOf(slide) : frontRef != null && frontRef.Count > 0 ? BoundsOf(frontRef) : all;

            // Agrupar piezas animadas
            if (slide.Count > 0) Group(root.transform, "Slide", slide);
            if (magazine.Count > 0) Group(root.transform, "Magazine", magazine);

            // Marcadores
            Vector3 muzzle;
            if (frontRef != null && frontRef.Count > 0)
            {
                Bounds fb = BoundsOf(frontRef);
                muzzle = new Vector3(fb.center.x, fb.center.y, all.max.z);
            }
            else
            {
                muzzle = new Vector3(slideB.center.x, slideB.center.y - slideB.extents.y * 0.25f, all.max.z);
            }
            Marker(root.transform, "MuzzlePoint", muzzle);

            Vector3 sightPos;
            if (sightRef != null && sightRef.Count > 0)
            {
                Bounds sb = BoundsOf(sightRef);
                sightPos = sb.center;
            }
            else
            {
                sightPos = new Vector3(slideB.center.x, slideB.max.y + slideB.size.y * 0.04f, slideB.min.z + slideB.size.z * 0.1f);
            }
            Marker(root.transform, "SightPoint", sightPos);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log("[RE4] Arma preparada: " + path);
        }

        private static void Group(Transform root, string name, List<Transform> parts)
        {
            Bounds b = BoundsOf(parts);
            GameObject g = new GameObject(name);
            g.transform.SetParent(root, false);
            g.transform.position = b.center;
            foreach (var p in parts) p.SetParent(g.transform, true);
        }

        private static void Marker(Transform root, string name, Vector3 worldPos)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(root, false);
            g.transform.position = worldPos;
        }

        // =========================================================== Materiales

        public static Material PbrMaterial(string name, string albedo, string normal, string metallic, string roughness, string ao)
        {
            string path = $"{MatFolder}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("Standard");
            m.color = Color.white;

            Texture2D a = Tex(albedo, false);
            if (a != null) m.SetTexture("_MainTex", a);

            Texture2D n = Tex(normal, true);
            if (n != null)
            {
                m.SetTexture("_BumpMap", n);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }

            Texture2D ms = BakeMetalSmooth(name, metallic, roughness);
            if (ms != null)
            {
                m.SetTexture("_MetallicGlossMap", ms);
                m.SetFloat("_GlossMapScale", 0.9f);
                m.EnableKeyword("_METALLICGLOSSMAP");
            }
            else
            {
                m.SetFloat("_Metallic", 0.7f);
                m.SetFloat("_Glossiness", 0.45f);
            }

            Texture2D o = Tex(ao, false, true);
            if (o != null)
            {
                m.SetTexture("_OcclusionMap", o);
                m.SetFloat("_OcclusionStrength", 1f);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Texture2D Tex(string path, bool normal, bool linear = false)
        {
            if (string.IsNullOrEmpty(path)) return null;
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return null;
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (linear && ti.sRGBTexture) { ti.sRGBTexture = false; dirty = true; }
            if (ti.maxTextureSize > 2048) { ti.maxTextureSize = 2048; dirty = true; }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Combina metalico (R) y suavidad = 1 - rugosidad (A) en la textura que usa el shader Standard.</summary>
        private static Texture2D BakeMetalSmooth(string name, string metallicPath, string roughnessPath)
        {
            if (string.IsNullOrEmpty(roughnessPath) || !File.Exists(roughnessPath)) return null;
            string outPath = $"{TexFolder}/{name}_MetalSmooth.png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            if (existing != null) return existing;

            const int size = 1024;
            Color[] rough = ReadResized(roughnessPath, size);
            Color[] metal = !string.IsNullOrEmpty(metallicPath) && File.Exists(metallicPath) ? ReadResized(metallicPath, size) : null;
            if (rough == null) return null;

            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                float mt = metal != null ? metal[i].r : 0.6f;
                px[i] = new Color(mt, mt, mt, 1f - rough[i].r);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath);
            if (AssetImporter.GetAtPath(outPath) is TextureImporter ti)
            {
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        private static Color[] ReadResized(string path, int size)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!src.LoadImage(File.ReadAllBytes(path))) { Object.DestroyImmediate(src); return null; }
            RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var dst = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            dst.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            dst.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color[] px = dst.GetPixels();
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
            return px;
        }
    }
}
