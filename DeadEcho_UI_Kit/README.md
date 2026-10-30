# Dead Echo — Kit de interfaces para Unity

Kit para que un agente de IA (Claude Code u otro con MCP) implemente en Unity todas las interfaces del juego: menú principal, modo cine, nueva partida, carga, opciones, pausa, inventario, créditos y salir.

## Qué hay en el zip

| Archivo / carpeta | Para qué sirve |
|---|---|
| `00_PROMPT_INICIAL.md` | **Empieza aquí.** Prompt corto para pegar en el chat del agente. |
| `01_DISENO_VISUAL.md` | Paleta, tipografías y diseño de cada pantalla con sus animaciones. |
| `02_PROMPTS_VISUALES.md` | Prompts para construir y ajustar visualmente cada pantalla. |
| `03_ESPECIFICACION_FUNCIONAL.md` | Qué hace cada botón, flujo entre pantallas, datos y arquitectura. |
| `04_PROMPT_EVALUACION_Y_PLAN.md` | Prompt para que el agente revise el proyecto actual y arme el plan de fases. |
| `05_CHECKLIST_QA.md` | Lista para aprobar cada pantalla. |
| `06_IDEAS_UIUX_MATERIA.md` | Ideas para la materia de Diseño de Interfaces. |
| `pantallas/*.html` | Referencia **animada** de cada pantalla. Ábrelas con Chrome. |
| `capturas/*.png` | Captura de cada pantalla (1280×720). |
| `referencias/` | Imágenes de referencia (zombies, pausa, opciones, inventario, carga) + `LEEME.md` con la animación de cada una. |
| `assets/fondo_carga_sin_logo.png` | Fondo provisional de la pantalla de carga (sin logo). |

> Las imágenes de `referencias/` y `assets/` son de otros juegos y tienen derechos de autor: sirven como guía y para mostrar el prototipo. En el juego final se reemplazan por escenas/modelos propios o gratuitos.

## Cómo ejecutarlo (paso a paso)

### 1. Requisitos
- Unity **2022.3 LTS o Unity 6**, proyecto **URP** recomendado.
- TextMeshPro (incluido en Unity) y el **Input System** nuevo.
- Opcional: DOTween (gratis en Asset Store) para animaciones.
- **Claude Code** (o Cursor / VS Code con un agente compatible con MCP).
- Python 3.10+ y `uv` (lo pide el servidor MCP de Unity).
- Git.

### 2. Conectar el agente a Unity (MCP)
1. Abrir el proyecto en Unity.
2. `Window > Package Manager > + > Add package from git URL…` y pegar la URL del servidor **MCP for Unity**: https://github.com/CoplayDev/unity-mcp (revisar su README, la URL exacta puede cambiar). Alternativa: https://github.com/CoderGamester/mcp-unity
3. En Unity abrir `Window > MCP for Unity` y pulsar **Auto-Setup / Configure** eligiendo **Claude Code**.
4. En una terminal, dentro de la carpeta del proyecto: `claude` para abrir Claude Code. Con `/mcp` se verifica que el servidor de Unity aparece conectado.
5. Prueba: pedir *"Lista los objetos de la escena actual"*. Si responde con la jerarquía, está listo.

### 3. Poner el kit dentro del proyecto
Descomprimir este zip en `Assets/_DeadEcho_UI_Kit/` (así el agente puede leer los `.md`, los HTML y las imágenes).

> Si usan git, agreguen `Assets/_DeadEcho_UI_Kit/referencias/` al `.gitignore` o no la suban a un repositorio público (imágenes con derechos de autor).

### 4. Empezar
Abrir `00_PROMPT_INICIAL.md`, copiar el prompt y pegarlo en Claude Code. El agente:
1. Revisa el estado actual del juego y te hace un resumen.
2. Propone un plan por fases adaptado a lo que ya existe (espera tu OK).
3. Implementa fase por fase, pidiéndote probar en Play después de cada una.

### 5. Ver las pantallas de referencia
Abrir cualquier archivo de `pantallas/` con Chrome (doble clic). Las animaciones se ven en vivo. Para que carguen las fuentes se necesita internet (Google Fonts).
