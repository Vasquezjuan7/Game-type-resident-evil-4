using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Gameplay.RE4;

namespace LevelSetup.RE4Tools
{
    /// <summary>Crea los assets de datos: materiales de efectos, objetos del maletin y armas.</summary>
    public static class RE4DataBuilder
    {
        public const string Root = "Assets/RE4";
        public const string DataFolder = Root + "/Data";
        public const string MatFolder = Root + "/Materials";
        public const string TexFolder = Root + "/Textures";

        private const string KevinWeapons = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Soldier Animations/Prefabs/Weapons/";

        public class Database
        {
            public CombatFXLibrary fx;
            public ItemDefinition ammoHandgun, ammoShotgun, ammoSmg, ammoRifle, ammoRocket, ammoMagnum;
            public ItemDefinition herbGreen, herbRed, herbGG, herbGR, spray;
            public ItemDefinition itemPistol, itemPistol2, itemShotgun, itemSmg, itemRifle, itemRocket, itemMagnum;
            public ItemDefinition pesetas;
            public ItemDefinition gunpowder, resourceS, resourceL, elenaPendant;
            public List<ItemDefinition> treasures = new List<ItemDefinition>();
            public List<ItemDefinition> documents = new List<ItemDefinition>();
            public List<CraftRecipe> recipes = new List<CraftRecipe>();
            public List<ItemDefinition> shopStock = new List<ItemDefinition>();
            public WeaponDefinition pistol, pistol2, shotgun, smg, rifle, rocket, magnum;
        }

        public static Database Build()
        {
            RE4EditorUtil.EnsureFolder(DataFolder);
            RE4EditorUtil.EnsureFolder(MatFolder);
            RE4EditorUtil.EnsureFolder(TexFolder);

            // Modelos de las pistolas y del AK con corredera/cargador separados
            RE4WeaponBuilder.BuildAll();
            RE4PropBuilder.Props props = RE4PropBuilder.Build();

            var db = new Database();
            db.fx = BuildFX();

            // --------------------------------------------------------- Municion
            db.ammoHandgun = Item("Ammo_Handgun", "ammo_handgun", "Balas de pistola", "Munición de 9 mm para pistolas.", ItemType.Ammo, new Vector2Int(1, 1), 50, new Color(0.55f, 0.45f, 0.18f), "9mm");
            db.ammoShotgun = Item("Ammo_Shotgun", "ammo_shotgun", "Cartuchos de escopeta", "Cartuchos del calibre 12.", ItemType.Ammo, new Vector2Int(2, 1), 15, new Color(0.62f, 0.16f, 0.1f), "Cartuchos");
            db.ammoSmg = Item("Ammo_SMG", "ammo_smg", "Munición de subfusil", "Cargadores para el subfusil TMP.", ItemType.Ammo, new Vector2Int(2, 1), 120, new Color(0.22f, 0.32f, 0.5f), "TMP");
            db.ammoRifle = Item("Ammo_Rifle", "ammo_rifle", "Balas 7.62 mm", "Munición 7.62 mm para el rifle de asalto AK-47.", ItemType.Ammo, new Vector2Int(2, 1), 90, new Color(0.3f, 0.45f, 0.22f), "7.62");
            db.ammoMagnum = Item("Ammo_Magnum", "ammo_magnum", "Balas Magnum", "Munición de gran calibre para el revólver.", ItemType.Ammo, new Vector2Int(2, 1), 12, new Color(0.6f, 0.48f, 0.2f), "Magnum");
            db.pesetas = Item("Pesetas", "pesetas", "Pesetas", "Dinero. El mercader lo acepta a cambio de mejoras.", ItemType.Money, new Vector2Int(1, 1), 999999, new Color(0.85f, 0.68f, 0.2f), "₧");
            db.ammoRocket = Item("Ammo_Rocket", "ammo_rocket", "Cohete", "Proyectil explosivo para el lanzacohetes.", ItemType.Ammo, new Vector2Int(3, 1), 2, new Color(0.42f, 0.4f, 0.18f), "Cohete");

            // --------------------------------------------------------- Curacion
            db.herbGreen = Item("Herb_Green", "herb_green", "Hierba verde", "Hierba medicinal autóctona. Recupera algo de vida.\nCombínala con otras hierbas.", ItemType.Herb, new Vector2Int(1, 2), 1, new Color(0.14f, 0.5f, 0.14f), "Verde");
            db.herbGreen.healPercent = 0.3f; db.herbGreen.herb = HerbColor.Green;
            db.herbRed = Item("Herb_Red", "herb_red", "Hierba roja", "Sola no tiene efecto. Combinada con una hierba verde cura por completo.", ItemType.Herb, new Vector2Int(1, 2), 1, new Color(0.6f, 0.1f, 0.1f), "Roja");
            db.herbRed.herb = HerbColor.Red;
            db.herbGG = Item("Herb_GG", "herb_gg", "Mezcla de hierbas (V+V)", "Dos hierbas verdes. Recupera bastante vida.", ItemType.Herb, new Vector2Int(1, 2), 1, new Color(0.1f, 0.62f, 0.2f), "V+V");
            db.herbGG.healPercent = 0.65f; db.herbGG.herb = HerbColor.GreenGreen;
            db.herbGR = Item("Herb_GR", "herb_gr", "Mezcla de hierbas (V+R)", "Hierba verde y roja. Recupera toda la vida.", ItemType.Herb, new Vector2Int(1, 2), 1, new Color(0.55f, 0.35f, 0.1f), "V+R");
            db.herbGR.healPercent = 1f; db.herbGR.herb = HerbColor.GreenRed;
            db.spray = Item("Heal_Spray", "spray", "Spray de primeros auxilios", "Medicina en aerosol. Recupera toda la vida.", ItemType.Heal, new Vector2Int(1, 2), 1, new Color(0.72f, 0.72f, 0.78f), "Spray");
            db.spray.healPercent = 1f;

            // --------------------------------------------------------- Armas
            // Pistola 1: Beretta 92F ("edge resident"). Mas balas y cadencia.
            db.pistol = Weapon("Weapon_Pistol", "pistol", "Pistola 92F", WeaponClass.Handgun, RE4WeaponBuilder.Folder + "/Pistola_92F.prefab", 0.22f, Vector3.zero);
            Animated(db.pistol, 0.18f, true);
            db.pistol.damage = 30f; db.pistol.pellets = 1; db.pistol.magazineSize = 15; db.pistol.fireInterval = 0.17f; db.pistol.automatic = false; db.pistol.reloadTime = 1.35f;
            db.pistol.hipSpread = 2.8f; db.pistol.aimSpread = 0.25f; db.pistol.aimFov = 54f; db.pistol.aimTime = 0.16f; db.pistol.headMultiplier = 3f; db.pistol.range = 80f;
            db.pistol.recoilKickBack = 0.045f; db.pistol.recoilPitch = 2.4f; db.pistol.recoilYaw = 0.6f; db.pistol.cameraPitchKick = 1.2f; db.pistol.staggerPower = 0.4f;
            db.pistol.shotPitch = 1.2f; db.pistol.shotBody = 0.65f; db.pistol.shotLength = 0.42f; db.pistol.noiseRadius = 40f;
            db.pistol.hipPosition = new Vector3(0.15f, -0.15f, 0.32f); db.pistol.aimPosition = new Vector3(0f, -0.08f, 0.27f);
            db.pistol.upgradeBaseCost = 3000; db.pistol.capacityPerLevel = 3;
            db.pistol.ammoItem = db.ammoHandgun;

            // Pistola 2: la pistola de "Arma resident". Mas potente y precisa, menos balas.
            db.pistol2 = Weapon("Weapon_Pistol2", "pistol2", "Pistola Resident", WeaponClass.Handgun, RE4WeaponBuilder.Folder + "/Pistola_Resident.prefab", 0.26f, Vector3.zero);
            Animated(db.pistol2, 0.15f, true);
            db.pistol2.damage = 42f; db.pistol2.pellets = 1; db.pistol2.magazineSize = 10; db.pistol2.fireInterval = 0.23f; db.pistol2.automatic = false; db.pistol2.reloadTime = 1.55f;
            db.pistol2.hipSpread = 3f; db.pistol2.aimSpread = 0.15f; db.pistol2.aimFov = 50f; db.pistol2.aimTime = 0.18f; db.pistol2.headMultiplier = 3.4f; db.pistol2.range = 90f;
            db.pistol2.recoilKickBack = 0.06f; db.pistol2.recoilPitch = 3.4f; db.pistol2.recoilYaw = 0.7f; db.pistol2.cameraPitchKick = 1.8f; db.pistol2.staggerPower = 0.55f;
            db.pistol2.shotPitch = 1.0f; db.pistol2.shotBody = 0.85f; db.pistol2.shotLength = 0.5f; db.pistol2.noiseRadius = 42f;
            db.pistol2.hipPosition = new Vector3(0.15f, -0.15f, 0.33f); db.pistol2.aimPosition = new Vector3(0f, -0.08f, 0.28f);
            db.pistol2.upgradeBaseCost = 4000; db.pistol2.capacityPerLevel = 2;
            db.pistol2.ammoItem = db.ammoHandgun;

            db.shotgun = Weapon("Weapon_Shotgun", "shotgun", "Escopeta W-870", WeaponClass.Shotgun, KevinWeapons + "Human_Rifle.prefab", 0.9f, Vector3.zero);
            db.shotgun.damage = 19f; db.shotgun.pellets = 8; db.shotgun.magazineSize = 6; db.shotgun.fireInterval = 0.95f; db.shotgun.reloadTime = 3.3f;
            db.shotgun.reloadOneByOne = true; db.shotgun.cycleAfterShot = true; db.shotgun.range = 35f;
            db.shotgun.hipSpread = 4f; db.shotgun.aimSpread = 2f; db.shotgun.aimFov = 58f; db.shotgun.headMultiplier = 1.6f;
            db.shotgun.recoilKickBack = 0.12f; db.shotgun.recoilPitch = 6f; db.shotgun.cameraPitchKick = 4f; db.shotgun.staggerPower = 0.9f; db.shotgun.knockback = 1.2f;
            db.shotgun.shotPitch = 0.75f; db.shotgun.shotBody = 1.6f; db.shotgun.shotLength = 0.9f; db.shotgun.noiseRadius = 60f;
            db.shotgun.hipPosition = new Vector3(0.17f, -0.17f, 0.42f); db.shotgun.aimPosition = new Vector3(0f, -0.1f, 0.36f);
            db.shotgun.ammoItem = db.ammoShotgun;

            db.smg = Weapon("Weapon_SMG", "smg", "Subfusil TMP", WeaponClass.SMG, KevinWeapons + "Human_AssaultRifle.prefab", 0.6f, Vector3.zero);
            db.smg.damage = 14f; db.smg.magazineSize = 40; db.smg.fireInterval = 0.075f; db.smg.automatic = true; db.smg.reloadTime = 1.9f;
            db.smg.hipSpread = 4f; db.smg.aimSpread = 1.1f; db.smg.aimFov = 58f; db.smg.headMultiplier = 2.2f;
            db.smg.recoilKickBack = 0.03f; db.smg.recoilPitch = 1.1f; db.smg.recoilYaw = 0.8f; db.smg.cameraPitchKick = 0.45f; db.smg.staggerPower = 0.15f;
            db.smg.shotPitch = 1.35f; db.smg.shotBody = 0.55f; db.smg.shotLength = 0.35f; db.smg.noiseRadius = 45f;
            db.smg.hipPosition = new Vector3(0.16f, -0.16f, 0.36f); db.smg.aimPosition = new Vector3(0f, -0.095f, 0.3f);
            db.smg.ammoItem = db.ammoSmg;

            db.rifle = Weapon("Weapon_Rifle", "rifle", "Rifle de asalto AK-47", WeaponClass.AssaultRifle, RE4WeaponBuilder.Folder + "/Rifle_AK47.prefab", 0.9f, Vector3.zero);
            Animated(db.rifle, 0.1f, false);
            db.rifle.useScopeOverlay = true;
            db.rifle.damage = 30f; db.rifle.pellets = 1; db.rifle.magazineSize = 30; db.rifle.fireInterval = 0.1f; db.rifle.automatic = true; db.rifle.reloadTime = 2.3f;
            db.rifle.cycleAfterShot = false; db.rifle.reloadOneByOne = false; db.rifle.penetration = 1; db.rifle.range = 160f;
            db.rifle.hipSpread = 4.5f; db.rifle.aimSpread = 0.35f; db.rifle.aimFov = 30f; db.rifle.aimTime = 0.25f; db.rifle.headMultiplier = 2.5f;
            db.rifle.recoilKickBack = 0.04f; db.rifle.recoilPitch = 1.5f; db.rifle.recoilYaw = 1f; db.rifle.cameraPitchKick = 0.7f; db.rifle.staggerPower = 0.3f; db.rifle.knockback = 0.5f;
            db.rifle.shotPitch = 0.95f; db.rifle.shotBody = 1.2f; db.rifle.shotLength = 0.55f; db.rifle.noiseRadius = 60f;
            db.rifle.hipPosition = new Vector3(0.17f, -0.17f, 0.42f); db.rifle.aimPosition = new Vector3(0f, -0.1f, 0.3f);
            db.rifle.upgradeBaseCost = 6000; db.rifle.capacityPerLevel = 5;
            db.rifle.ammoItem = db.ammoRifle;

            // Lanzacohetes de 4 tubos (modelo importado); si no esta, el bazuca de Kevin Iglesias
            bool newLauncher = AssetDatabase.LoadAssetAtPath<GameObject>(RE4WeaponBuilder.Folder + "/Lanzacohetes.prefab") != null;
            db.rocket = Weapon("Weapon_Rocket", "rocket", "Lanzacohetes", WeaponClass.RocketLauncher,
                newLauncher ? RE4WeaponBuilder.Folder + "/Lanzacohetes.prefab" : KevinWeapons + "Human_Bazooka.prefab", 1.0f, Vector3.zero);
            db.rocket.canonicalModel = newLauncher;
            if (newLauncher) db.rocket.modelEulerOffset = Vector3.zero;
            db.rocket.damage = 900f; db.rocket.explosionRadius = 6f; db.rocket.projectileSpeed = 40f; db.rocket.magazineSize = 1; db.rocket.fireInterval = 1.5f; db.rocket.reloadTime = 3f;
            db.rocket.hipSpread = 2f; db.rocket.aimSpread = 0.4f; db.rocket.aimFov = 50f;
            db.rocket.recoilKickBack = 0.2f; db.rocket.recoilPitch = 8f; db.rocket.cameraPitchKick = 6f;
            db.rocket.shotPitch = 0.6f; db.rocket.shotBody = 2f; db.rocket.shotLength = 1.5f; db.rocket.noiseRadius = 90f;
            db.rocket.hipPosition = new Vector3(0.2f, -0.12f, 0.3f); db.rocket.aimPosition = new Vector3(0.08f, -0.09f, 0.25f);
            db.rocket.ammoItem = db.ammoRocket;

            // Revolver: tambor que gira en cada disparo y se abre al recargar
            db.magnum = Weapon("Weapon_Magnum", "magnum", "Revólver Requiem", WeaponClass.Handgun, RE4WeaponBuilder.Folder + "/Revolver_Requiem.prefab", 0.3f, Vector3.zero);
            db.magnum.canonicalModel = true; db.magnum.modelEulerOffset = Vector3.zero;
            db.magnum.slidePart = ""; db.magnum.magazinePart = ""; db.magnum.slideLocksOnEmpty = false; db.magnum.useScopeOverlay = false;
            db.magnum.cylinderPart = RE4WeaponBuilder.RevolverCylinder; db.magnum.cylinderChambers = 6;
            db.magnum.damage = 120f; db.magnum.pellets = 1; db.magnum.magazineSize = 6; db.magnum.fireInterval = 0.55f; db.magnum.automatic = false; db.magnum.reloadTime = 2.4f;
            db.magnum.reloadOneByOne = false; db.magnum.cycleAfterShot = false; db.magnum.penetration = 2; db.magnum.range = 120f;
            db.magnum.hipSpread = 3.5f; db.magnum.aimSpread = 0.12f; db.magnum.aimFov = 48f; db.magnum.aimTime = 0.22f; db.magnum.headMultiplier = 2.5f;
            db.magnum.recoilKickBack = 0.12f; db.magnum.recoilPitch = 7f; db.magnum.recoilYaw = 1.2f; db.magnum.cameraPitchKick = 4.5f; db.magnum.staggerPower = 1f; db.magnum.knockback = 1f;
            db.magnum.shotPitch = 0.8f; db.magnum.shotBody = 1.6f; db.magnum.shotLength = 0.9f; db.magnum.noiseRadius = 70f;
            db.magnum.hipPosition = new Vector3(0.15f, -0.15f, 0.34f); db.magnum.aimPosition = new Vector3(0f, -0.08f, 0.3f);
            db.magnum.upgradeBaseCost = 8000; db.magnum.capacityPerLevel = 0;
            db.magnum.ammoItem = db.ammoMagnum;

            db.itemPistol = WeaponItem("Item_Pistol", db.pistol, new Vector2Int(3, 2), new Color(0.3f, 0.3f, 0.33f), "Pistola semiautomática 92F. Cargador grande y buena cadencia.");
            db.itemPistol2 = WeaponItem("Item_Pistol2", db.pistol2, new Vector2Int(3, 2), new Color(0.34f, 0.28f, 0.26f), "Pistola de gran calibre. Más potente y precisa, pero con menos balas.");
            db.itemShotgun = WeaponItem("Item_Shotgun", db.shotgun, new Vector2Int(7, 2), new Color(0.38f, 0.26f, 0.16f), "Escopeta de corredera. Devastadora a corta distancia y derriba grupos.");
            db.itemSmg = WeaponItem("Item_SMG", db.smg, new Vector2Int(3, 2), new Color(0.2f, 0.24f, 0.3f), "Subfusil automático. Gran cadencia, poco daño por bala.");
            db.itemRifle = WeaponItem("Item_Rifle", db.rifle, new Vector2Int(7, 2), new Color(0.3f, 0.35f, 0.24f), "Fusil de asalto AK-47 con mira. Fuego automático, cargador de 30 balas.");
            db.itemMagnum = WeaponItem("Item_Magnum", db.magnum, new Vector2Int(4, 2), new Color(0.32f, 0.3f, 0.3f), "Revólver Magnum. Seis balas que lo atraviesan todo. Ideal contra el Tyrant.");
            db.itemRocket = WeaponItem("Item_Rocket", db.rocket, new Vector2Int(7, 2), new Color(0.35f, 0.33f, 0.18f), "Lanzacohetes. Un solo disparo arrasa con todo.");

            BuildStoryItems(db);

            // Modelos 3D de los objetos recogibles
            WorldModel(db.spray, props.spray, 0.22f);
            WorldModel(db.herbGreen, props.herbGreen, 0.34f);
            WorldModel(db.herbRed, props.herbRed, 0.34f);
            WorldModel(db.herbGG, props.herbGG, 0.34f);
            WorldModel(db.herbGR, props.herbGR, 0.34f);
            WorldModel(db.ammoHandgun, props.ammoHandgun, 0.14f);
            WorldModel(db.ammoShotgun, props.ammoShotgun, 0.18f);
            WorldModel(db.ammoSmg, props.ammoSmg, 0.16f);
            WorldModel(db.ammoRifle, props.ammoRifle, 0.18f);
            WorldModel(db.ammoMagnum, props.ammoMagnum, 0.14f);

            foreach (var w in new[] { db.pistol, db.pistol2, db.shotgun, db.smg, db.rifle, db.rocket, db.magnum })
            {
                if (!w.canonicalModel) w.modelEulerOffset = RE4EditorUtil.ComputeWeaponOrientation(w.viewModelPrefab, w.modelEulerOffset);
                EditorUtility.SetDirty(w);
            }

            AssetDatabase.SaveAssets();
            return db;
        }

        private static ItemDefinition Item(string file, string id, string name, string desc, ItemType type, Vector2Int size, int stack, Color color, string label)
        {
            var it = RE4EditorUtil.CreateOrLoad<ItemDefinition>($"{DataFolder}/{file}.asset");
            it.id = id;
            it.displayName = name;
            it.description = desc;
            it.type = type;
            it.size = size;
            it.maxStack = stack;
            it.tileColor = color;
            it.shortLabel = label;
            EditorUtility.SetDirty(it);
            return it;
        }

        private static WeaponDefinition Weapon(string file, string id, string name, WeaponClass cls, string prefabPath, float length, Vector3 fallbackEuler)
        {
            var w = RE4EditorUtil.CreateOrLoad<WeaponDefinition>($"{DataFolder}/{file}.asset");
            w.id = id;
            w.displayName = name;
            w.weaponClass = cls;
            w.viewModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (w.viewModelPrefab == null) Debug.LogWarning("[RE4] Modelo de arma no encontrado: " + prefabPath);
            w.viewModelLength = length;
            w.modelEulerOffset = fallbackEuler;
            EditorUtility.SetDirty(w);
            return w;
        }

        /// <summary>Arma con corredera / palanca y cargador animados (prefab ya orientado por RE4WeaponBuilder).</summary>
        private static void Animated(WeaponDefinition w, float slideTravel, bool slideLocks)
        {
            w.canonicalModel = true;
            w.modelEulerOffset = Vector3.zero;
            w.slidePart = "Slide";
            w.magazinePart = "Magazine";
            w.slideTravel = slideTravel;
            w.slideLocksOnEmpty = slideLocks;
            w.useScopeOverlay = false;
        }

        // ------------------------------------------------------------ Historia, fabricacion y tienda

        private static void BuildStoryItems(Database db)
        {
            // Materiales de fabricacion (RE4 Remake)
            db.gunpowder = Item("Res_Polvora", "polvora", "Pólvora", "Material básico para fabricar munición en el maletín (tecla C).", ItemType.Resource, new Vector2Int(1, 1), 30, new Color(0.25f, 0.25f, 0.28f), "Pólvora");
            db.resourceS = Item("Res_RecursoS", "recurso_s", "Recurso (S)", "Piezas metálicas pequeñas. Se combinan con pólvora.", ItemType.Resource, new Vector2Int(1, 1), 20, new Color(0.45f, 0.42f, 0.35f), "Rec. S");
            db.resourceL = Item("Res_RecursoL", "recurso_l", "Recurso (L)", "Piezas metálicas grandes para munición pesada.", ItemType.Resource, new Vector2Int(1, 1), 10, new Color(0.5f, 0.38f, 0.25f), "Rec. L");
            db.resourceL.buyPrice = 3000;

            // Tesoros para vender al mercader
            db.treasures.Clear();
            db.treasures.Add(Treasure("Tesoro_Anillo", "anillo_plata", "Anillo de plata", "Un anillo con un ojo grabado. Al mercader le interesa.", 3000));
            db.treasures.Add(Treasure("Tesoro_Perla", "perla_negra", "Perla negra", "Una perla oscura y pesada.", 5000));
            db.treasures.Add(Treasure("Tesoro_Colgante", "colgante_rubi", "Colgante de rubí", "Joya antigua de la familia del alcalde.", 7500));
            db.treasures.Add(Treasure("Tesoro_Caliz", "caliz", "Cáliz de la Hermandad", "Cáliz de oro con restos de un líquido negro.", 12000));

            db.elenaPendant = Item("Clave_Colgante", "colgante_elena", "Colgante de Elena", "El colgante de Elena Ibarra. Tiene algo grabado en el reverso.", ItemType.Key, new Vector2Int(1, 1), 1, new Color(0.8f, 0.7f, 0.3f), "Elena");

            // Documentos (tecla J para releerlos)
            db.documents.Clear();
            db.documents.Add(Doc("Doc_DiarioAldeano", "doc_diario", "Diario de un aldeano",
                "14 de octubre\nDesde que el Padre llegó al pueblo ya nadie va a misa. Dicen que ahora rezan en la cantera, de noche, y que vuelven cantando.\n\n" +
                "17 de octubre\nMi mujer volvió de la reunión con los ojos rojos. No me habla. Tose sangre y se ríe sola.\n\n" +
                "19 de octubre\nMe han dado de beber el agua «bendita» del pozo. Me arde la nuca. Oigo una voz que no es la mía y me dice que espere al forastero.\n\n" +
                "Si alguien lee esto: no bebáis del pozo."));
            db.documents.Add(Doc("Doc_Mercader", "doc_mercader", "Nota manchada de aceite",
                "Forastero:\nSi buscas armas, sigue los faroles azules. Compro tesoros, vendo hierro y afino lo que traigas.\n\n" +
                "· Pestañas COMPRAR / VENDER / MEJORAR.\n· Las joyas de este pueblo valen más que sus vidas.\n· Un cuchillo roto no para golpes: tráemelo.\n\nJe, je, je."));
            db.documents.Add(Doc("Doc_Alcalde", "doc_alcalde", "Carta del alcalde",
                "A la Hermandad:\nEl forastero que pregunta por la muchacha americana llegará esta noche por el camino del sur.\n\n" +
                "El Padre la quiere viva y sin un rasguño: la semilla que lleva dentro aún no ha despertado.\nEl forastero, que alimente a la tierra.\n\n" +
                "Que suene la campana cuando acabe.\n— Alcalde Ramiro Ortega"));
            db.documents.Add(Doc("Doc_Tomas", "doc_tomas", "Nota temblorosa",
                "Me escondo en la cabaña del camino del oeste. No bebí del pozo, por eso me cazan.\n\n" +
                "Si eres de fuera, ven a verme. Sé adónde se llevaron a la chica rubia.\n\n— Tomás, el pastor"));
            db.documents.Add(Doc("Doc_Doctor", "doc_doctor", "Informe del doctor Salvatierra",
                "Paciente 12. Organismo adherido a la base del cráneo («la semilla», como lo llaman).\n\n" +
                "El huésped conserva fuerza y coordinación, pierde el miedo y obedece una voluntad común. Soporta heridas mortales en el torso.\n\n" +
                "Conclusiones: la cabeza es el punto débil. Un disparo en las piernas lo derriba y lo deja indefenso unos segundos.\n\n" +
                "Advertencia: en algunos huéspedes la semilla crece sin control. El sujeto «T» ya mide más de dos metros y rompe las jaulas."));
            db.documents.Add(Doc("Doc_Elena", "doc_elena", "Página arrancada de un diario",
                "No sé qué día es. Me tienen encerrada en la sacristía.\n\n" +
                "Esta mañana un hombre con túnica me miró los ojos con una lámpara y dijo: «Todavía no». Me han dado agua del pozo; la escupí.\n\n" +
                "Papá, si alguien te trae esto: van a llevarme al castillo. Dicen que el Padre me espera allí.\n— Elena"));
            db.documents.Add(Doc("Doc_Plegaria", "doc_plegaria", "Plegaria de la Hermandad",
                "Del pozo nace la semilla,\nde la semilla nace la voz,\nde la voz nace la obediencia.\n\n" +
                "Bebe, hermano, y no estarás solo nunca más.\nBebe, y el dolor será de otro.\nBebe, y el Padre te verá."));
            db.documents.Add(Doc("Doc_Aserradero", "doc_aserradero", "Registro del aserradero",
                "Encargo de la Hermandad: cuarenta jaulas de roble para la cantera.\n\n" +
                "Día 3: el grande rompió otra jaula. Tres muertos. El Padre dice que es una bendición.\n" +
                "Día 5: ya no le ponemos cadenas. Lo dejan suelto la noche de la campana.\n\n" +
                "Que Dios nos perdone."));

            // Recetas de fabricacion
            db.recipes.Clear();
            db.recipes.Add(Recipe(db.ammoHandgun, 12, db.gunpowder, 1, null, 0));
            db.recipes.Add(Recipe(db.ammoShotgun, 5, db.gunpowder, 1, db.resourceS, 1));
            db.recipes.Add(Recipe(db.ammoSmg, 30, db.gunpowder, 2, null, 0));
            db.recipes.Add(Recipe(db.ammoRifle, 15, db.gunpowder, 2, db.resourceS, 1));
            db.recipes.Add(Recipe(db.ammoMagnum, 3, db.gunpowder, 2, db.resourceL, 1));
            db.recipes.Add(Recipe(db.ammoRocket, 1, db.resourceL, 3, db.gunpowder, 4));

            // Tienda del mercader
            db.spray.buyPrice = 6000;
            db.herbGreen.buyPrice = 2500;
            db.itemShotgun.buyPrice = 18000;
            db.itemSmg.buyPrice = 16000;
            db.itemRifle.buyPrice = 30000;
            db.itemMagnum.buyPrice = 35000;
            db.itemRocket.buyPrice = 30000;
            db.shopStock = new List<ItemDefinition> { db.spray, db.herbGreen, db.resourceL, db.itemShotgun, db.itemSmg, db.itemRifle, db.itemMagnum, db.itemRocket };
            foreach (var it in new[] { db.spray, db.herbGreen, db.resourceL, db.itemShotgun, db.itemSmg, db.itemRifle, db.itemMagnum, db.itemRocket }) EditorUtility.SetDirty(it);
        }

        private static ItemDefinition Treasure(string file, string id, string name, string desc, int price)
        {
            ItemDefinition t = Item(file, id, name, desc, ItemType.Treasure, new Vector2Int(1, 1), 1, new Color(0.75f, 0.6f, 0.2f), name.Split(' ')[0]);
            t.sellPrice = price;
            EditorUtility.SetDirty(t);
            return t;
        }

        private static ItemDefinition Doc(string file, string id, string title, string text)
        {
            ItemDefinition d = Item(file, id, title, "Documento", ItemType.Document, new Vector2Int(1, 1), 1, new Color(0.85f, 0.8f, 0.65f), "Doc");
            d.documentText = text;
            EditorUtility.SetDirty(d);
            return d;
        }

        private static CraftRecipe Recipe(ItemDefinition result, int count, ItemDefinition a, int ca, ItemDefinition b, int cb)
        {
            return new CraftRecipe { result = result, resultCount = count, ingredientA = a, countA = ca, ingredientB = b, countB = cb };
        }

        private static void WorldModel(ItemDefinition item, GameObject prefab, float size)
        {
            if (item == null || prefab == null) return;
            item.worldModel = prefab;
            item.worldModelSize = size;
            EditorUtility.SetDirty(item);
        }

        private static ItemDefinition WeaponItem(string file, WeaponDefinition w, Vector2Int size, Color color, string desc)
        {
            var it = Item(file, "item_" + w.id, w.displayName, desc, ItemType.Weapon, size, 1, color, w.displayName);
            it.weapon = w;
            it.worldModel = w.viewModelPrefab;
            EditorUtility.SetDirty(it);
            return it;
        }

        // ------------------------------------------------------------ Efectos

        private static CombatFXLibrary BuildFX()
        {
            Texture2D soft = RE4EditorUtil.SaveTexture(TexFolder + "/FX_Soft.png", 64, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Clamp01(1f - d);
                return new Color(1f, 1f, 1f, a * a);
            });
            Texture2D flash = RE4EditorUtil.SaveTexture(TexFolder + "/FX_Flash.png", 128, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                float ang = Mathf.Atan2(y, x);
                float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3f)), 8f) * Mathf.Clamp01(1f - d) * 0.9f;
                float core = Mathf.Clamp01(1f - d * 2.2f);
                float a = Mathf.Clamp01(core + rays);
                return new Color(1f, Mathf.Lerp(0.6f, 1f, core), Mathf.Lerp(0.3f, 0.9f, core), a);
            });
            Texture2D splat = RE4EditorUtil.SaveTexture(TexFolder + "/FX_BloodSplat.png", 128, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                float ang = Mathf.Atan2(y, x);
                float edge = 0.55f + 0.18f * Mathf.Sin(ang * 5f) + 0.1f * Mathf.Sin(ang * 13f + 1.3f) + 0.08f * Mathf.PerlinNoise(x * 6f + 10f, y * 6f + 10f);
                float drops = Mathf.PerlinNoise(x * 9f + 3f, y * 9f + 7f) > 0.72f && d < 0.95f ? 1f : 0f;
                float a = Mathf.Max(Mathf.Clamp01((edge - d) * 8f), drops) * 0.92f;
                return new Color(0.32f + Mathf.PerlinNoise(x * 3f, y * 3f) * 0.1f, 0.01f, 0.01f, a);
            });

            var lib = RE4EditorUtil.CreateOrLoad<CombatFXLibrary>(DataFolder + "/CombatFXLibrary.asset");
            const string add = "Legacy Shaders/Particles/Additive";
            const string blend = "Legacy Shaders/Particles/Alpha Blended";
            lib.muzzleFlash = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_MuzzleFlash.mat", add, new Color(1f, 0.75f, 0.45f, 0.9f), flash);
            lib.tracer = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_Tracer.mat", add, new Color(1f, 0.85f, 0.5f, 0.6f), soft);
            lib.laser = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_Laser.mat", add, new Color(1f, 0.05f, 0.05f, 0.55f), soft);
            lib.laserDot = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_LaserDot.mat", add, new Color(1f, 0.1f, 0.1f, 1f), soft);
            lib.blood = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_Blood.mat", blend, new Color(0.5f, 0.03f, 0.03f, 1f), soft);
            lib.bloodDecal = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_BloodDecal.mat", blend, new Color(0.5f, 0.5f, 0.5f, 0.9f), splat);
            lib.dust = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_Dust.mat", blend, new Color(0.55f, 0.5f, 0.42f, 0.6f), soft);
            lib.spark = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_Spark.mat", add, new Color(1f, 0.75f, 0.35f, 1f), soft);
            lib.smoke = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_Smoke.mat", blend, new Color(0.35f, 0.34f, 0.32f, 0.5f), soft);
            lib.fire = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_Fire.mat", add, new Color(1f, 0.55f, 0.2f, 0.8f), soft);
            lib.itemGlint = RE4EditorUtil.SaveMaterial(MatFolder + "/FX_ItemGlint.mat", add, new Color(1f, 0.95f, 0.75f, 0.8f), flash);

            Material brass = RE4EditorUtil.SaveMaterial(MatFolder + "/Shell_Brass.mat", "Standard", new Color(0.78f, 0.58f, 0.22f));
            brass.SetFloat("_Metallic", 0.9f);
            brass.SetFloat("_Glossiness", 0.7f);
            lib.shellCasing = brass;

            Material steel = RE4EditorUtil.SaveMaterial(MatFolder + "/Knife_Steel.mat", "Standard", new Color(0.75f, 0.76f, 0.78f));
            steel.SetFloat("_Metallic", 1f);
            steel.SetFloat("_Glossiness", 0.85f);
            lib.knife = steel;

            EditorUtility.SetDirty(lib);
            return lib;
        }
    }
}
