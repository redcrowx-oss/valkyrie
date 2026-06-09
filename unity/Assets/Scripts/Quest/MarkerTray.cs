using System.Collections.Generic;
using Assets.Scripts.Content;
using Assets.Scripts.UI;
using UnityEngine;

// On-screen tray to pull markers from and place them on the board.
// Investigators are the 5 fixed-colour circles; everything else (monsters and
// effects like Fire/Darkness/Breach) is a coloured square with a typed label.
public class MarkerTray
{
    // Top-left corner: MoM shows no heroes/morale there, so the whole left edge is free
    private const float ColumnX = 0.5f;
    private const float ToggleY = 0.5f;
    private const float SwatchSize = 1.4f;
    private const float SwatchStep = 1.7f;
    private const float ToggleWidth = 5f;

    // 5 investigator colours (red, green, blue, purple, orange)
    private static readonly string[] CircleColors =
        { "#E53935", "#43A047", "#1E88E5", "#8E24AA", "#FB8C00" };

    // White (first monster instance) + the game's monster-ID colours (green, gray, purple, yellow, red, blue)
    private static readonly string[] SquareColors =
        { "#FFFFFF", "#43A047", "#9E9E9E", "#8E24AA", "#FDD835", "#E53935", "#1E88E5" };

    private readonly StringKey TOKEN_TRAY = new StringKey("val", "TOKEN_TRAY");
    private readonly StringKey TOKEN_TRAY_PLACEHOLDER = new StringKey("val", "TOKEN_TRAY_PLACEHOLDER");

    private bool open = false;
    private UIElement toggle;
    private UIElementEditable textField;
    private readonly List<UIElement> panel = new List<UIElement>();

    public MarkerTray()
    {
        DrawToggle();
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

        float circlesY = ToggleY + 2.3f;
        for (int i = 0; i < CircleColors.Length; i++)
        {
            AddSwatch(Marker.CIRCLE, CircleColors[i], ColumnX + i * SwatchStep, circlesY);
        }

        float fieldY = circlesY + SwatchStep + 0.4f;
        textField = new UIElementEditable(Game.QUESTUI);
        textField.SetLocation(ColumnX, fieldY, SquareColors.Length * SwatchStep - 0.3f, 1.4f);
        textField.SetText("");
        textField.SetSingleLine();
        textField.SetPlaceholder(TOKEN_TRAY_PLACEHOLDER);
        new UIElementBorder(textField);
        panel.Add(textField);

        float squaresY = fieldY + 2.0f;
        for (int i = 0; i < SquareColors.Length; i++)
        {
            AddSwatch(Marker.SQUARE, SquareColors[i], ColumnX + i * SwatchStep, squaresY);
        }
    }

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

    private void Spawn(string type, string color)
    {
        string text = (type == Marker.SQUARE && textField != null) ? textField.GetText() : "";
        Game game = Game.Get();
        Vector3 camera = game.cc.gameObject.transform.position;
        Marker marker = new Marker(type, color, text, camera.x, camera.y);
        game.CurrentQuest.markers.Add(marker);
    }
}
