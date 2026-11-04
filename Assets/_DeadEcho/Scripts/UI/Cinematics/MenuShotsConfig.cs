using UnityEngine;

namespace DeadEcho.UI
{
    /// <summary>
    /// Modelos de las 3 tomas del menu principal (F2). Lo crea y rellena el editor
    /// (Dead Echo > F2 · Configurar tomas del menú) en Resources/DeadEcho/MenuShots.
    /// Si falta un modelo, la toma usa una silueta sencilla.
    /// </summary>
    [CreateAssetMenu(menuName = "Dead Echo/Menu Shots", fileName = "MenuShots")]
    public class MenuShotsConfig : ScriptableObject
    {
        [Tooltip("Toma 1 · figura en la niebla (zombie casi inmovil).")]
        public GameObject shot1Main;
        [Tooltip("Toma 1 · figuras al fondo de la niebla.")]
        public GameObject shot1Background;
        [Tooltip("Toma 2 · bosque de noche.")]
        public GameObject shot2Main;
        [Tooltip("Toma 3 · calle en llamas.")]
        public GameObject shot3Main;

        [Tooltip("Segundos de cada toma.")]
        public float shotSeconds = 10f;
        [Tooltip("Fundido cruzado entre tomas.")]
        public float crossfadeSeconds = 1.5f;

        private static MenuShotsConfig _current;

        public static MenuShotsConfig Current
        {
            get
            {
                if (_current == null) _current = Resources.Load<MenuShotsConfig>("DeadEcho/MenuShots");
                if (_current == null) _current = CreateInstance<MenuShotsConfig>();
                return _current;
            }
        }
    }
}
