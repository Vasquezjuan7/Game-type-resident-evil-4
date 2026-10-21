using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Gameplay;
using Gameplay.RE4;
using Object = UnityEngine.Object;
using BodyPart = Gameplay.RE4.BodyPart;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Convierte "Assets/Perro Zombie" (glTF de Sketchfab, malla estatica sin huesos) en un enemigo jugable:
    /// lee el glTF, crea un esqueleto de cuadrupedo, calcula los pesos de la piel y genera el prefab
    /// con NavMeshAgent, zonas de impacto y ZombieDogAI (que anima el esqueleto por codigo).
    /// Unity no importa .gltf sin paquetes extra, por eso se lee a mano.
    /// </summary>
    public static class RE4DogBuilder
    {
        private const string SourceFolder = "Assets/Perro Zombie/";
        public const string PrefabPath = RE4EnemyBuilder.PrefabFolder + "/Perro_Zombi.prefab";
        private const string MeshPath = RE4EnemyBuilder.PrefabFolder + "/Perro_Zombi_Malla.asset";
        private const string MaterialPath = "Assets/RE4/Materials/Perro_Zombi.mat";

        // Escala: el modelo viene en centimetros y mide ~1.15 m con la cabeza alta
        private const float Scale = 0.0085f;

        // --------------------------------------------------------------- glTF minimo

        [Serializable] private class GAttr { public int POSITION = -1; public int NORMAL = -1; public int TEXCOORD_0 = -1; }
        [Serializable] private class GPrim { public GAttr attributes; public int indices = -1; }
        [Serializable] private class GMesh { public GPrim[] primitives; }
        [Serializable] private class GAccessor { public int bufferView; public int byteOffset; public int componentType; public int count; public string type; }
        [Serializable] private class GView { public int buffer; public int byteOffset; public int byteLength; public int byteStride; }
        [Serializable] private class GBuffer { public string uri; public int byteLength; }
        [Serializable] private class GRoot { public GMesh[] meshes; public GAccessor[] accessors; public GView[] bufferViews; public GBuffer[] buffers; }

        // --------------------------------------------------------------- Esqueleto (cm, x derecha, y arriba, z delante)

        private struct BoneDef
        {
            public string name;
            public int parent;
            public Vector3 pos;
            public Vector3 tip;
            public int side;   // -1 izq, 1 der, 0 centro
            public int leg;    // 0 no, 1 delantera, 2 trasera
        }

        private static readonly BoneDef[] Bones =
        {
            new BoneDef { name = "Raiz", parent = -1, pos = new Vector3(0f, 0f, 0f), tip = new Vector3(0f, 0f, 0f) },
            new BoneDef { name = "Cadera", parent = 0, pos = new Vector3(0f, 52f, -22f), tip = new Vector3(0f, 55f, 5f) },
            new BoneDef { name = "Columna", parent = 1, pos = new Vector3(0f, 55f, 5f), tip = new Vector3(0f, 57f, 32f) },
            new BoneDef { name = "Pecho", parent = 2, pos = new Vector3(0f, 57f, 32f), tip = new Vector3(0f, 68f, 46f) },
            new BoneDef { name = "Cuello", parent = 3, pos = new Vector3(0f, 68f, 46f), tip = new Vector3(0f, 84f, 56f) },
            new BoneDef { name = "Cabeza", parent = 4, pos = new Vector3(0f, 84f, 56f), tip = new Vector3(0f, 90f, 82f) },
            new BoneDef { name = "PataDel_Sup_L", parent = 3, pos = new Vector3(-8f, 48f, 45f), tip = new Vector3(-8f, 24f, 45f), side = -1, leg = 1 },
            new BoneDef { name = "PataDel_Inf_L", parent = 6, pos = new Vector3(-8f, 24f, 45f), tip = new Vector3(-8f, 0f, 50f), side = -1, leg = 1 },
            new BoneDef { name = "PataDel_Sup_R", parent = 3, pos = new Vector3(8f, 48f, 45f), tip = new Vector3(8f, 24f, 45f), side = 1, leg = 1 },
            new BoneDef { name = "PataDel_Inf_R", parent = 8, pos = new Vector3(8f, 24f, 45f), tip = new Vector3(8f, 0f, 50f), side = 1, leg = 1 },
            new BoneDef { name = "PataTras_Sup_L", parent = 1, pos = new Vector3(-13f, 46f, -16f), tip = new Vector3(-14f, 20f, -32f), side = -1, leg = 2 },
            new BoneDef { name = "PataTras_Inf_L", parent = 10, pos = new Vector3(-14f, 20f, -32f), tip = new Vector3(-15f, 0f, -30f), side = -1, leg = 2 },
            new BoneDef { name = "PataTras_Sup_R", parent = 1, pos = new Vector3(13f, 46f, -16f), tip = new Vector3(14f, 20f, -32f), side = 1, leg = 2 },
            new BoneDef { name = "PataTras_Inf_R", parent = 12, pos = new Vector3(14f, 20f, -32f), tip = new Vector3(15f, 0f, -30f), side = 1, leg = 2 },
        };

        public static GameObject Build()
        {
            string gltfPath = SourceFolder + "scene.gltf";
            if (!File.Exists(gltfPath))
            {
                Debug.LogWarning("[RE4] No se encontro el perro zombi en " + gltfPath);
                return null;
            }
            RE4EditorUtil.EnsureFolder(RE4EnemyBuilder.PrefabFolder);
            RE4EditorUtil.EnsureFolder("Assets/RE4/Materials");

            Mesh mesh = ReadMesh(gltfPath, out Vector3[] cmPositions);
            if (mesh == null) return null;

            // ---------------- Jerarquia
            GameObject root = new GameObject("Perro_Zombi");
            Transform model = new GameObject("Modelo").transform;
            model.SetParent(root.transform, false);
            Transform[] bones = new Transform[Bones.Length];
            for (int i = 0; i < Bones.Length; i++)
            {
                bones[i] = new GameObject(Bones[i].name).transform;
                bones[i].SetParent(Bones[i].parent < 0 ? model : bones[Bones[i].parent], false);
                bones[i].position = model.TransformPoint(Bones[i].pos * Scale);
                bones[i].rotation = model.rotation;
            }

            // ---------------- Pesos
            mesh.boneWeights = ComputeWeights(cmPositions);
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * model.localToWorldMatrix;
            mesh.bindposes = bind;
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh);

            GameObject skin = new GameObject("Malla");
            skin.transform.SetParent(model, false);
            SkinnedMeshRenderer smr = skin.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterial = BuildMaterial();
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0f, 0.5f, 0.1f), new Vector3(1.4f, 1.4f, 1.8f));

            // ---------------- Componentes de juego
            int bodyLayer = LayerMask.NameToLayer(RE4Layers.EnemyBodyLayerName);
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 0.9f;
            agent.baseOffset = 0f;
            agent.speed = 6f;
            agent.angularSpeed = 540f;
            agent.acceleration = 22f;
            agent.stoppingDistance = 1f;

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            CapsuleCollider body = root.AddComponent<CapsuleCollider>();
            body.direction = 2;
            body.center = new Vector3(0f, 0.45f, 0.1f);
            body.radius = 0.24f;
            body.height = 1.1f;
            if (bodyLayer >= 0) root.layer = bodyLayer;

            // Zonas de impacto
            Hitbox(bones[5], BodyPart.Head, new Vector3(0f, 0.01f, 0.08f), 0.13f, 0f, -1);
            Hitbox(bones[4], BodyPart.Head, new Vector3(0f, 0.06f, 0.04f), 0.1f, 0f, -1);
            Hitbox(bones[2], BodyPart.Body, new Vector3(0f, 0f, 0.06f), 0.2f, 0.75f, 2);
            for (int i = 6; i < bones.Length; i++)
            {
                float len = Vector3.Distance(Bones[i].pos, Bones[i].tip) * Scale;
                Vector3 mid = (Bones[i].tip - Bones[i].pos) * 0.5f * Scale;
                Hitbox(bones[i], BodyPart.Leg, mid, 0.06f, len + 0.08f, 1);
            }

            root.AddComponent<ZombieHealth>().maxHealth = 60f;
            ZombieDogAI ai = root.AddComponent<ZombieDogAI>();
            ai.model = model;
            ai.hips = bones[1];
            ai.spine = bones[2];
            ai.chest = bones[3];
            ai.neck = bones[4];
            ai.head = bones[5];
            ai.upperLegs = new[] { bones[6], bones[8], bones[10], bones[12] };
            ai.lowerLegs = new[] { bones[7], bones[9], bones[11], bones[13] };

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[RE4] Perro zombi creado: " + PrefabPath);
            return prefab;
        }

        private static void Hitbox(Transform bone, BodyPart part, Vector3 localOffset, float radius, float height, int direction)
        {
            GameObject go = new GameObject("Hitbox_" + bone.name);
            go.transform.SetParent(bone, false);
            go.transform.localPosition = localOffset;
            Collider c;
            if (direction < 0)
            {
                SphereCollider s = go.AddComponent<SphereCollider>();
                s.radius = radius;
                c = s;
            }
            else
            {
                CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
                cc.direction = direction;
                cc.radius = radius;
                cc.height = height;
                c = cc;
            }
            c.isTrigger = true;
            int layer = RE4Layers.Hitbox;
            if (layer >= 0) go.layer = layer;
            ZombieHitbox hb = go.AddComponent<ZombieHitbox>();
            hb.part = part;
        }

        // --------------------------------------------------------------- Lectura

        private static Mesh ReadMesh(string gltfPath, out Vector3[] cm)
        {
            cm = null;
            GRoot g = JsonUtility.FromJson<GRoot>(File.ReadAllText(gltfPath));
            if (g == null || g.meshes == null || g.meshes.Length == 0 || g.buffers == null || g.buffers.Length == 0)
            {
                Debug.LogWarning("[RE4] glTF del perro no valido.");
                return null;
            }
            byte[] bin = File.ReadAllBytes(SourceFolder + g.buffers[0].uri);
            GPrim prim = g.meshes[0].primitives[0];

            Vector3[] pos = ReadVec3(g, bin, prim.attributes.POSITION);
            Vector3[] nrm = prim.attributes.NORMAL >= 0 ? ReadVec3(g, bin, prim.attributes.NORMAL) : null;
            Vector2[] uv = prim.attributes.TEXCOORD_0 >= 0 ? ReadVec2(g, bin, prim.attributes.TEXCOORD_0) : null;
            int[] idx = ReadIndices(g, bin, prim.indices);

            // Sketchfab: Z arriba y la cabeza hacia -Y. Unity: Y arriba, cabeza hacia +Z (con reflejo en X)
            cm = new Vector3[pos.Length];
            var verts = new Vector3[pos.Length];
            var norms = new Vector3[pos.Length];
            var uvs = new Vector2[pos.Length];
            for (int i = 0; i < pos.Length; i++)
            {
                Vector3 p = pos[i];
                cm[i] = new Vector3(-p.x, p.z, -p.y);
                verts[i] = cm[i] * Scale;
                if (nrm != null) norms[i] = new Vector3(-nrm[i].x, nrm[i].z, -nrm[i].y).normalized;
                if (uv != null) uvs[i] = new Vector2(uv[i].x, -uv[i].y);
            }
            // el reflejo invierte el sentido de los triangulos
            for (int i = 0; i + 2 < idx.Length; i += 3)
            {
                int t = idx[i + 1];
                idx[i + 1] = idx[i + 2];
                idx[i + 2] = t;
            }

            var mesh = new Mesh { name = "Perro_Zombi" };
            if (verts.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            if (nrm != null) mesh.normals = norms;
            if (uv != null) mesh.uv = uvs;
            mesh.triangles = idx;
            if (nrm == null) mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int Stride(GAccessor a, GView v, int size) => v.byteStride > 0 ? v.byteStride : size;

        private static Vector3[] ReadVec3(GRoot g, byte[] bin, int accessor)
        {
            GAccessor a = g.accessors[accessor];
            GView v = g.bufferViews[a.bufferView];
            int stride = Stride(a, v, 12);
            int start = v.byteOffset + a.byteOffset;
            var r = new Vector3[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int o = start + i * stride;
                r[i] = new Vector3(BitConverter.ToSingle(bin, o), BitConverter.ToSingle(bin, o + 4), BitConverter.ToSingle(bin, o + 8));
            }
            return r;
        }

        private static Vector2[] ReadVec2(GRoot g, byte[] bin, int accessor)
        {
            GAccessor a = g.accessors[accessor];
            GView v = g.bufferViews[a.bufferView];
            int stride = Stride(a, v, 8);
            int start = v.byteOffset + a.byteOffset;
            var r = new Vector2[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int o = start + i * stride;
                r[i] = new Vector2(BitConverter.ToSingle(bin, o), BitConverter.ToSingle(bin, o + 4));
            }
            return r;
        }

        private static int[] ReadIndices(GRoot g, byte[] bin, int accessor)
        {
            GAccessor a = g.accessors[accessor];
            GView v = g.bufferViews[a.bufferView];
            int start = v.byteOffset + a.byteOffset;
            var r = new int[a.count];
            for (int i = 0; i < a.count; i++)
            {
                switch (a.componentType)
                {
                    case 5125: r[i] = (int)BitConverter.ToUInt32(bin, start + i * 4); break;
                    case 5123: r[i] = BitConverter.ToUInt16(bin, start + i * 2); break;
                    default: r[i] = bin[start + i]; break;
                }
            }
            return r;
        }

        // --------------------------------------------------------------- Pesos

        private static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        private static BoneWeight[] ComputeWeights(Vector3[] cm)
        {
            var result = new BoneWeight[cm.Length];
            var scores = new List<KeyValuePair<int, float>>();
            for (int v = 0; v < cm.Length; v++)
            {
                Vector3 p = cm[v];
                scores.Clear();
                for (int b = 1; b < Bones.Length; b++)
                {
                    BoneDef d = Bones[b];
                    float dist = SegmentDistance(p, d.pos, d.tip);
                    if (d.leg > 0)
                    {
                        // las patas solo afectan a su lado y por debajo del lomo
                        if (p.x * d.side < 1.5f) continue;
                        if (p.y > 54f) continue;
                        if (d.leg == 1 && p.z < 22f) continue;
                        if (d.leg == 2 && p.z > 4f) continue;
                    }
                    else if (p.y < 32f && Mathf.Abs(p.x) > 2f)
                    {
                        dist *= 3f; // los pies no deben seguir a la columna
                    }
                    float w = 1f / (Mathf.Pow(dist, 4f) + 1f);
                    scores.Add(new KeyValuePair<int, float>(b, w));
                }
                scores.Sort((x, y) => y.Value.CompareTo(x.Value));

                BoneWeight bw = new BoneWeight();
                int n = Mathf.Min(3, scores.Count);
                float sum = 0f;
                for (int i = 0; i < n; i++) sum += scores[i].Value;
                if (n == 0 || sum <= 0f) { bw.boneIndex0 = 2; bw.weight0 = 1f; result[v] = bw; continue; }
                if (n > 0) { bw.boneIndex0 = scores[0].Key; bw.weight0 = scores[0].Value / sum; }
                if (n > 1) { bw.boneIndex1 = scores[1].Key; bw.weight1 = scores[1].Value / sum; }
                if (n > 2) { bw.boneIndex2 = scores[2].Key; bw.weight2 = scores[2].Value / sum; }
                result[v] = bw;
            }
            return result;
        }

        // --------------------------------------------------------------- Assets

        private static Mesh SaveMesh(Mesh mesh)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }

        private static Material BuildMaterial()
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            m.shader = Shader.Find("Standard");
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceFolder + "material_0_baseColor.png");
            if (tex != null) m.SetTexture("_MainTex", tex);
            m.color = new Color(0.92f, 0.88f, 0.86f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", 0.28f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
