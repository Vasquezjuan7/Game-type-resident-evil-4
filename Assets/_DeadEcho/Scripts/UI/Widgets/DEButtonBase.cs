using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Base de todos los elementos seleccionables de Dead Echo: se navega con teclado/mando (EventSystem),
    /// pasar el raton por encima lo selecciona, Enter / A / clic lo confirma. La seleccion se anima en
    /// UITheme.tiempoSeleccion (0.15 s) con tiempo sin escala, y suena hover / confirmar.
    /// </summary>
    public abstract class DEButtonBase : Selectable, ISubmitHandler, IPointerClickHandler
    {
        [Tooltip("Texto de ayuda que la pantalla muestra al seleccionar este elemento.")]
        [TextArea] public string description = "";
        public UnityEvent onClick = new UnityEvent();

        /// <summary>Cualquier elemento seleccionado (para actualizar el texto de descripcion de la pantalla).</summary>
        public static event Action<DEButtonBase> OnAnySelected;

        protected float selectionBlend;   // 0 = normal, 1 = seleccionado (animado)
        private bool _selected;
        private static int _lastHoverSoundFrame;

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectionBlend = _selected ? 1f : 0f;
            ApplyVisual(selectionBlend);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            _selected = true;
            // un solo sonido por fotograma (al abrir una pantalla no se acumulan)
            if (Application.isPlaying && Time.frameCount != _lastHoverSoundFrame && Time.unscaledTime > 0.3f)
            {
                _lastHoverSoundFrame = Time.frameCount;
                UIAudio.Play(UISound.Hover);
            }
            OnAnySelected?.Invoke(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            _selected = false;
        }

        /// <summary>Hover = seleccionar (03_ESPECIFICACION_FUNCIONAL.md).</summary>
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (IsInteractable() && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public virtual void OnSubmit(BaseEventData eventData) => Press();

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Press();
        }

        protected virtual void Press()
        {
            if (!IsActive() || !IsInteractable())
            {
                UIAudio.Play(UISound.Error);
                return;
            }
            UIAudio.Play(UISound.Confirmar);
            onClick.Invoke();
        }

        public bool IsSelectedNow => _selected;

        protected virtual void Update()
        {
            float target = _selected && IsInteractable() ? 1f : 0f;
            float speed = Core.SettingsService.ReduceMotion ? 1000f : 1f / Mathf.Max(0.01f, UITheme.Current.tiempoSeleccion);
            float prev = selectionBlend;
            selectionBlend = Mathf.MoveTowards(selectionBlend, target, Time.unscaledDeltaTime * speed);
            if (!Mathf.Approximately(prev, selectionBlend)) ApplyVisual(UIFx.Ease(selectionBlend));
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            // las transiciones las dibuja cada componente; aqui solo el estado deshabilitado
            ApplyVisual(UIFx.Ease(selectionBlend));
        }

        /// <summary>Dibuja el estado: k = 0 normal, 1 seleccionado.</summary>
        protected abstract void ApplyVisual(float k);
    }
}
