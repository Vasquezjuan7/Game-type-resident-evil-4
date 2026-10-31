using UnityEngine;

namespace DeadEcho.Core
{
    /// <summary>Consejos de la pantalla de carga. Se carga desde Resources/DeadEcho/LoadingTips (si no existe, usa los de fabrica).</summary>
    [CreateAssetMenu(menuName = "Dead Echo/Loading Tips", fileName = "LoadingTips")]
    public class LoadingTips : ScriptableObject
    {
        [TextArea] public string[] tips =
        {
            "Dispara a las piernas de un aldeano para que se arrodille y remátalo con una patada (F).",
            "Un disparo a la cabeza aturde: aprovecha para acercarte y golpear.",
            "Ordena el maletín (Tab) para que te quepan más balas y hierbas.",
            "Combina hierbas en el maletín para curar más con menos espacio.",
            "El cuchillo (V) se desgasta, pero puede salvarte de un agarre.",
            "Busca el farol azul: el mercader compra, vende y mejora tus armas (U).",
            "De noche, la linterna (L) te muestra el camino… y te delata.",
            "Rompe cajas y barriles: suelen esconder pesetas y munición.",
            "Si un aldeano levanta el brazo para lanzar, apártate de su trayectoria.",
            "Lee los documentos (J): cuentan lo que ocurrió en el pueblo.",
            "Girar rápido (S + Shift) te saca de más de un aprieto.",
            "No malgastes munición: los aldeanos caídos pueden levantarse."
        };

        private static LoadingTips _current;

        public static LoadingTips Current
        {
            get
            {
                if (_current == null) _current = Resources.Load<LoadingTips>("DeadEcho/LoadingTips");
                if (_current == null) _current = CreateInstance<LoadingTips>();
                return _current;
            }
        }

        public string Random()
        {
            if (tips == null || tips.Length == 0) return "";
            return tips[UnityEngine.Random.Range(0, tips.Length)];
        }
    }
}
