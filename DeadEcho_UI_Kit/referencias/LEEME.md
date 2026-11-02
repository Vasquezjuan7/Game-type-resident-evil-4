# Imágenes de referencia (solo referencia visual, no son assets finales)

| Archivo | Uso | Animación que debe recrear el agente |
|---|---|---|
| `zombie_01_niebla.jpg` | Menú principal, toma 1 · panel derecho de Opciones › Gráficos | Empuje de cámara muy lento (escala 1.04→1.16 en 30 s), bancos de niebla blanca cruzando, ceniza cayendo. Zombie casi inmóvil, solo respira. |
| `zombie_02_bosque_noche.jpg` | Menú principal, toma 2 | Bosque de noche: luz de linterna/luna que titila de forma irregular, neblina baja que se mueve, el zombie "respira" (escala ±2 % en 5 s). |
| `zombie_03_fuego.jpg` | Menú principal, toma 3 | Calle en llamas: brillo naranja que palpita sobre el fondo, brasas subiendo, desenfoque de calor. Paneo lateral lento. |
| `pantalla_carga_referencia.png` | Pantalla de carga (usar `assets/fondo_carga_sin_logo.png` como fondo provisional) | Composición a seguir: cielo blanco nublado, ciudad en ruinas en blanco y negro, edificio central, logo arriba al centro. Nubes y niebla que se desplazan lento, ceniza cayendo. |
| `pausa_referencia.png` | Menú de pausa | Layout centrado, opción seleccionada en barra de vidrio. |
| `opciones_referencia.png` | Opciones | Solo la mitad izquierda (pestañas, secciones, filas ‹ valor ›, sliders). La derecha se reemplaza por el zombie en niebla. |
| `inventario_referencia.png` | Inventario | Cuadrícula, pestañas superiores, ficha de objeto con estadísticas. |

**Regla general:** los zombies NO deben verse animados ni caricaturescos. Movimientos mínimos (respiración, balanceo leve), cámara lenta, niebla, poca luz, grano de película y colores desaturados.

**En Unity:** recrear estas tomas con modelos 3D de zombies (Mixamo / Asset Store) y escenarios propios con post-proceso (URP Volume: Film Grain, Vignette, Depth of Field, Color Adjustments con saturación baja). Estas imágenes son de otro juego y tienen derechos de autor: no se pueden usar en el juego final, solo como guía visual.
