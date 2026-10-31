using DeadEcho.Core;
using UnityEngine;

namespace DeadEcho.UI
{
    /// <summary>
    /// Escena de prueba de la fase F0: conecta las filas de ejemplo con las opciones reales
    /// para comprobar "Reducir movimiento" y el grano de pelicula en vivo.
    /// </summary>
    public class UITestScreen : MonoBehaviour
    {
        public OptionRowSelector reduceMotion;
        public OptionRowSlider filmGrain;

        private void Start()
        {
            if (reduceMotion != null)
            {
                reduceMotion.SetValueWithoutNotify(SettingsService.Current.reduceMotion ? 1 : 0);
                reduceMotion.onValueChanged.AddListener(i =>
                {
                    SettingsService.Current.reduceMotion = i == 1;
                    SettingsService.Apply();
                    SettingsService.Save();
                });
            }
            if (filmGrain == null)
            {
                foreach (var s in GetComponentsInChildren<OptionRowSlider>(true))
                    if (s.name.Contains("Grano")) filmGrain = s;
            }
            if (filmGrain != null)
            {
                filmGrain.SetValueWithoutNotify(SettingsService.Current.filmGrain);
                filmGrain.onValueChanged.AddListener(v =>
                {
                    SettingsService.Current.filmGrain = Mathf.RoundToInt(v);
                    SettingsService.Apply();
                });
            }
        }
    }
}
