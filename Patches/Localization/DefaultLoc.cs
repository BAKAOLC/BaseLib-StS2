using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace BaseLib.Patches.Localization;

[HarmonyPatch(typeof(ModManager), nameof(ModManager.GetModdedLocTables))]
public static class DefaultLoc
{
    //Languages supported by basegame
    private static readonly string[] LanguagePreference = [
        "eng",
        "zhs",
        "jpn",
        "deu",
        "kor",
        "rus",
        "spa",
        "esp",
        "fra",
        "tur",
        "ita",
        "pol",
        "ptb",
        "tha"
    ];

    private static readonly Dictionary<string, string> _defaultLoc = [];
    
    /// <summary>
    /// Call this in your mod's initializer to load this type of localization first when loading any other language.
    /// This will result in using this language's localization for anything missing from the current set language.
    /// </summary>
    public static void Set(string modId, string defaultLoc)
    {
        if (_defaultLoc.Remove(modId, out var old))
        {
            BaseLibMain.Logger.Warn($"Default localization is set multiple times for {modId}; previous value {old}, new value {defaultLoc}");
        }
        _defaultLoc.Add(modId, defaultLoc);
    }

    [HarmonyPostfix]
    static void LoadDefaultTablesFirst(string language, string file, ref IEnumerable<string> __result)
    {
        List<string> defaultLocFirst = [];
        
        foreach (var mod in ModManager.GetLoadedMods())
        {
            if (mod.manifest?.id == null) continue;
            if (!_defaultLoc.TryGetValue(mod.manifest.id, out var defaultLoc))
            {
                //Check for loc files for mod if default path doesn't exist
                var origPath = $"res://{mod.manifest.id}/localization/{language}/{file}";
                if (ResourceLoader.Exists(origPath)) continue;
                BaseLibMain.Logger.VeryDebug($"\"{origPath}\" not found and DefaultLoc not set; looking for existing loc file");

                HashSet<string> existingLang = [];
                foreach (var directory in ResourceLoader.ListDirectory($"res://{mod.manifest.id}/localization/{language}/").Where(str => str.EndsWith('/')))
                {
                    if (directory == null) continue;
                    defaultLoc = directory;
                    existingLang.Add(directory);
                }
                
                if (existingLang.Count == 0) continue;

                foreach (var preferredLang in LanguagePreference)
                {
                    if (existingLang.Contains(preferredLang))
                    {
                        defaultLoc = preferredLang;
                        break;
                    }
                }

                if (defaultLoc == null) continue;
            }
            if (defaultLoc == language) continue;
            
            var path = $"res://{mod.manifest.id}/localization/{defaultLoc}/{file}";
            if (ResourceLoader.Exists(path))
            {
                defaultLocFirst.Add(path);
            }
        }

        BaseLibMain.Logger.Debug($"Found {defaultLocFirst.Count} default loc files; [{string.Join(", ", defaultLocFirst)}]");
        defaultLocFirst.AddRange(__result);
        __result = defaultLocFirst;
    }
}