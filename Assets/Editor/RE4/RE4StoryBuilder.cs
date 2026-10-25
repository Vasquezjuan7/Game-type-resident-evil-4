using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Gameplay;
using Gameplay.RE4;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Coloca la historia del Capitulo 1 en la escena: director de la historia, Tomas (NPC con IA),
    /// documentos de lore, zonas, mercaderes, cajas rompibles, sonidos de pasos, linterna y ambiente.
    /// Funciona tanto con el pueblo ampliado como con una escena antigua (usa posiciones de respaldo).
    /// </summary>
    public static class RE4StoryBuilder
    {
        public const string RootName = "--- RE4 HISTORIA ---";
        private const string NpcController = "Assets/RE4/Animation/NPC_Idle.controller";

        private struct Points
        {
            public Vector3 church, gate, tomas, chapel, sawmill, forest, start;
            public List<Vector3> merchants;
            public bool expanded;
        }

        public static void Build(RE4DataBuilder.Database db, RE4VillageBuilder.Result village, GameObject leon)
        {
            RE4EditorUtil.DestroyIfExists(RootName);
            RE4EditorUtil.DestroyIfExists("Mercader");
            GameObject root = new GameObject(RootName);
            Points pts = GetPoints(village, leon);

            // ---------------- Director de la historia
            GameObject systems = GameObject.Find("--- RE4 SISTEMAS ---");
            if (systems == null) systems = new GameObject("--- RE4 SISTEMAS ---");
            StoryDirector story = Ensure<StoryDirector>(systems);
            story.churchPoint = Marker(root, "Punto_Iglesia", pts.church);
            story.gatePoint = Marker(root, "Punto_Porton_Norte", pts.gate);
            story.elenaPendant = db.elenaPendant;
            Ensure<AmbienceManager>(systems);
            EditorUtility.SetDirty(story);

            HordeWaveManager mgr = systems.GetComponent<HordeWaveManager>();
            if (mgr == null) mgr = Object.FindAnyObjectByType<HordeWaveManager>();
            if (mgr != null)
            {
                mgr.gunpowder = db.gunpowder;
                mgr.resourceS = db.resourceS;
                mgr.resourceL = db.resourceL;
                mgr.treasures = new List<ItemDefinition>(db.treasures);
                EditorUtility.SetDirty(mgr);
            }

            // ---------------- Zonas
            Trigger(root, "pueblo", new Vector3(0f, 0f, -45f), 16f);
            Trigger(root, "cabana", pts.tomas, 16f);
            Trigger(root, "capilla", pts.chapel, 24f);
            Trigger(root, "aserradero", pts.sawmill, 24f);
            Trigger(root, "bosque", pts.forest, 22f);

            // ---------------- Tomas
            BuildTomas(root, pts.tomas);

            // ---------------- Documentos
            var docs = db.documents;
            Put(root, Doc(docs, "doc_diario"), 1, pts.start + new Vector3(-3f, 0f, 12f));
            Put(root, Doc(docs, "doc_mercader"), 1, pts.merchants[0] + new Vector3(-1.5f, 0f, -1f));
            Put(root, Doc(docs, "doc_alcalde"), 1, new Vector3(-9f, 0f, 9f));
            Put(root, Doc(docs, "doc_tomas"), 1, new Vector3(-60f, 0f, -5f));
            Put(root, Doc(docs, "doc_doctor"), 1, new Vector3(-74f, 0f, 41f));
            Put(root, Doc(docs, "doc_elena"), 1, pts.church + new Vector3(4f, 0f, -4f));
            Put(root, Doc(docs, "doc_plegaria"), 1, pts.chapel + new Vector3(2f, 0f, -6f));
            Put(root, Doc(docs, "doc_aserradero"), 1, pts.sawmill + new Vector3(-2f, 0f, -5f));

            // Tesoros escondidos y recursos
            if (db.treasures.Count >= 4)
            {
                Put(root, db.treasures[0], 1, pts.tomas + new Vector3(-3f, 0f, -3f));
                Put(root, db.treasures[1], 1, pts.chapel + new Vector3(-10f, 0f, 8f));
                Put(root, db.treasures[2], 1, new Vector3(14f, 0f, 14f));
                Put(root, db.treasures[3], 1, pts.sawmill + new Vector3(10f, 0f, 4f));
            }
            Put(root, db.gunpowder, 3, pts.start + new Vector3(4f, 0f, 20f));
            Put(root, db.gunpowder, 2, new Vector3(-30f, 0f, -6f));
            Put(root, db.resourceS, 2, new Vector3(40f, 0f, 6f));
            Put(root, db.resourceL, 1, pts.forest + new Vector3(-4f, 0f, -4f));

            // ---------------- Mercaderes
            foreach (var p in pts.merchants)
            {
                GameObject m = new GameObject("Mercader");
                m.transform.SetParent(root.transform, false);
                m.transform.position = Snap(p);
                Vector3 look = new Vector3(0f, 0f, p.z) - p;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) m.transform.rotation = Quaternion.LookRotation(look);
                m.AddComponent<Merchant>();
            }

            // ---------------- Cajas y barriles rompibles
            PlaceCrates(root, pts.expanded ? 60 : 32);

            // ---------------- Jugador
            if (leon != null)
            {
                Ensure<FootstepAudio>(leon);
                Ensure<Flashlight>(leon);
                Ensure<TalkUI>(leon);
                AttacheCase attache = leon.GetComponent<AttacheCase>();
                if (attache != null)
                {
                    attache.recipes = new List<CraftRecipe>(db.recipes);
                    EditorUtility.SetDirty(attache);
                }
                WeaponUpgradeUI shop = Ensure<WeaponUpgradeUI>(leon);
                shop.stock = new List<ItemDefinition>(db.shopStock);
                shop.requireMerchant = true;
                EditorUtility.SetDirty(shop);
                EditorUtility.SetDirty(leon);
            }

            // ---------------- Sonido de pasos en enemigos y superficies
            AddEnemyFootsteps();
            TagSurfaces();
            Debug.Log("[RE4] Historia colocada" + (pts.expanded ? " (mundo ampliado)." : " (escena antigua: usa 'Construir pueblo RE4 completo' para el mundo ampliado)."));
        }

        // =========================================================== Puntos

        private static Points GetPoints(RE4VillageBuilder.Result village, GameObject leon)
        {
            bool expanded = village != null || GameObject.Find("Capilla_Vieja") != null;
            var defaults = village ?? new RE4VillageBuilder.Result();
            var p = new Points { expanded = expanded, start = leon != null ? leon.transform.position : new Vector3(0f, 0f, -142f) };
            if (expanded)
            {
                p.church = defaults.churchPoint;
                p.gate = defaults.gatePoint;
                p.tomas = defaults.tomasPoint;
                p.chapel = defaults.chapelPoint;
                p.sawmill = defaults.sawmillPoint;
                p.forest = defaults.forestPoint;
                p.merchants = defaults.merchantPoints.Count > 0 ? defaults.merchantPoints
                    : new List<Vector3> { new Vector3(3.2f, 0f, -135f), new Vector3(26f, 0f, -26f), new Vector3(12f, 0f, 183f) };
            }
            else
            {
                // Pueblo antiguo (radio 150 m): mismas ideas dentro del mapa pequeno
                p.church = new Vector3(6f, 0f, 100f);
                p.gate = new Vector3(18f, 0f, 140f);
                p.tomas = new Vector3(-109f, 0f, -12f);
                p.chapel = new Vector3(-26f, 0f, 112f);
                p.sawmill = new Vector3(100f, 0f, 10f);
                p.forest = new Vector3(0f, 0f, 95f);
                p.merchants = new List<Vector3> { p.start + new Vector3(3.2f, 0f, 7f), new Vector3(26f, 0f, -26f), new Vector3(20f, 0f, 96f) };
            }
            return p;
        }

        private static Vector3 Snap(Vector3 p)
        {
            if (NavMesh.SamplePosition(p + Vector3.up, out NavMeshHit hit, 8f, NavMesh.AllAreas)) return hit.position;
            return p;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static Transform Marker(GameObject root, string name, Vector3 pos)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(root.transform, false);
            t.position = Snap(pos);
            return t;
        }

        private static void Trigger(GameObject root, string id, Vector3 pos, float radius)
        {
            Transform t = Marker(root, "Zona_" + id, pos);
            StoryTrigger st = t.gameObject.AddComponent<StoryTrigger>();
            st.zoneId = id;
            st.radius = radius;
        }

        private static ItemDefinition Doc(List<ItemDefinition> docs, string id) => docs.FirstOrDefault(d => d != null && d.id == id);

        private static void Put(GameObject root, ItemDefinition item, int count, Vector3 pos)
        {
            if (item == null) return;
            GameObject go = new GameObject("Objeto_" + item.id);
            go.transform.SetParent(root.transform, false);
            go.transform.position = Snap(pos);
            PickupSpawner s = go.AddComponent<PickupSpawner>();
            s.item = item;
            s.count = count;
        }

        // =========================================================== Tomas

        private static void BuildTomas(GameObject root, Vector3 pos)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx");
            GameObject npc = new GameObject("NPC_Tomas");
            npc.transform.SetParent(root.transform, false);
            npc.transform.position = Snap(pos);
            npc.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            if (model != null)
            {
                GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                body.name = "Modelo";
                body.transform.SetParent(npc.transform, false);
                Animator anim = body.GetComponent<Animator>();
                if (anim == null) anim = body.AddComponent<Animator>();
                anim.runtimeAnimatorController = IdleController();
                anim.applyRootMotion = false;
                // un poco mas encorvado y bajo: un pastor mayor
                body.transform.localScale = Vector3.one * 0.95f;
                foreach (var r in body.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        Material m = new Material(mats[i]) { name = mats[i].name + "_Tomas" };
                        m.color = new Color(0.62f, 0.55f, 0.48f);
                        mats[i] = m;
                    }
                    r.sharedMaterials = mats;
                }
            }
            CapsuleCollider cc = npc.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.3f;

            Light lamp = new GameObject("Candil").AddComponent<Light>();
            lamp.transform.SetParent(npc.transform, false);
            lamp.transform.localPosition = new Vector3(0.6f, 1.2f, 0.6f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.65f, 0.3f);
            lamp.range = 6f;
            lamp.intensity = 1.6f;
            lamp.gameObject.AddComponent<FlickerLight>().baseIntensity = 1.6f;

            NpcTalker t = npc.AddComponent<NpcTalker>();
            t.npcId = "tomas";
            t.displayName = "Tomás";
            t.talkRange = 3.2f;
            t.persona =
                "Tomás, pastor de setenta años de Valdeoscuro. Viudo, desconfiado pero buena persona. " +
                "Es de los pocos que no bebió el agua del pozo y por eso se esconde en una cabaña del camino del oeste. " +
                "Habla como un viejo de pueblo castellano (\"forastero\", \"muchacho\", \"mal rayo\"), en voz baja, con miedo, y tose a menudo. " +
                "Quiere ayudar a Leon porque su nieta Lucía también desapareció. Si Leon es amable, le da consejos; si es grosero, se ofende pero sigue ayudando.";
            t.knowledge =
                "- El pueblo se llama Valdeoscuro, en el norte de España.\n" +
                "- Hace un año llegó el Padre, un hombre con túnica que dirige la Hermandad. Nadie sabe su nombre verdadero.\n" +
                "- El Padre bendijo el agua del pozo. Quien la bebe tiene dentro \"la semilla\": se le ponen los ojos rojos, tose sangre y obedece a una voluntad común.\n" +
                "- El alcalde Ramiro Ortega sirve al Padre.\n" +
                "- Hace tres noches trajeron a una chica rubia extranjera (Elena) y la encerraron en la sacristía de la iglesia. Quieren llevarla al castillo que hay pasado el bosque del norte.\n" +
                "- Cuando los aldeanos descubren a un forastero lo atacan todos a la vez, y solo se retiran cuando suena la campana de la iglesia.\n" +
                "- Consejos: apuntar a la cabeza; disparar a las piernas los tumba y entonces se les puede dar una patada; con el cuchillo se pueden desviar sus golpes si se usa en el momento justo; si te acercas despacio por la espalda a uno que no te ha visto, puedes acabar con él en silencio.\n" +
                "- En el aserradero del este construyen jaulas para \"el grande\", un gigante deforme que sueltan la noche de la campana.\n" +
                "- En la capilla vieja del oeste reza la Hermandad. Allí hay joyas y papeles del culto.\n" +
                "- Hay un mercader encapuchado con un farol azul que vende armas y compra tesoros; nadie sabe de dónde sale.\n" +
                "- La salida es el camino del bosque al norte, pasando la iglesia, hasta un portón.\n" +
                "- De noche aúllan perros; también los infectó la semilla.";
            t.greeting = "¡Chist! Agáchate, forastero... No eres de aquí, ¿verdad? Tus ojos no están rojos. Gracias a Dios.";
            t.lines = new List<ScriptedLine>
            {
                new ScriptedLine { question = "¿Has visto a una chica rubia, Elena?", keywords = "elena,chica,rubia,hija,muchacha,niña",
                    answer = "Sí... la trajeron hace tres noches los hombres del alcalde. La encerraron en la iglesia, en la sacristía. Dicen que el Padre se la llevará al castillo." },
                new ScriptedLine { question = "¿Qué les pasa a los aldeanos?", keywords = "aldeanos,gente,pueblo,pasa,ojos,locos",
                    answer = "Bebieron el agua del pozo que bendijo el Padre. Algo les crece dentro de la cabeza... la semilla. Ya no son ellos, muchacho." },
                new ScriptedLine { question = "¿Cómo los detengo?", keywords = "matar,detener,arma,disparar,cabeza,débil,debil,consejo",
                    answer = "A la cabeza, siempre a la cabeza. Si les das en las piernas caen de rodillas, y entonces una buena patada. Y cuando suene la campana de la iglesia, se marcharán." },
                new ScriptedLine { question = "¿Quién es el Padre?", keywords = "padre,hermandad,líder,lider,secta,alcalde,culto",
                    answer = "Llegó hace un año con su túnica y sus rezos. El alcalde Ortega le lame las botas. Rezan en la capilla vieja del oeste, y en el aserradero del este construyen jaulas para algo enorme." },
                new ScriptedLine { question = "¿Cómo salgo de aquí?", keywords = "salir,escapar,portón,porton,castillo,norte,bosque,salida",
                    answer = "Por el camino del bosque, al norte, pasando la iglesia. Allí hay un portón. Más allá está el castillo... que Dios te acompañe, forastero." },
            };
            t.unknownAnswer = "No sé de qué me hablas, forastero. Y baja la voz, que nos oyen.";
        }

        private static AnimatorController IdleController()
        {
            AnimatorController c = AssetDatabase.LoadAssetAtPath<AnimatorController>(NpcController);
            if (c != null) return c;
            RE4EditorUtil.EnsureFolder("Assets/RE4/Animation");
            c = AnimatorController.CreateAnimatorControllerAtPath(NpcController);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle02.fbx")
                .OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
            if (clip != null) c.AddMotion(clip);
            return c;
        }

        // =========================================================== Cajas

        private static void PlaceCrates(GameObject root, int count)
        {
            Physics.SyncTransforms();
            Material wood = RE4EditorUtil.SaveMaterial(RE4DataBuilder.MatFolder + "/RE4_Caja_Madera.mat", "Standard", new Color(0.62f, 0.5f, 0.36f),
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LightingOptimizationTutorial/Textures/Environment/WoodTiled01Albedo.tif"));
            wood.SetFloat("_Glossiness", 0.12f);
            GameObject barrel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LightingOptimizationTutorial/Prefabs/Props/Barrel.prefab");
            var rng = new System.Random(4242);
            int placed = 0;
            for (int i = 0; i < count * 12 && placed < count; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float r = 10f + (float)rng.NextDouble() * 230f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                p = hit.position;
                if (Physics.CheckBox(p + Vector3.up * 0.6f, new Vector3(0.7f, 0.5f, 0.7f), Quaternion.identity, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                // cerca de alguna pared o casa (como en RE4), no en medio de la nada
                if (!NearStructure(p)) continue;

                GameObject crate;
                bool isBarrel = barrel != null && rng.NextDouble() < 0.35;
                if (isBarrel)
                {
                    crate = (GameObject)PrefabUtility.InstantiatePrefab(barrel);
                    PrefabUtility.UnpackPrefabInstance(crate, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    crate.transform.position = p;
                    if (crate.GetComponentInChildren<Collider>() == null) crate.AddComponent<BoxCollider>();
                }
                else
                {
                    crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    float s = 0.7f + (float)rng.NextDouble() * 0.25f;
                    crate.transform.localScale = Vector3.one * s;
                    crate.transform.position = p + Vector3.up * s * 0.5f;
                    crate.GetComponent<Renderer>().sharedMaterial = wood;
                }
                crate.name = isBarrel ? "Barril_Rompible" : "Caja_Rompible";
                crate.transform.SetParent(root.transform, true);
                crate.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                crate.AddComponent<Breakable>().health = isBarrel ? 16f : 10f;
                NavMeshObstacle ob = crate.AddComponent<NavMeshObstacle>();
                ob.carving = true;
                ob.shape = NavMeshObstacleShape.Box;
                ob.size = Vector3.one;
                Physics.SyncTransforms();
                placed++;
            }
            Debug.Log($"[RE4] Cajas rompibles colocadas: {placed}");
        }

        private static bool NearStructure(Vector3 p)
        {
            foreach (var c in Physics.OverlapSphere(p + Vector3.up * 1.5f, 5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (c.name == "Suelo" || c is TerrainCollider) continue;
                return true;
            }
            return false;
        }

        // =========================================================== Sonido

        private static void AddEnemyFootsteps()
        {
            foreach (var path in new[]
                     {
                         RE4EnemyBuilder.PrefabFolder + "/Ganado_Aldeano.prefab", RE4EnemyBuilder.PrefabFolder + "/Ganado_Aldeana.prefab",
                         RE4EnemyBuilder.PrefabFolder + "/Ganado_Aldeano_Torso.prefab", RE4DogBuilder.PrefabPath, RE4BossBuilder.PrefabPath
                     })
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                EnemyFootsteps f = contents.GetComponent<EnemyFootsteps>();
                if (f == null) f = contents.AddComponent<EnemyFootsteps>();
                bool dog = path == RE4DogBuilder.PrefabPath;
                bool boss = path == RE4BossBuilder.PrefabPath;
                f.dog = dog;
                f.stepsPerMeter = dog ? 2.4f : boss ? 0.9f : 1.3f;
                f.volume = boss ? 0.95f : dog ? 0.35f : 0.5f;
                f.pitch = boss ? 0.65f : dog ? 1.3f : 1f;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>Caminos y plaza de escenas antiguas: disparador fino con SurfaceTag para los pasos.</summary>
        private static void TagSurfaces()
        {
            GameObject village = GameObject.Find(RE4VillageBuilder.RootName);
            if (village == null) return;
            foreach (Transform t in village.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<SurfaceTag>() != null) continue;
                if (t.name == "Suelo") { t.gameObject.AddComponent<SurfaceTag>().surface = "dirt"; continue; }
                if (t.name != "Camino" && t.name != "Plaza") continue;
                Collider c = t.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
                BoxCollider box = t.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1f, 50f, 1f);
                t.gameObject.layer = 2;
                t.gameObject.AddComponent<SurfaceTag>().surface = "stone";
            }
        }
    }
}
