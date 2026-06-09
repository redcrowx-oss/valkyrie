---
trigger: model_decision
description: When adding ANY text that is visible in the User interface (ALWAYS check for any changes in folders unity\Assets\Scripts\QuestEditor or unity\Assets\Scripts\UI)
---

### Localization files
UI text MUST ALWAYS be localized. Do NOT use hardcoded strings for UI elements. Localization files are located in `Assets/StreamingAssets/text/`.
- `Localization.English.txt` is the master file.
- The format is `KEY,Value`.
- When adding new text:
1. Add the `KEY,English Value` to `Localization.English.txt`.
2. **CRITICAL**: Add a translated version `KEY,Translated Value` to *all* other relevant files (`Localization.German.txt`, `Localization.French.txt`, `Localization.Spanish.txt`, `Localization.Italian.txt`, `Localization.Chinese.txt`, `Localization.Czech.txt`, `Localization.Japanese.txt`, `Localization.Korean.txt`, `Localization.Polish.txt`, `Localization.Portuguese.txt`, `Localization.Russian.txt`, `Localization.Ukrainian.txt`) where the value is translated to the language specified in the filename **IMMEDIATELY**. You must not just copy the English text; you must provide a translation. Do not defer this task. Failing to do so will result in missing text for users of those languages.
3. In C# code, use `new StringKey("val", "KEY")` to reference the text.
4. For keys used inside a screen class (e.g. `QuestSelectionScreen`), declare a `private readonly StringKey` field at the top of the class alongside the other StringKey fields — do NOT inline `new StringKey(...)` at the call site. Example:
   ```csharp
   private readonly StringKey MY_KEY = new StringKey("val", "MY_KEY");
   ```
   Then use `ui.SetText(MY_KEY)` or `MY_KEY.Translate()` at the call site.
5. For commonly used keys, add a static reference in `Assets/Scripts/Content/CommonStringKeys.cs`.
6. **VERIFICATION**: Before finishing the task, use `find_by_name` or `list_dir` to list all `Localization.*.txt` files. Confirm that the new key has been added and translated to the respective file language to EACH file. Do not assume; verify.

### StringKey with parameters
When a translated string contains `{0}` placeholders (e.g. `Could not open "{0}".`):
- If constructing from a dict/key string: `new StringKey("val", "MY_KEY", someParam)` — sets parameters to `{0}:someParam`.
- If using a class-level `StringKey` field as template: `new StringKey(MY_KEY_FIELD, "{0}", someParam)` — uses the two-StringKey constructor which sets `parameters = "{0}:" + someParam`.
- For multi-part text that needs a `\n` between two separate keys, concatenate via `.Translate()`:
  ```csharp
  new StringKey(MY_KEY_FIELD, "{0}", param).Translate() + "\n" + OTHER_KEY.Translate()
  ```

### Line endings in localization files — CRITICAL
Line endings are **NOT uniform**: `Localization.English.txt` uses **CRLF (`\r\n`)**, but the other 12 files use **LF-only (`\n`)**. The parser (`DictionaryI18n.AddDataFromFile`) picks ONE delimiter for the whole file: `if (text.Contains('\r')) split('\r') else split('\n')`. So you MUST append using **the same line ending the target file already uses** — verify per file with `grep -q $'\r' <file>` (match → CRLF, no match → LF).

Introducing a single `\r` into an otherwise LF-only file is catastrophic: the parser flips to `split('\r')`, the entire LF body collapses into one un-parseable blob, that language ends up with no usable keys, and **the whole language silently falls back to English** (not just the edited lines). This is exactly what breaks "every language shows English".

**Rules:**
- Detect each file's existing ending and match it; never mix. The Edit tool preserves a file's endings, so it is safe. With a shell, branch on `grep -q $'\r'` and `printf` the matching terminator.
- After editing, verify: (a) `git diff --numstat` shows only your `+N -0` lines, and (b) the file's CR/LF class is UNCHANGED (English still CRLF, others still LF-only) — i.e. you did not introduce or remove a `\r`.
- Each non-English file has its own **localized** value for `QUEST_NAME_UPDATE` (e.g. `[Aktualisierung]` in German, `[Mise à jour]` in French). Do not assume the English value is shared — always grep for the actual key in each file before using it as an anchor.