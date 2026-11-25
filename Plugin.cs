using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using HarmonyLib;
using HUD.Buttons;
using PBLocalisation;
using UnityEngine.Events;
using YamlDotNet.Serialization;

namespace RTLC.TheRoadOfDustAndSorrow.RussianTranslate;

public sealed class TranslationEntry
{
    public string ENG { get; set; }
    public string UKR { get; set; }
    public string RUS { get; set; }
    public string[] UKR_VARIANTS { get; set; }
}

[BepInPlugin(Id, "The Road of Dust and Sorrow — Русский язык", "0.1.4")]
public sealed class Plugin : BaseUnityPlugin
{
    private const string Id = "rtlc.theroadofdustandsorrow.russiantranslate";
    private const int Russian = 2;
    private const string RussianSetting = "rtlc_russian_locale";
    private static readonly string[] LanguageOptions = { "Русский", "English", "Українська" };
    private static readonly int[] LocaleByOption = { Russian, (int)PBLocales.English, (int)PBLocales.Ukraine };
    private static Dictionary<string, TranslationEntry> translations;

    private void Awake()
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(Info.Location), "Russian.json");
            translations = new DeserializerBuilder().Build()
                .Deserialize<Dictionary<string, TranslationEntry>>(File.ReadAllText(path));
            if (translations == null || translations.Count == 0)
                throw new InvalidDataException("Russian.json contains no translations.");
            foreach (KeyValuePair<string, TranslationEntry> entry in translations)
            {
                if (entry.Value == null || entry.Value.RUS == null)
                    throw new InvalidDataException($"Russian.json entry '{entry.Key}' must contain a RUS string.");
            }

            var harmony = new Harmony(Id);
            try
            {
                harmony.PatchAll(typeof(Plugin).Assembly);
            }
            catch
            {
                harmony.UnpatchSelf();
                throw;
            }
            Logger.LogInfo($"Loaded {translations.Count} Russian strings.");
        }
        catch (Exception error)
        {
            Logger.LogError(error);
        }
    }

    [HarmonyPatch(typeof(OptionsButton), nameof(OptionsButton.SetOptions))]
    private static class LanguagePicker
    {
        private static void Prefix(ref int value, ref string[] options, ref UnityAction<int> callback)
        {
            if (callback?.Method.DeclaringType != typeof(HUD.Panels.BaseOptionsPanel)
                || callback.Method.Name != "OnLanguageButton")
                return;

            var onLanguageChanged = callback;
            value = Math.Max(0, Array.IndexOf(LocaleByOption, value));
            options = LanguageOptions;
            callback = index => onLanguageChanged(LocaleByOption[index]);
        }
    }

    [HarmonyPatch(typeof(OptionsButton), "UpdateLocalisation")]
    private static class ControlSchemeLabels
    {
        private static void Prefix(OptionsButton.LocalisationOptionsLabels ___localisationLabels,
            ref string[] ___options)
        {
            if (___localisationLabels != OptionsButton.LocalisationOptionsLabels.ControlScheme || ___options == null)
                return;

            var localisation = BaseGameManager<GameManager>.Instance.Localisation;
            if (localisation.IsInitialized)
                ___options = localisation.ControlSchemeLabels;
        }
    }

    [HarmonyPatch(typeof(ScenePreferencesScriptableObject), nameof(ScenePreferencesScriptableObject.GetLocalisation))]
    private static class SourceLocale
    {
        private static void Prefix(ref PBLocales locale)
        {
            if ((int)locale == Russian)
                locale = PBLocales.English;
        }
    }

    [HarmonyPatch(typeof(LocalisationController), nameof(LocalisationController.LoadData), typeof(PBLocales))]
    private static class LocalisationLoad
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var setter = AccessTools.PropertySetter(typeof(LocalisationController),
                nameof(LocalisationController.ObjectiveJournal));
            var apply = AccessTools.Method(typeof(Plugin), nameof(ApplyTranslation));
            var code = new List<CodeInstruction>(instructions);
            int index = code.FindIndex(instruction =>
                instruction.opcode == OpCodes.Call && Equals(instruction.operand, setter));
            if (index < 0)
                throw new InvalidOperationException("Cannot locate the end of the localisation table load.");

            code.InsertRange(index + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, apply)
            });
            return code;
        }
    }

    [HarmonyPatch(typeof(LocalisedTitle), "GetData")]
    private static class CachedTitle
    {
        private static void Prefix(ref TitlesData.Item ___data) => ___data = null;
    }

    [HarmonyPatch(typeof(GameSettingsController), nameof(GameSettingsController.LoadData))]
    private static class SettingsLoad
    {
        private static void Postfix(ref int ___localisation, ES3Settings ___settings)
        {
            if (!ES3.KeyExists(RussianSetting, ___settings)
                || (___localisation == (int)PBLocales.English
                    && ES3.Load(RussianSetting, false, ___settings)))
                ___localisation = Russian;
        }
    }

    [HarmonyPatch(typeof(GameSettingsController), nameof(GameSettingsController.PersistData))]
    private static class SettingsSave
    {
        // Store Russian alongside the native setting so the demo can still read it after uninstall.
        private static void Prefix(ref int ___localisation, ES3Settings ___settings, out int __state)
        {
            __state = ___localisation;
            ES3.Save(RussianSetting, ___localisation == Russian, ___settings);
            if (___localisation == Russian)
                ___localisation = (int)PBLocales.English;
        }

        private static Exception Finalizer(Exception __exception, ref int ___localisation, int __state)
        {
            ___localisation = __state;
            return __exception;
        }
    }

    private static void ApplyTranslation(LocalisationController controller, PBLocales locale)
    {
        if ((int)locale != Russian)
            return;

        using var hash = SHA256.Create();
        foreach (InventoryItemData.Item item in controller.InventoryItems.Values)
        {
            item.Title = Translate(item.Title, hash);
            item.Categoty = Translate(item.Categoty, hash);
            item.Description = Translate(item.Description, hash);
            item.Stats = Translate(item.Stats, hash);
        }

        foreach (TitlesData.Item item in controller.Titles.Values)
        {
            item.ActionLabel = Translate(item.ActionLabel, hash);
            item.DescriptionLabel = Translate(item.DescriptionLabel, hash);
            item.ApplyLabel = Translate(item.ApplyLabel, hash);
            TranslateLines(item.Text, hash);
            if (item.WrongItem != null)
                foreach (TitlesData.Item.WrongItemApply entry in item.WrongItem)
                    entry.Text = Translate(entry.Text, hash);
        }

        foreach (DialogueData.Item item in controller.Dialogues.Values)
        {
            if (item.Script == null)
                continue;
            for (int i = 0; i < item.Script.Length; i++)
                item.Script[i].Text = Translate(item.Script[i].Text, hash);
        }

        foreach (LetterData.Item item in controller.Letters.Values)
        {
            item.Title = Translate(item.Title, hash);
            item.Header = Translate(item.Header, hash);
            item.ActionLabel = Translate(item.ActionLabel, hash);
            TranslateLines(item.Text, hash);
        }

        foreach (ObjectiveJournalData.Item item in controller.ObjectiveJournal.Values)
            item.Title = Translate(item.Title, hash);
    }

    private static void TranslateLines(string[] lines, SHA256 hash)
    {
        if (lines == null)
            return;
        for (int i = 0; i < lines.Length; i++)
            lines[i] = Translate(lines[i], hash);
    }

    private static string Translate(string text, SHA256 hash)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        byte[] source = Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n"));
        string key = Convert.ToBase64String(hash.ComputeHash(source));
        return translations.TryGetValue(key, out TranslationEntry translation)
            && translation != null
            && translation.RUS != null
            ? translation.RUS
            : text;
    }
}
