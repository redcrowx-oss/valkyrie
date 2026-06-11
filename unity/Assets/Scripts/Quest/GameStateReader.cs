using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Content;

// Single adapter between the marker UI and Valkyrie's internals.
//
// Marker / MarkerTray talk ONLY to this class. They never read Quest, Hero,
// Monster, ContentData or texture resolution directly. If a future upstream
// version renames a field or changes how art is resolved, ONLY this file changes.
//
// It exposes plain data entries (no Valkyrie types leak out), so the rest of the
// marker code stays decoupled from the engine.
public static class GameStateReader
{
    // Investigator ring colours, one per stable roster slot (red, green, blue, purple, orange)
    private static readonly string[] InvestigatorColors =
        { "#E53935", "#43A047", "#1E88E5", "#8E24AA", "#FB8C00" };

    // Fallback monster colour when no art resolves (or before Block 4 art)
    private const string MonsterColor = "#9E9E9E";

    // Texture caches keyed by identity, so refreshing the tray never re-reads disk
    // nor re-scans content. (ContentData also caches by file path; this additionally
    // skips the Values<T>() scan.) A cached null means "no art" -> fallback stays.
    private static readonly Dictionary<string, Texture2D> portraitCache = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Texture2D> monsterImageCache = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<int, Sprite> badgeCache = new Dictionary<int, Sprite>();

    // One investigator in play. `id` is the exclusivity key (stable across sessions).
    public class InvestigatorEntry
    {
        public string id;          // heroData.sectionName — stable identity
        public string name;        // translated display name
        public string colorHex;    // deterministic assigned ring colour
        public Texture2D portrait; // raw portrait, or null -> tray/board fall back to a colour circle
    }

    // One live monster instance. `id` is the exclusivity key.
    public class MonsterEntry
    {
        public string id;          // "section:duplicate" — stable identity
        public string name;        // translated display name
        public int duplicate;      // 0 = first instance (no badge), 1.. = duplicates
        public string colorHex;
        public Texture2D image;    // monster art, or null -> fallback to a colour square + label
        public Sprite badge;       // duplicate badge sprite, or null for the first instance
    }

    // Investigators in play (selected and not defeated).
    //
    // The colour slot is stable for the whole quest: it is the hero's position when
    // ALL selected heroes (defeated included) are ordered by their persisted `id`.
    // That way a defeat never shifts another hero's colour, and the same ordering is
    // recomputed identically after a save/load, so colour -> investigator is deterministic.
    public static List<InvestigatorEntry> GetActiveInvestigators()
    {
        List<InvestigatorEntry> result = new List<InvestigatorEntry>();
        Quest quest = Game.Get().CurrentQuest;
        if (quest == null || quest.heroes == null) return result;

        // Stable roster = every selected hero, ordered by persisted id
        List<Quest.Hero> roster = new List<Quest.Hero>();
        foreach (Quest.Hero h in quest.heroes)
        {
            if (h.heroData != null) roster.Add(h);
        }
        roster.Sort((a, b) => a.id.CompareTo(b.id));

        for (int slot = 0; slot < roster.Count; slot++)
        {
            Quest.Hero h = roster[slot];
            if (h.defeated) continue; // keeps the colour slot reserved, just hides the tray entry
            result.Add(new InvestigatorEntry
            {
                id = h.heroData.sectionName,
                name = h.heroData.name.Translate(),
                colorHex = InvestigatorColors[slot % InvestigatorColors.Length],
                portrait = GetInvestigatorPortrait(h.heroData.sectionName)
            });
        }
        return result;
    }

    // Live monster instances (a monster is alive while it is in CurrentQuest.monsters;
    // the engine removes it from that list on death).
    public static List<MonsterEntry> GetActiveMonsters()
    {
        List<MonsterEntry> result = new List<MonsterEntry>();
        Quest quest = Game.Get().CurrentQuest;
        if (quest == null || quest.monsters == null) return result;

        foreach (Quest.Monster m in quest.monsters)
        {
            if (m.monsterData == null) continue;
            string id = m.GetIdentifier();
            result.Add(new MonsterEntry
            {
                id = id,
                name = m.monsterData.name.Translate(),
                duplicate = m.duplicate,
                colorHex = MonsterColor,
                image = GetMonsterImage(id),
                badge = GetMonsterBadge(id)
            });
        }
        return result;
    }

    // --- Texture resolution by identity (cached) -------------------------------
    // Art is resolved from the content TYPE, not the live instance, so a marker's
    // image survives the entity's death (ContentData keeps the type) and a reload.

    // Investigator portrait by stable identity (heroData.sectionName)
    public static Texture2D GetInvestigatorPortrait(string sectionName)
    {
        if (string.IsNullOrEmpty(sectionName)) return null;
        if (portraitCache.TryGetValue(sectionName, out Texture2D cached)) return cached;

        Texture2D texture = null;
        foreach (HeroData hd in Game.Get().cd.Values<HeroData>())
        {
            if (hd.sectionName == sectionName)
            {
                if (!string.IsNullOrEmpty(hd.image)) texture = ContentData.FileToTexture(hd.image);
                break;
            }
        }
        portraitCache[sectionName] = texture;
        return texture;
    }

    // Monster art by identity ("section:duplicate"), resolved from the monster type
    public static Texture2D GetMonsterImage(string identifier)
    {
        string section = SectionOf(identifier);
        if (string.IsNullOrEmpty(section)) return null;
        if (monsterImageCache.TryGetValue(section, out Texture2D cached)) return cached;

        Texture2D texture = null;
        foreach (MonsterData md in Game.Get().cd.Values<MonsterData>())
        {
            if (md.sectionName == section)
            {
                if (!string.IsNullOrEmpty(md.image)) texture = ContentData.FileToTexture(md.image);
                break;
            }
        }
        monsterImageCache[section] = texture;
        return texture;
    }

    // Duplicate badge sprite ("sprites/monster_duplicate_N"); null for the first
    // instance (duplicate 0), matching the physical ID-token system and MonsterCanvas.
    public static Sprite GetMonsterBadge(string identifier)
    {
        int duplicate = DuplicateOf(identifier);
        if (duplicate <= 0) return null;
        if (badgeCache.TryGetValue(duplicate, out Sprite cached)) return cached;

        Sprite sprite = null;
        Texture2D tex = Resources.Load("sprites/monster_duplicate_" + duplicate) as Texture2D;
        if (tex != null)
        {
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, 1);
        }
        badgeCache[duplicate] = sprite;
        return sprite;
    }

    // Identity is "section:duplicate"; split on the LAST ':' so a section name that
    // itself contains ':' is preserved.
    private static string SectionOf(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return identifier;
        int i = identifier.LastIndexOf(':');
        return i < 0 ? identifier : identifier.Substring(0, i);
    }

    private static int DuplicateOf(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return 0;
        int i = identifier.LastIndexOf(':');
        if (i < 0) return 0;
        int d = 0;
        int.TryParse(identifier.Substring(i + 1), out d);
        return d;
    }
}
