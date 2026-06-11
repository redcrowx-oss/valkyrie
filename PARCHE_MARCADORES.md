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
  `cc.GetMouseBoardPlane()` + offset de agarre) y `IPointerClickHandler` para el
  borrado. **Clave:** como la ficha **no** lleva el tag `Game.BOARD`, al pasar el
  ratón por encima `CameraController.ScrollEnabled()` devuelve `false`, así que
  arrastrar una ficha nunca mueve/zoomea el tablero.
  **Borrado (con confirmación):**
  - **Doble toque** (táctil): dos toques sobre el mismo marcador dentro de
    `DoubleTapWindow` (0,3 s) y cerca en pantalla (tolerancia = 5% de la altura),
    **sin arrastre entre medias**. Doble protección anti-accidente: (a)
    `OnPointerClick` solo se dispara en toques que NO fueron arrastre (el
    EventSystem invalida el click al superar el umbral de drag), y (b)
    `OnBeginDrag` resetea el primer toque pendiente (`lastTapTime = -1`), así
    *toque → arrastrar → toque* nunca borra. Reposicionar a toquecitos es seguro.
  - **Clic derecho** (ratón/escritorio/editor): se mantiene.
  - Ambos caminos abren un modal **Confirmar / Borrar (rojo) / Cancelar** antes de
    eliminar (mismo patrón que `EditorComponent.Delete()`), reutilizando las claves
    ya localizadas `CONFIRM`/`DELETE`/`CANCEL` de `CommonStringKeys` — **sin tocar
    ficheros de localización**. El diálogo se cierra con `Destroyer.Dialog()`.
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

---

## 5. Firebase desactivado (build de Android con package propio)

Valkyrie usa Firebase **solo para Crashlytics** (reporte de fallos), atado al
proyecto Firebase de upstream `valkyrie-59b81` vía `google-services.json` con el
package `com.bruce.valkyrie`. **No afecta al gameplay** en absoluto (Analytics se
incluye pero no se usa; las estadísticas van por Google Forms en `StatsManager.cs`).

Para un APK personal con package propio (p. ej. `com.rcx.valkyrie`), Unity pide un
"valid Bundle ID" porque el plugin de Firebase valida que coincida con
`google-services.json`. Además, `DebugManager.Enable()` llamaba a
`FirebaseApp.Create()` **sin try/catch** desde `Game.Awake()` (`Game.cs:179`), así
que un mismatch de package podía romper el arranque.

**Solución aplicada (nivel 1, quirúrgico):** se neutralizó el cuerpo de
`DebugManager.Enable()` en `unity/Assets/Scripts/DebugManager.cs` (comentado, no
borrado, para revertir fácil). Ya no se llama a Firebase ni se suscribe el captador
de logs `HandleLog`, así que `Crashlytics.Log` nunca se ejecuta. `Game.cs` sigue
llamando a `Enable()`, pero ahora es un no-op → sin riesgo de runtime y el package
name queda libre.

- Los AAR de Firebase siguen en `Plugins/Android/` (inertes); como mucho un AAR
  se auto-inicializa vía ContentProvider y deja un *warning* en el log, inofensivo.
- **No** hace falta cambiar a `com.bruce.valkyrie`; puedes usar tu propio package.
- **Re-activar:** restaura tu propio `google-services.json` y descomenta el cuerpo
  de `Enable()`.
- **Nivel 2 (limpieza total, no aplicado):** quitar los paquetes Firebase de
  `unity/Packages/manifest.json`, borrar los AAR de `Plugins/Android/` y el
  `google-services.json`. Elimina el diálogo de Unity por completo, pero toca más
  superficie.

---

## 6. V2 — Autopoblado de la bandeja (rama `feature/mom-markers-v2`)

La V2 hace que la bandeja **se rellene sola** con los investigadores y monstruos
en partida, en lugar de ofrecer colores fijos. Desarrollada en bloques; el
**Bloque 3** (lógica) ya está implementado y probado. Se parte de
`feature/mom-board-markers` (la V1, en producción) para no tocarla.

### Pieza central: `GameStateReader.cs` (adaptador único)
- Fichero nuevo en `unity/Assets/Scripts/Quest/`. Es el **único** punto del código
  de marcadores que lee clases internas de Valkyrie (`Quest`, `Quest.Hero`,
  `Quest.Monster`, `ContentData`…). `Marker`/`MarkerTray` hablan **solo** con él.
- Expone DTOs propios (`InvestigatorEntry`, `MonsterEntry`) — ningún tipo de
  Valkyrie se filtra a la UI. Si una versión futura de NPBruce renombra un campo o
  cambia cómo se resuelven texturas, **se parchea solo este fichero**.
- `GetActiveInvestigators()`: héroes con `heroData != null && !defeated`.
- `GetActiveMonsters()`: cada instancia de `CurrentQuest.monsters`.

### Color de investigador determinista (clave para la persistencia)
El color se deriva del **rango del héroe al ordenar por `hero.id`** (entero estable,
persistido en el save como `id=`) sobre el roster completo (derrotados incluidos,
para que el slot **no se desplace** si alguien cae). Nunca del orden de creación de
las fichas ni de la posición en la lista (que se reconstruye en load). En carga se
recomputa el mismo orden → mismo color → mapeo reproducible entre sesiones.

### Exclusividad bandeja ↔ tablero (estado derivado, §5.3 del brief)
- Regla: **bandeja = (entidades activas) − (ya colocadas en el tablero)**.
- La identidad de cada entidad va en el campo **`text`** del marcador (reutilizado,
  **cero claves nuevas** en el save):
  - Investigador: `text = heroData.sectionName`; `type = "investigator"` (forma
    círculo; el `text` no se pinta, identidad invisible).
  - Monstruo: `text = GetIdentifier()` (`"section:duplicate"`); `type = "monster"`
    (forma cuadrado).
  - Saves viejos solo traen `type = circle/square` → cargan igual (retrocompatible).
- `MarkerTray.DrawPanel()` calcula los "ya colocados" leyendo `CurrentQuest.markers`
  por `type`+`text`. Como la identidad se persiste en `text`, **la exclusividad
  sobrevive a guardar/cargar**.

### Refresco de la bandeja (sin ganchos en código de NPBruce)
- La bandeja se reconstruye **solo en eventos nuestros**: al abrir el panel y tras
  colocar (`SpawnEntity` → `DrawPanel`) o borrar (`Marker.Remove()` →
  `MarkerTray.NotifyBoardChanged()`) un marcador.
- **Decisión explícita:** NO se engancha nada en el código de Valkyrie (cada hook es
  un punto de rotura en futuros merges). Si la bandeja abierta se queda
  desactualizada al morir/spawnear un monstruo, el usuario la cierra y la reabre.

### Decisiones de diseño conocidas (no son bugs)
- **Respawn con marcador huérfano:** si un monstruo muere con su marcador aún en el
  tablero y luego aparece otra instancia con el **mismo `GetIdentifier()`**, la
  bandeja la verá como "ya colocada" y no la ofrecerá. Solución: el usuario borra el
  marcador viejo. Aceptado.
- **Monstruo muerto:** desaparece de la bandeja (ya no está activo) pero **su
  marcador en el tablero NO se toca** — lo borra el usuario a mano. El motor jamás
  toca el tablero.

### Huella en el core de la V2
- **Cero** ficheros nuevos de Valkyrie modificados respecto a la V1. Solo cambian
  ficheros nuestros: `GameStateReader.cs` (nuevo), `Marker.cs` (nuevos *valores* de
  `type`, sin claves nuevas; aviso a la bandeja en `Remove()`), `MarkerTray.cs`
  (autopoblado + exclusividad). El save mantiene exactamente las mismas claves
  (`type/color/text/posX/posY`).

### Capa estética (Bloque 4)
Resuelve el arte **a través de `GameStateReader`**, con caché por identidad (no
relee disco ni reescanea contenido en cada refresco de la bandeja) y **fallback** a
los gráficos planos del Bloque 3 si falta o no es legible una textura.
- **Resolución por TIPO, no por instancia viva:** `GetMonsterImage(identifier)` y
  `GetInvestigatorPortrait(sectionName)` buscan el `MonsterData`/`HeroData` iterando
  `cd.Values<T>()` por `sectionName` (patrón del repo). Consecuencia clave: el arte
  de un monstruo **sobrevive a su muerte** y al guardar/recargar (el tipo sigue en
  `ContentData` aunque la instancia ya no esté en `CurrentQuest.monsters`).
- **Identificador** parseado por **`LastIndexOf(':')`** (un `sectionName` con dos
  puntos se conserva).
- **Investigador:** token circular cacheado = aro del color asignado + retrato
  recortado en círculo (bake procedural por pixel en `Marker.BuildInvestigatorToken`
  / `ComposeCircularToken`, con guardia `try/catch` → si la textura no es legible,
  fallback). El mismo token se usa en bandeja y tablero (consistencia).
- **Monstruo:** arte (`preserveAspect`, todo el rect sigue siendo zona de toque) +
  badge de duplicado en la esquina (`Resources/Sprites/monster_duplicate_N`;
  `duplicate 0 = sin badge`, como el físico y `MonsterCanvas`).

### Estado por bloques
- **Bloque 3 (hecho, probado):** `GameStateReader` + autopoblado de investigadores y
  monstruos + exclusividad. Mecánica validada con gráficos provisionales.
- **Bloque 4 (hecho, probado PC + Android):** capa estética — retratos con aro y
  arte de monstruo con badge de duplicado, vía `GameStateReader` con caché y
  fallback. Probado: fallback forzado con textura inexistente y arte de monstruo
  muerto que sobrevive a guardar+recargar.
- **Bloque 5 (pendiente):** fichas de efecto (Fuego/Oscuridad en base;
  Brecha=`TokenRift`, Agua, Escombros en expansiones — con comprobación de pack en
  runtime y fallback) y comodín de objetos (icono horneado en carta → fallback de
  cuadrado + texto).
