using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gameplay.RE4;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Animaciones de Mixamo descargadas (paquete de zombis y paquetes de disparo para Leon).
    /// - Las importa como Humanoid, "en el sitio" (la raiz no se desplaza: manda el NavMeshAgent / el
    ///   CharacterController) y con la altura tomada de los pies (para que no floten).
    /// - Crea la biblioteca de variantes de los Ganados (Resources/RE4ZombieAnims) y añade al controlador
    ///   el estado "StandUp" (levantarse del suelo con animacion).
    /// - Crea el cuerpo de Leon que solo proyecta sombra (Resources/RE4Body/LeonCuerpo) con su controlador.
    /// Se ejecuta solo la primera vez y desde el menu RE4 > Integrar animaciones nuevas (Leon y zombis).
    /// </summary>
    [InitializeOnLoad]
    public class RE4MixamoImport : AssetPostprocessor
    {
        public const string ZombieFolder = "Assets/RE4/Importados/Anim_Zombis";
        public const string LeonFolder = "Assets/RE4/Importados/Anim_Leon";
        private const string LibraryPath = "Assets/RE4/Resources/RE4ZombieAnims.asset";
        private const string BodyFolder = "Assets/RE4/Resources/RE4Body";
        private const string BodyPrefab = BodyFolder + "/LeonCuerpo.prefab";
        private const string BodyController = "Assets/RE4/Animation/Leon_Cuerpo.controller";
        private const string BodyMask = "Assets/RE4/Animation/Leon_TrenSuperior.mask";

        static RE4MixamoImport()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!AssetDatabase.IsValidFolder(ZombieFolder) && !AssetDatabase.IsValidFolder(LeonFolder)) return;
                if (File.Exists(LibraryPath)) return; // ya integradas (para repetir: menu RE4)
                Integrate();
            };
        }

        // ============================================================ Importacion

        private static bool IsOurs(string path) => path.StartsWith(ZombieFolder + "/") || path.StartsWith(LeonFolder + "/");

        private static bool IsLoop(string clip)
        {
            string n = clip.ToLowerInvariant();
            if (n.Contains("turn") || n.Contains("transition") || n.Contains("jump")) return false;
            return n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("strafe") ||
                   n.Contains("crawl") || n.Contains("stumbling") || n.Contains("agonizing");
        }

        private static bool IsXBot(string path) => Path.GetFileNameWithoutExtension(path) == "X Bot";
        private static string FolderOf(string path) => Path.GetDirectoryName(path).Replace('\\', '/');

        /// <summary>Avatar del X Bot de la misma carpeta (personaje en pose T de Mixamo): referencia de todas las animaciones.</summary>
        private static Avatar XBotAvatar(string folder)
        {
            return AssetDatabase.LoadAllAssetsAtPath(folder + "/X Bot.fbx").OfType<Avatar>().FirstOrDefault(a => a.isHuman && a.isValid);
        }

        private static void SetupRig(ModelImporter imp, string path)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.importAnimation = true;
            Avatar reference = IsXBot(path) ? null : XBotAvatar(FolderOf(path));
            if (reference != null)
            {
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = reference;
            }
            else imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }

        private void OnPreprocessModel()
        {
            if (!IsOurs(assetPath)) return;
            var imp = (ModelImporter)assetImporter;
            SetupRig(imp, assetPath);
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsOurs(assetPath) || IsXBot(assetPath)) return;
            var imp = (ModelImporter)assetImporter;
            ConfigureClips(imp, assetPath);
        }

        private static bool ConfigureClips(ModelImporter imp, string path)
        {
            ModelImporterClipAnimation[] clips = imp.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return false;
            string baseName = Path.GetFileNameWithoutExtension(path);
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                c.name = i == 0 ? baseName : baseName + "_" + i;
                c.loopTime = IsLoop(baseName);
                // en el sitio: la posicion la controla el juego, no la animacion
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = false;
                c.heightFromFeet = true;     // altura tomada de los pies: no flotan ni se hunden
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
                clips[i] = c;
            }
            imp.clipAnimations = clips;
            return true;
        }

        /// <summary>Asegura la configuracion aunque los archivos se hubieran importado antes que este script.</summary>
        private static void EnsureImported(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            // primero el X Bot (su avatar es la referencia de los demas)
            var paths = AssetDatabase.FindAssets("t:Model", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => IsXBot(p) ? 0 : 1).ToList();
            foreach (string path in paths)
            {
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                Avatar reference = IsXBot(path) ? null : XBotAvatar(folder);
                bool rigOk = imp.animationType == ModelImporterAnimationType.Human &&
                             (reference == null || (imp.avatarSetup == ModelImporterAvatarSetup.CopyFromOther && imp.sourceAvatar == reference));
                bool clipsOk = IsXBot(path) || (imp.clipAnimations != null && imp.clipAnimations.Length > 0 &&
                                                imp.clipAnimations[0].name == Path.GetFileNameWithoutExtension(path));
                if (rigOk && clipsOk) continue;
                SetupRig(imp, path);
                imp.SaveAndReimport();
                imp = (ModelImporter)AssetImporter.GetAtPath(path);
                if (!IsXBot(path) && ConfigureClips(imp, path)) imp.SaveAndReimport();
            }
        }

        private static AnimationClip Clip(string folder, string file)
        {
            string path = $"{folder}/{file}.fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        private static AnimationClip[] Clips(string folder, params string[] files)
        {
            var list = new List<AnimationClip>();
            foreach (var f in files)
            {
                AnimationClip c = Clip(folder, f);
                if (c != null) list.Add(c);
            }
            return list.ToArray();
        }

        // ============================================================ Menu

        [MenuItem("RE4/Integrar animaciones nuevas (Leon y zombis)", priority = 12)]
        public static void Integrate()
        {
            EnsureImported(ZombieFolder);
            EnsureImported(LeonFolder);
            AssetDatabase.Refresh();
            RE4EditorUtil.EnsureFolder("Assets/RE4/Resources");
            RE4EditorUtil.EnsureFolder("Assets/RE4/Animation");

            int variants = BuildZombieLibrary();
            bool standUp = PatchGanadoController(AssetDatabase.LoadAssetAtPath<AnimatorController>(RE4ZombieRig.ControllerPath));
            bool body = BuildLeonBody();
            AssetDatabase.SaveAssets();
            Debug.Log($"[RE4] Animaciones integradas: {variants} variantes de zombi, levantarse {(standUp ? "sí" : "no")}, sombra de Leon {(body ? "sí" : "no")}.");
        }

        // ============================================================ Zombis

        private static int BuildZombieLibrary()
        {
            if (!AssetDatabase.IsValidFolder(ZombieFolder)) return 0;
            var lib = AssetDatabase.LoadAssetAtPath<ZombieAnimLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ZombieAnimLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            string z = ZombieFolder;
            lib.idles = Clips(z, "zombie idle", "zombie idle (2)", "zombie idle (3)", "zombie idle (4)", "zombie scratch idle");
            lib.walks = Clips(z, "walking", "walking (2)", "zombie stumbling");
            lib.runs = Clips(z, "zombie running");
            lib.attacks = Clips(z, "zombie punching", "zombie punching (2)", "zombie headbutt", "zombie kicking", "zombie kicking (2)", "zombie attack");
            lib.hits = Clips(z, "zombie reaction hit", "zombie reaction hit (2)");
            lib.screams = Clips(z, "Zombie Scream");
            lib.standUps = Clips(z, "zombie stand up", "zombie stand up (2)", "zombie stand up (3)");
            EditorUtility.SetDirty(lib);
            return lib.idles.Length + lib.walks.Length + lib.runs.Length + lib.attacks.Length + lib.hits.Length + lib.screams.Length + lib.standUps.Length;
        }

        /// <summary>
        /// Añade el estado "StandUp" (levantarse) entre "Down" y la locomocion, sin recrear el controlador
        /// (recrearlo cambiaria su identificador y los prefabs se quedarian sin animaciones).
        /// </summary>
        public static bool PatchGanadoController(AnimatorController ac)
        {
            if (ac == null) return false;
            AnimationClip standUp = Clip(ZombieFolder, ZombieAnimLibrary.StandUpBaseClip);
            if (standUp == null) return false;
            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            AnimatorState down = null, loco = null, existing = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == "Down") down = cs.state;
                else if (cs.state.name == "Locomotion") loco = cs.state;
                else if (cs.state.name == "StandUp") existing = cs.state;
            }
            if (down == null || loco == null) return false;
            if (existing != null) return true;

            if (!ac.parameters.Any(p => p.name == "StandUpSpeed"))
                ac.AddParameter(new AnimatorControllerParameter { name = "StandUpSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            AnimatorState st = sm.AddState("StandUp");
            st.motion = standUp;
            st.speedParameterActive = true;
            st.speedParameter = "StandUpSpeed";

            foreach (var t in down.transitions)
            {
                if (t.destinationState != loco) continue;
                t.destinationState = st;
                t.duration = 0.2f;
            }
            var back = st.AddTransition(loco);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.3f;
            EditorUtility.SetDirty(ac);
            return true;
        }

        // ============================================================ Leon

        private static bool BuildLeonBody()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(LeonFolder + "/X Bot.fbx");
            if (source == null) return false;
            string l = LeonFolder;
            AnimationClip idle = Clip(l, "rifle aiming idle");
            AnimationClip walk = Clip(l, "walking");
            if (idle == null || walk == null) return false;

            // Controlador: locomocion 2D + capa del tren superior para recargar, disparar y recibir golpes
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(BodyController) != null) AssetDatabase.DeleteAsset(BodyController);
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(BodyController);
            ac.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            ac.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

            AnimatorState locoState = ac.CreateBlendTreeInController("Locomocion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformCartesian2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveY";
            void Add(string file, float x, float y)
            {
                AnimationClip c = Clip(l, file);
                if (c != null) tree.AddChild(c, new Vector2(x, y));
            }
            tree.AddChild(idle, Vector2.zero);
            tree.AddChild(walk, new Vector2(0f, 1f));
            Add("rifle run", 0f, 2.2f);
            Add("walking backwards", 0f, -1f);
            Add("run backwards", 0f, -2f);
            Add("strafe left", -1f, 0f);
            Add("strafe right", 1f, 0f);
            ac.layers[0].stateMachine.defaultState = locoState;

            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(BodyMask);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, BodyMask);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                             part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                             part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers ||
                             part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            EditorUtility.SetDirty(mask);

            ac.AddLayer("Acciones");
            AnimatorControllerLayer[] layers = ac.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 1f;
            ac.layers = layers;
            AnimatorStateMachine upperSm = ac.layers[1].stateMachine;
            AnimatorState empty = upperSm.AddState("Vacio");
            upperSm.defaultState = empty;
            void Action(string stateName, string file, string trigger, float speed)
            {
                AnimationClip c = Clip(l, file);
                if (c == null) return;
                AnimatorState s = upperSm.AddState(stateName);
                s.motion = c;
                s.speed = speed;
                var t = upperSm.AddAnyStateTransition(s);
                t.AddCondition(AnimatorConditionMode.If, 0, trigger);
                t.hasExitTime = false;
                t.duration = 0.1f;
                t.canTransitionToSelf = true;
                var back = s.AddTransition(empty);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.2f;
            }
            Action("Recargar", "reloading", "Reload", 1.4f);
            Action("Disparar", "firing rifle", "Fire", 2f);
            Action("Golpe", "hit reaction", "Hit", 1.2f);
            EditorUtility.SetDirty(ac);

            // Prefab del cuerpo: X Bot de Mixamo a 1.8 m, pies en el suelo
            RE4EditorUtil.EnsureFolder(BodyFolder);
            GameObject root = new GameObject("LeonCuerpo");
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Modelo";
            model.transform.SetParent(root.transform, false);
            Animator anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ac;
            anim.applyRootMotion = false;
            Bounds b = RE4EditorUtil.WorldBounds(model);
            if (b.size.y > 0.01f) model.transform.localScale *= 1.8f / b.size.y;
            b = RE4EditorUtil.WorldBounds(model);
            model.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            PrefabUtility.SaveAsPrefabAsset(root, BodyPrefab);
            Object.DestroyImmediate(root);
            return true;
        }
    }
}
