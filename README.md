# 💀 DEAD echo

> Proyecto académico de Diseño de Interfaces (UI/UX). Un juego de *survival horror* en primera persona inspirado en las mecánicas de *Resident Evil 4 Remake*.

---

## 📋 Datos Generales

*   **Equipo de Desarrollo:** Juan Diego Gonzales Sánchez, Juan José Muñoz Jurado y Juan José Pérez Bravo.
*   **Contexto Académico:** Proyecto para la materia de Diseño de Interfaces (UI/UX). Desarrollado a partir de un kit de diseño propio con especificaciones, prototipos HTML animados y una lista de comprobación de calidad.
*   **Rama de Trabajo UI:** `feature/ui-dead-echo`
*   **Motor Gráfico:** Unity 6 (6000.6) | Render Built-in | Nuevo Input System | TextMeshPro.
*   **Tamaño del Código:** ~116 scripts en C# y cerca de 23.700 líneas (comentado en español).
    *   🎮 **Juego:** 56 scripts, ~12.000 líneas.
    *   🖥️ **Interfaces Dead Echo:** 45 scripts, ~6.500 líneas.
    *   🛠️ **Herramientas de Editor:** 15 scripts, ~5.200 líneas.

---

## 📖 Historia: Capítulo 1 "El pueblo"

**Ambientación:** Valdeoscuro, un remoto pueblo rural del norte de España. Es de noche, la niebla es espesa y la única luz proviene de antorchas y hogueras.

*   **Protagonista:** Leon, agente del gobierno de EE. UU., guiado por radio por Ingrid.
*   **Misión:** Rescatar a Elena Ibarra (19 años), hija del embajador, desaparecida en el pueblo hace tres días.
*   **Mitología:** "El Padre", líder de "la Hermandad", bendijo el agua del pozo. Quien la bebe alberga "la semilla": pierden el miedo, sus ojos se vuelven rojos y obedecen a una voluntad común. Elena lleva una semilla aún inactiva.

### Desarrollo del Capítulo
1. Leon llega al pueblo y encuentra al testigo Tomás (pastor de 70 años).
2. Sobrevive al asedio de los aldeanos infectados.
3. Registra la iglesia y encuentra el colgante de Elena con una pista: *"Me llevan al castillo. Lo llaman el Padre"*.
4. Escapa por el portón del bosque. *(Continuará en el Capítulo 2: El castillo)*.

---

## 🎮 Mecánicas de Juego

*   **Armamento y Economía:** Amplia variedad de armas (pistolas, revólver, escopeta, subfusil, AK-47, lanzacohetes). El mercader (con su característico farol azul) permite comprar, vender, mejorar armas y usar **pesetas** como moneda.
*   **Sistema de Combate:** 
    *   **Cuerpo a cuerpo:** Cuchillo con durabilidad para *parry* (desvíos), coberturas y bajas en sigilo. Patadas a enemigos aturdidos.
    *   **Disparo Táctico:** Disparos a la cabeza aturden; disparos a las piernas tumban.
*   **Inventario (Maletín):** Cuadrícula de 12×8. Los objetos tienen varios tamaños, se pueden rotar, asignar a atajos rápidos y permite la fabricación (ej. pólvora + recursos = balas).
*   **Enemigos e IA:** Aldeanos con navegación inteligente que lanzan armas, esquivan apuntados y atacan en grupos coordinados (máximo 3 a la vez). Incluye asedios por oleadas, perros infectados y un jefe gigante (Tyrant / Sujeto "T").
*   **IA Conversacional:** El NPC Tomás responde en tiempo real a los inputs del jugador integrando la API de **Gemini**, adaptando su conocimiento y personalidad al contexto. (Usa fallback a textos escritos si no hay conexión).
*   **Audio y Voces:** Sistema Text-to-Speech (TTS) con caché para las voces de los personajes. Música dinámica (exploración/combate) y pasos procedurales según la superficie.

---

## 🖥️ Interfaces "Dead Echo" (Núcleo del Proyecto)

El sistema visual de menús y HUD ha sido el foco principal de este desarrollo, construido enteramente por código en tiempo de ejecución (cero escenas de interfaz espagueti).

### Diseño Visual
*   **Paleta Cinematográfica:** Negro `#0B0B0B`, Hueso `#ECE8DE`, Gris medio `#9A968D` y Naranja Fuego como acento lumínico.
*   **Tipografía:** *Special Elite* (Logo/Máquina de escribir), *Courier Prime* (Menús), *Rajdhani* (Sistema) y *Oswald* (Pantallas de carga).
*   **Componentes UI:** Selecciones de "vidrio", trazos de tinta, pestañas trapezoidales, deslizadores y filas de valor.
*   **Capas de Lente:** Grano de película, viñeteado, parpadeos de luz (cada 9s) y degradados laterales.
*   **Navegación:** Gestión mediante `UIRouter` con memoria de selección al retroceder.

### Fases de Implementación

| Fase | Pantalla | Descripción |
| :--- | :--- | :--- |
| **F0** | Base | Tema, fuentes (tildes/ñ), componentes reutilizables, SFX de UI y detección mando/teclado. |
| **F1** | Menú Principal | Continuar, Nueva partida, Opciones, Créditos, Salir (con safe-default en "No"). |
| **F2** | Modo Cine | 3 tomas 3D de fondo con fundidos cada 10s. Tras 20s de inactividad: franjas negras y *"Presiona cualquier tecla"*. |
| **F3** | Pantalla de Carga | Ciudad en ruinas, nubes, ceniza, consejo aleatorio (tiempo mínimo: 2s). |
| **F4** | Nueva Partida | 3 dificultades (daño x0.5 / x1 / x1.5), elección de ranura y ficha táctica de Leon. |
| **F5** | Opciones | 7 pestañas: Controles, Cámara, HUD, Gráficos, Audio, Idioma y Accesibilidad. |
| **F6** | Pausa | Fondo desenfocado, time-scale 0. Guardar, Cargar, Tutoriales, Opciones y Salir. |
| **F7** | Maletín | Pestañas (Mapa/Objetos/Documentos) y ficha con barras de estadísticas del jugador. |
| **F8** | Pulido | Accesibilidad, Live-Language, input adaptativo y QA. |

### ♿ Accesibilidad y UX (Opciones F5)
Las opciones se aplican **en tiempo real en el juego** y requieren confirmación (`Enter`), marcando los cambios sin guardar con un rombo (`◆`). 
*   **Visuales:** Modo daltónico (protanopía, deuteranopía, tritanopía vía shader), ajuste de tamaño de texto, desactivación de movimiento/grano/parpadeos.
*   **Juego:** Apuntado (mantener/pulsar), subtítulos con emisor, idioma dinámico (ES/EN) sin recargar.
*   **Input:** Cambio automático de iconos al alternar entre teclado/ratón y mando. Sonidos de feedback claros (hover, confirmación, error).

---

## 💾 Sistema de Guardado

El juego cuenta con un sistema de guardado real en 3 ranuras que persiste:
*   Posición, vida y armamento.
*   Estado exacto del maletín, pesetas y mejoras.
*   Objetivos, documentos, colgante y recolección de escenario (objetos únicos).
*   **Restricciones:** Fiel al *survival horror* clásico, no se puede guardar durante los asedios. 
*   **Métodos:** Autoguardado por objetivos (desactivado en dificultad Pesadilla) y manual en Menú de Pausa.

---

## ⚙️ Arquitectura Técnica

*   `GameStateManager`: Control centralizado de estados del juego y pausa.
*   `UIRouter` / `UIScreen`: Arquitectura de pila (stack) para las pantallas.
*   `InputModeWatcher`: Detección en vivo del periférico de entrada.
*   `SettingsService` & `GameplaySettingsBridge`: Serialización JSON de configuración y aplicación al juego sin acoplar dependencias.
*   `SaveService` / `GameSaveSystem`: Orquestación de guardado.
*   `SceneLoader`: Carga asíncrona real.
*   `ScreenGrade`: Shader personalizado para procesado de color, brillo, grano y daltonismo.
*   **Herramientas Custom Editor:** Menús propios (`RE4 > Construir pueblo completo` y `Dead Echo > Construir UI`) para agilizar el pipeline de desarrollo.

---

## 🚧 Retos y Soluciones

1.  **Rendimiento del Menú:** Las tomas cinematográficas se renderizan en texturas usando cámaras dedicadas con niebla independiente, sin necesidad de usar IA ni sobrecargar la escena.
2.  **Mantenibilidad de UI:** Hacer las interfaces 100% por código eliminó los conflictos de merge en Unity y facilitó la traducción dinámica.
3.  **Gestión de Audio sin Mixer:** El volumen por canales se calcula matemáticamente aplicando multiplicadores independientes a la música/voces sobre el volumen maestro.
4.  **Límites de IA:** Para evitar el bloqueo de la API gratuita de Gemini, se espaciaron las peticiones y se cachearon las respuestas de audio (TTS).
5.  **Estado del Mundo:** Restaurar partidas requirió asignar IDs únicos a objetos interactivos y rastrear recogibles por su posición exacta en el mundo.

---

## 🚀 Limitaciones y Trabajo Futuro

*   **Capítulo 2:** Expansión hacia "El Castillo" y el enfrentamiento con "El Padre".
*   **Input Actions:** Migrar la lectura directa de teclado para permitir la reasignación de teclas (Keybinding) de forma nativa.
*   **Persistencia Menor:** Actualmente no se guarda el estado de los enemigos vivos o las cajas de loot destructibles entre cargas.
*   **Localización:** Ampliar la traducción al inglés en los consejos de carga y el HUD de juego.
*   **Propiedad Intelectual:** Para una publicación comercial, se deberán reemplazar nombres propios (Leon, Ganado, Tyrant) propiedad de Capcom.

---
*Desarrollado con ❤️ y Unity para la clase de Diseño de Interfaces.*

