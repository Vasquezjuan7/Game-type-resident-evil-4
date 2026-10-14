using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Gameplay.RE4;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Genera el pueblo estilo Resident Evil 4 (aprox. 580 x 500 m, mundo semiabierto):
    /// plaza con hoguera y torre, anillo de casas, caminos con casas, granja, iglesia con cementerio,
    /// cabana de Tomas, capilla vieja (oeste), aserradero (este), campamento del bosque y porton norte,
    /// arboles secos, antorchas, coches oxidados y un perimetro de rocas.
    /// </summary>
    public static class RE4VillageBuilder
    {
        public const string RootName = "--- PUEBLO RE4 ---";

        private const string LOT = "Assets/LightingOptimizationTutorial/Prefabs/";
        private const string FORGE = "Assets/3DForge/";
        private const string CEM = "Assets/Cemetery Kit V1.25/Prefabs/";

        public class Result
        {
            public GameObject root;
            public List<Vector3> spawnPoints = new List<Vector3>();
            public List<Vector3> villagerPoints = new List<Vector3>();
            public Vector3 playerStart = new Vector3(0f, 0.1f, -140f);
            public Quaternion playerRotation = Quaternion.identity;

            // Historia y mundo ampliado
            public Vector3 gatePoint = new Vector3(0f, 0f, 272f);
            public Vector3 churchPoint = new Vector3(6f, 0f, 100f);
            public Vector3 tomasPoint = new Vector3(-109f, 0f, -12f);
            public Vector3 chapelPoint = new Vector3(-250f, 0f, 40f);
            public Vector3 sawmillPoint = new Vector3(250f, 0f, -25f);
            public Vector3 forestPoint = new Vector3(0f, 0f, 195f);
            public List<Vector3> merchantPoints = new List<Vector3>();
        }

        // Centro y radio del mundo (circulo cortado por un muro al sur, detras del inicio)
        public static readonly Vector3 WorldCenter = new Vector3(0f, 0f, 70f);
        public const float WorldRadius = 290f;
        public const float SouthWall = -152f;

        private static bool InsideWorld(Vector3 p, float margin)
        {
            Vector3 d = p - WorldCenter;
            d.y = 0f;
            return d.magnitude < WorldRadius - margin && p.z > SouthWall + margin;
        }

        private struct Road
        {
            public Vector2 a, b;
            public float width;
        }

        private static System.Random _rng;
        private static readonly List<Bounds> Occupied = new List<Bounds>();
        private static readonly List<Road> Roads = new List<Road>();
        private static Transform _root, _buildings, _props, _nature, _lights;

        private static float R(float min, float max) => (float)(min + _rng.NextDouble() * (max - min));
        private static int RI(int min, int maxExclusive) => _rng.Next(min, maxExclusive);

        private static readonly string[] HousePrefabs =
        {
            LOT + "Environment/HouseBig01.prefab",
            LOT + "Environment/HouseBig02.prefab",
            LOT + "Environment/HouseSmall.prefab",
            LOT + "Environment/BarracksSingle.prefab",
            LOT + "Environment/BarracksDouble.prefab",
            LOT + "Environment/Storage.prefab",
            FORGE + "FantasyExteriors/Village & Towns/Prefabs/Base/Templates/BasicHouse.prefab",
            FORGE + "FantasyExteriors/Village & Towns/Prefabs/Buildings/ForestVillage/Smithy.prefab",
            FORGE + "FantasyExteriors/Village & Towns/Prefabs/Buildings/ForestVillage/ForestVillage02.prefab",
            FORGE + "Blueprints/PremiumBlueprints/PB_VIK_VEK/FrontierSettlement/BLUEPRINTS/Frontiers/PB_FS_House.prefab",
            FORGE + "Blueprints/PremiumBlueprints/PB_VIK_VEK/FrontierSettlement/BLUEPRINTS/Frontiers/PB_FS_Blacksmith.prefab",
        };

        private static readonly string[] SmallProps =
        {
            LOT + "Props/Barrel.prefab", LOT + "Props/Barrels01.prefab", LOT + "Props/Barrels02.prefab",
            LOT + "Props/Bucket.prefab", LOT + "Props/Buckets.prefab", LOT + "Props/Plank01.prefab",
            LOT + "Props/Plank02.prefab", LOT + "Props/Shovel01.prefab", LOT + "Props/WallLogs.prefab",
            LOT + "Props/Fence.prefab", LOT + "Props/FenceDouble.prefab", LOT + "Props/Shield01.prefab",
        };

        // =========================================================== API

        public static Result Build(int seed)
        {
            _rng = new System.Random(seed);
            Occupied.Clear();
            Roads.Clear();

            var res = new Result();
            _root = new GameObject(RootName).transform;
            res.root = _root.gameObject;
            _buildings = Group("Edificios");
            _props = Group("Objetos");
            _nature = Group("Naturaleza");
            _lights = Group("Luces");

            BuildGround();
            BuildRoads();
            BuildPlaza(res);
            BuildChurch(res);
            BuildFarm(res);
            BuildTomasShack(res);
            BuildChapel(res);
            BuildSawmill(res);
            BuildForestCamp(res);
            BuildHouses(res);
            BuildCars();
            BuildTorches();
            BuildTrees();
            BuildProps();
            BuildPerimeter();
            EnsureColliders(_root.gameObject);
            MarkStatic(_root.gameObject);

            // Accesos al pueblo donde aparecen las oleadas
            res.spawnPoints.AddRange(new[]
            {
                new Vector3(0f, 0f, -120f), new Vector3(-125f, 0f, 0f), new Vector3(125f, 0f, 0f),
                new Vector3(0f, 0f, 95f), new Vector3(-70f, 0f, 60f), new Vector3(70f, 0f, -60f),
                new Vector3(-60f, 0f, -70f), new Vector3(65f, 0f, 70f), new Vector3(-110f, 0f, 40f), new Vector3(110f, 0f, -35f),
                new Vector3(0f, 0f, 205f), new Vector3(-225f, 0f, 30f), new Vector3(228f, 0f, -18f), new Vector3(-150f, 0f, 150f),
                new Vector3(150f, 0f, 140f), new Vector3(-180f, 0f, -100f), new Vector3(180f, 0f, -110f)
            });
            res.merchantPoints.Add(new Vector3(3.2f, 0f, -135f));
            res.merchantPoints.Add(new Vector3(26f, 0f, -26f));
            res.merchantPoints.Add(new Vector3(12f, 0f, 183f));
            res.playerStart = new Vector3(0f, 0.1f, -142f);
            res.playerRotation = Quaternion.identity;
            return res;
        }

        // =========================================================== Utilidades

        private static Transform Group(string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(_root, false);
            return t;
        }

        private static GameObject Place(string path, Transform parent, Vector3 pos, float yaw, float scale = 1f, bool checkOverlap = true, float margin = 1f)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale *= scale;

            Bounds b = RE4EditorUtil.WorldBounds(go);
            // Apoyar sobre el suelo
            if (b.size.y > 0.01f && Mathf.Abs(b.min.y - pos.y) > 0.05f && b.min.y > pos.y - 0.5f)
            {
                go.transform.position += Vector3.up * (pos.y - b.min.y);
                b = RE4EditorUtil.WorldBounds(go);
            }

            if (checkOverlap)
            {
                Bounds test = b;
                test.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
                if (Overlaps(test))
                {
                    Object.DestroyImmediate(go);
                    return null;
                }
            }
            Occupied.Add(b);
            return go;
        }

        private static bool Overlaps(Bounds b)
        {
            Rect r = new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
            foreach (var o in Occupied)
            {
                if (r.Overlaps(new Rect(o.min.x, o.min.z, o.size.x, o.size.z))) return true;
            }
            foreach (var road in Roads)
            {
                if (SegmentRectOverlap(road, r)) return true;
            }
            if (r.Overlaps(new Rect(-19f, -19f, 38f, 38f)) && !(r.xMin > 19f || r.xMax < -19f || r.yMin > 19f || r.yMax < -19f))
            {
                // plaza circular: comprobar la esquina mas cercana
                Vector2 c = new Vector2(Mathf.Clamp(0f, r.xMin, r.xMax), Mathf.Clamp(0f, r.yMin, r.yMax));
                if (c.magnitude < 19f) return true;
            }
            return false;
        }

        private static bool SegmentRectOverlap(Road road, Rect r)
        {
            // muestreo a lo largo del camino
            float len = Vector2.Distance(road.a, road.b);
            int steps = Mathf.CeilToInt(len / 2f);
            float half = road.width * 0.5f + 1.5f;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(road.a, road.b, i / (float)steps);
                Rect pr = new Rect(p.x - half, p.y - half, half * 2f, half * 2f);
                if (pr.Overlaps(r)) return true;
            }
            return false;
        }

        private static Material LoadOrCreateMat(string path, string fallbackTex, Color color, Vector2 tiling)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && m.shader != null && m.shader.name == "Standard") return m;
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackTex);
            m = RE4EditorUtil.SaveMaterial(path.Replace("Assets/Materials/", RE4DataBuilder.MatFolder + "/RE4_"), "Standard", color, tex);
            m.mainTextureScale = tiling;
            m.SetFloat("_Glossiness", 0.1f);
            return m;
        }

        // =========================================================== Suelo y caminos

        private static void BuildGround()
        {
            Material ground = LoadOrCreateMat("Assets/Materials/Pueblo_Terreno_Cesped.mat",
                "Assets/LightingOptimizationTutorial/Textures/Environment/TerrainMudDryAlbedo.tif", new Color(0.62f, 0.58f, 0.5f), new Vector2(100f, 100f));
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Suelo";
            g.transform.SetParent(_root, false);
            g.transform.position = new Vector3(0f, -1f, 70f);
            g.transform.localScale = new Vector3(640f, 2f, 640f);
            g.AddComponent<SurfaceTag>().surface = "dirt";
            g.GetComponent<Renderer>().sharedMaterial = ground;
        }

        private static void BuildRoads()
        {
            Material road = LoadOrCreateMat("Assets/Materials/Pueblo_Camino_Piedra.mat",
                "Assets/LightingOptimizationTutorial/Textures/Environment/SmithyFloorStoneAlbedo.tif", new Color(0.6f, 0.55f, 0.48f), new Vector2(3f, 30f));

            AddRoad(new Vector2(0f, -150f), new Vector2(0f, -17f), 8f, road);
            AddRoad(new Vector2(0f, 17f), new Vector2(0f, 98f), 7f, road);
            AddRoad(new Vector2(-17f, 0f), new Vector2(-150f, 0f), 6f, road);
            AddRoad(new Vector2(17f, 0f), new Vector2(150f, 0f), 6f, road);
            AddRoad(new Vector2(-20f, 0f), new Vector2(-62f, 38f), 5f, road); // hacia la granja
            AddRoad(new Vector2(0f, 136f), new Vector2(0f, 270f), 6f, road);    // camino del bosque (norte)
            AddRoad(new Vector2(-150f, 0f), new Vector2(-236f, 34f), 5f, road); // hacia la capilla vieja
            AddRoad(new Vector2(150f, 0f), new Vector2(236f, -22f), 5f, road);  // hacia el aserradero

            GameObject plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plaza.name = "Plaza";
            plaza.transform.SetParent(_root, false);
            plaza.transform.position = new Vector3(0f, 0.005f, 0f);
            plaza.transform.localScale = new Vector3(38f, 0.005f, 38f);
            MakeSurface(plaza, "stone");
            Material plazaMat = new Material(road) { name = "RE4_Plaza", mainTextureScale = new Vector2(10f, 10f) };
            plaza.GetComponent<Renderer>().sharedMaterial = plazaMat;
        }

        private static void AddRoad(Vector2 a, Vector2 b, float width, Material mat)
        {
            Roads.Add(new Road { a = a, b = b, width = width });
            GameObject r = GameObject.CreatePrimitive(PrimitiveType.Cube);
            r.name = "Camino";
            r.transform.SetParent(_root, false);
            Vector2 mid = (a + b) * 0.5f;
            Vector2 dir = b - a;
            r.transform.position = new Vector3(mid.x, 0.006f, mid.y);
            r.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f);
            r.transform.localScale = new Vector3(width, 0.01f, dir.magnitude);
            MakeSurface(r, "stone");
            Material m = new Material(mat) { mainTextureScale = new Vector2(width / 4f, dir.magnitude / 4f) };
            r.GetComponent<Renderer>().sharedMaterial = m;
        }

        /// <summary>El collider queda como disparador fino (capa Ignore Raycast) solo para el sonido de los pasos.</summary>
        private static void MakeSurface(GameObject go, string surface)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1f, 50f, 1f); // ~0.5 m de alto en el mundo (la escala Y del camino es 0.01)
            go.layer = 2;
            go.AddComponent<SurfaceTag>().surface = surface;
        }

        // =========================================================== Zonas

        private static void BuildPlaza(Result res)
        {
            Transform plaza = Group("Plaza_Hoguera");

            // Hoguera central (como en RE4)
            Place(LOT + "Paericle Systems/Fire.prefab", plaza, Vector3.zero, 0f, 1.6f, false);
            Place(LOT + "Paericle Systems/fx_fire.prefab", plaza, new Vector3(0f, 0.2f, 0f), 0f, 1.4f, false);
            Place(LOT + "Paericle Systems/Smoke.prefab", plaza, new Vector3(0f, 1.5f, 0f), 0f, 1.5f, false);
            Place(LOT + "Paericle Systems/Sparks.prefab", plaza, new Vector3(0f, 0.8f, 0f), 0f, 1f, false);
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                Place(LOT + "Props/Boulder01.prefab", plaza, new Vector3(Mathf.Cos(a) * 1.8f, 0f, Mathf.Sin(a) * 1.8f), R(0f, 360f), 0.35f, false);
            }
            // Obstaculo para la NavMesh alrededor del fuego
            GameObject block = new GameObject("Hoguera_Colision");
            block.transform.SetParent(plaza, false);
            CapsuleCollider cap = block.AddComponent<CapsuleCollider>();
            cap.radius = 2.1f;
            cap.height = 2f;
            cap.center = new Vector3(0f, 1f, 0f);
            Occupied.Add(new Bounds(Vector3.zero, new Vector3(5f, 3f, 5f)));
            AddLight(plaza, new Vector3(0f, 1.8f, 0f), new Color(1f, 0.55f, 0.2f), 22f, 2.6f);

            // Torre de vigilancia
            Place(LOT + "Environment/Tower.prefab", plaza, new Vector3(13f, 0f, 13f), 45f, 1f, false);
            Occupied.Add(new Bounds(new Vector3(13f, 0f, 13f), new Vector3(6f, 10f, 6f)));
            Place(LOT + "Props/Flag.prefab", plaza, new Vector3(-12f, 0f, 12f), 0f, 1f, false);
            Place(LOT + "Props/Pillar.prefab", plaza, new Vector3(-12f, 0f, -12f), 0f, 1f, false);
            Place(LOT + "Props/Barrels01.prefab", plaza, new Vector3(9f, 0f, -14f), 30f, 1f, false);
            Place(LOT + "Props/WallLogs.prefab", plaza, new Vector3(-15f, 0f, 4f), 90f, 1f, false);

            // Aldeanos trabajando alrededor del fuego
            for (int i = 0; i < 7; i++)
            {
                float a = (i * 51f + 15f) * Mathf.Deg2Rad;
                res.villagerPoints.Add(new Vector3(Mathf.Cos(a) * R(5f, 11f), 0f, Mathf.Sin(a) * R(5f, 11f)));
            }
        }

        private static void BuildChurch(Result res)
        {
            Transform zone = Group("Iglesia_Cementerio");
            GameObject church = Place(CEM + "Buildings/Church 1 Closed.prefab", zone, new Vector3(0f, 0f, 118f), 180f, 1f, false);
            if (church == null) church = Place(CEM + "Buildings/Cathedral 1 Closed.prefab", zone, new Vector3(0f, 0f, 118f), 180f, 1f, false);
            if (church != null) church.name = "Iglesia_Campanario";

            Place(CEM + "Church Supplies/Cross Arch.prefab", zone, new Vector3(-16f, 0f, 98f), 0f, 1f, false);
            // Cementerio
            string[] graves =
            {
                CEM + "Graves/Grave 1.prefab", CEM + "Graves/Grave 2.prefab", CEM + "Graves/Grave 3.prefab",
                CEM + "Graves/Grave 4.prefab", CEM + "Graves/Grave 5.prefab", CEM + "Graves/Grave 6.prefab",
                CEM + "Graves/Gravestones/Cross 1.prefab", CEM + "Graves/Gravestones/Cross 3.prefab",
                CEM + "Graves/Gravestones/Grave 2.prefab", CEM + "Graves/Gravestones/Grave 5.prefab",
            };
            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 5; z++)
                {
                    Vector3 p = new Vector3(-36f + x * 4.2f + R(-0.4f, 0.4f), 0f, 102f + z * 5f + R(-0.4f, 0.4f));
                    Place(graves[RI(0, graves.Length)], zone, p, 180f + R(-8f, 8f), 1f, false);
                }
            }
            // Valla del cementerio
            for (int i = 0; i < 12; i++)
            {
                Place(CEM + "Gates, Fences/Fence_1A.prefab", zone, new Vector3(-40f + i * 2.4f, 0f, 99f), 0f, 1f, false);
                Place(CEM + "Gates, Fences/Fence_1A.prefab", zone, new Vector3(-40f + i * 2.4f, 0f, 129f), 0f, 1f, false);
            }
            Occupied.Add(new Bounds(new Vector3(-26f, 0f, 114f), new Vector3(30f, 4f, 32f)));
            Occupied.Add(new Bounds(new Vector3(0f, 0f, 120f), new Vector3(22f, 20f, 30f)));

            res.villagerPoints.Add(new Vector3(10f, 0f, 100f));
            res.villagerPoints.Add(new Vector3(-12f, 0f, 94f));
        }

        private static void BuildFarm(Result res)
        {
            Transform farm = Group("Granja");
            Vector3 c = new Vector3(-80f, 0f, 48f);
            Place(FORGE + "FantasyExteriors/Village & Towns/Prefabs/Buildings/Farm/Stables/Stable_2horse.prefab", farm, c + new Vector3(0f, 0f, 10f), 180f, 1f, false);
            Place(FORGE + "FantasyExteriors/Village & Towns/Prefabs/Buildings/Farm/Troughs/fi_vil_farm_trough02_2m.prefab", farm, c + new Vector3(-6f, 0f, -2f), 0f, 1f, false);
            Place(FORGE + "FantasyExteriors/Village & Towns/Prefabs/Buildings/Farm/Troughs/fi_vil_farm_trough02_1m.prefab", farm, c + new Vector3(6f, 0f, -3f), 90f, 1f, false);
            Place(LOT + "Props/Shed01.prefab", farm, c + new Vector3(14f, 0f, 2f), 270f, 1f, false);
            Place(LOT + "Props/Shed02.prefab", farm, c + new Vector3(-15f, 0f, 3f), 90f, 1f, false);
            Place(LOT + "Props/Barrels02.prefab", farm, c + new Vector3(11f, 0f, -8f), 0f, 1f, false);
            Place(LOT + "Props/Buckets.prefab", farm, c + new Vector3(-3f, 0f, -6f), 0f, 1f, false);
            Place(LOT + "Props/Plank03.prefab", farm, c + new Vector3(4f, 0f, -9f), 30f, 1f, false);

            // Cerca del corral con entrada hacia el camino
            string fence = FORGE + "FantasyExteriors/Village & Towns/Prefabs/Fences/Fence02/fi_vil_fence02_2,5m_B.prefab";
            for (int i = 0; i < 14; i++)
            {
                float x = -18f + i * 2.5f;
                Place(fence, farm, c + new Vector3(x, 0f, 22f), 0f, 1f, false);
                if (i < 5 || i > 8) Place(fence, farm, c + new Vector3(x, 0f, -14f), 0f, 1f, false);
            }
            for (int i = 0; i < 14; i++)
            {
                float z = -14f + i * 2.5f;
                Place(fence, farm, c + new Vector3(-19f, 0f, z), 90f, 1f, false);
                Place(fence, farm, c + new Vector3(17f, 0f, z), 90f, 1f, false);
            }
            Occupied.Add(new Bounds(c + new Vector3(-1f, 0f, 4f), new Vector3(38f, 5f, 38f)));

            res.villagerPoints.Add(c + new Vector3(-4f, 0f, 2f));
            res.villagerPoints.Add(c + new Vector3(5f, 0f, 6f));
            res.villagerPoints.Add(c + new Vector3(8f, 0f, -6f));
        }

        private static void BuildTomasShack(Result res)
        {
            Transform zone = Group("Cabana_Tomas");
            Vector3 c = new Vector3(-114f, 0f, -16f);
            Place(LOT + "Props/Shed01.prefab", zone, c, 90f, 1.15f, false);
            Place(LOT + "Props/Barrels01.prefab", zone, c + new Vector3(5f, 0f, -4f), 20f, 1f, false);
            Place(LOT + "Props/WallLogs.prefab", zone, c + new Vector3(-5f, 0f, 5f), 90f, 1f, false);
            Occupied.Add(new Bounds(c, new Vector3(14f, 5f, 14f)));
            AddLight(zone, c + new Vector3(4f, 2.2f, 4f), new Color(1f, 0.62f, 0.3f), 9f, 1.4f);
            res.tomasPoint = c + new Vector3(5.5f, 0f, 4f);
        }

        private static void BuildChapel(Result res)
        {
            Transform zone = Group("Capilla_Vieja");
            Vector3 c = res.chapelPoint;
            GameObject chapel = Place(CEM + "Buildings/Chapel 1.prefab", zone, c + new Vector3(0f, 0f, 14f), 180f, 1f, false);
            if (chapel == null) Place(CEM + "Buildings/Chapel_1A.prefab", zone, c + new Vector3(0f, 0f, 14f), 180f, 1f, false);
            Place(CEM + "Church Supplies/Cross Arch.prefab", zone, c + new Vector3(0f, 0f, -8f), 0f, 1f, false);
            string[] graves = { CEM + "Graves/Grave 1.prefab", CEM + "Graves/Grave 3.prefab", CEM + "Graves/Grave 5.prefab", CEM + "Graves/Gravestones/Cross 1.prefab", CEM + "Graves/Gravestones/Cross 3.prefab" };
            for (int x = 0; x < 4; x++)
                for (int z = 0; z < 3; z++)
                    Place(graves[RI(0, graves.Length)], zone, c + new Vector3(-16f + x * 4f + R(-0.5f, 0.5f), 0f, -2f + z * 5f), 180f + R(-12f, 12f), 1f, false);
            for (int i = 0; i < 10; i++)
                Place(CEM + "Gates, Fences/Fence_1A.prefab", zone, c + new Vector3(-20f + i * 2.4f, 0f, -12f), 0f, 1f, false);
            Occupied.Add(new Bounds(c + new Vector3(-4f, 0f, 6f), new Vector3(42f, 10f, 40f)));
            AddLight(zone, c + new Vector3(4f, 2.5f, -9f), new Color(0.55f, 0.65f, 1f), 12f, 1.2f);
            AddLight(zone, c + new Vector3(-6f, 2.5f, 4f), new Color(1f, 0.55f, 0.25f), 10f, 1.3f);
            res.villagerPoints.Add(c + new Vector3(-8f, 0f, 2f));
            res.villagerPoints.Add(c + new Vector3(6f, 0f, -4f));
            res.villagerPoints.Add(c + new Vector3(12f, 0f, 6f));
        }

        private static void BuildSawmill(Result res)
        {
            Transform zone = Group("Aserradero");
            Vector3 c = res.sawmillPoint;
            Place(LOT + "Props/Shed02.prefab", zone, c + new Vector3(0f, 0f, 10f), 180f, 1.3f, false);
            Place(LOT + "Props/Shed01.prefab", zone, c + new Vector3(-14f, 0f, 2f), 90f, 1.2f, false);
            Place(LOT + "Environment/Storage.prefab", zone, c + new Vector3(14f, 0f, 6f), 270f, 1f, false);
            for (int i = 0; i < 5; i++) Place(LOT + "Props/WallLogs.prefab", zone, c + new Vector3(-8f + i * 3.5f, 0f, -8f), R(80f, 100f), 1f, false);
            Place(LOT + "Props/Plank01.prefab", zone, c + new Vector3(6f, 0f, -3f), 20f, 1f, false);
            Place(LOT + "Props/Plank02.prefab", zone, c + new Vector3(-4f, 0f, -2f), 70f, 1f, false);
            Place(LOT + "Props/Plank03.prefab", zone, c + new Vector3(2f, 0f, 1f), 10f, 1f, false);
            Place(LOT + "Props/Barrels02.prefab", zone, c + new Vector3(10f, 0f, -10f), 0f, 1f, false);
            Occupied.Add(new Bounds(c + new Vector3(0f, 0f, 2f), new Vector3(40f, 8f, 34f)));
            AddLight(zone, c + new Vector3(0f, 2.6f, -2f), new Color(1f, 0.6f, 0.28f), 14f, 1.6f);
            res.villagerPoints.Add(c + new Vector3(-6f, 0f, -4f));
            res.villagerPoints.Add(c + new Vector3(5f, 0f, -6f));
            res.villagerPoints.Add(c + new Vector3(8f, 0f, 2f));
        }

        private static void BuildForestCamp(Result res)
        {
            Transform zone = Group("Campamento_Bosque");
            Vector3 c = res.forestPoint;
            Place(LOT + "Environment/BarracksSingle.prefab", zone, c + new Vector3(-17f, 0f, 2f), 90f, 1f, false);
            Place(LOT + "Environment/Storage.prefab", zone, c + new Vector3(17f, 0f, 14f), 270f, 1f, false);
            Place(LOT + "Paericle Systems/Fire.prefab", zone, c + new Vector3(8f, 0f, -10f), 0f, 1.1f, false);
            Place(LOT + "Paericle Systems/fx_fire.prefab", zone, c + new Vector3(8f, 0.2f, -10f), 0f, 1f, false);
            Place(LOT + "Props/WallLogs.prefab", zone, c + new Vector3(10f, 0f, -14f), 90f, 1f, false);
            AddLight(zone, c + new Vector3(8f, 1.6f, -10f), new Color(1f, 0.55f, 0.2f), 16f, 2.2f);
            Occupied.Add(new Bounds(c, new Vector3(46f, 8f, 40f)));
            res.villagerPoints.Add(c + new Vector3(-6f, 0f, 8f));
            res.villagerPoints.Add(c + new Vector3(4f, 0f, 14f));

            // Porton de salida (final del capitulo)
            Place(LOT + "Environment/Gate.prefab", zone, new Vector3(0f, 0f, 274f), 0f, 1f, false);
            AddLight(zone, new Vector3(4f, 3f, 270f), new Color(0.6f, 0.7f, 1f), 12f, 1.5f);
            AddLight(zone, new Vector3(-4f, 3f, 270f), new Color(0.6f, 0.7f, 1f), 12f, 1.5f);
            res.gatePoint = new Vector3(0f, 0f, 268f);
        }

        private static void BuildHouses(Result res)
        {
            int placed = 0;
            // Anillo alrededor de la plaza
            for (int i = 0; i < 16; i++)
            {
                float ang = i * 22.5f + 11.25f;
                float a = ang * Mathf.Deg2Rad;
                float radius = R(30f, 36f);
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                float yaw = Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg;
                if (TryHouse(p, yaw)) placed++;
            }

            // A lo largo de los caminos
            foreach (var road in new List<Road>(Roads))
            {
                Vector2 dir = (road.b - road.a).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x);
                float len = Vector2.Distance(road.a, road.b);
                for (float d = 26f; d < len - 6f; d += R(17f, 23f))
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 p2 = road.a + dir * d + normal * side * (road.width * 0.5f + R(11f, 15f));
                        Vector3 p = new Vector3(p2.x, 0f, p2.y);
                        float yaw = Mathf.Atan2(-normal.x * side, -normal.y * side) * Mathf.Rad2Deg;
                        if (TryHouse(p, yaw)) placed++;
                    }
                }
            }

            // Casas dispersas en las afueras
            for (int i = 0; i < 110; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                float radius = R(55f, 230f);
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                if (!InsideWorld(p, 20f)) continue;
                if (TryHouse(p, R(0f, 360f))) placed++;
            }

            // Algunos aldeanos junto a las casas de los caminos
            res.villagerPoints.Add(new Vector3(-6f, 0f, -48f));
            res.villagerPoints.Add(new Vector3(5f, 0f, -75f));
            res.villagerPoints.Add(new Vector3(-55f, 0f, -5f));
            res.villagerPoints.Add(new Vector3(60f, 0f, 5f));
            res.villagerPoints.Add(new Vector3(4f, 0f, 55f));
            Debug.Log($"[RE4] Casas colocadas: {placed}");
        }

        private static bool TryHouse(Vector3 p, float yaw)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                string path = HousePrefabs[RI(0, HousePrefabs.Length)];
                GameObject h = Place(path, _buildings, p, yaw + (attempt > 1 ? 90f : 0f), 1f, true, 1.5f);
                if (h != null)
                {
                    // Algunas casas con luz en la ventana/puerta
                    if (_rng.NextDouble() < 0.35)
                    {
                        Bounds b = RE4EditorUtil.WorldBounds(h);
                        AddLight(_lights, new Vector3(b.center.x, 2.2f, b.center.z) + (new Vector3(-p.x, 0f, -p.z).normalized * (b.extents.magnitude * 0.55f)),
                            new Color(1f, 0.6f, 0.3f), 7f, 1.1f);
                    }
                    return true;
                }
            }
            return false;
        }

        private static void BuildCars()
        {
            string[] cars =
            {
                "Assets/OlegWER/RustyCarsFree/Prefabs/SM_asset_00.prefab", "Assets/OlegWER/RustyCarsFree/Prefabs/SM_asset_01.prefab",
                "Assets/OlegWER/RustyCarsFree/Prefabs/SM_asset_02.prefab",
            };
            Vector3[] spots = { new Vector3(12f, 0f, -115f), new Vector3(-12f, 0f, -95f), new Vector3(100f, 0f, 12f) };
            for (int i = 0; i < spots.Length; i++)
            {
                Place(cars[i % cars.Length], _props, spots[i], R(0f, 360f), 1f, true, 0.5f);
            }
        }

        private static void BuildTorches()
        {
            foreach (var road in Roads)
            {
                Vector2 dir = (road.b - road.a).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x);
                float len = Vector2.Distance(road.a, road.b);
                int side = 1;
                for (float d = 8f; d < len; d += 20f)
                {
                    Vector2 p2 = road.a + dir * d + normal * side * (road.width * 0.5f + 1.3f);
                    Vector3 p = new Vector3(p2.x, 0f, p2.y);
                    GameObject t = Place(LOT + "Props/TorchPole.prefab", _props, p, 0f, 1f, false);
                    if (t != null)
                    {
                        Bounds b = RE4EditorUtil.WorldBounds(t);
                        AddLight(_lights, new Vector3(p.x, b.max.y + 0.2f, p.z), new Color(1f, 0.58f, 0.25f), 11f, 1.5f);
                    }
                    side = -side;
                }
            }
        }

        private static void BuildTrees()
        {
            Material bark = RE4EditorUtil.SaveMaterial(RE4DataBuilder.MatFolder + "/RE4_DeadBark.mat", "Standard", new Color(0.2f, 0.16f, 0.12f),
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LightingOptimizationTutorial/Textures/Environment/TerrainMudDryAlbedo.tif"));
            bark.SetFloat("_Glossiness", 0.05f);

            int count = 0;
            for (int i = 0; i < 1400 && count < 420; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                float radius = R(40f, 280f);
                Vector3 p = WorldCenter + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                if (!InsideWorld(p, 6f) || p.magnitude < 40f) continue;
                Bounds test = new Bounds(p + Vector3.up * 3f, new Vector3(2.5f, 6f, 2.5f));
                if (Overlaps(test)) continue;

                GameObject tree = new GameObject("ArbolSeco");
                tree.transform.SetParent(_nature, false);
                tree.transform.position = p;
                tree.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                float h = R(6f, 11f);
                float w = R(0.35f, 0.6f);
                Limb(tree.transform, Vector3.zero, Vector3.up, h, w, bark);
                int branches = RI(3, 6);
                for (int b = 0; b < branches; b++)
                {
                    float y = R(h * 0.45f, h * 0.9f);
                    Vector3 dir = Quaternion.Euler(R(-55f, -25f), b * (360f / branches) + R(-20f, 20f), 0f) * Vector3.forward;
                    dir = (dir + Vector3.up * 0.8f).normalized;
                    Limb(tree.transform, new Vector3(0f, y, 0f), dir, R(1.8f, 3.5f), w * 0.35f, bark);
                }
                CapsuleCollider cc = tree.AddComponent<CapsuleCollider>();
                cc.radius = w * 0.6f;
                cc.height = h;
                cc.center = new Vector3(0f, h * 0.5f, 0f);
                Occupied.Add(test);
                count++;
            }
        }

        private static void Limb(Transform parent, Vector3 start, Vector3 dir, float length, float width, Material mat)
        {
            GameObject l = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(l.GetComponent<Collider>());
            l.name = "Rama";
            l.transform.SetParent(parent, false);
            l.transform.localPosition = start + dir * length * 0.5f;
            l.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            l.transform.localScale = new Vector3(width, length * 0.5f, width);
            l.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void BuildProps()
        {
            int n = 0;
            for (int i = 0; i < 700 && n < 170; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                float radius = R(16f, 230f);
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                if (!InsideWorld(p, 10f)) continue;
                if (Place(SmallProps[RI(0, SmallProps.Length)], _props, p, R(0f, 360f), 1f, true, 0.4f) != null) n++;
            }
        }

        private static void BuildPerimeter()
        {
            Transform rim = Group("Perimetro");
            string[] rocks =
            {
                "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Prefabs/MountainRocks01.prefab",
                "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Prefabs/MountainRocks02.prefab",
                "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Prefabs/MountainsRocks03.prefab",
                "Assets/TheTalesFactory/Photoscanned MoutainsRocks PBR/Prefabs/MountainRocks01_A.prefab",
            };
            var points = new List<Vector3>();
            for (int i = 0; i < 64; i++)
            {
                float a = i * (360f / 64f) * Mathf.Deg2Rad;
                Vector3 p = WorldCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (WorldRadius + 10f);
                if (p.z > SouthWall - 4f) points.Add(p);
            }
            float halfWall = Mathf.Sqrt(WorldRadius * WorldRadius - (WorldCenter.z - SouthWall) * (WorldCenter.z - SouthWall));
            for (float x = -halfWall; x <= halfWall; x += 26f) points.Add(new Vector3(x, 0f, SouthWall - 12f));

            for (int i = 0; i < points.Count; i++)
            {
                GameObject rock = Place(rocks[i % rocks.Length], rim, points[i], R(0f, 360f), 1f, false);
                if (rock == null) continue;
                Bounds b = RE4EditorUtil.WorldBounds(rock);
                float maxSize = Mathf.Max(b.size.x, b.size.z);
                if (maxSize > 0.01f && maxSize < 30f) rock.transform.localScale *= 36f / maxSize;
            }

            // Muro invisible: arco del mundo + muro recto al sur (detras del inicio)
            for (int i = 0; i < 90; i++)
            {
                float deg = i * 4f;
                float a = deg * Mathf.Deg2Rad;
                Vector3 p = WorldCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * WorldRadius;
                if (p.z < SouthWall) continue;
                GameObject w = new GameObject("Limite");
                w.transform.SetParent(rim, false);
                w.transform.position = p + Vector3.up * 5f;
                w.transform.rotation = Quaternion.Euler(0f, -deg, 0f);
                BoxCollider bc = w.AddComponent<BoxCollider>();
                bc.size = new Vector3(2f, 10f, 22f);
            }
            GameObject south = new GameObject("Limite_Sur");
            south.transform.SetParent(rim, false);
            south.transform.position = new Vector3(0f, 5f, SouthWall);
            BoxCollider sb = south.AddComponent<BoxCollider>();
            sb.size = new Vector3(halfWall * 2f + 10f, 10f, 2f);

            // Porton en la entrada sur
            Place(LOT + "Environment/Gate.prefab", rim, new Vector3(0f, 0f, -148f), 0f, 1f, false);
        }

        private static void AddLight(Transform parent, Vector3 pos, Color color, float range, float intensity)
        {
            GameObject l = new GameObject("Luz_Fuego");
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            Light light = l.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            FlickerLight f = l.AddComponent<FlickerLight>();
            f.baseIntensity = intensity;
        }

        // =========================================================== Colisiones y estatico

        public static void EnsureColliders(GameObject root)
        {
            int added = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = child.gameObject;
                if (go.GetComponent<ParticleSystem>() != null) continue;
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if (go.GetComponent<Collider>() != null) continue;
                if (go.name == "Rama" || go.name == "Plaza" || go.name == "Camino") continue;

                // si un ancestro (prefab) ya trae colisiones propias, respetarlas
                bool parentHas = false;
                Transform p = child.parent;
                while (p != null && p != root.transform)
                {
                    if (p.GetComponent<Collider>() != null) { parentHas = true; break; }
                    p = p.parent;
                }
                if (parentHas) continue;

                Bounds b = mf.sharedMesh.bounds;
                if (b.size.magnitude * child.lossyScale.magnitude < 0.15f) continue;
                MeshCollider mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                added++;
            }
            Debug.Log($"[RE4] Colisiones de malla agregadas: {added}");
        }

        private static void MarkStatic(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Light>() != null || t.GetComponent<ParticleSystem>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }
        }
    }
}
