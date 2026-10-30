# Prompts visuales (construir y ajustar cada pantalla)

Usar después de aprobar el plan (`04_PROMPT_EVALUACION_Y_PLAN.md`). Uno por fase. En cada prompt, si el agente puede ver imágenes, adjunta la captura correspondiente.

---

## V0 · Base visual compartida
```
Crea la base visual compartida de Dead Echo según 01_DISENO_VISUAL.md:
- TMP Font Assets para Special Elite, Courier Prime, Rajdhani (500/600) y Oswald 700 (con tildes y ñ).
- Un ScriptableObject UITheme con la paleta y tamaños.
- Prefabs reutilizables: GlassButton (selección vidrio), InkMenuItem (trazo de tinta), SectionHeader (pestaña
  trapezoidal), OptionRowSelector (‹ valor › + segmentos), OptionRowSlider (slider con marcas), KeyHint (tecla en círculo + texto).
- Un prefab LensFX con grano de película, viñeta y parpadeo (Image con material o URP Volume) reutilizable.
- Respeta la opción "Reducir movimiento" (apaga grano, parpadeo y movimientos de cámara).
Compara cada componente con las capturas y muéstrame una escena de prueba con todos.
```

## V1 · Menú principal
```
Implementa el menú principal según 01_DISENO_VISUAL.md (sección 01) y pantallas/01_Main.html.
- Izquierda: línea vertical rayada, logo con efecto eco, opciones con InkMenuItem.
- Fondo: un sistema de "tomas" (lista de ScriptableObjects CinematicShot) que rota cada 10 s con crossfade 1.5 s.
  Recrea las 3 tomas de referencias/LEEME.md. Mientras no haya escenas 3D, usa las imágenes de referencias/
  como placeholders en RawImage, animadas como en el HTML (empuje, niebla, linterna, fuego, brasas).
  Deja preparado el cambio a cámaras de Cinemachine sobre escenas 3D con zombies en idle.
- Capa LensFX encima y degradado negro a la izquierda.
Compara con capturas/01_Main.png y con el HTML abierto en Chrome.
```

## V2 · Modo cine
```
Implementa el modo cine (pantallas/02_Inactivo.html): tras 20 s sin input, la UI del menú hace fade out 1.2 s,
entran franjas negras (letterbox) de 70 px arriba y abajo, logo pequeño abajo a la izquierda y
"PRESIONA CUALQUIER TECLA" pulsando. Las tomas siguen. Cualquier input sale sin ejecutar acción.
```

## V3 · Pantalla de carga
```
Implementa la pantalla de carga según pantallas/04_Carga.html y referencias/pantalla_carga_referencia.png.
Usa assets/fondo_carga_sin_logo.png como placeholder (en el futuro, una captura propia del juego en B/N).
Animación: empuje lento 40 s, nubes y niebla desplazándose (capas con UV scroll o imágenes desenfocadas),
partículas de ceniza (UI Particle o ParticleSystem en cámara), logo DEAD | echo | arriba al centro.
Abajo: consejo aleatorio (lista en ScriptableObject) y "CARGANDO" con icono girando.
```

## V4 · Nueva partida
```
Implementa pantallas/03_NuevaPartida.html: dificultad con GlassButton, sección "Ranura de memoria DEMO"
con 3 ranuras, botón Guardar progreso, toast animado, panel del protagonista con línea de escaneo
(RenderTexture del modelo si existe, si no silueta), equipo inicial y efectos de la dificultad que cambian
al seleccionar otra dificultad (animar valores 0.3 s).
```

## V5 · Opciones
```
Implementa pantallas/05_Opciones.html. Solo la mitad izquierda sigue referencias/opciones_referencia.png.
Pestañas con Q/E, secciones, filas selector/slider, rombo ◆ para valores modificados, scroll vertical.
Panel derecho: zombie en la niebla en blanco y negro (referencias/zombie_01_niebla.jpg como placeholder),
niebla, ceniza y empuje lento, fundido con el fondo negro por los bordes.
```

## V6 · Pausa
```
Implementa pantallas/06_Pausa.html sobre la escena de juego: captura/desenfoca el fondo (blur + oscurecer),
franjas de luz verticales en los bordes, menú centrado con GlassButton y descripción abajo.
```

## V7 · Inventario
```
Implementa pantallas/07_Inventario.html siguiendo referencias/inventario_referencia.png:
cuadrícula 10×7, objetos que ocupan varias celdas, selección con brillo pulsante, pestañas Mapa/Objetos/Documentos,
ficha del objeto y estadísticas con barras. Iconos de objetos: placeholders hasta tener los definitivos.
```

## V8 · Créditos y Salir
```
Implementa pantallas/08_Creditos.html (palabras cayendo en loop hasta el pie de página) y
pantallas/09_Salir.html (modal con "No, volver" seleccionado por defecto).
```

---

## Prompts de ajuste visual (usar cuando algo no se parezca)

- *"Pon lado a lado la pantalla en Game view y capturas/XX.png. Lista las diferencias de posición, tamaño, color y tipografía y corrígelas."*
- *"La animación de [pantalla] se ve muy rápida/artificial. Hazla más lenta y sutil: duplica las duraciones y usa ease in-out."*
- *"Los zombies se ven demasiado animados. Deja solo respiración y un balanceo leve; nada de movimientos bruscos."*
- *"La selección vidrio no coincide: revisa gradiente, borde 1 px blanco 50 % y la línea de luz inferior."*
- *"El texto pierde legibilidad sobre el fondo: aumenta el degradado negro de la izquierda o la sombra del texto."*
- *"Revisa la pantalla en 1280×720, 1920×1080, 2560×1440 y 21:9 y corrige los anclajes."*
- *"Activa Reducir movimiento y confirma que se apagan grano, parpadeo, empujes de cámara y partículas."*
