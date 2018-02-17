using BenLib;
using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VGMGUI
{
    public class Settings
    {
        public static FileIniDataParser Parser { get; set; } = new FileIniDataParser();

        public static IniData SettingsData { get; set; }

        /// <summary>
        /// Indique si la conversion utilise le multithreading.
        /// </summary>
        public static bool ConversionMultithreading { get; set; } = true;

        /// <summary>
        /// Nombre maximal de processus vgmstream en cours d'exécution lors de la conversion.
        /// </summary>
        public static int ConversionMaxProcessCount { get; set; } = 5;

        /// <summary>
        /// Indique si l'affichage des échantillons doit se faire sous la forme "x secondes" ou sous la forme "xx:xx:xx"
        /// </summary>
        public static bool HMSSamplesDisplay { get; set; } = false;

        /// <summary>
        /// Nombre maximal de décimales pour l'affichage des échantillons.
        /// </summary>
        public static int SamplesDisplayMaxDec { get; set; } = 4;

        /// <summary>
        /// Indique si la lecture d'un fichier doit être arrêtée quand celui-ci est supprimé.
        /// </summary>
        public static bool StopPlayingWhenDeleteFile { get; set; } = true;

        /// <summary>
        /// Indique si l'ajout et l'analyse utilisent le multithreading.
        /// </summary>
        public static bool AddingMultithreading { get; set; } = true;

        /// <summary>
        /// Nombre maximal d'ajouts de fichiers simultanés.
        /// </summary>
        public static int AddingMaxProcessCount { get; set; } = 5;

        /// <summary>
        /// Délai avant le rafraîchissement de <see cref="View"/> quand une recherche est lancée.
        /// </summary>
        public static int SearchDelay { get; set; } = 250;

        /// <summary>
        /// Indique si les fichiers doivent être analysés à l'ajout.
        /// </summary>
        public static bool PreAnalyse { get; set; } = true;

        public class AdditionalFormats
        {
            public static bool DKCTFCSMP { get; set; } = true;
            public static bool Any => DKCTFCSMP;
        }

        public class StatusBar
        {
            private static bool s_display = true;
            private static bool s_counter = true;
            private static bool s_RAM = true;

            public static bool Display
            {
                get => s_display;
                set
                {
                    s_display = value;
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("Display"));
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("DisplayVisibility"));
                }
            }
            public static bool Counter
            {
                get => s_counter;
                set
                {
                    s_counter = value;
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("Counter"));
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("CounterVisibility"));
                }
            }
            public static bool RAM
            {
                get => s_RAM;
                set
                {
                    s_RAM = value;
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("RAM"));
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("RAMVisibility"));
                }
            }
        }

        public static Visibility DisplayVisibility => StatusBar.Display ? Visibility.Visible : Visibility.Collapsed;
        public static Visibility CounterVisibility => StatusBar.Counter ? Visibility.Visible : Visibility.Collapsed;
        public static Visibility RAMVisibility => StatusBar.RAM ? Visibility.Visible : Visibility.Collapsed;

        private static FichierOutData m_defaultOutData;
        private static string s_searchFilter;
        private static bool s_searchCaseSensitive;
        private static FileListColumn s_searchColumn;

        /// <summary>
        /// Données de sortie par défaut d'un fichier.
        /// </summary>
        public static FichierOutData DefaultOutData
        {
            get => m_defaultOutData;
            set
            {
                m_defaultOutData = value;
                SettingsData.Global["DefaultOutData"] = "\"" + value.OriginalDestination + " | " + value.FadeDelay + " | " + value.FadeOut + " | " + value.FadeTime + " | " + value.LoopCount + " | " + value.StartEndLoop + "\"";
                TryWriteSettings();
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("DefaultOutDataLoopCount"));
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("DefaultOutDataFadeDelay"));
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("DefaultOutDataFadeTime"));
            }
        }

        public static string DefaultOutDataLoopCount => string.Format(App.Str("MW_LoopCount"), DefaultOutData.LoopCount?.ToString() ?? "2");
        public static string DefaultOutDataFadeDelay => string.Format(App.Str("MW_FadeDelay"), DefaultOutData.FadeDelay?.ToString() ?? "0");
        public static string DefaultOutDataFadeTime => string.Format(App.Str("MW_FadeTime"), DefaultOutData.FadeTime?.ToString() ?? "10");

        /// <summary>
        /// Filtre de la liste de fichiers.
        /// </summary>
        public static string RestoreSearchFilter
        {
            get => s_searchFilter;
            set
            {
                s_searchFilter = value;
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("RestoreSearchFilter"));
            }
        }

        /// <summary>
        /// Filtre effectif de la liste de fichiers.
        /// </summary>
        public static string SearchFilter { get; set; }

        /// <summary>
        /// Colone où rechercher des fichiers.
        /// </summary>
        public static FileListColumn SearchColumn
        {
            get => s_searchColumn;
            set
            {
                s_searchColumn = value;
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("SearchColumn"));
            }
        }

        /// <summary>
        /// Indique si la recherche doit respecter la case.
        /// </summary>
        public static bool SearchCaseSensitive
        {
            get => s_searchCaseSensitive;
            set
            {
                s_searchCaseSensitive = value;
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("SearchCaseSensitive"));
            }
        }

        public static event EventHandler<PropertyChangedEventArgs> StaticPropertyChanged;

        /// <summary>
        /// Inscrit <see cref="SettingsData"/> dans un fichier.
        /// </summary>
        public static void WriteSettings() => Parser.WriteFile(Path.Combine(App.AppPath, "VGMGUI.ini"), SettingsData);

        /// <summary>
        /// Tente d'inscrire <see cref="SettingsData"/> dans un fichier.
        /// </summary>
        public static async Task<TryResult> TryWriteSettings() => await Parser.TryAndRetryWriteFile(Path.Combine(App.AppPath, "VGMGUI.ini"), SettingsData);
    }
}
