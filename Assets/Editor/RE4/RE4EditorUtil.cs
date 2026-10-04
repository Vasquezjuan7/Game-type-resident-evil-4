using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LevelSetup.RE4Tools
{
    public static class RE4EditorUtil
    {
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Crea las capas necesarias en el TagManager si no existen.</summary>
        public static void EnsureLayers(params string[] names)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            foreach (string name in names)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                {
                    if (layers.GetArrayElementAtIndex(i).stringValue == name) { exists = true; break; }
                }
                if (exists) continue;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    SerializedProperty p = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(p.stringValue))
                    {
                        p.stringValue = name;
                        break;
                    }
                }
            }
            tagManager.ApplyModifiedProperties();
        }

        public static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        // ------------------------------------------------------------ Texturas y materiales

        public static Texture2D SaveTexture(string path, int size, System.Func<float, float, Color> pixel)
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, pixel((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material SaveMaterial(string path, string shaderName, Color color, Texture tex = null)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (tex != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------ Orientacion automatica de armas

        /// <summary>
        /// Analiza la malla del arma y calcula la rotacion para que el canon apunte a +Z y la parte de arriba a +Y.
        /// El extremo del canon es el lado con menor seccion; "arriba" es hacia donde se concentran los vertices
        /// (la empunadura y el cargador cuelgan hacia abajo).
        /// </summary>
        public static Vector3 ComputeWeaponOrientation(GameObject prefab, Vector3 fallback)
        {
            if (prefab == null) return fallback;
            var points = new List<Vector3>();
            try
            {
                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    foreach (var v in mf.sharedMesh.vertices) points.Add(prefab.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
                }
                foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh == null) continue;
                    foreach (var v in smr.sharedMesh.vertices) points.Add(prefab.transform.InverseTransformPoint(smr.transform.TransformPoint(v)));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RE4] No se pudo leer la malla de " + prefab.name + ": " + e.Message);
                return fallback;
            }
            if (points.Count < 20) return fallback;

            Bounds b = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) b.Encapsulate(p);

            int longAxis = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : b.size.y >= b.size.z ? 1 : 2;
            int a1 = (longAxis + 1) % 3, a2 = (longAxis + 2) % 3;
            float min = b.min[longAxis], len = b.size[longAxis];

            // Seccion en cada extremo (20% del largo)
            float lowA1Min = float.MaxValue, lowA1Max = float.MinValue, lowA2Min = float.MaxValue, lowA2Max = float.MinValue;
            float hiA1Min = float.MaxValue, hiA1Max = float.MinValue, hiA2Min = float.MaxValue, hiA2Max = float.MinValue;
            foreach (var p in points)
            {
                float t = (p[longAxis] - min) / Mathf.Max(0.0001f, len);
                if (t < 0.2f)
                {
                    lowA1Min = Mathf.Min(lowA1Min, p[a1]); lowA1Max = Mathf.Max(lowA1Max, p[a1]);
                    lowA2Min = Mathf.Min(lowA2Min, p[a2]); lowA2Max = Mathf.Max(lowA2Max, p[a2]);
                }
                else if (t > 0.8f)
                {
                    hiA1Min = Mathf.Min(hiA1Min, p[a1]); hiA1Max = Mathf.Max(hiA1Max, p[a1]);
                    hiA2Min = Mathf.Min(hiA2Min, p[a2]); hiA2Max = Mathf.Max(hiA2Max, p[a2]);
                }
            }
            float lowArea = (lowA1Max - lowA1Min) * (lowA2Max - lowA2Min);
            float hiArea = (hiA1Max - hiA1Min) * (hiA2Max - hiA2Min);
            Vector3 forward = Vector3.zero;
            forward[longAxis] = hiArea <= lowArea ? 1f : -1f;

            // Eje vertical: el mayor de los otros dos ejes
            int vAxis = b.size[a1] >= b.size[a2] ? a1 : a2;
            float centroid = 0f;
            foreach (var p in points) centroid += p[vAxis];
            centroid /= points.Count;
            Vector3 up = Vector3.zero;
            up[vAxis] = centroid >= b.center[vAxis] ? 1f : -1f;

            Quaternion modelToCanonical = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            return modelToCanonical.eulerAngles;
        }

        // ------------------------------------------------------------ Escena

        public static void DestroyIfExists(string name)
        {
            GameObject go = GameObject.Find(name);
            while (go != null)
            {
                Object.DestroyImmediate(go);
                go = GameObject.Find(name);
            }
        }

        public static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            bool has = false;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
