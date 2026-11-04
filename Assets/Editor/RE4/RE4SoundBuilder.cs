using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Gameplay.RE4;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Asigna los audios del proyecto: FreeWeaponSFX a cada arma (disparo, cola, equipar, apuntar, recarga)
    /// y Assets/Audio (pasos por superficie, respiracion, viento, bosque, crujidos, drones) al SoundBank.
    /// Las armas sin audio grabado (revolver, subfusil...) reutilizan otro con distinto tono.
    /// </summary>
    public static class RE4SoundBuilder
    {
        private const string Sfx = "Assets/FreeWeaponSFX_v1.1/";
        private const string Audio = "Assets/Audio/";
        public const string BankPath = RE4DataBuilder.DataFolder + "/SoundBank.asset";

        public static SoundBank Build(RE4DataBuilder.Database db)
        {
            var bank = RE4EditorUtil.CreateOrLoad<SoundBank>(BankPath);

            // ---------------- Armas
            Handgun(db.pistol, 1f);
            Handgun(db.pistol2, 0.9f);
            Handgun(db.magnum, 0.7f);
            if (db.magnum != null)
            {
                db.magnum.rackBackClip = Clip(Sfx + "Handgun/Reload/sfx_wpn_hgn_reload_01_slideLock.wav");
                db.magnum.magOutClip = null;
                db.magnum.magInClip = Clip(Sfx + "Shotgun/Reload/sfx_wpn_sgn_reload_04_chambering.wav");
                db.magnum.rackClip = Clip(Sfx + "Handgun/Reload/sfx_wpn_hgn_reload_05_charge.wav");
            }
            Rifle(db.rifle, 1f);
            Rifle(db.smg, 1.22f);
            Shotgun(db.shotgun);
            // Francotirador: disparo de rifle mas grave, cerrojo con los sonidos metalicos de la escopeta
            Rifle(db.sniper, 0.78f);
            if (db.sniper != null)
            {
                db.sniper.rackBackClip = Clip(Sfx + "Shotgun/Reload/sfx_wpn_sgn_reload_01_charge.wav");
                db.sniper.rackClip = Clip(Sfx + "AssaultRifle/Reload/sfx_wpn_ar_reload_04_charge.wav");
                EditorUtility.SetDirty(db.sniper);
            }
            if (db.rocket != null)
            {
                db.rocket.shotClips = Clips(Sfx + "GrenadeLauncher", "fire");
                db.rocket.tailClips = Clips(Sfx + "AssaultRifle/Gunshots", "tail");
                db.rocket.rackBackClip = Clip(Sfx + "GrenadeLauncher/sfx_wpn_gl_reload_01_breechOpen.wav");
                db.rocket.magInClip = Clip(Sfx + "GrenadeLauncher/sfx_wpn_gl_reload_03_projectileInsert.wav");
                db.rocket.rackClip = Clip(Sfx + "GrenadeLauncher/sfx_wpn_gl_reload_04_breechClose.wav");
                db.rocket.equipClip = Clip(Sfx + "AssaultRifle/Foley/sfx_wpn_ar_foley_equip_02.wav");
                db.rocket.clipPitch = 0.85f;
                EditorUtility.SetDirty(db.rocket);
            }
            bank.explosion = Clip(Sfx + "GrenadeLauncher/sfx_wpn_gl_explosion.wav");

            // ---------------- Pasos y respiracion
            bank.steps = new[]
            {
                Steps("dirt", "PLAYER_steps_dirt/PLAYER_steps_dirt_soft", "PLAYER_steps_dirt/PLAYER_steps_dirt_hard"),
                Steps("stone", "PLAYER_steps_stone/PLAYER_steps_stone_slow", "PLAYER_steps_stone/PLAYER_steps_stone_fast"),
                Steps("gravel", "PLAYER_steps_gravel/PLAYER_steps_gravel_slow", "PLAYER_steps_gravel/PLAYER_steps_gravel_fast"),
                Steps("grass", "PLAYER_steps_grass/PLAYER_steps_grass_slow", "PLAYER_steps_grass/PLAYER_steps_grass_fast"),
                Steps("wood", "PLAYER_steps_wood_coat/PLAYER_steps_wood_coat_slow", "PLAYER_steps_wood_coat/PLAYER_steps_wood_coat_fast"),
            };
            bank.breathing = Clips(Audio + "CHAR/PLAYER_breathing/PLAYER_breathing_fast", null);
            bank.enemySteps = Clips(Audio + "CHAR/PLAYER_steps_dirt/PLAYER_steps_dirt_hard", null);
            bank.dogSteps = Clips(Audio + "CHAR/PLAYER_steps_dry_forest/PLAYER_steps_dry_forest_soft", null);

            // ---------------- Ambiente
            bank.windLoops = Clips(Audio + "AMB/WIND", null);
            var forest = new List<AudioClip>();
            forest.AddRange(Clips(Audio + "AMB/WOODS_FOREST/WOODS_FOREST_ST_wind_loop", null));
            forest.AddRange(Clips(Audio + "AMB/WOODS_FOREST/WOODS_FOREST_ST_light_breeze_A", null));
            bank.forestLoops = forest.ToArray();
            var shots = new List<AudioClip>();
            foreach (var f in new[] { "AMB/WOODS_FOREST/WOODS_FOREST_M_cracks", "AMB/WOODS_FOREST/WOODS_FOREST_M_squeaks", "FX/FX_woodscreak", "FX/FX_stuff_falling_woods", "FX/WOODS_HIVE_M_hits", "FX/FX_stones_falling" })
                shots.AddRange(Clips(Audio + f, null));
            bank.randomOneShots = shots.ToArray();
            var drones = new List<AudioClip>();
            drones.AddRange(Clips(Audio + "FX/Forest_LOWDRONE", null));
            drones.AddRange(Clips(Audio + "FX/Forest_PIPEDRONE", null));
            bank.tensionDrones = drones.ToArray();
            var music = Clips(Audio + "MUSIC", "MUSICAL");
            bank.music = music.Length > 0 ? music[0] : null;

            EditorUtility.SetDirty(bank);
            if (db.fx != null)
            {
                db.fx.sounds = bank;
                EditorUtility.SetDirty(db.fx);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[RE4] Sonidos: {bank.steps.Length} superficies, {bank.randomOneShots.Length} efectos de ambiente, {bank.windLoops.Length} vientos.");
            return bank;
        }

        private static void Handgun(WeaponDefinition w, float pitch)
        {
            if (w == null) return;
            string d = Sfx + "Handgun/";
            w.shotClips = Clips(d + "Gunshots", "gunshot");
            w.tailClips = Clips(d + "Gunshots", "tail");
            w.equipClip = Clip(d + "Foley/sfx_wpn_hgn_foley_equip_01.wav");
            w.aimClip = Clip(d + "Foley/sfx_wpn_hgn_foley_aim_01.wav");
            w.magOutClip = Clip(d + "Reload/sfx_wpn_hgn_reload_02_magOut.wav");
            w.magDrawClip = Clip(d + "Reload/sfx_wpn_hgn_reload_03_magDraw.wav");
            w.magInClip = Clip(d + "Reload/sfx_wpn_hgn_reload_04_magIn.wav");
            w.rackClip = Clip(d + "Reload/sfx_wpn_hgn_reload_05_charge.wav");
            w.rackBackClip = Clip(d + "Reload/sfx_wpn_hgn_reload_01_slideLock.wav");
            w.clipPitch = pitch;
            EditorUtility.SetDirty(w);
        }

        private static void Rifle(WeaponDefinition w, float pitch)
        {
            if (w == null) return;
            string d = Sfx + "AssaultRifle/";
            w.shotClips = Clips(d + "Gunshots", "gunshot");
            w.tailClips = Clips(d + "Gunshots", "tail");
            w.equipClip = Clip(d + "Foley/sfx_wpn_ar_foley_equip_01.wav");
            w.aimClip = Clip(d + "Foley/sfx_wpn_ar_foley_aim_01.wav");
            w.magOutClip = Clip(d + "Reload/sfx_wpn_ar_reload_01_magOut.wav");
            w.magDrawClip = Clip(d + "Reload/sfx_wpn_ar_reload_02_magDraw.wav");
            w.magInClip = Clip(d + "Reload/sfx_wpn_ar_reload_03_magIn.wav");
            w.rackClip = Clip(d + "Reload/sfx_wpn_ar_reload_04_charge.wav");
            w.rackBackClip = null;
            w.clipPitch = pitch;
            EditorUtility.SetDirty(w);
        }

        private static void Shotgun(WeaponDefinition w)
        {
            if (w == null) return;
            string d = Sfx + "Shotgun/";
            w.shotClips = Clips(d + "Gunshots", "gunshot");
            w.tailClips = Clips(d + "Gunshots", "tail");
            w.equipClip = Clip(d + "Foley/sfx_wpn_sgn_foley_equip_01.wav");
            w.aimClip = Clip(d + "Foley/sfx_wpn_sgn_foley_aim_01.wav");
            w.magOutClip = null;
            w.magDrawClip = Clip(d + "Reload/sfx_wpn_sgn_reload_03_shellDraw.wav");
            w.magInClip = Clip(d + "Reload/sfx_wpn_sgn_reload_04_chambering.wav");
            w.rackBackClip = Clip(d + "Reload/sfx_wpn_sgn_reload_02_pumpBack.wav");
            w.rackClip = Clip(d + "Reload/sfx_wpn_sgn_reload_05_pumpForward.wav");
            w.clipPitch = 1f;
            EditorUtility.SetDirty(w);
        }

        private static SurfaceSteps Steps(string surface, string walk, string run)
        {
            return new SurfaceSteps { surface = surface, walk = Clips(Audio + "CHAR/" + walk, null), run = Clips(Audio + "CHAR/" + run, null) };
        }

        private static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        /// <summary>Todos los AudioClip de una carpeta (y subcarpetas) cuyo nombre contenga el filtro.</summary>
        private static AudioClip[] Clips(string folder, string contains)
        {
            var list = new List<AudioClip>();
            if (!AssetDatabase.IsValidFolder(folder)) return list.ToArray();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (contains != null && !Path.GetFileName(path).ToLowerInvariant().Contains(contains.ToLowerInvariant())) continue;
                AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (c != null) list.Add(c);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }
    }
}
