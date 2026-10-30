# Ideas de interfaces para Diseño de Interfaces (UI/UX)

Idea rectora: **interfaz diegética** — todo vive "dentro del mundo" (papel, radios, monitores CRT, libretas). Es un concepto fuerte para sustentar ante el profesor.

## Pantallas clave (prioridad alta)
1. **HUD mínimo diegético** — la vida no es una barra: el borde de la pantalla se mancha y el pulso suena más fuerte; la munición se ve en el arma al recargar. Sustento: reduce carga cognitiva e inmersión.
2. **Inventario en maletín (grid tipo Tetris)** — objetos con tamaño real; rotar con R; combinar arrastrando uno sobre otro. Estados claros: seleccionado, combinable (contorno), no válido (sacudida).
3. **Archivos del caso / tablero de pistas** — el jugador conecta pistas con hilos rojos (drag & drop). Al conectar bien, se desbloquea una deducción. Es la mecánica RPG convertida en interfaz.
4. **Mapa dibujado a mano** — habitaciones rojas = con objetos pendientes, azules = completadas, puertas con candado con icono del objeto que piden. Siempre con leyenda (no solo color).
5. **Árbol de habilidades del RPG** como **expediente médico** del personaje: cada mejora es una "inyección" o "informe" con antes/después de stats.
6. **Diálogo con decisiones** — temporizador sutil (mecha de vela que se consume), opciones con icono de tono (empatía / agresivo / mentir).
7. **Pantalla de guardado** — máquina de escribir + cinta de tinta como recurso limitado; cada slot muestra miniatura, ubicación, tiempo jugado y "% del caso".
8. **Pantalla de carga** — tips escritos en notas de papel + objeto 3D giratorio que se puede rotar con el mouse (el jugador no se aburre).
9. **Game Over** — "Expediente cerrado" con sello rojo; opciones Reintentar (seleccionada por defecto), Cargar, Menú.
10. **Opciones con vista previa en vivo** — calibrar brillo con el clásico "logo apenas visible"; cada opción muestra a su lado un ejemplo de qué cambia.

## Detalles que suben la nota
- **Sistema de diseño propio**: documentar paleta, tipografías, espaciado (grilla de 8 px), componentes (botón, slot, nota, modal) y sus estados. Entregarlo como lámina/Figma.
- **Microinteracciones**: cada componente con hover, presionado, deshabilitado, foco, error. Duraciones 150–300 ms.
- **Feedback multimodal**: visual + sonido + vibración del mando para cada acción importante.
- **Accesibilidad**: modo daltónico, tamaño de texto, subtítulos con nombre de hablante y fondo, reducir movimiento, reasignación de teclas, mantener vs. pulsar. Contraste AA (4.5:1).
- **Soporte de entrada adaptativo**: iconos que cambian entre teclado/Xbox/PlayStation al instante.
- **Onboarding sin texto largo**: enseñar controles con la primera nota que encuentra el personaje.
- **Consistencia**: misma ubicación de "Confirmar / Atrás" en todas las pantallas.

## Entregables sugeridos para la materia
1. **Moodboard** y justificación del estilo (survival horror + documental de investigación).
2. **User flow** completo: Arranque → Splash → Menú → Nueva partida → Juego → Pausa → Inventario/Mapa/Archivos → Game Over.
3. **Wireframes** baja fidelidad → **mockups** alta fidelidad → **prototipo** interactivo (Figma o el HTML de este kit).
4. **Guía de estilos / design system**.
5. **Pruebas de usabilidad** con 5 personas: tareas ("combina dos objetos", "encuentra la llave en el mapa"), medir tiempo, errores y SUS (System Usability Scale). Mostrar iteración antes/después.
6. **Heurísticas de Nielsen** aplicadas: una tabla mostrando cómo cada pantalla cumple cada heurística.
7. **Video demo** del attract mode y transiciones.
