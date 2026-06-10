# Parche: Marcadores de tablero (Mansions of Madness)

Documentación de mantenimiento del fork privado **redcrowx-oss/valkyrie**, rama
`feature/mom-board-markers`. Esta función añade marcadores ("fichas") que el
jugador coloca y arrastra a mano sobre el tablero digital ya montado, para
seguir las posiciones de investigadores/monstruos y efectos (fuego, oscuridad,
brecha). Así una sola pantalla compartida sustituye a las losetas físicas.

Los marcadores son **"tontos"**: no llevan ninguna lógica de reglas (sin
adyacencia, sin pathfinding). Solo son formas de color que se arrastran. La
función es **exclusiva de MoM**; D2E no se toca.

> ⚠️ **Este fork no se subirá a upstream (NPBruce/valkyrie).** La función reduce
> la dependencia de los componentes físicos, lo que no encaja con la política
> del proyecto original. Se mantiene como fork privado.

---

## 1. Huella en el código *core* (para resolver conflictos al actualizar)

Estos son los **únicos** ficheros existentes que se modificaron. Cuando traigas
versiones nuevas desde upstream, los conflictos se concentrarán aquí. Cada
gancho es pequeño y aislado a propósito.

### `unity/Assets/Scripts/Game.cs` (+24)
- **Campo nuevo** `public Canvas markerCanvas;` (junto a `boardCanvas` /
  `tokenCanvas`). Es el lienzo donde cuelgan todas las fichas, un nivel por
  encima del de tokens.
- **En `Awake()`**, tras `tokenCanvas = GameObject.Find(...)`:
  `markerCanvas = CreateMarkerCanvas();` — crea el lienzo por código (no existe
  en la escena `Game.unity`, así no se toca la escena).
- **Método nuevo `CreateMarkerCanvas()`**: clona transform/renderMode/cámara del
  `tokenCanvas`, le pone `sortingOrder = tokenCanvas.sortingOrder + 1` y le añade
  un `GraphicRaycaster` (necesario para que el EventSystem reciba el arrastre).
- **En `QuestStartEvent()`**, tras crear el `NextStageButton`:
  `if (gameType is MoMGameType) { new MarkerTray(); }` — crea la bandeja solo en
  MoM, al empezar una partida nueva.

### `unity/Assets/Scripts/SaveManager.cs` (+6)
- **En el camino de carga**, tras recrear los botones de HUD
  (`SkillButton`/`InventoryButton`/`NextStageButton`):
  `if (game.gameType is MoMGameType) { new MarkerTray(); }` — recrea la bandeja
  al **cargar** una partida (el `QuestStartEvent` de `Game.cs` solo cubre partida
  nueva).

### `unity/Assets/Scripts/Quest/Quest.cs` (+26)
- **Campo nuevo** `public List<Marker> markers;` (junto a `List<Monster> monsters`).
- **Inicialización en 3 sitios** (mismo patrón que `monsters`), `markers = new List<Marker>();` en:
  1. el constructor de partida nueva,
  2. `ChangeQuest(...)`,
  3. `LoadQuest(...)`.
- **En `LoadQuest`**, tras el barrido `FindGameObjectsWithTag(Game.BOARD)` que
  limpia el tablero: `Marker.RemoveAll();` — borra las fichas viejas antes de
  reconstruir (no llevan tag `board`, así que la limpieza normal no las pilla).
- **En `LoadQuest`**, tras el bucle que carga los monstruos: barrido de secciones
  cuya clave empieza por `"Marker"` → `markers.Add(new Marker(kv.Value));`. Se
  escanean igual que Hero/Monster, porque las fichas **no** tienen respaldo en
  `QuestData.components` y no pueden ir en la lista `[Board]`.
- **En `ToString()`** (serialización del guardado), tras escribir los monstruos:
  ```csharp
  int markerId = 0;
  foreach (Marker m in markers) { r += m.ToString(markerId++); }
  ```
  Genera secciones INI `[Marker0]`, `[Marker1]`, …

### `unity/Assets/Scripts/Destroyer.cs` (+3)
- **En `Destroy()`**, justo tras `Game game = Game.Get();`:
  `Marker.RemoveAll();` — al salir del escenario, borra todas las fichas
  colocadas (si no, se quedaban en pantalla).

> **Compatibilidad de guardados:** un save antiguo simplemente no tiene secciones
> `[MarkerN]`, así que el barrido no encuentra nada y la lista queda vacía. No se
> rompe ningún formato existente.

---

## 2. Ficheros nuevos

Los tres viven en `unity/Assets/Scripts/Quest/` (con sus `.meta`).

- **`Marker.cs`** — la clase del marcador. Campos: `type` (`circle`/`square`),
  `color` (nombre o `#RRGGBB`), `text`, `posX`, `posY`, y el `GameObject` de
  Unity. Dibuja la ficha en `markerCanvas`: un borde negro (forma) con un relleno
  de color algo más pequeño encima (contraste) y, en los cuadrados con texto, una
  etiqueta `TextMeshProUGUI` con auto-tamaño y color negro/blanco según
  luminancia. Sprites procedurales en caché (`GetCircleSprite`/`GetSquareSprite`,
  expuestos vía `ShapeSprite` para reutilizarlos en la bandeja). Incluye:
  constructor desde datos de guardado, `ToString(int id)` (sección INI),
  `Remove()` (quita de la lista + destruye), y el estático **`RemoveAll()`**
  (destruye los hijos del `markerCanvas` y vacía la lista; protegido contra
  nulos).
- **`MarkerDrag.cs`** — `MonoBehaviour` que se engancha a cada ficha. Implementa
  `IBeginDragHandler`/`IDragHandler` (arrastre con botón izquierdo, vía
  `cc.GetMouseBoardPlane()` + offset de agarre) y `IPointerClickHandler` (botón
  derecho → `marker.Remove()`). **Clave:** como la ficha **no** lleva el tag
  `Game.BOARD`, al pasar el ratón por encima `CameraController.ScrollEnabled()`
  devuelve `false`, así que arrastrar una ficha nunca mueve/zoomea el tablero.
- **`MarkerTray.cs`** — la bandeja en pantalla (esquina superior izquierda, libre
  en MoM porque no muestra héroes ni moral ahí). Un botón que despliega/oculta el
  panel: una fila de 5 círculos de color (investigadores), un campo de texto
  editable, y una fila de 7 cuadrados de color. Escribes una etiqueta y pulsas un
  color → aparece la ficha. Textos localizados (`TOKEN_TRAY`,
  `TOKEN_TRAY_PLACEHOLDER`).

### Localización
Se añadieron las claves `TOKEN_TRAY` (es: "Fichas") y `TOKEN_TRAY_PLACEHOLDER`
(es: "Nombre / efecto") a los **13** ficheros `Assets/StreamingAssets/text/Localization.*.txt`.

> ⚠️ **Finales de línea — crítico.** Solo `Localization.English.txt` usa **CRLF**;
> los otros 12 usan **solo LF**. El parser elige UN delimitador por fichero
> (`if (text.Contains('\r')) split('\r') else split('\n')`). Meter un solo `\r`
> en un fichero LF colapsa todo el idioma y cae a inglés. Al editar, respeta
> SIEMPRE el final de línea que ya tiene cada fichero. (Ver
> `.agent/rules/text-localization.md`, corregida en este parche.)

---

## 3. Build de las librerías nativas en Linux/Fedora (desde cero)

Unity (2019.4.41f1) solo compila `unity/Assets/`. Las librerías auxiliares de
`libraries/` se compilan aparte; en **Release** su `OutputPath` apunta a
`unity/Assets/Plugins/` (carpeta *gitignored*), así que las DLL caen directas ahí.
Sin ellas → ~57 errores `CS0246` en la consola de Unity (`IniData` vive en
**ValkyrieTools**; `FFGImport`/`FFGAppImport` en **FFGAppImport**).

Esta máquina **no** tiene `msbuild` ni `csc` de Roslyn nativos. Tiene Mono 6.14
(`xbuild`, `mcs`) y `dotnet-sdk-8.0`. Unity está en
`/home/rcx/Unity/Hub/Editor/2019.4.41f1/Editor/Data/Managed`.

### Paquetes necesarios
```bash
sudo dnf install GConf2 mono-devel dotnet-sdk-8.0
```
(`GConf2` lo pide Unity/Mono en runtime; `mono-devel` trae `xbuild`;
`dotnet-sdk-8.0` trae el compilador Roslyn moderno.)

### Paso A — ValkyrieTools (con xbuild; no usa C# moderno)
```bash
UNITY_MANAGED=/home/rcx/Unity/Hub/Editor/2019.4.41f1/Editor/Data/Managed
xbuild libraries/ValkyrieTools/ValkyrieTools.csproj \
  /p:Configuration=Release /p:NoWarn=0108 \
  /p:TargetFrameworkVersion=v4.5 \
  "/p:ReferencePath=$UNITY_MANAGED"
```

### Paso B — FFGAppImport (con dotnet/Roslyn; usa C# 7 *pattern matching*)
`mcs` de Mono no compila *pattern matching* (lanza
`NotImplementedException: type pattern matching`). Hay que usar Roslyn vía
`dotnet`, apuntando a los *reference assemblies* de Mono con
`FrameworkPathOverride`. Este comando **también** reconstruye ValkyrieTools por
`ProjectReference`:
```bash
dotnet build libraries/FFGAppImport/FFGAppImport.csproj -c Release \
  -p:TargetFrameworkVersion=v4.5 -p:FrameworkPathOverride=/usr/lib/mono/4.5-api \
  -p:NoWarn=0108 -p:DebugType=none -p:DebugSymbols=false \
  "-p:ReferencePath=/home/rcx/Unity/Hub/Editor/2019.4.41f1/Editor/Data/Managed%3B/home/rcx/valkyrie/unity/Assets/Plugins"
```

**Detalles que hay que respetar:**
- Los `.csproj` referencian `UnityEngine.dll` con una ruta Windows fija →
  `-p:ReferencePath` la redirige (va antes del HintPath roto, sin editar ficheros).
- Las deps de NuGet (System.Buffers, System.Memory, K4os.LZ4, …) ya están
  *vendored* en `Plugins/`, por eso se añade esa carpeta a ReferencePath y **se
  salta NuGet por completo** (nuget.exe no arranca en este Mono).
- Mono no tiene perfil v3.5 real → se targetea **v4.5**.
- Varias rutas en ReferencePath se unen con `%3B` (un `;` literal lo parte
  MSBuild en switches distintos).
- **NO** compiles `libraries.sln` entero: `Injection`/`MoMInjection` apuntan a
  rutas de Steam que aquí no existen.

### Paso C — Limpieza post-build OBLIGATORIA
`dotnet` copia el `UnityEngine.dll` referenciado dentro del OutputPath
(`Plugins/`). Si se queda, provoca ~971 errores `CS0433` ("el tipo existe en
UnityEngine.CoreModule y en UnityEngine") en Unity. Bórralo **cada vez**:
```bash
rm -f unity/Assets/Plugins/UnityEngine.dll unity/Assets/Plugins/UnityEngine.dll.meta
```
(Está *gitignored*; el `build.ps1` oficial hace el mismo `Remove-Item`.) En
`Plugins/` deben quedar solo las **9** DLL reales: ValkyrieTools, FFGAppImport +
las 7 originales (Ionic.Zip.Unity, K4os.Compression.LZ4, System.Buffers,
System.IO.Compression, System.Memory, System.Numerics.Vectors,
System.Runtime.CompilerServices.Unsafe).

### Error benigno
Al arrancar verás `Loading assembly failed: Assets/Plugins/System.IO.Compression.dll`.
Es una de las 7 DLL **originales** del repo (idéntica byte a byte a git, no la
regenera el build): una *facade* compilada en Windows que Mono no sabe parsear.
Su `.meta` tiene el Editor desactivado. **Es inofensivo** — el modo Play funciona.

### Tras el build
Enfoca Unity (o *Assets ▸ Reimport All*) para que recompile. Es un *setup* único;
solo se repite si cambian las librerías.

---

## 4. Generar el ejecutable (Linux x86_64)

1. **Instala el módulo de soporte** *Linux Build Support (Mono/IL2CPP)* para
   2019.4.41f1 desde Unity Hub (*Installs ▸ ⋯ ▸ Add Modules*).
2. En Unity: **File ▸ Build Settings**.
3. Selecciona la plataforma **Linux** y pulsa *Switch Platform* si hace falta.
4. *Target Platform* = **Linux**, *Architecture* = **x86_64**.
5. Asegúrate de que la escena `Game.unity` (la única) está en la lista de
   *Scenes In Build*.
6. **Build** y elige la carpeta de salida. Genera el binario y la carpeta
   `*_Data` que hay que distribuir juntos.

> Requisito previo: las librerías nativas del §3 ya deben estar compiladas en
> `Plugins/`, o el build fallará igual que la compilación del editor.
