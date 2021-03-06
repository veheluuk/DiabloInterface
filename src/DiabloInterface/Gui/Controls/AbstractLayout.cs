namespace Zutatensuppe.DiabloInterface.Gui.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Forms;

    using Zutatensuppe.D2Reader;
    using Zutatensuppe.D2Reader.Models;
    using Zutatensuppe.DiabloInterface.Business.Services;
    using Zutatensuppe.DiabloInterface.Business.Settings;
    using Zutatensuppe.DiabloInterface.Core.Extensions;
    using Zutatensuppe.DiabloInterface.Core.Logging;

    class Def
    {
        public string name;
        public string maxString;
        public Func<ApplicationSettings, Tuple<bool, Color, int>> settings;
        public Label[] labels;
        public Dictionary<Label, string> defaults;
        public Def(string name, string maxString, Func<ApplicationSettings, Tuple<bool, Color, int>> settings, string[] labels)
        {
            this.name = name;
            this.maxString = maxString;
            this.settings = settings;
            this.labels = (from n in labels select new Label() { Text = n }).ToArray();
            this.defaults = new Dictionary<Label, string>();
            foreach (Label l in this.labels)
            {
                this.defaults.Add(l, l.Text);
            }
        }
    }

    abstract class AbstractLayout : UserControl
    {
        static readonly ILogger Logger = LogServiceLocator.Get(MethodBase.GetCurrentMethod().DeclaringType);

        protected ISettingsService settingsService;
        protected IGameService gameService;

        protected Dictionary<string, Def> def = new Dictionary<string, Def>();

        protected bool realFrwIas;
        GameDifficulty? activeDifficulty;
        CharacterClass? activeCharacterClass;

        protected IEnumerable<FlowLayoutPanel> RunePanels { get; set; }

        protected abstract Panel RuneLayoutPanel { get; }

        protected void Add(string nam, string maxStr, Func<ApplicationSettings, Tuple<bool, Color, int>> s, params string[] names)
        {
            def.Add(nam, new Def(nam, maxStr, s, names));
        }

        protected void UpdateLabel(string nam, string value)
        {
            foreach (Label l in def[nam].labels)
            {
                l.Text = def[nam].defaults[l].Replace("{}", value);
            }
        }

        protected void UpdateLabel(string nam, int value)
        {
            UpdateLabel(nam, "" + value);
        }

        protected void RegisterServiceEventHandlers()
        {
            settingsService.SettingsChanged += SettingsServiceOnSettingsChanged;
            gameService.CharacterCreated += GameServiceOnCharacterCreated;
            gameService.DataRead += GameServiceOnDataRead;
        }

        protected void UnregisterServiceEventHandlers()
        {
            settingsService.SettingsChanged -= SettingsServiceOnSettingsChanged;

            gameService.CharacterCreated -= GameServiceOnCharacterCreated;
            gameService.DataRead -= GameServiceOnDataRead;
        }

        void SettingsServiceOnSettingsChanged(object sender, ApplicationSettingsEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke((Action)(() => SettingsServiceOnSettingsChanged(sender, e)));
                return;
            }

            activeCharacterClass = null;

            UpdateSettings(e.Settings);
        }

        void GameServiceOnCharacterCreated(object sender, CharacterCreatedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke((Action)(() => GameServiceOnCharacterCreated(sender, e)));
                return;
            }

            Reset();
        }

        void GameServiceOnDataRead(object sender, DataReadEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke((Action)(() => GameServiceOnDataRead(sender, e)));
                return;
            }

            UpdateLabels(e.Character, e.Quests, e.Game);
            UpdateClassRuneList(e.Character.CharClass);
            UpdateRuneDisplay(e.Character.InventoryItemIds);
        }

        public void Reset()
        {
            foreach (FlowLayoutPanel fp in RunePanels)
            {
                if (fp.Controls.Count <= 0)
                    continue;

                foreach (RuneDisplayElement c in fp.Controls)
                {
                    c.SetHaveRune(false);
                }
            }

            foreach (KeyValuePair<string, Def> pair in def)
            {
                foreach (Label l in pair.Value.labels)
                {
                    l.Text = pair.Value.defaults[l];
                }
            }
        }

        abstract protected void UpdateSettings(ApplicationSettings settings);

        abstract protected void UpdateLabels(Character player, Quests quests, Game game);

        void UpdateClassRuneList(CharacterClass characterClass)
        {
            var settings = settingsService.CurrentSettings;
            if (!settings.DisplayRunes) return;

            var targetDifficulty = gameService.TargetDifficulty;
            var isCharacterClassChanged = activeCharacterClass == null || activeCharacterClass != characterClass;
            var isGameDifficultyChanged = activeDifficulty != targetDifficulty;

            if (!isCharacterClassChanged && !isGameDifficultyChanged)
                return;

            Logger.Info("Loading rune list.");
            
            var runeSettings = GetMostSpecificRuneSettings(characterClass, targetDifficulty);
            UpdateRuneList(settings, runeSettings?.Runes?.ToList());

            activeDifficulty = targetDifficulty;
            activeCharacterClass = characterClass;
        }

        void UpdateRuneList(ApplicationSettings settings, IReadOnlyList<Rune> runes)
        {
            var panel = RuneLayoutPanel;
            if (panel == null) return;

            panel.Controls.ClearAndDispose();
            panel.Visible = runes?.Count > 0;
            runes?.ForEach(rune => panel.Controls.Add(
                new RuneDisplayElement(rune, settings.DisplayRunesHighContrast, false, false)));
        }

        /// <summary>
        /// Gets the most specific rune settings in the order:
        ///     Class+Difficulty > Class > Difficulty > None
        /// </summary>
        /// <param name="characterClass">Active character class.</param>
        /// <param name="targetDifficulty">Manual difficulty selection.</param>
        /// <returns>The rune settings.</returns>
        ClassRuneSettings GetMostSpecificRuneSettings(CharacterClass characterClass, GameDifficulty targetDifficulty)
        {
            IEnumerable<ClassRuneSettings> runeClassSettings = settingsService.CurrentSettings.ClassRunes.ToList();
            return runeClassSettings.FirstOrDefault(rs => rs.Class == characterClass && rs.Difficulty == targetDifficulty)
                ?? runeClassSettings.FirstOrDefault(rs => rs.Class == characterClass && rs.Difficulty == null)
                ?? runeClassSettings.FirstOrDefault(rs => rs.Class == null && rs.Difficulty == targetDifficulty)
                ?? runeClassSettings.FirstOrDefault(rs => rs.Class == null && rs.Difficulty == null);
        }

        void UpdateRuneDisplay(IEnumerable<int> itemIds)
        {
            var panel = RuneLayoutPanel;
            if (panel == null) return;

            // Count number of items of each type.
            Dictionary<int, int> itemClassCounts = itemIds
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (RuneDisplayElement runeElement in panel.Controls)
            {
                var itemClassId = (int)runeElement.Rune + 610;

                if (itemClassCounts.ContainsKey(itemClassId) && itemClassCounts[itemClassId] > 0)
                {
                    itemClassCounts[itemClassId]--;
                    runeElement.SetHaveRune(true);
                }
            }
        }

        protected Size MeasureText(string str, Control control)
        {
            return TextRenderer.MeasureText(str, control.Font, Size.Empty, TextFormatFlags.SingleLine);
        }
    }
}
