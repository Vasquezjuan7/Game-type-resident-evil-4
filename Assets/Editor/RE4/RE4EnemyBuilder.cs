using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Gameplay;
using Gameplay.RE4;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Crea los prefabs de Ganado listos para el juego: modelo humanoide con el controlador nuevo,
    /// Root Motion desactivado, pies a la altura del suelo, NavMeshAgent, capsula de cuerpo y scripts de IA/vida.
    /// </summary>
    public static class RE4EnemyBuilder
    {
        public const string PrefabFolder = "Assets/RE4/Prefabs/Enemies";

        private struct Source
        {
            public string name;
            public string path;
            public bool useZombieAvatar;
        }

        private static readonly Source[] Sources =
        {
            new Source { name = "Aldeano", path = "Assets/Zombie/Prefab/Zombie.prefab", useZombieAvatar = true },
            new Source { name = "Aldeana", path = "Assets/Female Zombie/Prefabs/Female Zombie.prefab" },
            new Source { name = "Aldeano_Torso", path = "Assets/NewPunch/ShirtlessZombieFree/Prefabs/ShirtlessZombie_FREE.prefab" },
        };

        public static List<GameObject> Build(AnimatorController controller, Avatar zombieAvatar)
        {
            RE4EditorUtil.EnsureFolder(PrefabFolder);
            var result = new List<GameObject>();
            int bodyLayer = LayerMask.NameToLayer(RE4Layers.EnemyBodyLayerName);

            foreach (var src in Sources)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(src.path);
                if (source == null)
                {
                    Debug.LogWarning("[RE4] No se encontro el modelo de enemigo: " + src.path);
                    continue;
                }

                GameObject root = new GameObject("Ganado_" + src.name);
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                if (PrefabUtility.IsPartOfPrefabInstance(model))
                {
                    PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                model.name = "Modelo";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                CleanModel(model);

                Animator anim = model.GetComponentInChildren<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                if (src.useZombieAvatar && zombieAvatar != null) anim.avatar = zombieAvatar;
                if (anim.avatar == null || !anim.avatar.isHuman)
                {
                    Debug.LogWarning($"[RE4] '{src.name}' no tiene un Avatar humanoide valido; se omite.");
                    Object.DestroyImmediate(root);
                    continue;
                }
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // Altura ~1.8 m y pies en y = 0
                Bounds b = RE4EditorUtil.WorldBounds(model);
                if (b.size.y > 0.01f && (b.size.y < 1.45f || b.size.y > 2.3f))
                {
                    model.transform.localScale *= 1.8f / b.size.y;
                    b = RE4EditorUtil.WorldBounds(model);
                }
                model.transform.localPosition += new Vector3(-(b.center.x - root.transform.position.x), -(b.min.y - root.transform.position.y), -(b.center.z - root.transform.position.z));

                NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.35f;
                agent.height = 1.8f;
                agent.baseOffset = 0f;
                agent.speed = 1.4f;
                agent.angularSpeed = 360f;
                agent.acceleration = 10f;
                agent.stoppingDistance = 1.2f;

                Rigidbody rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                CapsuleCollider body = root.AddComponent<CapsuleCollider>();
                body.center = new Vector3(0f, 0.9f, 0f);
                body.height = 1.8f;
                body.radius = 0.3f;
                if (bodyLayer >= 0) root.layer = bodyLayer;

                root.AddComponent<ZombieHealth>();
                root.AddComponent<ZombieAI>();

                string path = $"{PrefabFolder}/Ganado_{src.name}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                if (prefab != null) result.Add(prefab);
            }

            Debug.Log($"[RE4] Prefabs de enemigos creados: {result.Count}");
            return result;
        }

        private static void CleanModel(GameObject model)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
            // Primero los scripts (tienen RequireComponent), luego sus dependencias
            foreach (var c in model.GetComponentsInChildren<ZombieAI>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<ZombieHealth>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<ZombieHitbox>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<CharacterController>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }
    }
}
