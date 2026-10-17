using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Convierte el rig del zombi (generico, huesos tipo Mixamo "Zombie_*") a Humanoid para que
    /// las mismas animaciones funcionen en todos los modelos de zombi, y crea el Animator Controller del Ganado.
    /// Las animaciones se importan "en el sitio" (root horneado) para que el NavMeshAgent controle la posicion:
    /// asi ningun zombi se hunde ni atraviesa el suelo.
    /// </summary>
    public static class RE4ZombieRig
    {
        public const string BaseMeshPath = "Assets/Zombie/Base mesh/Base mesh fbx.fbx";
        public const string AnimFolder = "Assets/Zombie/Animation/";
        public const string ControllerPath = "Assets/RE4/Animation/Ganado_Humanoid.controller";
        private const string Prefix = "Zombie_";

        private static readonly string[] LoopClips = { "idle1", "idle2", "walk", "run" };
        private static readonly string[] AllClips = { "idle1", "idle2", "walk", "run", "roar", "atack1", "atack2", "atack3", "atack4", "gethit", "death1", "death2" };

        private static readonly Dictionary<string, string> Mapping = new Dictionary<string, string>
        {
            { "Hips", "Hips" }, { "Spine", "Spine" }, { "Spine1", "Chest" }, { "Spine2", "UpperChest" },
            { "Neck", "Neck" }, { "Head", "Head" },
            { "LeftShoulder", "LeftShoulder" }, { "LeftArm", "LeftUpperArm" }, { "LeftForeArm", "LeftLowerArm" }, { "LeftHand", "LeftHand" },
            { "RightShoulder", "RightShoulder" }, { "RightArm", "RightUpperArm" }, { "RightForeArm", "RightLowerArm" }, { "RightHand", "RightHand" },
            { "LeftUpLeg", "LeftUpperLeg" }, { "LeftLeg", "LeftLowerLeg" }, { "LeftFoot", "LeftFoot" }, { "LeftToeBase", "LeftToes" },
            { "RightUpLeg", "RightUpperLeg" }, { "RightLeg", "RightLowerLeg" }, { "RightFoot", "RightFoot" }, { "RightToeBase", "RightToes" },
            { "LeftHandThumb1", "Left Thumb Proximal" }, { "LeftHandThumb2", "Left Thumb Intermediate" }, { "LeftHandThumb3", "Left Thumb Distal" },
            { "LeftHandIndex1", "Left Index Proximal" }, { "LeftHandIndex2", "Left Index Intermediate" }, { "LeftHandIndex3", "Left Index Distal" },
            { "LeftHandMiddle1", "Left Middle Proximal" }, { "LeftHandMiddle2", "Left Middle Intermediate" }, { "LeftHandMiddle3", "Left Middle Distal" },
            { "LeftHandRing1", "Left Ring Proximal" }, { "LeftHandRing2", "Left Ring Intermediate" }, { "LeftHandRing3", "Left Ring Distal" },
            { "LeftHandPinky1", "Left Little Proximal" }, { "LeftHandPinky2", "Left Little Intermediate" }, { "LeftHandPinky3", "Left Little Distal" },
            { "RightHandThumb1", "Right Thumb Proximal" }, { "RightHandThumb2", "Right Thumb Intermediate" }, { "RightHandThumb3", "Right Thumb Distal" },
            { "RightHandIndex1", "Right Index Proximal" }, { "RightHandIndex2", "Right Index Intermediate" }, { "RightHandIndex3", "Right Index Distal" },
            { "RightHandMiddle1", "Right Middle Proximal" }, { "RightHandMiddle2", "Right Middle Intermediate" }, { "RightHandMiddle3", "Right Middle Distal" },
            { "RightHandRing1", "Right Ring Proximal" }, { "RightHandRing2", "Right Ring Intermediate" }, { "RightHandRing3", "Right Ring Distal" },
            { "RightHandPinky1", "Right Little Proximal" }, { "RightHandPinky2", "Right Little Intermediate" }, { "RightHandPinky3", "Right Little Distal" },
        };

        public static Avatar LoadAvatar(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        }

        /// <summary>Convierte el modelo base a Humanoid. Devuelve el Avatar o null si falla.</summary>
        public static Avatar ConvertBaseMesh()
        {
            ModelImporter imp = AssetImporter.GetAtPath(BaseMeshPath) as ModelImporter;
            if (imp == null)
            {
                Debug.LogError("[RE4] No se encontro el modelo base del zombi: " + BaseMeshPath);
                return null;
            }

            Avatar existing = LoadAvatar(BaseMeshPath);
            if (imp.animationType == ModelImporterAnimationType.Human && existing != null && existing.isHuman && existing.isValid)
            {
                return existing;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BaseMeshPath);
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var names = new HashSet<string>(transforms.Select(t => t.name));

            var human = new List<HumanBone>();
            foreach (var kv in Mapping)
            {
                string bone = Prefix + kv.Key;
                if (!names.Contains(bone)) continue;
                var hb = new HumanBone { boneName = bone, humanName = kv.Value };
                hb.limit.useDefaultValues = true;
                human.Add(hb);
            }

            var skeleton = new List<SkeletonBone>();
            foreach (var t in transforms)
            {
                skeleton.Add(new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                });
            }

            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            HumanDescription hd = imp.humanDescription;
            hd.human = human.ToArray();
            hd.skeleton = skeleton.ToArray();
            hd.upperArmTwist = 0.5f;
            hd.lowerArmTwist = 0.5f;
            hd.upperLegTwist = 0.5f;
            hd.lowerLegTwist = 0.5f;
            hd.armStretch = 0.05f;
            hd.legStretch = 0.05f;
            hd.feetSpacing = 0f;
            hd.hasTranslationDoF = false;
            imp.humanDescription = hd;
            imp.SaveAndReimport();

            Avatar avatar = LoadAvatar(BaseMeshPath);
            if (avatar == null || !avatar.isHuman || !avatar.isValid)
            {
                Debug.LogError("[RE4] No se pudo crear el Avatar humanoide del zombi. Revisa 'Configure Avatar' en " + BaseMeshPath);
                return null;
            }
            Debug.Log($"[RE4] Avatar humanoide del zombi creado ({human.Count} huesos mapeados).");
            return avatar;
        }

        /// <summary>Reimporta las animaciones del zombi como Humanoid, en el sitio y sin desplazamiento vertical.</summary>
        public static void ConvertAnimations(Avatar avatar)
        {
            foreach (string clipName in AllClips)
            {
                string path = AnimFolder + "zombie@" + clipName + ".fbx";
                ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;

                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = avatar;
                imp.importAnimation = true;

                ModelImporterClipAnimation[] clips = imp.defaultClipAnimations;
                if (clips == null || clips.Length == 0) clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    imp.SaveAndReimport();
                    clips = imp.defaultClipAnimations;
                }
                if (clips == null || clips.Length == 0) continue;

                bool loop = LoopClips.Contains(clipName);
                for (int i = 0; i < clips.Length; i++)
                {
                    var c = clips[i];
                    c.name = i == 0 ? clipName : clipName + "_" + i;
                    c.loopTime = loop;
                    c.loopPose = loop;
                    c.lockRootRotation = true;
                    c.keepOriginalOrientation = true;
                    c.lockRootHeightY = true;
                    c.keepOriginalPositionY = true;
                    c.heightFromFeet = false;
                    c.lockRootPositionXZ = true;
                    c.keepOriginalPositionXZ = true;
                    clips[i] = c;
                }
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
            }
        }

        public static AnimationClip LoadClip(string clipName)
        {
            string path = AnimFolder + "zombie@" + clipName + ".fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        /// <summary>Crea (o recrea) el Animator Controller del Ganado.</summary>
        public static AnimatorController BuildController()
        {
            RE4EditorUtil.EnsureFolder(Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            ac.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Stagger", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Down", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Grab", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Roar", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            ac.AddParameter("DeathIndex", AnimatorControllerParameterType.Int);

            AnimatorStateMachine sm = ac.layers[0].stateMachine;

            // Locomocion (blend tree 1D: quieto / caminar / correr)
            AnimatorState loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(LoadClip("idle1"), 0f);
            tree.AddChild(LoadClip("walk"), 1f);
            tree.AddChild(LoadClip("run"), 2f);
            sm.defaultState = loco;

            AnimatorState State(string name, string clip, float speed = 1f)
            {
                AnimatorState s = sm.AddState(name);
                s.motion = LoadClip(clip);
                s.speed = speed;
                return s;
            }

            AnimatorState death1 = State("Death1", "death1");
            AnimatorState death2 = State("Death2", "death2");
            AnimatorState down = State("Down", "death1", 1.15f);
            AnimatorState grab = State("Grab", "atack1", 0.35f);
            AnimatorState stagger = State("Stagger", "gethit", 0.45f);
            AnimatorState hit = State("Hit", "gethit", 1.2f);
            AnimatorState roar = State("Roar", "roar");
            AnimatorState[] attacks =
            {
                State("Attack1", "atack1"), State("Attack2", "atack2"), State("Attack3", "atack3"), State("Attack4", "atack4")
            };

            AnimatorStateTransition Any(AnimatorState to, float duration)
            {
                var t = sm.AddAnyStateTransition(to);
                t.hasExitTime = false;
                t.duration = duration;
                t.canTransitionToSelf = false;
                return t;
            }

            // El orden define la prioridad
            var td1 = Any(death1, 0.12f);
            td1.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            td1.AddCondition(AnimatorConditionMode.Equals, 0, "DeathIndex");
            var td2 = Any(death2, 0.12f);
            td2.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            td2.AddCondition(AnimatorConditionMode.NotEqual, 0, "DeathIndex");

            var tDown = Any(down, 0.1f);
            tDown.AddCondition(AnimatorConditionMode.If, 0, "Down");
            tDown.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");

            var tGrab = Any(grab, 0.15f);
            tGrab.AddCondition(AnimatorConditionMode.If, 0, "Grab");
            tGrab.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            tGrab.AddCondition(AnimatorConditionMode.IfNot, 0, "Down");

            var tStag = Any(stagger, 0.1f);
            tStag.AddCondition(AnimatorConditionMode.If, 0, "Stagger");
            tStag.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            tStag.AddCondition(AnimatorConditionMode.IfNot, 0, "Down");
            tStag.AddCondition(AnimatorConditionMode.IfNot, 0, "Grab");

            var tHit = Any(hit, 0.08f);
            tHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            tHit.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            tHit.AddCondition(AnimatorConditionMode.IfNot, 0, "Down");
            tHit.AddCondition(AnimatorConditionMode.IfNot, 0, "Grab");
            tHit.AddCondition(AnimatorConditionMode.IfNot, 0, "Stagger");

            var tRoar = Any(roar, 0.2f);
            tRoar.AddCondition(AnimatorConditionMode.If, 0, "Roar");
            tRoar.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");

            for (int i = 0; i < attacks.Length; i++)
            {
                var ta = Any(attacks[i], 0.12f);
                ta.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                ta.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");
                ta.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                ta.AddCondition(AnimatorConditionMode.IfNot, 0, "Grab");
                ta.AddCondition(AnimatorConditionMode.IfNot, 0, "Stagger");

                var back = attacks[i].AddTransition(loco);
                back.hasExitTime = true;
                back.exitTime = 0.92f;
                back.duration = 0.2f;
            }

            // Salidas
            var hBack = hit.AddTransition(loco);
            hBack.hasExitTime = true; hBack.exitTime = 0.9f; hBack.duration = 0.15f;

            var rBack = roar.AddTransition(loco);
            rBack.hasExitTime = true; rBack.exitTime = 0.9f; rBack.duration = 0.25f;

            var sBack = stagger.AddTransition(loco);
            sBack.hasExitTime = false; sBack.duration = 0.3f;
            sBack.AddCondition(AnimatorConditionMode.IfNot, 0, "Stagger");

            var gBack = grab.AddTransition(loco);
            gBack.hasExitTime = false; gBack.duration = 0.25f;
            gBack.AddCondition(AnimatorConditionMode.IfNot, 0, "Grab");

            // Levantarse: mezcla larga desde la pose tumbada
            var dBack = down.AddTransition(loco);
            dBack.hasExitTime = false; dBack.duration = 0.9f;
            dBack.AddCondition(AnimatorConditionMode.IfNot, 0, "Down");
            dBack.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");

            // levantarse con animacion de Mixamo (si el paquete de zombis esta importado)
            RE4MixamoImport.PatchGanadoController(ac);

            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            return ac;
        }
    }
}
