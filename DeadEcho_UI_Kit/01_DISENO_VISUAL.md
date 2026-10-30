# Diseño visual — Dead Echo

Resolución de referencia **1920×1080** (Canvas Scaler: Scale With Screen Size, match 0.5). Las medidas de los HTML están en 1280×720: multiplicar ×1.5.

## Sistema visual

**Paleta (casi monocromática, cinematográfica)**

| Token | Hex | Uso |
|---|---|---|
| negro-fondo | `#0B0B0B` | Fondo de pantallas de sistema |
| hueso | `#ECE8DE` / `#E6E6E2` | Texto principal |
| gris-medio | `#9A968D` | Opciones no seleccionadas |
| gris-línea | `#2E2E2C` | Divisores de filas |
| sección | `#2C2C2B → #3A3A38` | Pestaña trapezoidal de secciones |
| vidrio | blanco 6–22 % + borde blanco 50 % + brillo inferior | Selección (pausa, opciones, nueva partida) |
| verde-ok | `#B9E4B0` | Munición cargada / durabilidad |
| fuego | `#FF7A1E` (solo como luz) | Toma 3 del menú |

**Tipografías (Google Fonts, licencia OFL)**
- **Special Elite** — logo y textos "de máquina de escribir".
- **Courier Prime** — opciones y textos del menú principal.
- **Rajdhani 500/600** — toda la interfaz de sistema (pausa, opciones, inventario, nueva partida, salir).
- **Oswald 700** — "DEAD" grande en la pantalla de carga.

**Logo:** "DEAD" en mayúsculas espaciadas + "echo" en minúscula de máquina de escribir sobre un trazo de pincel blanco. El "echo" deja dos copias que se desplazan a la derecha y se desvanecen (efecto eco, 3.2 s en loop).

**Selección "vidrio":** gradiente blanco translúcido, borde de 1 px blanco 50 %, brillo interior abajo y una línea de luz centrada. Animar la entrada en 0.15 s.

**Capas de lente (todas las pantallas cinemáticas):** grano de película (18 %), viñeta, parpadeo de luz cada 9 s, y degradado negro a la izquierda para legibilidad.

## Pantallas

### 01 · Menú principal (`pantallas/01_Main.html`)
- Izquierda: línea vertical rayada, línea con punto, logo, opciones **Continuar, Nueva partida, Opciones, Créditos, Salir**. Seleccionada = trazo de tinta negra + texto blanco en negrita.
- Abajo: descripción de la opción + "↵ Confirmar". Arriba derecha: nombre de usuario.
- Fondo: **3 tomas de 10 s en loop** con crossfade de 1.5 s (ver `referencias/LEEME.md`):
  1. Soldado en la niebla: empuje de cámara lento + niebla que cruza.
  2. Bosque de noche: respiración leve + linterna que titila + neblina baja.
  3. Calle en llamas: luz naranja que palpita + brasas subiendo + paneo lateral.
- Los zombies casi no se mueven. En Unity: modelos 3D con animación *idle* o *breathing*, cámara cinemática (Cinemachine dolly) y post-proceso.

### 02 · Modo cine (`02_Inactivo.html`)
Tras **20 s sin input**: la interfaz se desvanece (1.2 s), aparecen franjas negras arriba y abajo, logo pequeño y "PRESIONA CUALQUIER TECLA" pulsando. Las mismas tomas siguen a pantalla completa. Cualquier input regresa al menú con la misma opción seleccionada.

### 03 · Nueva partida (`03_NuevaPartida.html`)
Un solo personaje. Secciones: **Dificultad** (Narrativa / Superviviente / Pesadilla), **Ranura de memoria DEMO** (3 ranuras, botón *Guardar progreso* que asigna una memoria falsa), panel del protagonista con línea de escaneo, equipo inicial y efectos de la dificultad. Toast "Memoria falsa asignada". Botón **Comenzar →**.

### 04 · Carga (`04_Carga.html`)
Basada en `referencias/pantalla_carga_referencia.png`: cielo blanco nublado, ciudad en ruinas en blanco y negro, edificio al centro, logo "DEAD | echo |" arriba al centro. Animación: empuje lento (40 s), nubes y niebla desplazándose, ceniza cayendo. Abajo: consejo aleatorio + "CARGANDO" con icono girando.

### 05 · Opciones (`05_Opciones.html`)
Basada en la mitad izquierda de `referencias/opciones_referencia.png`. Pestañas Q/E: Controles, Cámara, HUD, Gráficos, Audio, Idioma, Accesibilidad. Secciones con pestaña trapezoidal, filas con `‹ valor ›` + indicador de segmentos, sliders con marcas, rombo ◆ si el valor cambió respecto al default, barra de scroll. Derecha: **zombie en la niebla** al estilo de la pantalla de carga (blanco y negro, niebla, ceniza, empuje lento). Pie: R Restablecer todo · T Restablecer categoría · ↵ Confirmar · Esc Volver.

### 06 · Pausa (`06_Pausa.html`)
Basada en `referencias/pausa_referencia.png`. Fondo del juego desenfocado y oscurecido, franjas de luz verticales en los bordes. Centrado: PAUSA, Reanudar, Reiniciar desde punto de control, Cargar partida, Tutoriales, Opciones, Salir al menú principal. Descripción abajo + controles.

### 07 · Inventario (`07_Inventario.html`)
Basado en `referencias/inventario_referencia.png`. Objetivo arriba izquierda, pestañas Mapa / **Objetos** / Documentos, contador arriba derecha, cuadrícula 10×7 con objetos de varios tamaños, numeración de filas, ficha del objeto (nombre + descripción) y estadísticas con barras. Pie: Ordenar, Mover/rotar, Usar/combinar, Cerrar.

### 08 · Créditos (`08_Creditos.html`)
"HECHO CON AMOR POR" + Juan Diego Gonzales Sánchez, Juan José Muñoz Jurado, Juan José Pérez Bravo. Las palabras caen desde arriba hasta el pie de página en loop (12 s por línea, desfase de 0.15 s entre palabras, 3 s entre líneas).

### 09 · Salir (`09_Salir.html`)
Modal centrado estilo pausa: "¿Salir de Dead Echo?", **No, volver** (seleccionado por defecto) y Sí, salir del juego.
