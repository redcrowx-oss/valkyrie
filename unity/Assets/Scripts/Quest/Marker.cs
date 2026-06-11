using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A "dumb" board marker the player places by hand to track investigators,
// monsters and effects. It carries no game rules: just a coloured shape the
// user drags around like a physical mini. See MARKERS_FEATURE_BRIEF.md.
public class Marker
{
    public const string CIRCLE = "circle";
    public const string SQUARE = "square";
    // Auto-populated entity markers. They reuse the existing INI fields (no new keys):
    // `text` carries the entity identity used for tray exclusivity (see brief 5.3).
    public const string INVESTIGATOR = "investigator"; // circle-shaped, identity = heroData.sectionName
    public const string MONSTER = "monster";           // square-shaped, identity = "section:duplicate"

    public string type;
    // Colour name or #RRGGBB, passed to ColorUtil
    public string color;
    public string text;
    public float posX;
    public float posY;

    public GameObject unityObject;

    // Marker size and contrast border thickness, in board units
    private const float Size = 0.8f;
    private const float BorderInset = 0.07f;

    private static Sprite circleSprite;
    private static Sprite squareSprite;
    private static TMP_FontAsset fontAsset;

    public Marker(string type, string color, string text, float posX, float posY)
    {
        this.type = type;
        this.color = color;
        this.text = text;
        this.posX = posX;
        this.posY = posY;
        Draw();
    }

    // Reconstruct from save data (a [Marker<id>] INI section)
    public Marker(Dictionary<string, string> data)
    {
        type = data["type"];
        color = data["color"];
        data.TryGetValue("text", out text);
        if (text == null) text = "";
        float.TryParse(data["posX"], out posX);
        float.TryParse(data["posY"], out posY);
        Draw();
    }

    private void Draw()
    {
        Game game = Game.Get();
        Sprite shape = IsCircleShape(type) ? GetCircleSprite() : GetSquareSprite();

        unityObject = new GameObject("Marker");
        unityObject.transform.SetParent(game.markerCanvas.transform);
        unityObject.transform.SetAsLastSibling();

        // Contrast border: the shape drawn dark behind a slightly smaller colour fill
        Image border = unityObject.AddComponent<Image>();
        border.sprite = shape;
        border.color = Color.black;
        border.rectTransform.sizeDelta = new Vector2(Size, Size);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(unityObject.transform);
        Image fill = fillObject.AddComponent<Image>();
        fill.sprite = shape;
        fill.raycastTarget = false;
        fill.color = ColorUtil.ColorFromName(color);
        InsetToParent(fill.rectTransform, BorderInset);

        // Square-shaped markers (wildcard + monster) show their text; circles do not,
        // so an investigator's identity stored in `text` stays invisible.
        if (!IsCircleShape(type) && !string.IsNullOrEmpty(text))
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(unityObject.transform);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = GetFont();
            label.color = LabelColor(fill.color);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.05f;
            label.fontSizeMax = 0.4f;
            InsetToParent(label.rectTransform, BorderInset * 1.5f);
        }

        unityObject.transform.position = new Vector3(posX, posY, 0);

        MarkerDrag drag = unityObject.AddComponent<MarkerDrag>();
        drag.marker = this;
    }

    // Shared shape sprites, reused by the tray swatches
    public static Sprite ShapeSprite(string type)
    {
        return IsCircleShape(type) ? GetCircleSprite() : GetSquareSprite();
    }

    // Circle-shaped marker types (round shapes); everything else draws as a square
    private static bool IsCircleShape(string type)
    {
        return type == CIRCLE || type == INVESTIGATOR;
    }

    public void SetPosition(float x, float y)
    {
        posX = x;
        posY = y;
        unityObject.transform.position = new Vector3(x, y, 0);
    }

    public void Remove()
    {
        Game.Get().CurrentQuest.markers.Remove(this);
        Object.Destroy(unityObject);
        // An entity left the board: let the tray re-offer it (if open). Our event only.
        MarkerTray.NotifyBoardChanged();
    }

    // Destroy every placed marker (markers hang from the marker canvas, not tagged
    // Game.BOARD, so the normal board teardown does not catch them)
    public static void RemoveAll()
    {
        Game game = Game.Get();
        if (game.markerCanvas != null)
        {
            foreach (Transform child in game.markerCanvas.transform)
            {
                Object.Destroy(child.gameObject);
            }
        }
        if (game.CurrentQuest != null && game.CurrentQuest.markers != null)
        {
            game.CurrentQuest.markers.Clear();
        }
    }

    // Serialise to a [Marker<id>] INI section for the save game
    public string ToString(int id)
    {
        string nl = System.Environment.NewLine;
        string r = "[Marker" + id + "]" + nl;
        r += "type=" + type + nl;
        r += "color=" + color + nl;
        r += "text=" + text + nl;
        r += "posX=" + posX + nl;
        r += "posY=" + posY + nl;
        return r;
    }

    private static void InsetToParent(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.localScale = Vector3.one;
    }

    private static Color LabelColor(Color background)
    {
        float luminance = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
        return luminance > 0.6f ? Color.black : Color.white;
    }

    private static TMP_FontAsset GetFont()
    {
        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(Game.Get().gameType.GetFont());
        }
        return fontAsset;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite == null)
        {
            Texture2D texture = new Texture2D(4, 4);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100);
        }
        return squareSprite;
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite == null)
        {
            int size = 128;
            float radius = size / 2f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius + 0.5f;
                    float dy = y - radius + 0.5f;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }
        return circleSprite;
    }
}
