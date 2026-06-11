using System.Collections.Generic;
using Assets.Scripts.Content;
using Assets.Scripts.UI;
using UnityEngine;

// On-screen tray. Auto-populates from live game state via GameStateReader:
//  - one circle per investigator in play (its deterministic assigned colour),
//  - one square per live monster instance,
// minus whatever is already placed on the board (exclusivity, see brief 5.3).
// A free-text "wildcard" square row covers effects, items and anything else.
//
// Block 3 uses the plain shapes (no portraits / monster art yet) to validate the
// logic before the art layer. The tray talks ONLY to GameStateReader for game state.
public class MarkerTray
{
    // Top-left corner: MoM shows no heroes/morale there, so the whole left edge is free
    private const float ColumnX = 0.5f;
    private const float ToggleY = 0.5f;
    private const float SwatchSize = 1.4f;
    private const float SwatchStep = 1.7f;
    private const float ToggleWidth = 5f;
    private const int MaxPerRow = 7;

    // Wildcard square palette: white + the monster-ID colours (green, gray, purple, yellow, red, blue)
    private static readonly string[] SquareColors =
        { "#FFFFFF", "#43A047", "#9E9E9E", "#8E24AA", "#FDD835", "#E53935", "#1E88E5" };

    private readonly StringKey TOKEN_TRAY = new StringKey("val", "TOKEN_TRAY");
    private readonly StringKey TOKEN_TRAY_PLACEHOLDER = new StringKey("val", "TOKEN_TRAY_PLACEHOLDER");

    // The single live tray, so a board change (e.g. a marker removed from the board)
    // can ask it to refresh. Updated whenever a tray is built (quest start / load).
    private static MarkerTray instance;

    private bool open = false;
    private UIElement toggle;
    private UIElementEditable textField;
    private readonly List<UIElement> panel = new List<UIElement>();

    public MarkerTray()
    {
        instance = this;
        DrawToggle();
    }

    // Rebuild the open panel after one of OUR actions changed the board (a marker
    // placed or removed). No hooks into Valkyrie code (brief 6, pt 3): if a monster
    // dies while the panel is open, the user closes and reopens it to refresh.
    public static void NotifyBoardChanged()
    {
        if (instance != null && instance.open)
        {
            instance.DrawPanel();
        }
    }

    private void DrawToggle()
    {
        Game game = Game.Get();
        toggle = new UIElement(Game.QUESTUI);
        toggle.SetLocation(ColumnX, ToggleY, ToggleWidth, 2);
        toggle.SetText(TOKEN_TRAY);
        toggle.SetFont(game.gameType.GetHeaderFont());
        toggle.SetFontSize(UIScaler.GetMediumFont());
        toggle.SetButton(Toggle);
        new UIElementBorder(toggle);
    }

    private void Toggle()
    {
        open = !open;
        if (open)
        {
            DrawPanel();
        }
        else
        {
            ClearPanel();
        }
    }

    private void ClearPanel()
    {
        foreach (UIElement element in panel)
        {
            element.Destroy();
        }
        panel.Clear();
        textField = null;
    }

    private void DrawPanel()
    {
        ClearPanel();
        Game game = Game.Get();

        // Entities already on the board (exclusivity). Read from our own marker list,
        // matching the identity we stored in `text` when the marker was placed.
        HashSet<string> placedInvestigators = new HashSet<string>();
        HashSet<string> placedMonsters = new HashSet<string>();
        foreach (Marker m in game.CurrentQuest.markers)
        {
            if (m.type == Marker.INVESTIGATOR) placedInvestigators.Add(m.text);
            else if (m.type == Marker.MONSTER) placedMonsters.Add(m.text);
        }

        float y = ToggleY + 2.3f;

        // Investigators in play, not yet on the board (one row of coloured circles)
        int col = 0;
        foreach (GameStateReader.InvestigatorEntry e in GameStateReader.GetActiveInvestigators())
        {
            if (placedInvestigators.Contains(e.id)) continue;
            AddInvestigatorSwatch(e, ColumnX + col * SwatchStep, y);
            col++;
        }
        if (col > 0) y += SwatchStep + 0.5f;

        // Live monster instances, not yet on the board (wraps every MaxPerRow)
        col = 0;
        int row = 0;
        foreach (GameStateReader.MonsterEntry e in GameStateReader.GetActiveMonsters())
        {
            if (placedMonsters.Contains(e.id)) continue;
            AddMonsterSwatch(e, ColumnX + col * SwatchStep, y + row * SwatchStep);
            col++;
            if (col >= MaxPerRow) { col = 0; row++; }
        }
        int monsterRows = row + (col > 0 ? 1 : 0);
        if (monsterRows > 0) y += monsterRows * SwatchStep + 0.5f;

        // Effect tokens: a flat row, always offered (unlimited, no exclusivity).
        // Kept as its own pass so switching to an "Effects +" accordion later is
        // purely presentational (the spawn path does not change).
        List<GameStateReader.EffectEntry> effects = GameStateReader.GetEffects();
        for (int i = 0; i < effects.Count; i++)
        {
            AddEffectSwatch(effects[i], ColumnX + i * SwatchStep, y);
        }
        if (effects.Count > 0) y += SwatchStep + 0.5f;

        // Wildcard: free-text label + colour squares (unchanged behaviour)
        textField = new UIElementEditable(Game.QUESTUI);
        textField.SetLocation(ColumnX, y, SquareColors.Length * SwatchStep - 0.3f, 1.4f);
        textField.SetText("");
        textField.SetSingleLine();
        textField.SetPlaceholder(TOKEN_TRAY_PLACEHOLDER);
        new UIElementBorder(textField);
        panel.Add(textField);
        y += 2.0f;

        for (int i = 0; i < SquareColors.Length; i++)
        {
            AddSwatch(Marker.SQUARE, SquareColors[i], ColumnX + i * SwatchStep, y);
        }
    }

    // Investigator swatch: the same circular portrait token as the board marker,
    // or a colour circle if the portrait is missing/unreadable (fallback).
    private void AddInvestigatorSwatch(GameStateReader.InvestigatorEntry e, float x, float y)
    {
        UIElement ui = new UIElement(Game.QUESTUI);
        ui.SetLocation(x, y, SwatchSize, SwatchSize);

        Texture2D token = (e.portrait != null)
            ? Marker.BuildInvestigatorToken(e.id + "|" + e.colorHex, e.portrait, ColorUtil.ColorFromName(e.colorHex))
            : null;
        if (token != null)
        {
            ui.SetImage(token);
        }
        else
        {
            ui.SetImage(Marker.ShapeSprite(Marker.INVESTIGATOR));
            ui.SetBGColor(ColorUtil.ColorFromName(e.colorHex));
        }
        ui.SetButton(delegate { SpawnEntity(Marker.INVESTIGATOR, e.colorHex, e.id); });
        panel.Add(ui);
    }

    // Monster swatch: its art with the duplicate badge in the corner, or a colour
    // square + name label if the art is missing (fallback).
    private void AddMonsterSwatch(GameStateReader.MonsterEntry e, float x, float y)
    {
        UIElement ui = new UIElement(Game.QUESTUI);
        ui.SetLocation(x, y, SwatchSize, SwatchSize);
        if (e.image != null)
        {
            ui.SetImage(e.image);
        }
        else
        {
            ui.SetImage(Marker.ShapeSprite(Marker.MONSTER));
            ui.SetBGColor(ColorUtil.ColorFromName(e.colorHex));
            ui.SetText(e.name, Color.black);
        }
        ui.SetButton(delegate { SpawnEntity(Marker.MONSTER, e.colorHex, e.id); });
        new UIElementBorder(ui);
        panel.Add(ui);

        // Duplicate badge overlay in the bottom-right corner (a small dead spot for
        // the spawn button, but the rest of the swatch stays clickable).
        if (e.badge != null)
        {
            UIElement badge = new UIElement(Game.QUESTUI);
            badge.SetLocation(x + SwatchSize * 0.55f, y + SwatchSize * 0.55f, SwatchSize * 0.45f, SwatchSize * 0.45f);
            badge.SetImage(e.badge);
            panel.Add(badge);
        }
    }

    // Effect swatch: its token art, or a colour square if the art/pack is missing
    private void AddEffectSwatch(GameStateReader.EffectEntry e, float x, float y)
    {
        UIElement ui = new UIElement(Game.QUESTUI);
        ui.SetLocation(x, y, SwatchSize, SwatchSize);
        if (e.image != null)
        {
            ui.SetImage(e.image);
        }
        else
        {
            ui.SetImage(Marker.ShapeSprite(Marker.EFFECT));
            ui.SetBGColor(ColorUtil.ColorFromName(e.colorHex));
        }
        ui.SetButton(delegate { SpawnEffect(e.id, e.colorHex); });
        new UIElementBorder(ui);
        panel.Add(ui);
    }

    // Wildcard colour square (free-text label taken from the field)
    private void AddSwatch(string type, string color, float x, float y)
    {
        UIElement ui = new UIElement(Game.QUESTUI);
        ui.SetLocation(x, y, SwatchSize, SwatchSize);
        ui.SetImage(Marker.ShapeSprite(type));
        ui.SetBGColor(ColorUtil.ColorFromName(color));
        ui.SetButton(delegate { Spawn(type, color); });
        if (type == Marker.SQUARE)
        {
            new UIElementBorder(ui);
        }
        panel.Add(ui);
    }

    // Place an auto-populated entity marker, then refresh so it drops out of the tray
    private void SpawnEntity(string type, string colorHex, string identity)
    {
        Game game = Game.Get();
        Vector3 camera = game.cc.gameObject.transform.position;
        Marker marker = new Marker(type, colorHex, identity, camera.x, camera.y);
        game.CurrentQuest.markers.Add(marker);
        DrawPanel();
    }

    // Place an effect marker (catalogue key in `text`). Unlimited and outside
    // exclusivity, so no tray refresh is needed.
    private void SpawnEffect(string effectId, string colorHex)
    {
        Game game = Game.Get();
        Vector3 camera = game.cc.gameObject.transform.position;
        Marker marker = new Marker(Marker.EFFECT, colorHex, effectId, camera.x, camera.y);
        game.CurrentQuest.markers.Add(marker);
    }

    // Place a wildcard square with the free-text label
    private void Spawn(string type, string color)
    {
        string text = (textField != null) ? textField.GetText() : "";
        Game game = Game.Get();
        Vector3 camera = game.cc.gameObject.transform.position;
        Marker marker = new Marker(type, color, text, camera.x, camera.y);
        game.CurrentQuest.markers.Add(marker);
    }
}
