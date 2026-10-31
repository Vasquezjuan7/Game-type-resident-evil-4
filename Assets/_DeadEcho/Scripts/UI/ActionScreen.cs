using System;

namespace DeadEcho.UI
{
    /// <summary>Pantalla cuyo Esc / B ejecuta una accion propia (por ejemplo, Reanudar en la pausa).</summary>
    public class ActionScreen : UIScreen
    {
        public Action onBack;

        public override void OnBack()
        {
            if (onBack == null)
            {
                base.OnBack();
                return;
            }
            UIAudio.Play(UISound.Atras);
            onBack();
        }
    }
}
