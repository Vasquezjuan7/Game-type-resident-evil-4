using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Gameplay;
using Gameplay.RE4;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Menu "RE4": construye todo el juego estilo Resident Evil 4 en la escena SampleScene.
    /// </summary>
    public static class RE4Setup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SystemsName = "--- RE4 SISTEMAS ---";
        private const string PointsName = "--- RE4 PUNTOS ---";
        private const string ItemsName = "--- RE4 OBJETOS ---";
        private const string NavMeshPath = "Assets/RE4/NavMesh/NavMesh_Pueblo.asset";

        [MenuItem("RE4/★ Construir pueblo RE4 completo", priority = 0)]
        public static void BuildAll()
        {
            if (!EnsureScene()) return;
            if (!EditorUtility.DisplayDialog("Construir pueblo RE4",
                    "Se va a reconstruir SampleScene:\n\n" +
                    "• Mundo ampliado de noche: pueblo, granja, iglesia, capilla vieja, aserradero y bosque\n" +
                    "• Historia del Capítulo 1 con Tomás (NPC con IA), documentos y radio\n" +
                    "• Leon en primera persona: parry, sigilo, fabricación y mercader\n" +
                    "• Ganados, perros zombi y el Tyrant\n\n" +
                    "El pueblo y los sistemas anteriores se reemplazan. ¿Continuar?", "Construir", "Cancelar"))
                return;

            try
            {
                Progress("Capas y configuración", 0.02f);
                RE4EditorUtil.EnsureLayers(RE4Layers.WeaponLayerName, RE4Layers.HitboxLayerName, RE4Layers.EnemyBodyLayerName);
                ReportRenderPipeline();

                Progress("Convirtiendo zombis a Humanoid", 0.08f);
                Avatar zombieAvatar = RE4ZombieRig.ConvertBaseMesh();
                if (zombieAvatar != null) RE4ZombieRig.ConvertAnimations(zombieAvatar);
                var controller = RE4ZombieRig.BuildController();

                Progress("Datos de armas y objetos", 0.25f);
                RE4ImportBuilder.BuildAll(); // casas y animales descargados
                var db = RE4DataBuilder.Build();
                RE4SoundBuilder.Build(db);

                Progress("Prefabs de enemigos", 0.32f);
                List<GameObject> enemies = RE4EnemyBuilder.Build(controller, zombieAvatar);
                GameObject dog = RE4DogBuilder.Build();
                GameObject boss = RE4BossBuilder.Build(controller);

                Progress("Limpiando escena anterior", 0.38f);
                CleanScene();

                Progress("Construyendo el pueblo", 0.45f);
                var village = RE4VillageBuilder.Build(1987);
                RE4VillageBuilder.AddExpansion(village.root);

                Progress("Iluminación y niebla", 0.6f);
                SetupAtmosphere();

                Progress("Horneando NavMesh", 0.68f);
                BakeNavMesh(village.root);

                Progress("Leon y sistemas", 0.82f);
                GameObject leon = BuildPlayer(db, village.playerStart, village.playerRotation);
                BuildSystems(db, enemies, dog, village);
                SetBoss(boss);
                PlaceItems(db);
                PlaceNewItems(db);
                PlaceExtraLoot(db);
                PlaceAnimals();

                Progress("Historia, NPC y ambiente", 0.9f);
                RE4StoryBuilder.Build(db, village, leon);

                Progress("Guardando", 0.95f);
                SetBuildScenes();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
                if (leon != null) Selection.activeGameObject = leon;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog("¡Pueblo RE4 listo!",
                "Pulsa Play (▶).\n\n" +
                "CONTROLES\n" +
                "WASD mover · Shift correr · S+Shift giro rápido · L linterna\n" +
                "Clic derecho apuntar (láser) · Clic izquierdo disparar · R recargar\n" +
                "1-5 / rueda cambiar arma\n" +
                "V cuchillo (pulsa al recibir un golpe = PARRY, mantén = guardia, por la espalda = sigilo)\n" +
                "F patada a un enemigo aturdido · E recoger / hablar\n" +
                "TAB maletín (C fabricar munición) · J archivos · U tienda junto al mercader\n" +
                "ESPACIO repetido para soltarte de un agarre\n\n" +
                "IA: configura tu clave en RE4 > Configurar IA para hablar libremente con Tomás.\n\n" +
                "Empiezas con la Pistola 92F (1) y la Pistola Resident (2).\n" +
                "Armas en el pueblo: escopeta (granja), subfusil (torre de la plaza), AK-47 (iglesia), lanzacohetes (detrás de la iglesia).\n" +
                "Cuidado con los perros zombi: rodean y saltan.\n\n" +
                "Los valores de cada arma están en Assets/RE4/Data.", "¡A jugar!");
        }

        [MenuItem("RE4/Rehornear NavMesh", priority = 20)]
        public static void RebakeOnly()
        {
            GameObject root = GameObject.Find(RE4VillageBuilder.RootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("RE4", "No se encontró el pueblo. Usa primero 'Construir pueblo RE4 completo'.", "OK");
                return;
            }
            BakeNavMesh(root);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        [MenuItem("RE4/Regenerar enemigos (rig + prefabs)", priority = 21)]
        public static void RebuildEnemiesOnly()
        {
            RE4EditorUtil.EnsureLayers(RE4Layers.WeaponLayerName, RE4Layers.HitboxLayerName, RE4Layers.EnemyBodyLayerName);
            Avatar avatar = RE4ZombieRig.ConvertBaseMesh();
            if (avatar != null) RE4ZombieRig.ConvertAnimations(avatar);
            var controller = RE4ZombieRig.BuildController();
            var enemies = RE4EnemyBuilder.Build(controller, avatar);
            var mgr = Object.FindAnyObjectByType<HordeWaveManager>();
            if (mgr != null)
            {
                mgr.zombiePrefabs = enemies;
                EditorUtility.SetDirty(mgr);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        /// <summary>Aplica armas nuevas, mejoras, mercader y perros a la escena ya construida (sin rehacer el pueblo).</summary>
        [MenuItem("RE4/Actualizar armas, mejoras y perros (sin reconstruir)", priority = 10)]
        public static void UpdateWeaponsAndDogs()
        {
            if (!EnsureScene()) return;
            try
            {
                Progress("Capas", 0.05f);
                RE4EditorUtil.EnsureLayers(RE4Layers.WeaponLayerName, RE4Layers.HitboxLayerName, RE4Layers.EnemyBodyLayerName);
                Progress("Preparando pistolas y AK-47", 0.2f);
                RE4ImportBuilder.BuildAll(); // casas y animales descargados
                var db = RE4DataBuilder.Build();
                RE4SoundBuilder.Build(db);
                Progress("Creando el perro zombi", 0.6f);
                GameObject dog = RE4DogBuilder.Build();
                Progress("Creando el jefe Tyrant", 0.75f);
                GameObject boss = RE4BossBuilder.Build(RE4ZombieRig.BuildController());

                Progress("Actualizando la escena", 0.85f);
                SetBoss(boss);
                GameObject villageRoot = GameObject.Find(RE4VillageBuilder.RootName);
                if (villageRoot != null)
                {
                    Progress("Control abandonado y coches en los caminos", 0.88f);
                    RE4VillageBuilder.AddExpansion(villageRoot);
                    BakeNavMesh(villageRoot);
                }
                PlaceNewItems(db);
                PlaceExtraLoot(db);
                PlaceAnimals();
                AttacheCase attache = Object.FindAnyObjectByType<AttacheCase>();
                if (attache != null)
                {
                    SetStartingItems(attache, db);
                    Ensure<WeaponUpgradeUI>(attache.gameObject);
                    EditorUtility.SetDirty(attache);
                }
                var mgr = Object.FindAnyObjectByType<HordeWaveManager>();
                if (mgr != null)
                {
                    mgr.dogPrefab = dog;
                    mgr.pesetasItem = db.pesetas;
                    mgr.ammoDrops = new List<ItemDefinition> { db.ammoHandgun, db.ammoShotgun, db.ammoSmg, db.ammoRifle, db.ammoSniper };
                    EditorUtility.SetDirty(mgr);
                }
                foreach (var sp in Object.FindObjectsByType<PickupSpawner>(FindObjectsSortMode.None))
                {
                    if (sp.item == db.ammoRifle && sp.count < 30) { sp.count = 30; EditorUtility.SetDirty(sp); }
                }
                Progress("Historia, NPC, noche y sonidos", 0.92f);
                SetupAtmosphere();
                RE4StoryBuilder.Build(db, null, attache != null ? attache.gameObject : GameObject.Find("Leon"));

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            EditorUtility.DisplayDialog("RE4 actualizado",
                "• Pistola 92F y Pistola Resident (corredera y cargador animados)\n" +
                "• AK-47 automático con cargador de 30\n" +
                "• Escopeta y subfusil con modelos nuevos y rifle de francotirador (capilla vieja)\n" +
                "• Mucha más munición: alijos en cada zona y el mercader vende balas\n" +
                "• Casas nuevas (clásicas, de madera y la del veterano) repartidas por el pueblo\n" +
                "• Corral con vacas y cerdos que pastan, huyen de los disparos y se pueden abatir; cadáveres de animales\n" +
                "• Control de carretera abandonado y coches oxidados en los caminos\n" +
                "• Mercader junto al inicio: [E] o tecla U para mejorar armas\n" +
                "• Perros zombi en las oleadas y merodeando por el pueblo\n" +
                "• Revólver Requiem (junto a la plaza) con tambor animado\n" +
                "• Lanzacohetes nuevo de 4 tubos\n" +
                "• Spray, hierbas y cajas de munición con modelo 3D\n" +
                "• Jefe Tyrant en la última oleada\n" +
                "• Noche, linterna (L), sonidos de armas, pasos y ambiente\n" +
                "• Historia: Tomás (IA), documentos (J), radio y final del capítulo\n" +
                "• Parry y sigilo con cuchillo, fabricación (C en el maletín), comprar/vender\n\n" +
                "El mundo ampliado (capilla, aserradero, bosque) necesita 'Construir pueblo RE4 completo'.", "OK");
        }

        [MenuItem("RE4/Configurar IA (clave de Claude)", priority = 30)]
        public static void ConfigureAI() => RE4AIKeyWindow.Open();

        [MenuItem("RE4/Seleccionar datos de armas", priority = 40)]
        public static void SelectData()
        {
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(RE4DataBuilder.DataFolder);
            if (folder != null)
            {
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
        }

        // =========================================================== Pasos

        private static void Progress(string msg, float t) => EditorUtility.DisplayProgressBar("Construyendo pueblo RE4", msg, t);

        private static bool EnsureScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        private static void ReportRenderPipeline()
        {
            RenderPipelineAsset rp = GraphicsSettings.defaultRenderPipeline;
            if (rp != null)
            {
                Debug.LogWarning($"[RE4] Render pipeline activo: {rp.GetType().Name}. Los packs de este proyecto usan materiales Standard (Built-in); " +
                                 "si ves objetos rosas, quita el asset en Project Settings > Graphics.");
            }
            else
            {
                Debug.Log("[RE4] Render pipeline: Built-in (compatible con los materiales Standard de los packs).");
            }
        }

        private static void CleanScene()
        {
            string[] old =
            {
                RE4VillageBuilder.RootName, SystemsName, PointsName, ItemsName, RE4StoryBuilder.RootName, "Mercader",
                "--- ENTORNO PUEBLO ---", "--- SPWAN_POINTS_HORDAS ---", "--- MUNICION DISPERSA ---", "--- HORDA DE ZOMBIES ---",
                "Horde_Manager", "PlayerHUD_Canvas", "Horde_Canvas", AnimalsRootName, "Town_Ground", "Terreno_Pueblo_Cesped",
            };
            foreach (var n in old) RE4EditorUtil.DestroyIfExists(n);
        }

        private static void SetupAtmosphere()
        {
            Light moon = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (moon == null)
            {
                moon = new GameObject("Luna").AddComponent<Light>();
                moon.type = LightType.Directional;
            }
            // Noche cerrada con luna fria y niebla: el terror del pueblo de RE4
            moon.name = "Luna";
            moon.transform.rotation = Quaternion.Euler(32f, -150f, 0f);
            moon.color = new Color(0.55f, 0.64f, 0.9f);
            moon.intensity = 0.2f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.7f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.07f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.1f, 0.11f, 0.16f);
            RenderSettings.ambientEquatorColor = new Color(0.06f, 0.06f, 0.08f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.025f);
            RenderSettings.sun = moon;

            Shader sky = Shader.Find("Skybox/Procedural");
            if (sky != null)
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>(RE4DataBuilder.MatFolder + "/Cielo_Noche.mat");
                if (m == null)
                {
                    m = new Material(sky);
                    AssetDatabase.CreateAsset(m, RE4DataBuilder.MatFolder + "/Cielo_Noche.mat");
                }
                m.shader = sky;
                m.SetFloat("_SunSize", 0.03f);
                m.SetFloat("_AtmosphereThickness", 0.35f);
                m.SetColor("_SkyTint", new Color(0.18f, 0.2f, 0.32f));
                m.SetColor("_GroundColor", new Color(0.03f, 0.03f, 0.04f));
                m.SetFloat("_Exposure", 0.22f);
                EditorUtility.SetDirty(m);
                RenderSettings.skybox = m;
            }

            // Las hogueras y antorchas iluminan algo mas de noche
            foreach (var f in Object.FindObjectsByType<FlickerLight>(FindObjectsSortMode.None))
            {
                Light l = f.GetComponent<Light>();
                if (l == null || l.name == "Candil") continue;
                l.range = Mathf.Max(l.range, 9f) * 1.15f;
                f.baseIntensity = Mathf.Max(f.baseIntensity, 1.3f) * 1.2f;
                EditorUtility.SetDirty(f);
            }
        }

        private static void BakeNavMesh(GameObject root)
        {
            NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
            if (surface == null) surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            int mask = ~0;
            foreach (string l in new[] { RE4Layers.WeaponLayerName, RE4Layers.HitboxLayerName, RE4Layers.EnemyBodyLayerName, "Ignore Raycast" })
            {
                int idx = LayerMask.NameToLayer(l);
                if (idx >= 0) mask &= ~(1 << idx);
            }
            surface.layerMask = mask;
            surface.BuildNavMesh();

            if (surface.navMeshData != null)
            {
                RE4EditorUtil.EnsureFolder("Assets/RE4/NavMesh");
                if (AssetDatabase.LoadAssetAtPath<Object>(NavMeshPath) != null) AssetDatabase.DeleteAsset(NavMeshPath);
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
                EditorUtility.SetDirty(surface);
                Debug.Log("[RE4] NavMesh horneada y guardada en " + NavMeshPath);
            }
            else
            {
                Debug.LogError("[RE4] No se pudo hornear la NavMesh.");
            }
        }

        // =========================================================== Jugador

        private static GameObject BuildPlayer(RE4DataBuilder.Database db, Vector3 start, Quaternion rot)
        {
            GameObject leon = GameObject.Find("Leon");
            if (leon == null)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Leon Keneddy/fbx_explore_survivor_leon.fbx");
                leon = model != null ? (GameObject)PrefabUtility.InstantiatePrefab(model) : new GameObject();
                leon.name = "Leon";
            }

            foreach (Transform t in leon.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (var c in leon.GetComponents<PlayerMovement>()) Object.DestroyImmediate(c);
            foreach (var c in leon.GetComponents<SimpleSmoothFollow>()) Object.DestroyImmediate(c);
            foreach (var c in leon.GetComponents<LineRenderer>()) Object.DestroyImmediate(c);
            Animator anim = leon.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            leon.tag = "Player";
            leon.transform.SetPositionAndRotation(start, rot);

            // Cuerpo oculto en primera persona
            foreach (var r in leon.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            CharacterController cc = leon.GetComponent<CharacterController>();
            if (cc == null) cc = leon.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.06f;
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;

            Transform holder = leon.transform.Find("CameraHolder");
            if (holder == null)
            {
                holder = new GameObject("CameraHolder").transform;
                holder.SetParent(leon.transform, false);
            }
            holder.localPosition = new Vector3(0f, 1.62f, 0.05f);
            holder.localRotation = Quaternion.identity;

            // Camara principal
            Camera main = Camera.main;
            if (main == null)
            {
                main = new GameObject("Main Camera").AddComponent<Camera>();
                main.tag = "MainCamera";
                main.gameObject.AddComponent<AudioListener>();
            }
            main.transform.SetParent(holder, false);
            main.transform.localPosition = Vector3.zero;
            main.transform.localRotation = Quaternion.identity;
            main.nearClipPlane = 0.05f;
            main.farClipPlane = 450f;
            main.fieldOfView = 68f;
            main.clearFlags = CameraClearFlags.Skybox;
            int weaponLayer = LayerMask.NameToLayer(RE4Layers.WeaponLayerName);
            main.cullingMask = weaponLayer >= 0 ? ~(1 << weaponLayer) : ~0;
            main.depth = 0;
            if (main.GetComponent<AudioListener>() == null) main.gameObject.AddComponent<AudioListener>();

            // Camara de armas (evita que el arma atraviese paredes)
            Transform wcT = main.transform.Find("WeaponCamera");
            Camera weaponCam = wcT != null ? wcT.GetComponent<Camera>() : null;
            if (weaponCam == null)
            {
                weaponCam = new GameObject("WeaponCamera").AddComponent<Camera>();
                weaponCam.transform.SetParent(main.transform, false);
            }
            weaponCam.clearFlags = CameraClearFlags.Depth;
            weaponCam.cullingMask = weaponLayer >= 0 ? 1 << weaponLayer : 0;
            weaponCam.depth = 1;
            weaponCam.nearClipPlane = 0.01f;
            weaponCam.farClipPlane = 10f;
            weaponCam.fieldOfView = 68f;

            Transform weaponHolder = holder.Find("WeaponHolder");
            if (weaponHolder == null)
            {
                weaponHolder = new GameObject("WeaponHolder").transform;
                weaponHolder.SetParent(holder, false);
            }
            weaponHolder.localPosition = Vector3.zero;
            weaponHolder.localRotation = Quaternion.identity;
            for (int i = weaponHolder.childCount - 1; i >= 0; i--) Object.DestroyImmediate(weaponHolder.GetChild(i).gameObject);

            var fps = Ensure<FirstPersonController>(leon);
            fps.cameraHolder = holder;
            fps.weaponHolder = weaponHolder;
            fps.weaponCamera = weaponCam;

            Ensure<PlayerHealth>(leon);

            var attache = Ensure<AttacheCase>(leon);
            attache.width = 12;
            attache.height = 8;
            attache.mixGreenGreen = db.herbGG;
            attache.mixGreenRed = db.herbGR;
            SetStartingItems(attache, db);

            var weapons = Ensure<WeaponSystem>(leon);
            weapons.weaponHolder = weaponHolder;

            Ensure<PlayerInteractor>(leon);
            Ensure<AttacheCaseUI>(leon);
            Ensure<WeaponUpgradeUI>(leon);
            Ensure<RE4HUD>(leon);

            EditorUtility.SetDirty(leon);
            return leon;
        }

        private static void SetStartingItems(AttacheCase attache, RE4DataBuilder.Database db)
        {
            attache.startingPesetas = 5000;
            attache.startingItems = new List<StartingItem>
            {
                new StartingItem { item = db.itemPistol, count = 1 },
                new StartingItem { item = db.itemPistol2, count = 1 },
                new StartingItem { item = db.ammoHandgun, count = 45 },
                new StartingItem { item = db.herbGreen, count = 1 },
            };
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        // =========================================================== Sistemas y objetos

        private static void BuildSystems(RE4DataBuilder.Database db, List<GameObject> enemies, GameObject dog, RE4VillageBuilder.Result village)
        {
            GameObject systems = new GameObject(SystemsName);
            FXManager fx = systems.AddComponent<FXManager>();
            fx.library = db.fx;

            HordeWaveManager mgr = systems.AddComponent<HordeWaveManager>();
            mgr.zombiePrefabs = enemies;
            mgr.ammoDrops = new List<ItemDefinition> { db.ammoHandgun, db.ammoShotgun, db.ammoSmg, db.ammoRifle, db.ammoSniper };
            mgr.greenHerb = db.herbGreen;
            mgr.redHerb = db.herbRed;
            mgr.dogPrefab = dog;
            mgr.pesetasItem = db.pesetas;

            GameObject points = new GameObject(PointsName);
            Transform spawns = new GameObject("Accesos_Oleadas").transform;
            spawns.SetParent(points.transform, false);
            Transform villagers = new GameObject("Aldeanos_Iniciales").transform;
            villagers.SetParent(points.transform, false);

            int i = 0;
            foreach (var p in village.spawnPoints)
            {
                Transform t = new GameObject($"Acceso_{++i}").transform;
                t.SetParent(spawns, false);
                t.position = Snap(p);
                t.rotation = Quaternion.LookRotation(-new Vector3(p.x, 0f, p.z).normalized + Vector3.forward * 0.0001f);
                mgr.spawnPoints.Add(t);
            }
            i = 0;
            foreach (var p in village.villagerPoints)
            {
                Transform t = new GameObject($"Aldeano_{++i}").transform;
                t.SetParent(villagers, false);
                t.position = Snap(p);
                t.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                mgr.villagerPoints.Add(t);
            }
            EditorUtility.SetDirty(mgr);
        }

        private static Vector3 Snap(Vector3 p)
        {
            if (NavMesh.SamplePosition(p + Vector3.up, out NavMeshHit hit, 8f, NavMesh.AllAreas)) return hit.position;
            return p;
        }

        private static void PlaceItems(RE4DataBuilder.Database db)
        {
            GameObject root = new GameObject(ItemsName);

            void Put(ItemDefinition item, int count, Vector3 pos)
            {
                if (item == null) return;
                GameObject go = new GameObject("Objeto_" + item.id);
                go.transform.SetParent(root.transform, false);
                go.transform.position = Snap(pos);
                PickupSpawner s = go.AddComponent<PickupSpawner>();
                s.item = item;
                s.count = count;
            }

            // Armas (como en RE4, repartidas por el pueblo)
            Put(db.itemShotgun, 1, new Vector3(-66f, 0f, 40f));     // granja
            Put(db.ammoShotgun, 8, new Vector3(-64f, 0f, 42f));
            Put(db.itemSmg, 1, new Vector3(9f, 0f, 10f));            // torre de la plaza
            Put(db.ammoSmg, 60, new Vector3(10f, 0f, 8f));
            Put(db.itemRifle, 1, new Vector3(12f, 0f, 98f));         // patio de la iglesia
            Put(db.ammoRifle, 30, new Vector3(14f, 0f, 97f));
            Put(db.ammoRifle, 30, new Vector3(-28f, 0f, 100f));
            Put(db.pesetas, 2500, new Vector3(-62f, 0f, 36f));
            Put(db.pesetas, 3000, new Vector3(-6f, 0f, 14f));
            Put(db.pesetas, 4000, new Vector3(10f, 0f, 120f));
            Put(db.itemRocket, 1, new Vector3(4f, 0f, 136f));        // detras de la iglesia
            Put(db.ammoRocket, 1, new Vector3(6f, 0f, 136f));

            // Municion y curacion a lo largo del recorrido
            Vector3[] handgun = { new Vector3(3f, 0f, -125f), new Vector3(-4f, 0f, -90f), new Vector3(5f, 0f, -55f), new Vector3(-30f, 0f, 4f), new Vector3(35f, 0f, -4f), new Vector3(-3f, 0f, 60f), new Vector3(90f, 0f, 4f) };
            foreach (var p in handgun) Put(db.ammoHandgun, 15, p);
            Put(db.ammoShotgun, 5, new Vector3(-100f, 0f, 4f));
            Put(db.ammoShotgun, 5, new Vector3(4f, 0f, 80f));
            Put(db.ammoSmg, 40, new Vector3(60f, 0f, -4f));

            Vector3[] greens = { new Vector3(-5f, 0f, -110f), new Vector3(6f, 0f, -30f), new Vector3(-70f, 0f, 30f), new Vector3(45f, 0f, 4f), new Vector3(-20f, 0f, 92f), new Vector3(120f, 0f, -4f) };
            foreach (var p in greens) Put(db.herbGreen, 1, p);
            Put(db.herbRed, 1, new Vector3(-88f, 0f, 40f));
            Put(db.herbRed, 1, new Vector3(-30f, 0f, 108f));
            Put(db.herbRed, 1, new Vector3(-115f, 0f, -4f));
            Put(db.spray, 1, new Vector3(8f, 0f, 112f));
        }

        private static void SetBoss(GameObject boss)
        {
            var mgr = Object.FindAnyObjectByType<HordeWaveManager>();
            if (mgr == null) return;
            mgr.bossPrefab = boss;
            mgr.bossName = "TYRANT";
            EditorUtility.SetDirty(mgr);
        }

        public const string AnimalsRootName = "RE4_Animales";

        /// <summary>
        /// Animales de granja (fuera de la raiz del pueblo para que no tapen la NavMesh): vacas y cerdos en el
        /// corral, cerdos sueltos junto a la choza de Tomas, vacas pastando y algunos cadaveres por el camino.
        /// </summary>
        private static void PlaceAnimals()
        {
            GameObject old = GameObject.Find(AnimalsRootName);
            if (old != null) Object.DestroyImmediate(old);
            GameObject cow = AssetDatabase.LoadAssetAtPath<GameObject>(RE4ImportBuilder.CowPrefab);
            GameObject pig = AssetDatabase.LoadAssetAtPath<GameObject>(RE4ImportBuilder.PigPrefab);
            if (cow == null && pig == null) return;
            GameObject root = new GameObject(AnimalsRootName);

            void Spawn(GameObject prefab, Vector3 pos, float radius, bool dead)
            {
                if (prefab == null) return;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(root.transform, true);
                go.transform.position = Snap(pos);
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                var fa = go.GetComponent<FarmAnimal>();
                if (fa != null)
                {
                    fa.homeRadius = radius;
                    fa.startDead = dead;
                    EditorUtility.SetDirty(fa);
                }
            }
            Vector3 Around(Vector3 c, float r)
            {
                Vector2 v = Random.insideUnitCircle * r;
                return c + new Vector3(v.x, 0f, v.y);
            }

            // Corral
            Vector3 pen = RE4VillageBuilder.PenCenter;
            for (int i = 0; i < 3; i++) Spawn(cow, Around(pen, 4f), 5f, false);
            for (int i = 0; i < 4; i++) Spawn(pig, Around(pen, 4.5f), 5f, false);
            // Sueltos
            for (int i = 0; i < 2; i++) Spawn(pig, Around(new Vector3(-104f, 0f, -18f), 3f), 8f, false);  // choza de Tomas
            for (int i = 0; i < 2; i++) Spawn(cow, Around(new Vector3(-52f, 0f, 62f), 4f), 12f, false);    // prado de la granja
            Spawn(cow, Around(new Vector3(120f, 0f, -14f), 3f), 10f, false);                                // camino del aserradero

            // Cadaveres: algo se los ha comido
            Vector3? cp = ZoneCenter(RE4VillageBuilder.CheckpointName);
            if (cp.HasValue) Spawn(cow, cp.Value + new Vector3(-7f, 0f, -6f), 0f, true);
            Spawn(cow, new Vector3(9f, 0f, 160f), 0f, true);                                                // camino del bosque
            Vector3? chapel = ZoneCenter("Capilla_Vieja");
            if (chapel.HasValue) Spawn(pig, chapel.Value + new Vector3(7f, 0f, -7f), 0f, true);
            Vector3? saw = ZoneCenter("Aserradero");
            if (saw.HasValue) Spawn(pig, saw.Value + new Vector3(-8f, 0f, 6f), 0f, true);
        }

        /// <summary>Centro de una zona del pueblo (media de sus hijos), o null si no existe en la escena.</summary>
        private static Vector3? ZoneCenter(string name)
        {
            GameObject g = GameObject.Find(name);
            if (g == null || g.transform.childCount == 0) return null;
            Vector3 sum = Vector3.zero;
            foreach (Transform c in g.transform) sum += c.position;
            Vector3 p = sum / g.transform.childCount;
            p.y = 0f;
            return p;
        }

        /// <summary>
        /// Rifle de francotirador y mucha mas municion repartida por todo el mapa (alijos en cada zona).
        /// Se puede llamar varias veces: borra lo que coloco antes (objetos "Extra_").
        /// </summary>
        private static void PlaceExtraLoot(RE4DataBuilder.Database db)
        {
            GameObject root = GameObject.Find(ItemsName);
            if (root == null) root = new GameObject(ItemsName);
            foreach (var sp in root.GetComponentsInChildren<PickupSpawner>(true))
                if (sp.name.StartsWith("Extra_")) Object.DestroyImmediate(sp.gameObject);

            void Put(ItemDefinition item, int count, Vector3 pos)
            {
                if (item == null) return;
                GameObject go = new GameObject("Extra_" + item.id);
                go.transform.SetParent(root.transform, false);
                go.transform.position = Snap(pos);
                PickupSpawner s = go.AddComponent<PickupSpawner>();
                s.item = item;
                s.count = count;
            }
            // Alijo: varias cajas juntas alrededor de un punto
            void Stash(Vector3 c, params (ItemDefinition item, int count)[] items)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    float a = i * Mathf.PI * 2f / Mathf.Max(1, items.Length);
                    Put(items[i].item, items[i].count, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.2f);
                }
            }

            // --- Rifle de francotirador: en la capilla vieja (premio por explorar); si no existe, en la torre
            Vector3 chapel = ZoneCenter("Capilla_Vieja") ?? new Vector3(16f, 0f, 10f);
            Put(db.itemSniper, 1, chapel + new Vector3(2f, 0f, 2f));
            Put(db.ammoSniper, 10, chapel + new Vector3(3.5f, 0f, 1f));
            Put(db.ammoSniper, 5, new Vector3(-12f, 0f, 104f));          // iglesia
            Put(db.ammoSniper, 5, new Vector3(18f, 0f, 16f));            // pie de la torre

            // --- Municion a lo largo del camino y la plaza
            Vector3[] handgun = { new Vector3(-6f, 0f, -140f), new Vector3(6f, 0f, -75f), new Vector3(-14f, 0f, -20f), new Vector3(20f, 0f, 22f),
                                  new Vector3(-45f, 0f, 20f), new Vector3(60f, 0f, 12f), new Vector3(-10f, 0f, 75f), new Vector3(25f, 0f, 110f) };
            foreach (var p in handgun) Put(db.ammoHandgun, 15, p);
            Put(db.ammoShotgun, 6, new Vector3(-50f, 0f, 30f));
            Put(db.ammoShotgun, 6, new Vector3(30f, 0f, 60f));
            Put(db.ammoShotgun, 6, new Vector3(-20f, 0f, -60f));
            Put(db.ammoSmg, 50, new Vector3(-30f, 0f, 60f));
            Put(db.ammoSmg, 50, new Vector3(75f, 0f, -10f));
            Put(db.ammoRifle, 30, new Vector3(40f, 0f, 90f));
            Put(db.ammoMagnum, 4, new Vector3(-90f, 0f, -10f));
            Put(db.gunpowder, 3, new Vector3(0f, 0f, -100f));
            Put(db.gunpowder, 3, new Vector3(-35f, 0f, 45f));
            Put(db.gunpowder, 3, new Vector3(50f, 0f, 30f));

            // --- Alijos en cada zona (granja, iglesia, aserradero, bosque, capilla)
            Stash(new Vector3(-74f, 0f, 44f), (db.ammoHandgun, 20), (db.ammoShotgun, 6), (db.herbGreen, 1));
            Stash(new Vector3(8f, 0f, 104f), (db.ammoHandgun, 20), (db.ammoRifle, 30), (db.gunpowder, 2));
            Vector3? saw = ZoneCenter("Aserradero");
            if (saw.HasValue) Stash(saw.Value + new Vector3(4f, 0f, -6f), (db.ammoShotgun, 8), (db.ammoSmg, 60), (db.ammoHandgun, 20), (db.resourceS, 2));
            Vector3? camp = ZoneCenter("Campamento_Bosque");
            if (camp.HasValue) Stash(camp.Value + new Vector3(-4f, 0f, 4f), (db.ammoSniper, 5), (db.ammoHandgun, 25), (db.ammoShotgun, 6), (db.herbRed, 1));
            Stash(chapel + new Vector3(-5f, 0f, -3f), (db.ammoHandgun, 20), (db.ammoMagnum, 4), (db.herbGreen, 1));

            // Control abandonado: lo que dejaron los agentes desaparecidos
            Vector3? checkpoint = ZoneCenter(RE4VillageBuilder.CheckpointName);
            if (checkpoint.HasValue)
            {
                Vector3 cp = checkpoint.Value;
                Stash(cp + new Vector3(2f, 0f, 3f), (db.ammoHandgun, 25), (db.ammoSmg, 60), (db.ammoRifle, 30), (db.spray, 1));
                Put(db.dogtag, 1, cp + new Vector3(-3f, 0f, -2f));
                Put(db.dogtag, 1, cp + new Vector3(5f, 0f, -4f));
                Put(db.ammoSniper, 5, cp + new Vector3(-6f, 0f, 4f));
            }
        }

        /// <summary>Revolver y su municion (se puede llamar sobre una escena ya construida).</summary>
        private static void PlaceNewItems(RE4DataBuilder.Database db)
        {
            GameObject root = GameObject.Find(ItemsName);
            if (root == null) root = new GameObject(ItemsName);
            foreach (var sp in root.GetComponentsInChildren<PickupSpawner>(true))
            {
                if (sp.item == db.itemMagnum || sp.item == db.ammoMagnum) Object.DestroyImmediate(sp.gameObject);
            }
            void Put(ItemDefinition item, int count, Vector3 pos)
            {
                if (item == null) return;
                GameObject go = new GameObject("Objeto_" + item.id);
                go.transform.SetParent(root.transform, false);
                go.transform.position = Snap(pos);
                PickupSpawner s = go.AddComponent<PickupSpawner>();
                s.item = item;
                s.count = count;
            }
            Put(db.itemMagnum, 1, new Vector3(30f, 0f, -6f));   // camino este de la plaza
            Put(db.ammoMagnum, 6, new Vector3(32f, 0f, -6f));
            Put(db.ammoMagnum, 6, new Vector3(-8f, 0f, 96f));    // iglesia
            Put(db.ammoMagnum, 6, new Vector3(-60f, 0f, 44f));   // granja
        }

        /// <summary>El mercader espera con su farol a un lado del camino, cerca del inicio.</summary>
        private static void PlaceMerchant(Vector3 start, Quaternion rot)
        {
            RE4EditorUtil.DestroyIfExists("Mercader");
            GameObject m = new GameObject("Mercader");
            GameObject items = GameObject.Find(ItemsName);
            if (items != null) m.transform.SetParent(items.transform, true);
            Vector3 pos = Snap(start + rot * new Vector3(3.2f, 0f, 7f));
            m.transform.position = pos;
            Vector3 look = start - pos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) m.transform.rotation = Quaternion.LookRotation(look);
            m.AddComponent<Merchant>();
        }

        private static void SetBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == ScenePath) continue;
                scenes.Add(new EditorBuildSettingsScene(s.path, false));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
