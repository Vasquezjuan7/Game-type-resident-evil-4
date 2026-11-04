using UnityEditor;
using UnityEngine;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Prefabs de los objetos recogibles con los modelos importados en Assets/RE4/Importados:
    /// spray de primeros auxilios, hierbas (verde, roja y mezclas) y cajas de municion (una por tipo, tenidas).
    /// </summary>
    public static class RE4PropBuilder
    {
        public const string Folder = "Assets/RE4/Prefabs/Objetos";
        private const string Src = "Assets/RE4/Importados/";

        public class Props
        {
            public GameObject spray;
            public GameObject herbGreen, herbRed, herbGG, herbGR;
            public GameObject ammoHandgun, ammoShotgun, ammoSmg, ammoRifle, ammoMagnum, ammoSniper;
        }

        public static Props Build()
        {
            RE4EditorUtil.EnsureFolder(Folder);
            var p = new Props();
            p.spray = Safe(BuildSpray);

            p.herbGreen = Safe(() => BuildHerb("Hierba_Verde", Color.white));
            p.herbRed = Safe(() => BuildHerb("Hierba_Roja", new Color(1f, 0.35f, 0.3f)));
            p.herbGG = Safe(() => BuildHerb("Hierba_Mezcla_VV", new Color(0.75f, 1f, 0.6f)));
            p.herbGR = Safe(() => BuildHerb("Hierba_Mezcla_VR", new Color(1f, 0.75f, 0.45f)));

            p.ammoHandgun = Safe(() => BuildAmmo("Municion_Pistola", Color.white));
            p.ammoShotgun = Safe(() => BuildAmmo("Municion_Escopeta", new Color(1f, 0.55f, 0.5f)));
            p.ammoSmg = Safe(() => BuildAmmo("Municion_Subfusil", new Color(0.6f, 0.75f, 1f)));
            p.ammoRifle = Safe(() => BuildAmmo("Municion_Rifle", new Color(0.7f, 0.9f, 0.6f)));
            p.ammoMagnum = Safe(() => BuildAmmo("Municion_Magnum", new Color(1f, 0.85f, 0.45f)));
            p.ammoSniper = Safe(() => BuildAmmo("Municion_Francotirador", new Color(0.8f, 0.7f, 0.55f)));
            AssetDatabase.SaveAssets();
            return p;
        }

        private static GameObject Safe(System.Func<GameObject> f)
        {
            try { return f(); }
            catch (System.Exception e) { Debug.LogError("[RE4] Error creando objeto: " + e); return null; }
        }

        // ------------------------------------------------------------ Modelos

        private static GameObject BuildSpray()
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(Src + "Spray/Spray.dae");
            if (src == null) { Debug.LogWarning("[RE4] Falta el modelo del spray."); return null; }
            string t = Src + "Spray/";
            Material Mat(string n) => RE4WeaponBuilder.PbrMaterial("Spray_" + n, $"{t}{n}_albedo.jpg", $"{t}{n}_normal.png", $"{t}{n}_metallic.jpg", $"{t}{n}_roughness.jpg", $"{t}{n}_AO.jpg");
            Material blackPlastic = Mat("Black_plastic"), metal = Mat("Metall"), whiteMetal = Mat("White_metalll"), whitePlastic = Mat("White_plastic");
            return Save("Spray", src, n =>
                n.Contains("black") ? blackPlastic :
                n.Contains("white_metal") ? whiteMetal :
                n.Contains("white") ? whitePlastic : metal);
        }

        private static GameObject BuildHerb(string name, Color tint)
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(Src + "HierbaVerde/SM_GreenHerb.fbx");
            if (src == null) { Debug.LogWarning("[RE4] Falta el modelo de la hierba."); return null; }
            string t = Src + "HierbaVerde/T_GreenHerb_";
            Material m = RE4WeaponBuilder.PbrMaterial(name, t + "BaseColor.png", null, t + "Metallic.png", t + "Roughness.png", t + "Occlusion.png");
            m.color = tint;
            EditorUtility.SetDirty(m);
            return Save(name, src, _ => m);
        }

        private static GameObject BuildAmmo(string name, Color tint)
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(Src + "CajaMunicion/10mmAmmoBox_done_01.fbx");
            if (src == null) { Debug.LogWarning("[RE4] Falta el modelo de la caja de municion."); return null; }
            string t = Src + "CajaMunicion/AmmoBox_";
            // el mapa de normales viene en formato DirectX: se invierte el canal verde
            if (AssetImporter.GetAtPath(t + "Normal_DirectX_01.png") is TextureImporter ti && !ti.flipGreenChannel)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.flipGreenChannel = true;
                ti.SaveAndReimport();
            }
            Material m = RE4WeaponBuilder.PbrMaterial(name, t + "Base_color_01.png", t + "Normal_DirectX_01.png", t + "Metallic_01.png", t + "Roughness_01.png", t + "AO.png");
            m.color = tint;
            EditorUtility.SetDirty(m);
            return Save(name, src, _ => m);
        }

        /// <summary>Envuelve el modelo, le pone los materiales y deja el pivote abajo en el centro.</summary>
        private static GameObject Save(string name, GameObject src, System.Func<string, Material> pick)
        {
            GameObject root = new GameObject(name);
            GameObject inst = Object.Instantiate(src, root.transform);
            inst.name = "Modelo";
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in inst.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = pick(mats[i] != null ? mats[i].name.ToLowerInvariant() : r.name.ToLowerInvariant());
                r.sharedMaterials = mats;
            }
            Bounds b = RE4EditorUtil.WorldBounds(inst);
            inst.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            string path = $"{Folder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
