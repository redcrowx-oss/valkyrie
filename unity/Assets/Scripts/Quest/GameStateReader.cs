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

    // Provisional monster colour for Block 3 (real art + duplicate badge come in Block 4)
    private const string MonsterColor = "#9E9E9E";

    // One investigator in play. `id` is the exclusivity key (stable across sessions).
    public class InvestigatorEntry
    {
        public string id;          // heroData.sectionName — stable identity
        public string name;        // translated display name
        public string colorHex;    // deterministic assigned ring colour
        public Texture2D portrait; // null until Block 4
    }

    // One live monster instance. `id` is the exclusivity key.
    public class MonsterEntry
    {
        public string id;          // "section:duplicate" — stable identity
        public string name;        // translated display name
        public int duplicate;      // 0 = first instance (no badge), 1.. = duplicates
        public string colorHex;
        public Texture2D image;    // null until Block 4
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
                portrait = null
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
            result.Add(new MonsterEntry
            {
                id = m.GetIdentifier(),
                name = m.monsterData.name.Translate(),
                duplicate = m.duplicate,
                colorHex = MonsterColor,
                image = null
            });
        }
        return result;
    }
}
