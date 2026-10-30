using TMPro;
using UnityEngine;

namespace DeadEcho.UI
{
    /// <summary>
    /// Paleta, tipografias, tamaños y sprites de Dead Echo (01_DISENO_VISUAL.md).
    /// Medidas a 1920x1080 (las del HTML de referencia x1.5). Se carga desde Resources/DeadEcho/UITheme.
    /// </summary>
    [CreateAssetMenu(menuName = "Dead Echo/UI Theme", fileName = "UITheme")]
    public class UITheme : ScriptableObject
    {
        [Header("Paleta")]
        public Color negroFondo = Hex("#0B0B0B");
        public Color hueso = Hex("#ECE8DE");
        public Color huesoClaro = Hex("#E6E6E2");
        public Color grisMedio = Hex("#9A968D");
        public Color grisLinea = Hex("#2E2E2C");
        public Color seccionA = Hex("#2C2C2B");
        public Color seccionB = Hex("#3A3A38");
        public Color verdeOk = Hex("#B9E4B0");
        public Color fuego = Hex("#FF7A1E");

        [Header("Tipografias (TMP con tildes y ñ)")]
        [Tooltip("Logo y textos de maquina de escribir.")]
        public TMP_FontAsset specialElite;
        [Tooltip("Opciones y textos del menu principal.")]
        public TMP_FontAsset courierPrime;
        public TMP_FontAsset courierPrimeBold;
        [Tooltip("Interfaz de sistema: pausa, opciones, inventario, nueva partida, salir.")]
        public TMP_FontAsset rajdhaniMedium;
        public TMP_FontAsset rajdhaniSemiBold;
        [Tooltip("DEAD grande de la pantalla de carga.")]
        public TMP_FontAsset oswaldBold;

        [Header("Tamaños (px a 1920x1080)")]
        public float tituloPantalla = 54f;
        public float opcionMenu = 30f;
        public float filaSistema = 28f;
        public float textoNormal = 24f;
        public float textoPequeno = 20f;
        public float alturaFila = 70f;

        [Header("Animacion")]
        [Tooltip("Entrada de la seleccion vidrio / tinta.")]
        public float tiempoSeleccion = 0.15f;
        public float tiempoPantalla = 0.25f;

        [Header("Sprites generados")]
        public Sprite blanco;
        public Sprite vidrio;
        public Sprite tinta;
        public Sprite pincelBlanco;
        public Sprite trapecio;
        public Sprite aro;
        public Sprite rombo;
        public Sprite enter;
        public Sprite flechaIzq;
        public Sprite flechaDer;
        public Sprite degradadoIzq;
        public Sprite vineta;
        public Texture2D grano;

        private static UITheme _current;

        /// <summary>Tema activo (Resources/DeadEcho/UITheme). Nunca es null.</summary>
        public static UITheme Current
        {
            get
            {
                if (_current == null) _current = Resources.Load<UITheme>("DeadEcho/UITheme");
                if (_current == null) _current = CreateInstance<UITheme>();
                return _current;
            }
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
