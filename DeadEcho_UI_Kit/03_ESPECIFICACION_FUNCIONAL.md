# Especificación funcional — qué debe hacer cada pantalla y botón

## 1. Arquitectura sugerida (el agente puede adaptarla a lo que ya exista)

| Pieza | Responsabilidad |
|---|---|
| `GameStateManager` (singleton, persiste entre escenas) | Estado global: `MainMenu`, `Loading`, `Playing`, `Paused`, `Inventory`. Dispara eventos de cambio. |
| `UIRouter` | Pila de pantallas (push/pop). Abre/cierra paneles con transición y restaura la selección al volver con Esc/B. |
| `InputModeWatcher` | Detecta el último dispositivo (teclado/ratón o mando) y cambia los iconos de `KeyHint`. |
| `IdleWatcher` | Cuenta inactividad en el menú principal y activa el modo cine. |
| `SettingsService` | Lee/guarda opciones (JSON en `Application.persistentDataPath` o PlayerPrefs) y las aplica. |
| `SaveService` (DEMO) | Maneja las ranuras de memoria **falsas**. No guarda progreso real. |
| `SceneLoader` | Carga asíncrona con la pantalla de carga y consejo aleatorio. |
| `InventoryModel` + `InventoryView` | Cuadrícula, objetos multi-celda, selección y acciones. |

Escenas: `MainMenu` (menú, modo cine, nueva partida, opciones, créditos, salir) · `Loading` (aditiva o propia) · escenas de juego (con pausa e inventario como prefabs de UI).

## 2. Flujo entre pantallas

```
Arranque → Menú principal ──20 s sin input──► Modo cine ──cualquier tecla──► Menú principal
Menú principal
 ├─ Continuar ─────► Carga ─► Juego (desde la memoria de la ranura más reciente)
 ├─ Nueva partida ─► Nueva partida ─► Comenzar ─► Carga ─► Juego
 ├─ Opciones ──────► Opciones ─► Esc ─► Menú
 ├─ Créditos ──────► Créditos ─► Esc ─► Menú
 └─ Salir ─────────► Modal Salir ─► Sí ─► cerrar app / No ─► Menú
Juego ─Esc/Start─► Pausa      Juego ─Tab/Select─► Inventario
```

## 3. Pantalla por pantalla

### Menú principal
- Navegación ↑↓ / stick / ratón (hover = seleccionar), con wrap. Enter/A confirma.
- Cada opción tiene un texto de descripción que cambia con la selección.
- **Continuar**: deshabilitado (gris al 40 % y se salta) si `SaveService` no tiene ninguna ranura con memoria. Si hay, carga la más reciente.
- **Salir**: abre el modal; no cierra directamente.
- Las tomas cinemáticas corren siempre; se pausan si la ventana pierde el foco.

### Modo cine
- `IdleWatcher`: 20 s sin input (configurable). Solo activo en el menú principal raíz (no dentro de Opciones/Créditos).
- Entrar: fade out UI 1.2 s + letterbox. Salir con cualquier input; **ese input no ejecuta ninguna acción**; restaurar selección.

### Nueva partida
- **Dificultad**: Narrativa / Superviviente (por defecto) / Pesadilla. Guarda en `GameSession.difficulty`. Cambia el panel "Con esta dificultad" (daño recibido ×0.5 / ×1.0 / ×1.5; autoguardado sí / sí / no).
- **Ranuras de memoria (DEMO)**: 3 ranuras. **Guardar progreso** es solo un botón de demostración:
  - Asigna a la ranura seleccionada (o la primera vacía) una **memoria falsa** con datos inventados: ubicación "Hospital San Aurelio", tiempo jugado aleatorio, fecha/hora actual, dificultad elegida, % del caso aleatorio.
  - Muestra el toast "Memoria falsa asignada a la ranura N" (3 s) e icono de guardado.
  - Persiste esa memoria falsa en un JSON (`demo_slots.json`) para que **Continuar** se habilite y la ranura se vea al volver. No guarda posición ni inventario reales.
  - Extras falsos opcionales: indicador "Sincronizando…" de 1.5 s, miniatura de captura de pantalla de la ranura.
- **Comenzar →**: crea `GameSession` (personaje único, dificultad, equipo inicial) y carga la escena de juego vía `SceneLoader`.
- Esc vuelve al menú.

### Pantalla de carga
- `SceneLoader.Load(sceneName)`: muestra la pantalla, `LoadSceneAsync` con `allowSceneActivation=false`, mínimo 2 s en pantalla para que se lea el consejo, activa la escena al 90 % y hace fade a negro 0.6 s.
- Consejo aleatorio desde un ScriptableObject `LoadingTips`.

### Opciones (desde menú y desde pausa)
- Pestañas con Q/E (LB/RB), filas con ↑↓, cambiar valor con ←→, sliders con ←→ (mantener = rápido).
- **Gráficos**: Resolución (`Screen.resolutions`), Modo de pantalla (`FullScreenMode`), Límite FPS (`Application.targetFrameRate`), Preajuste de calidad (`QualitySettings.SetQualityLevel`), Sombras, V-Sync (`QualitySettings.vSyncCount`), Brillo y Grano de película (parámetros del Volume de post-proceso).
- **Audio**: General, Música, Efectos, Voces (AudioMixer, en dB), Subtítulos.
- **Controles**: Sensibilidad, Invertir Y, Vibración, Reasignar teclas (Input System rebinding).
- **Cámara / HUD**: placeholders listos para que el equipo los complete.
- **Idioma**: Español / English (Unity Localization). Cambia en vivo.
- **Accesibilidad**: Tamaño de texto, Modo daltónico, Reducir movimiento, Mantener vs. pulsar.
- ◆ aparece cuando el valor difiere del default. **R** restablece todo (con confirmación), **T** restablece la categoría.
- Cambio de resolución/modo: pedir confirmación "¿Mantener esta configuración? 15 s" y revertir si no se confirma.
- Esc con cambios sin aplicar → "¿Descartar cambios?".
- El panel derecho es decorativo (zombie en niebla). Opcional: que refleje en vivo brillo y grano.

### Pausa
- Esc/Start en juego: `Time.timeScale = 0`, audio de juego atenuado, capturar fondo y desenfocarlo. Las animaciones de UI usan tiempo *unscaled*.
- **Reanudar**: cierra y restaura `timeScale`. **Reiniciar desde punto de control**: confirmación → recargar escena. **Cargar partida**: lista de ranuras (DEMO). **Tutoriales**: panel de texto. **Opciones**: abre Opciones. **Salir al menú principal**: confirmación → Carga → MainMenu.
- Esc/B cierra la pausa.

### Inventario
- Tab/Select abre y cierra; pausa el juego igual que la pausa.
- Cuadrícula 10×7. Cada `ItemData` (ScriptableObject): nombre, descripción, icono, tamaño (ancho×alto), apilable, cantidad máx., estadísticas.
- Navegación por celdas; al seleccionar un objeto se actualiza la ficha y las estadísticas.
- **R**: mover/rotar (validar que cabe). **Enter**: usar / combinar (combinar = seleccionar otro objeto compatible). **F mantener**: ordenar automáticamente. Q/E cambia a Mapa / Documentos (placeholders).
- Objetivo actual arriba a la izquierda desde `ObjectiveManager` (o texto fijo por ahora).

### Créditos
- Animación en loop; Esc vuelve al menú. Nombres: Juan Diego Gonzales Sánchez, Juan José Muñoz Jurado, Juan José Pérez Bravo.

### Salir
- "No, volver" seleccionado por defecto. Sí → fade a negro 0.5 s → `Application.Quit()` (en editor: `EditorApplication.isPlaying = false`).

## 4. Reglas generales
- Todo funciona con teclado, ratón y mando. Ningún botón es solo visual excepto los marcados como decorativos.
- Selección inicial siempre definida al abrir cada pantalla (`EventSystem.SetSelectedGameObject`).
- Sonidos: hover, confirmar, atrás, error (campos en un `UIAudio` ScriptableObject).
- 60 FPS en menús; Canvas separados para elementos animados; sin asignaciones de memoria por frame.
- Respetar "Reducir movimiento" en todas las animaciones.
