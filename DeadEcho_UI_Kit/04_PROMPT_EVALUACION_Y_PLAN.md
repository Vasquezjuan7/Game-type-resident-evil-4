# Evaluación del proyecto y plan por fases

Este archivo lo sigue el agente ANTES de programar. Si se pega a mano, usar los prompts en orden.

## Paso 1 — Contexto del progreso actual (no modificar nada)
```
Antes de programar nada, analiza el estado actual del proyecto de Unity (solo lectura):
1. Versión de Unity, render pipeline (URP/HDRP/Built-in) y paquetes instalados (Input System, TextMeshPro,
   Cinemachine, Localization, DOTween, post-proceso).
2. Escenas existentes y cuál es la escena inicial en Build Settings.
3. Scripts existentes relacionados con: menús/UI, pausa, guardado, inventario, opciones/configuración,
   carga de escenas, input, audio, estado del juego. Para cada uno: qué hace y si está completo.
4. Prefabs, fuentes, sprites y modelos (personaje, zombies, escenarios) que ya existan y sirvan para las interfaces.
5. Convenciones del equipo: carpetas, namespaces, estilo de código, uso de git (rama actual).
6. Errores o advertencias actuales en la consola.
Entrégame un resumen corto en tablas: "Qué existe", "Qué se puede reutilizar", "Qué falta" y "Riesgos/conflictos"
comparando contra 03_ESPECIFICACION_FUNCIONAL.md y 01_DISENO_VISUAL.md.
```

## Paso 2 — Evaluación técnica de cómo hacerlo funcional
```
Con ese contexto, evalúa cómo implementar 03_ESPECIFICACION_FUNCIONAL.md en NUESTRO proyecto:
- ¿Qué arquitectura propones (adapta la sugerida a lo que ya existe, no dupliques sistemas)?
- ¿uGUI o UI Toolkit? Justifica según lo que ya usamos.
- ¿Cómo haremos las tomas cinemáticas: placeholders de imagen primero y luego Cinemachine + modelos 3D?
- ¿Qué paquetes hay que instalar y por qué?
- Lista de scripts, prefabs y ScriptableObjects a crear o modificar.
- Riesgos (rendimiento, input en mando, timeScale en pausa, guardado DEMO, fuentes con tildes).
No escribas código todavía.
```

## Paso 3 — Re-ordenar en fases
```
Propón un plan por fases ajustado al progreso actual. Orden sugerido (cámbialo si el proyecto lo pide):
  F0 Base: GameStateManager, UIRouter, InputModeWatcher, SettingsService, UITheme y prefabs base (V0).
  F1 Menú principal funcional (navegación + Salir + Créditos) con fondo placeholder.
  F2 Tomas cinemáticas y modo cine (V1, V2).
  F3 Pantalla de carga + SceneLoader (V3).
  F4 Nueva partida + SaveService DEMO (memoria falsa) + Continuar habilitado (V4).
  F5 Opciones completas aplicando valores reales (V5).
  F6 Pausa en escenas de juego (V6).
  F7 Inventario (V7).
  F8 Pulido: audio, transiciones, Reducir movimiento, resoluciones, QA (05_CHECKLIST_QA.md).
Para cada fase indica: objetivo, archivos que toca, cómo la pruebo en Play, y tiempo estimado.
Marca qué fases dependen de otras y cuáles podemos repartir entre los 3 integrantes.
Espera mi aprobación antes de empezar F0.
```

## Paso 4 — Ejecución
```
Ejecuta la fase [N] del plan aprobado. Usa el prompt visual correspondiente de 02_PROMPTS_VISUALES.md.
Al terminar: revisa la consola, haz commit en la rama de trabajo con un mensaje claro, y dime exactamente
qué debo probar en Play y qué debo asignar a mano en el Inspector.
```
