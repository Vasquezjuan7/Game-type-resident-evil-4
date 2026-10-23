using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Gameplay;
using Gameplay.RE4;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Jefe Tyrant (Assets/RE4/Importados/Tyrant). Su esqueleto es el estandar de Unreal (pelvis, spine_01...),
    /// asi que se importa como Humanoid y reutiliza las animaciones y el controlador de los zombis.
    /// </summary>
    public static class RE4BossBuilder
    {
        private const string ModelPath = "Assets/RE4/Importados/Tyrant/Tyrant.fbx";
        public const string PrefabPath = RE4EnemyBuilder.PrefabFolder + "/Jefe_Tyrant.prefab";
        private const string MaterialPath = "Assets/RE4/Materials/Tyrant.mat";

        public static GameObject Build(AnimatorController controller)
        {
            ModelImporter mi = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (mi == null)
            {
                Debug.LogWarning("[RE4] No se encontro el Tyrant en " + ModelPath);
                return null;
            }
            if (mi.animationType != ModelImporterAnimationType.Human || mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = false;
                mi.SaveAndReimport();
            }

            Avatar avatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ModelPath)) if (o is Avatar a) avatar = a;
            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogWarning("[RE4] Unity no pudo crear un Avatar humanoide para el Tyrant (revisa Rig > Configure en el FBX). No se crea el jefe.");
                return null;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject root = new GameObject("Jefe_Tyrant");
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Modelo";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            Material mat = BuildMaterial();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }

            Animator anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Mide unos 2.4 m y los pies en el suelo
            Bounds b = RE4EditorUtil.WorldBounds(model);
            if (b.size.y > 0.01f) model.transform.localScale *= 2.4f / b.size.y;
            b = RE4EditorUtil.WorldBounds(model);
            model.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.5f;
            agent.height = 2.4f;
            agent.baseOffset = 0f;
            agent.speed = 1.3f;
            agent.angularSpeed = 240f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 1.8f;

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            CapsuleCollider body = root.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 1.2f, 0f);
            body.height = 2.4f;
            body.radius = 0.45f;
            int bodyLayer = LayerMask.NameToLayer(RE4Layers.EnemyBodyLayerName);
            if (bodyLayer >= 0) root.layer = bodyLayer;

            ZombieHealth hp = root.AddComponent<ZombieHealth>();
            hp.maxHealth = 2200f;
            hp.dropChance = 1f;

            ZombieAI ai = root.AddComponent<ZombieAI>();
            ai.heavy = true;
            ai.heavyStaggerDamage = 320f;
            ai.walkSpeed = 1.3f;
            ai.runSpeed = 2.5f;
            ai.isRunner = false;
            ai.sightRange = 45f;
            ai.sightAngle = 120f;
            ai.attackRange = 2.1f;
            ai.attackDamage = 32f;
            ai.attackCooldown = 2.2f;
            ai.attackHitTime = 0.6f;
            ai.attackDuration = 1.5f;
            ai.grabChance = 0f;
            ai.staggerDuration = 1.4f;
            ai.startAlerted = true;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[RE4] Jefe Tyrant creado: " + PrefabPath);
            return prefab;
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
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RE4/Importados/Tyrant/T_Tyrant_D.png"));
            const string normalPath = "Assets/RE4/Importados/Tyrant/T_Tyrant_N.png";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            Texture2D n = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (n != null)
            {
                m.SetTexture("_BumpMap", n);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", 0.3f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
