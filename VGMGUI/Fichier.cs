using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Threading;
using System.IO;
using BenLib;
using static VGMGUI.Settings;

namespace VGMGUI
{
    public class Fichier : INotifyPropertyChangedExtended<object>, INotifyPropertyChanged
    {
        #region Champs & Propriétés

        public event PropertyChangedExtendedEventHandler<object> PropertyChangedExtended;
        public event PropertyChangedEventHandler PropertyChanged;
        
        #region List

        /// <summary>
        /// Indique si le fichier est coché dans une liste.
        /// </summary>
        public bool Checked
        {
            get => m_checked;
            set
            {
                m_checked = value;
                NotifyPropertyChanged("Checked");
            }
        }
        private bool m_checked = true;
        
        /// <summary>
        /// Indique si le fichier est sélectionné dans une liste.
        /// </summary>
        public bool Selected
        {
            get => m_selected;
            set
            {
                m_selected = value;
                NotifyPropertyChanged("Selected");
            }
        }
        private bool m_selected = false;

        /// <summary>
        /// Index de cet objet dans une liste.
        /// </summary>
        public int Index { get; set; }

        #endregion

        #region FileInfos

        /// <summary>
        /// Chemin complet du fichier.
        /// </summary>
        public string Path
        {
            get => m_path;
            set
            {
                m_path = value;
                NotifyPropertyChanged("Path");
                NotifyPropertyChanged("Date");
                NotifyPropertyChanged("DateString");
                NotifyPropertyChanged("Folder");
                NotifyPropertyChanged("Name");
                NotifyPropertyChanged("Size");
                NotifyPropertyChanged("SizeString");
                NotifyPropertyChanged("DefaultIcon");
            }
        }
        private string m_path;

        /// <summary>
        /// Date de dernière modification du fichier.
        /// </summary>
        public DateTime Date => File.Exists(Path) ? new FileInfo(Path).LastWriteTime : default;

        /// <summary>
        /// Date de dernière modification du fichier.
        /// </summary>
        public string DateString => Date.ToString();

        /// <summary>
        /// Dossier du fichier.
        /// </summary>
        public string Folder => File.Exists(Path) ? System.IO.Path.GetDirectoryName(Path) : null;

        /// <summary>
        /// Nom du fichier.
        /// </summary>
        public string Name => File.Exists(Path) ? System.IO.Path.GetFileName(Path) : null;

        /// <summary>
        /// Taille du fichier.
        /// </summary>
        public long Size => File.Exists(Path) ? new FileInfo(Path).Length : 0;

        /// <summary>
        /// Taille du fichier.
        /// </summary>
        public string SizeString => IO.GetFileSize(Size) ?? App.Str("TT_Error");

        /// <summary>
        /// Permet de réguler l'accès au fichier.
        /// </summary>
        public FileStream Stream { get; set; }

        #endregion

        #region UI
        
        /// <summary>
        /// Icône du fichier.
        /// </summary>
        public object Icon
        {
            get => m_icon;
            set
            {
                m_icon = value;
                NotifyPropertyChanged("Icon");
            }
        }
        private object m_icon = Application.Current.Resources["MusicFile"];

        /// <summary>
        /// Icône par défaut du fichier.
        /// </summary>
        /// <param name="attr">Attributs du fichier.</param>
        /// <param name="iconSize">Taille de l'icône.</param>
        /// <returns></returns>
        public ImageSource GetDefaultIcon(FileAttributes? attr = null, Imaging.SystemIconSize iconSize = Imaging.SystemIconSize.Small) => Imaging.ShellIcon.GetFileIconAndType(Path, attr, iconSize).Icon;

        /// <summary>
        /// Icône par défaut du fichier.
        /// </summary>
        public ImageSource DefaultIcon => Imaging.ShellIcon.GetFileIconAndType(Path, null, Imaging.SystemIconSize.Small).Icon;

        /// <summary>
        /// Couleur de la police pour ce fichier.
        /// </summary>
        public Brush TextBrush
        {
            get => m_textbrush;
            set
            {
                m_textbrush = value;
                NotifyPropertyChanged("TextBrush");
            }
        }
        private Brush m_textbrush = Application.Current.Resources["ListViewTxtBrush"] as SolidColorBrush;        

        /// <summary>
        /// Épaisseur de la police pour ce fichier.
        /// </summary>
        public FontWeight FontWeight
        {
            get => m_fontWeight;
            set
            {
                m_fontWeight = value;
                NotifyPropertyChanged("FontWeight");
            }
        }
        private FontWeight m_fontWeight = FontWeights.Normal;

        #endregion

        #region VGMStream

        private CancellationTokenSource m_cts = new CancellationTokenSource();

        /// <summary>
        /// Indique si le fichier est analysé.
        /// </summary>
        public bool Analyzed { get; set; }

        /// <summary>
        /// Indique si le fichier n'est pas décodable par vgmstream.
        /// </summary>
        public bool Invalid { get; set; }

        public bool Played { get; set; }

        public CancellationToken CancellationToken => m_cts.Token;

        public bool IsCancellable => !CancellationToken.IsCancellationRequested && (OriginalState == "FSTATE_Queued" || OriginalState == "FSTATE_Conversion" || OriginalState == "FSTATE_Suspended");

        public static bool Overflow { get; set; }

        #region State

        /// <summary>
        /// État des opérations sur le fichier.
        /// </summary>
        public string OriginalState
        {
            get => m_state;
            set
            {
                if (m_state != value)
                {
                    m_state = value;
                    NotifyPropertyChanged("State");
                    NotifyPropertyChanged("OriginalState");
                }
            }
        }
        private string m_state = "FSTATE_Queued";

        /// <summary>
        /// État des opérations sur le fichier.
        /// </summary>
        public string State
        {
            get => App.Str(m_state) ?? m_state;
            set
            {
                if (m_state != value)
                {
                    m_state = App.Res(value) ?? m_state;
                    NotifyPropertyChanged("State");
                    NotifyPropertyChanged("OriginalState");
                }
            }
        }

        #endregion

        #region Entrée

        /// <summary>
        /// Nombre de cannaux audio du fichier.
        /// </summary>
        public int Channels
        {
            get => m_channels;
            set
            {
                if (value != Channels)
                {
                    var tmp = m_channels;
                    m_channels = value;
                    NotifyPropertyChanged("Channels", tmp, value);
                }
            }
        }
        private int m_channels;

        /// <summary>
        /// Encodage audio du fichier.
        /// </summary>
        public string Encoding
        {
            get => m_encoding;
            set
            {
                if (value != Encoding)
                {
                    var tmp = m_encoding;
                    m_encoding = value;
                    NotifyPropertyChanged("Encoding", tmp, value);
                }
            }
        }
        private string m_encoding = String.Empty;

        /// <summary>
        /// Structure du stream.
        /// </summary>
        public string Layout
        {
            get => m_layout;
            set
            {
                if (value != Layout)
                {
                    var tmp = m_layout;
                    m_layout = value;
                    NotifyPropertyChanged("Layout", tmp, value);
                }
            }
        }
        private string m_layout = String.Empty;

        #region Bitrate

        /// <summary>
        /// Débit binaire du fichier.
        /// </summary>
        public int Bitrate
        {
            get => m_bitrate;
            set
            {
                if (value != Bitrate)
                {
                    var tmp = Bitrate;
                    var strtmp = BitrateString;

                    m_bitrate = value;

                    NotifyPropertyChanged("Bitrate", tmp, Bitrate);
                    NotifyPropertyChanged("BitrateString", strtmp, BitrateString);
                }
            }
        }
        private int m_bitrate;

        /// <summary>
        /// Débit binaire du fichier.
        /// </summary>
        public string BitrateString => $"{Bitrate} kbps";

        #endregion

        #region Format

        /// <summary>
        /// Format audio du fichier.
        /// </summary>
        public string OriginalFormat
        {
            get => m_format;
            set
            {
                if (value != OriginalFormat)
                {
                    var ortmp = m_format;
                    var tmp = Format;

                    m_format = value;

                    NotifyPropertyChanged("OriginalFormat", ortmp, value);
                    NotifyPropertyChanged("Format", tmp, Format);
                }
            }
        }
        private string m_format = String.Empty;

        /// <summary>
        /// Format audio du fichier.
        /// </summary>
        public string Format
        {
            get => App.Str(m_format) ?? m_format;
            set
            {
                if (value != Format)
                {
                    var ortmp = m_format;
                    var tmp = Format;
                    string s = App.Res(value);

                    if (s != null)
                    {
                        m_format = s ?? value;
                        NotifyPropertyChanged("OriginalFormat", ortmp, value);
                        NotifyPropertyChanged("Format", tmp, Format);
                    }
                }
            }
        }

        #endregion

        #region Interleave

        /// <summary>
        /// Entrelacement des pistes audio du fichier.
        /// </summary>
        public int Interleave
        {
            get => m_interleave;
            set
            {
                if (value != Interleave)
                {
                    var tmp = Interleave;
                    var strtmp = InterleaveString;

                    m_interleave = value;

                    NotifyPropertyChanged("Interleave", tmp, Interleave);
                    NotifyPropertyChanged("InterleaveString", strtmp, InterleaveString);
                }
            }
        }
        private int m_interleave;

        /// <summary>
        /// Entrelacement des pistes audio du fichier.
        /// </summary>
        public string InterleaveString => "0x" + Interleave.ToString("X4") + " " + App.Str("TT_bytes");

        #endregion

        #region LoopFlag

        /// <summary>
        /// Indique si des informations de boucle sont présentes sur le fichier.
        /// </summary>
        public bool LoopFlag
        {
            get => m_loopFlag;
            set
            {
                if (value != LoopFlag)
                {
                    var tmp = m_loopFlag;
                    var strtmp = LoopFlagString;

                    m_loopFlag = value;

                    NotifyPropertyChanged("LoopFlag", tmp, value);
                    NotifyPropertyChanged("LoopFlagString", strtmp, LoopFlagString);

                    if (!value)
                    {
                        LoopStart = 0;
                        LoopEnd = 0;
                    }
                }
            }
        }
        private bool m_loopFlag;

        /// <summary>
        /// Indique si des informations de boucle sont présentes sur le fichier.
        /// </summary>
        public string LoopFlagString => LoopFlag ? App.Str("TT_Yes") : App.Str("TT_No");

        #endregion

        #region LoopStart

        /// <summary>
        /// Début de la boucle du stream.
        /// </summary>
        public int LoopStart
        {
            get => m_loopStart;
            set
            {
                if (LoopFlag && value != LoopStart)
                {
                    var tmp = m_loopStart;
                    var timetmp = LoopStartTime;
                    var strtmp = LoopStartString;

                    m_loopStart = value;

                    NotifyPropertyChanged("LoopStart", tmp, value);
                    NotifyPropertyChanged("LoopStartTime", timetmp, LoopStartTime);
                    NotifyPropertyChanged("LoopStartString", strtmp, LoopStartString);
                }
            }
        }
        private int m_loopStart;

        /// <summary>
        /// Début de la boucle du stream.
        /// </summary>
        public Time LoopStartTime => SampleRate > 0 ? new Time((double)LoopStart / SampleRate) : new Time(0);

        /// <summary>
        /// Début de la boucle du stream.
        /// </summary>
        public string LoopStartString => HMSSamplesDisplay ?
            LoopStart + " " + App.Str("TT_samples") + " (" + LoopStartTime.ToString(SamplesDisplayMaxDec, true).TrimStart("00:", 1) + ")" :
            LoopStart + " " + App.Str("TT_samples") + " (" + LoopStartTime.TotalSeconds.ToString(SamplesDisplayMaxDec, true) + " " + App.Str("TT_seconds") + ")";

        #endregion

        #region LoopEnd

        /// <summary>
        /// Fin de la boucle du stream.
        /// </summary>
        public int LoopEnd
        {
            get => m_loopEnd;
            set
            {
                if (LoopFlag && value != LoopEnd)
                {
                    var tmp = m_loopEnd;
                    var timetmp = LoopEndTime;
                    var strtmp = LoopEndString;

                    m_loopEnd = value;

                    NotifyPropertyChanged("LoopEnd", tmp, value);
                    NotifyPropertyChanged("LoopEndTime", timetmp, LoopEndTime);
                    NotifyPropertyChanged("LoopEndString", strtmp, LoopEndString);
                }
            }
        }
        private int m_loopEnd;

        /// <summary>
        /// Fin de la boucle du stream.
        /// </summary>
        public Time LoopEndTime => SampleRate > 0 ? new Time((double)LoopEnd / SampleRate) : new Time(0);

        /// <summary>
        /// Fin de la boucle du stream.
        /// </summary>
        public string LoopEndString => HMSSamplesDisplay ?
            LoopEnd + " " + App.Str("TT_samples") + " (" + LoopEndTime.ToString(SamplesDisplayMaxDec, true).TrimStart("00:", 1) + ")" :
            LoopEnd + " " + App.Str("TT_samples") + " (" + LoopEndTime.TotalSeconds.ToString(SamplesDisplayMaxDec, true) + " " + App.Str("TT_seconds") + ")";

        #endregion

        #region SampleRate

        /// <summary>
        /// Taux d'échantillonnage du stream.
        /// </summary>
        public int SampleRate
        {
            get => m_sampleRate;
            set
            {
                if (value != SampleRate)
                {
                    var tmp = m_sampleRate;
                    var strtmp = SampleRateString;
                    var starttimetmp = LoopStartTime;
                    var endtimetmp = LoopEndTime;
                    var durtmp = Duration;
                    var strstarttimetmp = LoopStartString;
                    var strendtimetmp = LoopEndString;
                    var strdurtmp = DurationString;

                    m_sampleRate = value;

                    NotifyPropertyChanged("SampleRate", tmp, value);
                    NotifyPropertyChanged("SampleRateString", strtmp, SampleRateString);
                    if (LoopStart > 0)
                    {
                        NotifyPropertyChanged("LoopStartTime", starttimetmp, LoopStartTime);
                        NotifyPropertyChanged("LoopStartString", strstarttimetmp, LoopStartString);
                    }
                    if (LoopEnd > 0)
                    {
                        NotifyPropertyChanged("LoopEndTime", endtimetmp, LoopEndTime);
                        NotifyPropertyChanged("LoopEndString", strendtimetmp, LoopEndString);
                    }
                    if (TotalSamples > 0)
                    {
                        NotifyPropertyChanged("Duration", durtmp, Duration);
                        NotifyPropertyChanged("DurationString", strdurtmp, DurationString);
                    }
                }
            }
        }
        private int m_sampleRate;

        /// <summary>
        /// Taux d'échantillonnage du stream.
        /// </summary>
        public string SampleRateString { get => SampleRate.ToString() + " Hz"; }

        #endregion

        #region TotalSamples & Duration

        /// <summary>
        /// Nombre total d'échantillons du stream.
        /// </summary>
        public int TotalSamples
        {
            get => m_totalSamples;
            set
            {
                if (value != TotalSamples)
                {
                    var tmp = m_totalSamples;
                    var strtmp = TotalSamplesString;
                    var durtmp = Duration;
                    var strdurtmp = DurationString;

                    m_totalSamples = value;

                    NotifyPropertyChanged("TotalSamples", tmp, value);
                    NotifyPropertyChanged("TotalSamplesString", strtmp, TotalSamplesString);
                    NotifyPropertyChanged("Duration", durtmp, Duration);
                    NotifyPropertyChanged("DurationString", strdurtmp, DurationString);
                }
            }
        }
        private int m_totalSamples;

        /// <summary>
        /// Nombre total d'échantillons du stream.
        /// </summary>
        public string TotalSamplesString => HMSSamplesDisplay ?
            TotalSamples + " " + App.Str("TT_samples") + " (" + Duration.ToString(SamplesDisplayMaxDec, true).TrimStart("00:", 1) + ")" :
            TotalSamples + " " + App.Str("TT_samples") + " (" + Duration.TotalSeconds.ToString(SamplesDisplayMaxDec, true) + " " + App.Str("TT_seconds") + ")";

        /// <summary>
        /// Durée du stream.
        /// </summary>
        public Time Duration => SampleRate > 0 ? new Time((double)TotalSamples / SampleRate) : new Time(0);

        /// <summary>
        /// Durée du stream.
        /// </summary>
        public string DurationString => Duration.ToString("hh:mm:ss.ssss").TrimStart("00:", 1);

        #endregion

        #endregion

        #region Sortie

        /// <summary>
        /// Délai avant le fondu de sortie du stream.
        /// </summary>
        public double FadeDelay
        {
            get => m_fadeDelay;
            set
            {
                if (value != FadeDelay && value >= 0)
                {
                    var tmp = m_fadeDelay;
                    var tmptp = SamplesToPlay;
                    var strtmptp = SamplesToPlayString;
                    m_fadeDelay = value;
                    NotifyPropertyChanged("FadeDelay", tmp, value);
                    NotifyPropertyChanged("SamplesToPlay", tmptp, SamplesToPlay);
                    NotifyPropertyChanged("SamplesToPlayString", strtmptp, SamplesToPlayString);
                }
            }
        }
        private double m_fadeDelay = DefaultOutData.FadeDelay ?? 0;

        /// <summary>
        /// Durée du fondu de sortie du stream.
        /// </summary>
        public double FadeTime
        {
            get => m_fadeTime;
            set
            {
                if (value != FadeTime && value >= 0)
                {
                    var tmp = m_fadeTime;
                    var tmptp = SamplesToPlay;
                    var strtmptp = SamplesToPlayString;
                    m_fadeTime = value;
                    NotifyPropertyChanged("FadeTime", tmp, value);
                    NotifyPropertyChanged("SamplesToPlay", tmptp, SamplesToPlay);
                    NotifyPropertyChanged("SamplesToPlayString", strtmptp, SamplesToPlayString);
                }
            }
        }
        private double m_fadeTime = DefaultOutData.FadeTime ?? 10;

        /// <summary>
        /// Indique si le stream doit se terminer par un fondu de sortie.
        /// </summary>
        public bool FadeOut
        {
            get => m_fadeOut;
            set
            {
                if (value != FadeOut)
                {
                    var tmp = m_fadeOut;
                    var tmptp = SamplesToPlay;
                    var strtmptp = SamplesToPlayString;
                    m_fadeOut = value;
                    NotifyPropertyChanged("FadeOut", tmp, value);
                    NotifyPropertyChanged("SamplesToPlay", tmptp, SamplesToPlay);
                    NotifyPropertyChanged("SamplesToPlayString", strtmptp, SamplesToPlayString);
                }
            }
        }
        private bool m_fadeOut = DefaultOutData.FadeOut ?? true;

        /// <summary>
        /// Nombre de répétitions de la boucle du stream.
        /// </summary>
        public int LoopCount
        {
            get => m_loopCount;
            set
            {
                if (value != LoopCount && value >= 0)
                {
                    var tmp = m_loopCount;
                    var tmptp = SamplesToPlay;
                    var strtmptp = SamplesToPlayString;
                    m_loopCount = value;
                    NotifyPropertyChanged("LoopCount", tmp, value);
                    NotifyPropertyChanged("SamplesToPlay", tmptp, SamplesToPlay);
                    NotifyPropertyChanged("SamplesToPlayString", strtmptp, SamplesToPlayString);
                }
            }
        }
        private int m_loopCount = DefaultOutData.LoopCount ?? 2;

        /// <summary>
        /// Indique si la boucle est une boucle Début → Fin.
        /// </summary>
        public bool StartEndLoop
        {
            get => m_startEndLoop;
            set
            {
                if (value != StartEndLoop)
                {
                    var tmp = m_startEndLoop;
                    var tmptp = SamplesToPlay;
                    var strtmptp = SamplesToPlayString;
                    m_startEndLoop = value;
                    NotifyPropertyChanged("StartEndLoop", tmp, value);
                    NotifyPropertyChanged("SamplesToPlay", tmptp, SamplesToPlay);
                    NotifyPropertyChanged("SamplesToPlayString", strtmptp, SamplesToPlayString);
                }
            }
        }
        private bool m_startEndLoop = DefaultOutData.StartEndLoop ?? false;

        public int SamplesToPlay
        {
            get
            {
                checked
                {
                    try { return (int)(LoopStart + LoopCount * (StartEndLoop ? TotalSamples : LoopFlag ? LoopEnd - LoopStart : 0) + (FadeOut && (LoopFlag || StartEndLoop) ? (FadeTime + FadeDelay) * SampleRate : TotalSamples - LoopEnd)); }
                    catch { return -1; }
                }
            }
        }
        public string SamplesToPlayString => HMSSamplesDisplay ?
            SamplesToPlay + " " + App.Str("TT_samples") + " (" + (SampleRate > 0 ? new Time((double)SamplesToPlay / SampleRate) : new Time(0)).ToString(SamplesDisplayMaxDec, true).TrimStart("00:", 1) + ")" :
            SamplesToPlay + " " + App.Str("TT_samples") + " (" + (SampleRate > 0 ? new Time((double)SamplesToPlay / SampleRate) : new Time(0)).TotalSeconds.ToString(SamplesDisplayMaxDec, true) + " " + App.Str("TT_seconds") + ")";

        /// <summary>
        /// Données de sortie du fichier.
        /// </summary>
        public FichierOutData OutData => new FichierOutData() { OriginalDestination = OriginalDestination, FadeDelay = FadeDelay, FadeOut = FadeOut, FadeTime = FadeTime, LoopCount = LoopCount, StartEndLoop = StartEndLoop };
        
        #region Destination

        /// <summary>
        /// Dossier de destination du fichier.
        /// </summary>
        public string OriginalDestination
        {
            get => m_destination;
            set
            {
                if (value != OriginalDestination && (App.Str(value) != null || Directory.Exists(value)))
                {
                    var tmp = Destination;
                    var ortmp = OriginalDestination;

                    m_destination = value;

                    NotifyPropertyChanged("Destination", tmp, Destination);
                    NotifyPropertyChanged("OriginalDestination", ortmp, value);
                }
            }
        }
        private string m_destination = App.Str(DefaultOutData.OriginalDestination) != null || Directory.Exists(DefaultOutData.OriginalDestination) ? DefaultOutData.OriginalDestination : "DEST_Principal";

        /// <summary>
        /// Dossier de destination du fichier.
        /// </summary>
        public string Destination
        {
            get => App.Str(m_destination) ?? m_destination;
            set
            {
                if (value != Destination)
                {
                    string s;
                    if ((s = App.Res(value)) != null || Directory.Exists(value))
                    {
                        var tmp = Destination;
                        var ortmp = OriginalDestination;

                        m_destination = s ?? value;

                        NotifyPropertyChanged("Destination", tmp, value);
                        NotifyPropertyChanged("OriginalDestination", ortmp, OriginalDestination);
                    }
                }
            }
        }

        /// <summary>
        /// Destination du fichier.
        /// </summary>
        public string FinalDestination { get; set; }

        #endregion

        #endregion

        #endregion

        #endregion

        #region Constructeur

        public Fichier(string fileName, FichierOutData outData = default)
        {
            Path = fileName;
            OriginalDestination = outData.OriginalDestination ?? OriginalDestination;
            FadeDelay = outData.FadeDelay ?? FadeDelay;
            FadeTime = outData.FadeTime ?? FadeTime;
            LoopCount = outData.LoopCount ?? LoopCount;
            StartEndLoop = outData.StartEndLoop ?? StartEndLoop;
            App.LanguageChanged += App_LanguageChanged;
        }

        #endregion

        #region Méthodes

        public void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public void NotifyPropertyChanged(string propertyName, object oldValue, object newValue)
        {
            PropertyChangedExtended?.Invoke(this, new PropertyChangedExtendedEventArgs<object>(propertyName, oldValue, newValue));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetInvalid(string originalState = "FSTATE_Error")
        {
            OriginalState = originalState.TrimEnd(Environment.NewLine.ToCharArray());
            TextBrush = Application.Current.Resources["ErrorBrush"] as Brush;
            Icon = Application.Current.Resources["Error"];
            Invalid = true;
        }

        public void SetValid(string originalState = "FSTATE_Queued")
        {
            OriginalState = originalState.TrimEnd(Environment.NewLine.ToCharArray());
            TextBrush = Application.Current.Resources["ListViewTxtBrush"] as Brush;
            Icon = Application.Current.Resources["MusicFile"];
            Invalid = false;
        }

        public void RefreshValidity()
        {
            if (Invalid) SetInvalid(OriginalState);
        }

        public void Cancel() => m_cts.Cancel();
        public void CancelIfCancellable() { if (IsCancellable) Cancel(); }

        public void ResetCancellation() => m_cts = new CancellationTokenSource();

        public override string ToString() => Path;

        #endregion

        #region Events

        private void App_LanguageChanged(object sender, PropertyChangedExtendedEventArgs<string> e) => NotifyPropertyChanged(String.Empty);

        #endregion
    }

    [Serializable]
    public struct FichierOutData
    {
        public string OriginalDestination { get; set; }
        public bool? FadeOut { get; set; }
        public double? FadeDelay { get; set; }
        public double? FadeTime { get; set; }
        public int? LoopCount { get; set; }
        public bool? StartEndLoop { get; set; }
    }
}
