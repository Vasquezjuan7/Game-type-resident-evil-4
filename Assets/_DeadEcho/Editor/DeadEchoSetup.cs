using System.Collections.Generic;
using System.IO;
using DeadEcho.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace DeadEcho.EditorTools
{
    /// <summary>
    /// Fase F0 (base visual compartida, 02_PROMPTS_VISUALES.md V0): genera sprites, fuentes TMP con tildes,
    /// UITheme, UIAudio, prefabs de los componentes y la escena de prueba. La base se crea sola la primera vez
    /// que Unity compila este script; la escena de prueba se crea desde el menu "Dead Echo".
    /// </summary>
    [InitializeOnLoad]
    public static class DeadEchoSetup
    {
        private const string Root = "Assets/_DeadEcho";
        private const string SpriteFolder = Root + "/Sprites";
        private const string FontFolder = Root + "/Fonts";
        private const string SdfFolder = Root + "/Fonts/SDF";
        private const string ResFolder = Root + "/Resources/DeadEcho";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string SceneFolder = Root + "/Scenes";
        public const string ThemePath = ResFolder + "/UITheme.asset";
        private const string AudioPath = ResFolder + "/UIAudio.asset";
        private const string TestScene = SceneFolder + "/UI_Pruebas.unity";

        static DeadEchoSetup()
        {
            // crear la base automaticamente si todavia no existe (primera compilacion)
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath) == null) BuildBase();
                // F1: la escena del menu principal se crea sola la primera vez (sin tocar la escena abierta)
                if (!File.Exists(MainMenuScene)) BuildMainMenuScene();
                // F2: modelos de las tomas cinematicas
                if (AssetDatabase.LoadAssetAtPath<MenuShotsConfig>(MenuShotsPath) == null) BuildMenuShots();
                ApplyStartInMenu();
            };
        }

        // =========================================================== Arrancar en el menu principal

        private const string StartInMenuPref = "DeadEcho.EmpezarEnMenu";

        /// <summary>
        /// Al pulsar Play (con cualquier escena abierta) el juego empieza en el menu principal, como en la build.
        /// Se puede desactivar desde el menu Dead Echo para probar una escena suelta.
        /// </summary>
        private static void ApplyStartInMenu()
        {
            bool on = EditorPrefs.GetBool(StartInMenuPref, true);
            SceneAsset menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScene);
            EditorSceneManager.playModeStartScene = on ? menu : null;
            Menu.SetChecked(StartInMenuMenu, on);
        }

        private const string StartInMenuMenu = "Dead Echo/Play empieza en el menú principal";

        [MenuItem(StartInMenuMenu, priority = 2)]
        private static void ToggleStartInMenu()
        {
            EditorPrefs.SetBool(StartInMenuPref, !EditorPrefs.GetBool(StartInMenuPref, true));
            ApplyStartInMenu();
            Debug.Log("[DeadEcho] Play empieza en el menú principal: " + (EditorPrefs.GetBool(StartInMenuPref, true) ? "SÍ" : "NO (empieza en la escena abierta)"));
        }

        // =========================================================== F2 · Tomas del menu

        public const string MenuShotsPath = ResFolder + "/MenuShots.asset";

        /// <summary>
        /// Crea (o rellena) Resources/DeadEcho/MenuShots con los modelos del juego para las 3 tomas:
        /// aldeano en la niebla (y aldeanas al fondo), aldeana en el bosque y el Tyrant en la calle en llamas.
        /// Se puede cambiar cualquier modelo desde el Inspector del asset.
        /// </summary>
        [MenuItem("Dead Echo/F2 · Configurar tomas del menú", priority = 11)]
        public static void BuildMenuShots()
        {
            EnsureFolder(ResFolder);
            var cfg = AssetDatabase.LoadAssetAtPath<MenuShotsConfig>(MenuShotsPath);
            bool created = cfg == null;
            if (created) cfg = ScriptableObject.CreateInstance<MenuShotsConfig>();

            const string enemies = "Assets/RE4/Prefabs/Enemies/";
            GameObject aldeano = AssetDatabase.LoadAssetAtPath<GameObject>(enemies + "Ganado_Aldeano.prefab");
            GameObject aldeana = AssetDatabase.LoadAssetAtPath<GameObject>(enemies + "Ganado_Aldeana.prefab");
            GameObject tyrant = AssetDatabase.LoadAssetAtPath<GameObject>(enemies + "Jefe_Tyrant.prefab");
            if (cfg.shot1Main == null) cfg.shot1Main = aldeano;
            if (cfg.shot1Background == null) cfg.shot1Background = aldeana != null ? aldeana : aldeano;
            if (cfg.shot2Main == null) cfg.shot2Main = aldeana != null ? aldeana : aldeano;
            if (cfg.shot3Main == null) cfg.shot3Main = tyrant != null ? tyrant : aldeano;

            if (created) AssetDatabase.CreateAsset(cfg, MenuShotsPath);
            else EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            if (aldeano == null) Debug.LogWarning("[DeadEcho] No se encontraron los prefabs de enemigos (menú RE4 > Construir pueblo): las tomas usarán siluetas.");
            Debug.Log("[DeadEcho] Tomas del menú configuradas en " + MenuShotsPath + ".");
        }

        // =========================================================== Menu

        [MenuItem("Dead Echo/F0 · Regenerar base visual (sprites, fuentes, tema)", priority = 0)]
        public static void BuildBase()
        {
            foreach (var f in new[] { SpriteFolder, SdfFolder, ResFolder, PrefabFolder, SceneFolder }) EnsureFolder(f);
            UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            BuildSprites(theme);
            BuildFonts(theme);
            EditorUtility.SetDirty(theme);

            if (AssetDatabase.LoadAssetAtPath<UIAudio>(AudioPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<UIAudio>(), AudioPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[DeadEcho] Base visual lista: sprites, fuentes TMP (con tildes), UITheme y UIAudio.");
        }

        [MenuItem("Dead Echo/F0 · Crear prefabs y escena de prueba", priority = 1)]
        public static void BuildTestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath) == null) BuildBase();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildPrefabs();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UITheme th = UITheme.Current;

            // Camara de fondo negro
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = th.negroFondo;
            cam.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();

            // EventSystem con el Input System nuevo (teclado, raton y mando)
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().moveRepeatDelay = 0.35f;

            Canvas canvas = CreateCanvas("Canvas_Pruebas");
            Transform root = canvas.transform;
            var router = canvas.gameObject.AddComponent<UIRouter>();

            // Pantalla de prueba con todos los componentes
            RectTransform screenRt = UIFx.Stretch(UIFx.Rect(root, "Pantalla_Pruebas"));
            screenRt.gameObject.AddComponent<CanvasGroup>();
            var screen = screenRt.gameObject.AddComponent<UIScreen>();
            screen.canGoBack = false;
            router.initialScreen = screen;

            Image bg = UIFx.Image(screenRt, "Fondo", th.blanco, th.negroFondo);
            UIFx.Stretch(bg.rectTransform);

            var title = UIFx.Text(screenRt, "Titulo", "F0 · BASE VISUAL", th.rajdhaniSemiBold, th.tituloPantalla, th.hueso, TextAlignmentOptions.Left, 3f);
            UIFx.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -40f), new Vector2(900f, 70f));

            // Muestra de fuentes con tildes
            var fonts = new (TMP_FontAsset f, string n)[]
            {
                (th.specialElite, "Special Elite"), (th.courierPrime, "Courier Prime"), (th.courierPrimeBold, "Courier Prime Bold"),
                (th.rajdhaniMedium, "Rajdhani 500"), (th.rajdhaniSemiBold, "Rajdhani 600"), (th.oswaldBold, "Oswald 700")
            };
            for (int i = 0; i < fonts.Length; i++)
            {
                var t = UIFx.Text(screenRt, "Fuente_" + i, $"{fonts[i].n}: ¿Qué pasó? ¡Ñandú, ácido, éxito, ímpetu, ópalo, último! → 0123", fonts[i].f, 24f, th.huesoClaro);
                UIFx.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -130f - i * 36f), new Vector2(1100f, 34f));
            }

            // Columna izquierda: menu con tinta y botones vidrio
            RectTransform left = Column(screenRt, "Columna_Menu", new Vector2(96f, -380f), 460f);
            var ink1 = InkMenuItem.Create(left, "Continuar");
            ink1.interactable = false; // deshabilitado (gris 40 %) y la navegacion lo salta
            ink1.description = "Sin memoria guardada: deshabilitado.";
            var ink2 = InkMenuItem.Create(left, "Nueva partida");
            ink2.description = "Comienza una nueva investigación.";
            var ink3 = InkMenuItem.Create(left, "Opciones");
            ink3.description = "Ajusta gráficos, audio y controles.";
            Spacer(left, 20f);
            var g1 = GlassButton.Create(left, "Reanudar", new Vector2(480f, 60f));
            g1.description = "Vuelve a la partida donde la dejaste.";
            var g2 = GlassButton.Create(left, "Reiniciar desde punto de control", new Vector2(480f, 60f));
            g2.description = "Pide confirmación y recarga la escena.";
            var g3 = GlassButton.Create(left, "Salir al menú principal", new Vector2(480f, 60f));
            g3.description = "Vuelve al menú principal.";

            // Columna derecha: secciones y filas de opciones
            RectTransform right = Column(screenRt, "Columna_Opciones", new Vector2(700f, -380f), 960f);
            SectionHeader.Create(right, "Pantalla");
            var sel1 = OptionRowSelector.Create(right, "Modo de pantalla", new[] { "Pantalla completa", "Ventana sin bordes", "Ventana" }, 0, 0);
            sel1.description = "Cambia entre pantalla completa y ventana.";
            var sel2 = OptionRowSelector.Create(right, "Límite de FPS", new[] { "30", "60", "120", "144", "Sin límite" }, 1, 1);
            sel2.description = "Fotogramas por segundo máximos.";
            SectionHeader.Create(right, "Imagen");
            var sl1 = OptionRowSlider.Create(right, "Brillo", 0f, 100f, 55f, 50f);
            sl1.description = "Brillo general de la imagen.";
            var sl2 = OptionRowSlider.Create(right, "Grano de película", 0f, 100f, 30f, 30f);
            sl2.description = "Intensidad del grano cinematográfico.";
            SectionHeader.Create(right, "Ranura de memoria", "DEMO");
            var reduce = OptionRowSelector.Create(right, "Reducir movimiento", new[] { "Desactivado", "Activado" }, 0, 0);
            reduce.description = "Apaga grano, parpadeo y movimientos de cámara.";
            reduce.wrap = true;

            // Descripcion de la seleccion + pistas de control
            var desc = UIFx.Text(screenRt, "Descripcion", "", th.rajdhaniMedium, th.textoNormal, th.huesoClaro);
            UIFx.Place(desc.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(96f, 110f), new Vector2(1200f, 34f));
            desc.gameObject.AddComponent<DescriptionLabel>();
            RectTransform hints = UIFx.Place(UIFx.Rect(screenRt, "Pistas"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1400f, 48f));
            var hl = hints.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 40f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = false;
            KeyHint.Create(hints, "R", "Y", "Restablecer todo");
            KeyHint.Create(hints, "T", "X", "Restablecer categoría");
            KeyHint.Create(hints, "Enter", "A", "Confirmar");
            KeyHint.Create(hints, "Esc", "B", "Volver");

            // Navegacion vertical explicita entre columnas y conexion de "Reducir movimiento"
            LinkColumn(new Selectable[] { ink1, ink2, ink3, g1, g2, g3 });
            LinkColumn(new Selectable[] { sel1, sel2, sl1, sl2, reduce });
            var demo = screenRt.gameObject.AddComponent<UITestScreen>();
            demo.reduceMotion = reduce;
            screen.firstSelected = ink2;

            // Capa de lente encima de todo
            LensFX.Create(root, 30, false);

            Directory.CreateDirectory(SceneFolder);
            EditorSceneManager.SaveScene(scene, TestScene);
            Debug.Log("[DeadEcho] Escena de prueba creada: " + TestScene + ". Pulsa Play y navega con flechas, ratón o mando.");
        }

        // =========================================================== F1 · Escena del menu principal

        private const string MainMenuScene = SceneFolder + "/" + DeadEcho.Core.SceneNames.MainMenu + ".unity";

        /// <summary>
        /// Crea la escena MainMenu (solo camara + MainMenuController, que construye la interfaz al arrancar)
        /// SIN cerrar la escena abierta, y la pone la primera en Build Settings delante del juego.
        /// </summary>
        [MenuItem("Dead Echo/F1 · Crear escena MainMenu y añadirla a Build Settings", priority = 10)]
        public static void BuildMainMenuScene()
        {
            if (AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath) == null) BuildBase();
            EnsureFolder(SceneFolder);

            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(previous.path))
            {
                Debug.LogWarning("[DeadEcho] Guarda primero la escena abierta (File > Save) y vuelve a usar el menú Dead Echo > F1.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UITheme.Current.negroFondo;
            cam.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().moveRepeatDelay = 0.35f;

            new GameObject("[MenuPrincipal]").AddComponent<MainMenuController>();

            EditorSceneManager.SaveScene(scene, MainMenuScene);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);

            AddMainMenuToBuild();
            Debug.Log("[DeadEcho] Escena " + MainMenuScene + " creada y puesta la primera en Build Settings. Ábrela y pulsa Play para probar el menú.");
        }

        /// <summary>MainMenu primero (es la escena de arranque) y el resto de escenas detras, sin duplicados.</summary>
        private static void AddMainMenuToBuild()
        {
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(MainMenuScene, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != MainMenuScene) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }

        // =========================================================== Utilidades de escena

        public static Canvas CreateCanvas(string name, int order = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920f, 1080f);
            s.matchWidthOrHeight = 0.5f;
            return c;
        }

        private static RectTransform Column(RectTransform parent, string name, Vector2 pos, float width)
        {
            RectTransform rt = UIFx.Place(UIFx.Rect(parent, name), new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(width, 560f));
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = v.childControlHeight = false;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            return rt;
        }

        private static void Spacer(Transform parent, float h)
        {
            UIFx.Rect(parent, "Espacio").sizeDelta = new Vector2(10f, h);
        }

        /// <summary>Navegacion explicita arriba/abajo con salto de los deshabilitados (lo hace el EventSystem) y wrap.</summary>
        public static void LinkColumn(IList<Selectable> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = items[(i - 1 + items.Count) % items.Count],
                    selectOnDown = items[(i + 1) % items.Count],
                };
                items[i].navigation = nav;
            }
        }

        // =========================================================== Prefabs

        private static void BuildPrefabs()
        {
            EnsureFolder(PrefabFolder);
            var temp = new GameObject("Temp", typeof(RectTransform));
            Save(GlassButton.Create(temp.transform, "Boton vidrio", new Vector2(480f, 60f)).gameObject, "GlassButton");
            Save(InkMenuItem.Create(temp.transform, "Opción de menú").gameObject, "InkMenuItem");
            Save(SectionHeader.Create(temp.transform, "Sección").gameObject, "SectionHeader");
            Save(OptionRowSelector.Create(temp.transform, "Opción", new[] { "Bajo", "Medio", "Alto" }, 1, 1).gameObject, "OptionRowSelector");
            Save(OptionRowSlider.Create(temp.transform, "Valor", 0f, 100f, 50f, 50f).gameObject, "OptionRowSlider");
            Save(KeyHint.Create(temp.transform, "Esc", "B", "Volver").gameObject, "KeyHint");
            Save(LensFX.Create(temp.transform).gameObject, "LensFX");
            Object.DestroyImmediate(temp);
        }

        private static void Save(GameObject go, string name)
        {
            go.transform.SetParent(null, false);
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(go);
        }

        // =========================================================== Fuentes

        private static void BuildFonts(UITheme theme)
        {
            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            theme.specialElite = Font("SpecialElite-Regular", fallback);
            theme.courierPrime = Font("CourierPrime-Regular", fallback);
            theme.courierPrimeBold = Font("CourierPrime-Bold", fallback);
            theme.rajdhaniMedium = Font("Rajdhani-Medium", fallback);
            theme.rajdhaniSemiBold = Font("Rajdhani-SemiBold", fallback);
            theme.oswaldBold = Font("Oswald-Bold", fallback);
        }

        /// <summary>Font Asset dinamico (genera los glifos que hagan falta, tildes y ñ incluidas) con respaldo para → y simbolos.</summary>
        private static TMP_FontAsset Font(string file, TMP_FontAsset fallback)
        {
            string path = $"{SdfFolder}/{file} SDF.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            Font source = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{file}.ttf");
            if (source == null)
            {
                Debug.LogWarning("[DeadEcho] Falta la fuente " + file);
                return null;
            }
            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = file + " SDF";
            AssetDatabase.CreateAsset(fa, path);
            // el atlas y el material van dentro del mismo asset
            fa.atlasTextures[0].name = file + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = file + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            if (fallback != null) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            // precargar los caracteres del español para que no haya saltos la primera vez
            fa.TryAddCharacters(" ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789áéíóúÁÉÍÓÚñÑüÜ¿¡.,;:!?-–—()[]/%+×·‹›\"'«»_#", out _);
            EditorUtility.SetDirty(fa);
            return fa;
        }

        // =========================================================== Sprites

        private static void BuildSprites(UITheme th)
        {
            th.blanco = SpriteTex("blanco", 4, 4, (x, y) => Color.white, Vector4.zero);

            // Vidrio: brillo arriba, centro mas apagado, brillo interior abajo y borde 1 px blanco 50 %
            th.vidrio = SpriteTex("vidrio", 64, 64, (x, y) =>
            {
                float v = y / 63f;
                float a = Mathf.Lerp(0.07f, 0.2f, Mathf.Pow(Mathf.Abs(v - 0.45f) * 2f, 1.6f));
                if (v < 0.18f) a += (0.18f - v) * 0.9f; // brillo inferior
                bool border = x == 0 || y == 0 || x == 63 || y == 63;
                return border ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 1f, 1f, a);
            }, new Vector4(4, 4, 4, 4));

            // Trazo de tinta negra con bordes irregulares (opcion seleccionada del menu)
            th.tinta = SpriteTex("tinta", 256, 64, (x, y) => Brush(x, y, 256, 64, 7, Color.black), new Vector4(40, 0, 70, 0));
            th.pincelBlanco = SpriteTex("pincel_blanco", 512, 128, (x, y) => Brush(x, y, 512, 128, 21, new Color(0.94f, 0.92f, 0.88f)), new Vector4(80, 0, 120, 0));

            // Pestaña trapezoidal: degradado #2C2C2B → #3A3A38 con el lado derecho inclinado
            th.trapecio = SpriteTex("trapecio", 256, 48, (x, y) =>
            {
                float slantStart = 256 - 48;
                float edge = slantStart + (48 - y) * 1f;
                if (x > edge) return Color.clear;
                Color c = Color.Lerp(th.seccionB, th.seccionA, x / 256f);
                return c;
            }, new Vector4(8, 0, 56, 0));

            th.aro = SpriteTex("aro", 128, 128, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f));
                float a = Mathf.Clamp01(1.5f - Mathf.Abs(d - 60f));
                return new Color(1f, 1f, 1f, a);
            }, Vector4.zero);

            th.rombo = SpriteTex("rombo", 32, 32, (x, y) =>
            {
                float d = Mathf.Abs(x - 15.5f) + Mathf.Abs(y - 15.5f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(15f - d));
            }, Vector4.zero);

            // Icono ↵ (tecla Enter): linea vertical a la derecha y flecha hacia la izquierda
            th.enter = SpriteTex("enter", 64, 64, (x, y) =>
            {
                bool vert = x >= 44 && x <= 49 && y >= 24 && y <= 52;
                bool horiz = y >= 24 && y <= 29 && x >= 14 && x <= 49;
                float ax = x - 12f, ay = Mathf.Abs(y - 26.5f);
                bool head = ax >= 0f && ax <= 14f && ay <= ax * 0.95f && ay >= ax * 0.95f - 7f;
                return vert || horiz || head ? Color.white : Color.clear;
            }, Vector4.zero);

            th.degradadoIzq = SpriteTex("degradado_izq", 256, 4, (x, y) =>
            {
                float t = x / 255f;
                return new Color(0f, 0f, 0f, Mathf.Clamp01(1f - t * t * (3f - 2f * t)));
            }, Vector4.zero);

            th.vineta = SpriteTex("vineta", 256, 256, (x, y) =>
            {
                float dx = (x - 127.5f) / 127.5f, dy = (y - 127.5f) / 127.5f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                return new Color(0f, 0f, 0f, Mathf.Clamp01(Mathf.InverseLerp(0.55f, 1.35f, d)));
            }, Vector4.zero);

            // Grano: ruido gris que se repite (la capa lo desplaza al azar 24 veces por segundo)
            var rng = new System.Random(7);
            th.grano = Texture("grano", 256, 256, (x, y) =>
            {
                float n = (float)rng.NextDouble();
                return new Color(n, n, n, 1f);
            }, true);
        }

        /// <summary>Pincelada: borde irregular arriba y abajo y final deshilachado a la derecha.</summary>
        private static Color Brush(int x, int y, int w, int h, int seed, Color c)
        {
            float u = x / (float)(w - 1), v = y / (float)(h - 1);
            float top = 0.86f - 0.08f * Mathf.PerlinNoise(u * 9f + seed, 0.3f) - 0.04f * Mathf.PerlinNoise(u * 40f, seed);
            float bottom = 0.14f + 0.08f * Mathf.PerlinNoise(u * 8f + seed * 2f, 1.7f) + 0.04f * Mathf.PerlinNoise(u * 45f, seed + 3f);
            float right = 0.9f + 0.08f * Mathf.PerlinNoise(v * 12f + seed, 4.1f);
            float left = 0.02f + 0.03f * Mathf.PerlinNoise(v * 10f, seed + 9f);
            if (v > top || v < bottom || u > right || u < left) return Color.clear;
            // estrias del pincel
            float streak = Mathf.PerlinNoise(u * 3f, v * 60f + seed);
            float a = 1f - Mathf.Clamp01((u - (right - 0.12f)) / 0.12f) * (streak > 0.5f ? 1f : 0.6f);
            return new Color(c.r, c.g, c.b, Mathf.Clamp01(a) * (0.88f + 0.12f * streak));
        }

        private static Sprite SpriteTex(string name, int w, int h, System.Func<int, int, Color> px, Vector4 border)
        {
            string path = $"{SpriteFolder}/{name}.png";
            WritePng(path, w, h, px);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.spriteBorder = border;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D Texture(string name, int w, int h, System.Func<int, int, Color> px, bool repeat)
        {
            string path = $"{SpriteFolder}/{name}.png";
            WritePng(path, w, h, px);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void WritePng(string path, int w, int h, System.Func<int, int, Color> px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var data = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    data[y * w + x] = px(x, y);
            tex.SetPixels(data);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
