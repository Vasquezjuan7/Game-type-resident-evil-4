using System.Collections;
using System.Collections.Generic;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Modo cine (F2, 02_Inactivo.html) = IdleWatcher de la especificacion. Tras 20 s sin input en la raiz del
    /// menu principal la interfaz se desvanece (1.2 s), entran franjas negras arriba y abajo, logo pequeño y
    /// "PRESIONA CUALQUIER TECLA" pulsando. Cualquier input vuelve al menu con la misma opcion seleccionada,
    /// y ese input NO ejecuta ninguna accion (el menu no responde hasta que se suelta todo).
    /// </summary>
    public class IdleCinemaMode : MonoBehaviour
    {
        public float idleSeconds = 20f;
        public float fadeOutSeconds = 1.2f;
        public float barHeight = 130f;

        private UIRouter _router;
        private UIScreen _menu;
        private readonly List<CanvasGroup> _fadeWithMenu = new List<CanvasGroup>();
        private RectTransform _top, _bottom;
        private CanvasGroup _overlay;
        private TextMeshProUGUI _press;

        private bool _active, _waitRelease;
        private float _enteredAt, _k;
        private GameObject _savedSelection;
        private Coroutine _anim;

        public bool IsActive => _active;

        /// <summary>Crea el modo cine sobre el Canvas del menu. 'alsoFade': capas que se ocultan con el menu (degradado, lente).</summary>
        public static IdleCinemaMode Create(Transform canvasRoot, UIRouter router, UIScreen menu, params CanvasGroup[] alsoFade)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Stretch(UIFx.Rect(canvasRoot, "ModoCine"));
            var mode = rt.gameObject.AddComponent<IdleCinemaMode>();
            mode._router = router;
            mode._menu = menu;
            foreach (var g in alsoFade) if (g != null) mode._fadeWithMenu.Add(g);

            // franjas negras
            mode._top = UIFx.Image(rt, "FranjaArriba", th.blanco, Color.black).rectTransform;
            mode._top.anchorMin = new Vector2(0f, 1f);
            mode._top.anchorMax = new Vector2(1f, 1f);
            mode._top.pivot = new Vector2(0.5f, 1f);
            mode._top.sizeDelta = new Vector2(0f, 0f);
            mode._bottom = UIFx.Image(rt, "FranjaAbajo", th.blanco, Color.black).rectTransform;
            mode._bottom.anchorMin = new Vector2(0f, 0f);
            mode._bottom.anchorMax = new Vector2(1f, 0f);
            mode._bottom.pivot = new Vector2(0.5f, 0f);
            mode._bottom.sizeDelta = new Vector2(0f, 0f);

            // logo pequeño + texto que pulsa
            RectTransform over = UIFx.Stretch(UIFx.Rect(rt, "Overlay"));
            mode._overlay = over.gameObject.AddComponent<CanvasGroup>();
            mode._overlay.alpha = 0f;
            mode._overlay.blocksRaycasts = false;
            mode._overlay.interactable = false;

            var dead = UIFx.Text(over, "DEAD", "DEAD", th.specialElite, 22f, th.hueso, TextAlignmentOptions.Left, 18f);
            UIFx.Place(dead.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -30f), new Vector2(300f, 30f));
            Image echoBack = UIFx.Image(over, "FondoEcho", th.blanco, th.hueso);
            UIFx.Place(echoBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -62f), new Vector2(150f, 40f));
            var echo = UIFx.Text(echoBack.transform, "echo", "echo", th.specialElite, 32f, th.negroFondo, TextAlignmentOptions.Center, 18f);
            UIFx.Stretch(echo.rectTransform);

            mode._press = UIFx.Text(over, "Presiona", "PRESIONA CUALQUIER TECLA", th.courierPrime, 21f, th.hueso, TextAlignmentOptions.Center, 16f);
            UIFx.Place(mode._press.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 52f), new Vector2(1400f, 40f));
            return mode;
        }

        private void Update()
        {
            if (_active)
            {
                // pulso de "PRESIONA CUALQUIER TECLA" (2.6 s)
                float a = SettingsService.ReduceMotion ? 0.85f : Mathf.Lerp(0.25f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 2.6f));
                Color c = _press.color;
                c.a = a;
                _press.color = c;

                if (InputModeWatcher.LastInputTime > _enteredAt + 0.25f) Exit();
                return;
            }

            if (_waitRelease)
            {
                // el input que despierta al menu no debe pulsar nada: esperar a que se suelte todo
                if (AnyPressed()) return;
                _waitRelease = false;
                RestoreMenuInput();
                return;
            }

            if (_menu == null || _router == null || SceneLoader.IsLoading) return;
            bool atRoot = _router.Top == _menu && _menu.Group.interactable;
            if (atRoot && Application.isFocused && Time.unscaledTime - InputModeWatcher.LastInputTime >= idleSeconds) Enter();
        }

        private void Enter()
        {
            _active = true;
            _enteredAt = Time.unscaledTime;
            EventSystem es = EventSystem.current;
            _savedSelection = es != null ? es.currentSelectedGameObject : null;
            if (es != null) es.sendNavigationEvents = false;
            _menu.Group.interactable = false;
            _menu.Group.blocksRaycasts = false;
            Animate(1f, fadeOutSeconds);
        }

        private void Exit()
        {
            _active = false;
            _waitRelease = true;
            InputModeWatcher.Touch();
            Animate(0f, 0.6f);
        }

        private void RestoreMenuInput()
        {
            _menu.Group.interactable = true;
            _menu.Group.blocksRaycasts = true;
            EventSystem es = EventSystem.current;
            if (es != null)
            {
                es.sendNavigationEvents = true;
                es.SetSelectedGameObject(null);
                GameObject sel = _savedSelection != null && _savedSelection.activeInHierarchy ? _savedSelection
                    : _menu.firstSelected != null ? _menu.firstSelected.gameObject : null;
                if (sel != null) es.SetSelectedGameObject(sel);
            }
            InputModeWatcher.Touch();
        }

        private void Animate(float to, float seconds)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(AnimRoutine(to, seconds));
        }

        private IEnumerator AnimRoutine(float to, float seconds)
        {
            float from = _k;
            yield return UIFx.Tween(seconds, t =>
            {
                _k = Mathf.Lerp(from, to, t);
                _menu.Group.alpha = 1f - _k;
                foreach (var g in _fadeWithMenu) if (g != null) g.alpha = 1f - _k;
                _top.sizeDelta = new Vector2(0f, barHeight * _k);
                _bottom.sizeDelta = new Vector2(0f, barHeight * _k);
                _overlay.alpha = _k;
            });
            _anim = null;
        }

        private static bool AnyPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.anyKey.isPressed) return true;
            Mouse m = Mouse.current;
            if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed)) return true;
            Gamepad p = Gamepad.current;
            if (p != null && (p.buttonSouth.isPressed || p.buttonEast.isPressed || p.buttonNorth.isPressed || p.buttonWest.isPressed
                              || p.startButton.isPressed || p.selectButton.isPressed || p.leftShoulder.isPressed || p.rightShoulder.isPressed
                              || p.leftTrigger.isPressed || p.rightTrigger.isPressed || p.dpad.ReadValue().sqrMagnitude > 0.1f
                              || p.leftStick.ReadValue().magnitude > 0.5f))
                return true;
            return false;
        }
    }
}
