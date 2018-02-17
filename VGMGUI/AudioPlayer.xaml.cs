using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenLib;
using System.Collections.Generic;
using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Interops.Signatures;
using System.ComponentModel;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour AudioPlayer.xaml
    /// </summary>
    public partial class AudioPlayer : UserControl, INotifyPropertyChanged
    {
        #region Champs & Propriétés

        /// <summary>
        /// Indique si <see cref="Play"/> est en cours.
        /// </summary>
        public bool Loading { get; set; }

        /// <summary>
        /// Fichier au format WAV contenant les données audio.
        /// </summary>
        private string m_filename;

        /// <summary>
        /// Fichier au format WAV contenant les données audio.
        /// </summary>
        private Stream m_stream;

        /// <summary>
        /// Fichier au format WAV contenant les données audio.
        /// </summary>
        public string FileName => m_filename;

        /// <summary>
        /// Stream contenant les les données audio au format WAV.
        /// </summary>
        public Stream Stream => m_stream;

        /// <summary>
        /// Statut de la lecture.
        /// </summary>
        public MediaStates State => m_player.State;

        /// <summary>
        /// Se produit qunad la lecture est arrêtée.
        /// </summary>
        public event EventHandler<VlcMediaPlayerEndReachedEventArgs> EndReached { add => m_player.EndReached += value; remove => m_player.EndReached -= value; }

        public event EventHandler<AudioPlayerStopEventArgs> Stopped;

        /// <summary>
        /// Le fichier qui est actuellement lu ou décodé pour la lecture.
        /// </summary>
        public Fichier CurrentPlaying { get; set; }

        public List<Fichier> Playlist { get; set; }

        /// <summary>
        /// Se produit quand <see cref="LoopType"/> change de valeur.
        /// </summary>
        public event PropertyChangedExtendedEventHandler<LoopTypes> LoopTypeChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Action à effectuer à la fin de la lecture d'un fichier.
        /// </summary>
        private LoopTypes m_loopType = LoopTypes.None;

        /// <summary>
        /// Action à effectuer à la fin de la lecture d'un fichier.
        /// </summary>
        public LoopTypes LoopType
        {
            get => m_loopType;
            set
            {
                if (LoopType != value)
                {
                    var tmp = LoopType;
                    m_loopType = value;
                    LoopTypeChanged?.Invoke(this, new PropertyChangedExtendedEventArgs<LoopTypes>("LoopType", tmp, value));
                }
            }
        }

        private VlcMediaPlayer m_player;
        public VlcMediaPlayer Player => m_player;

        public double Position => positionslider.IsEnabled && !positionslider.IsMouseCaptureWithin ? m_player.Position : positionslider.Value;

        private bool m_mute;
        public bool Mute
        {
            get => m_mute;
            set
            {
                m_player.Audio.IsMute = m_mute = value;
                volumeslider.Opacity = value ? 0.313 : 1;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("VolumeIcon"));
                Settings.SettingsData.Global["Mute"] = value.ToString();
            }
        }

        private int m_volume = 100;
        public int Volume
        {
            get => m_volume;
            set
            {
                if (value < 0) value = 0;
                if (value > 100) value = 100;
                Mute = false;
                m_player.Audio.Volume = m_volume = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Volume"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("VolumeIcon"));
                Settings.SettingsData.Global["Volume"] = Volume.ToString();
            }
        }

        public object VolumeIcon
        {
            get
            {
                if (Mute) return Application.Current.Resources["Mute"];
                if (Volume > 66) return Application.Current.Resources["High Volume"];
                else if (Volume >= 33 && Volume <= 66) return Application.Current.Resources["Medium Volume"];
                else if (Volume < 33 && Volume > 0) return Application.Current.Resources["Low Volume"];
                else return Application.Current.Resources["Mute"];
            }
        }

        public string PositionString => new Time(m_player.Length * positionslider.Value / 1000).ToString("hh:mm:ss");
        public string LengthString => m_player.LengthTime().ToString("hh:mm:ss");

        #endregion

        #region Constructeur

        /// <summary>
        /// Initialise une nouvelle instance de la classe AudioPlayer
        /// </summary>
        public AudioPlayer()
        {
            try { m_player = new VlcMediaPlayer(new DirectoryInfo(App.VLCFolder)); }
            catch (Exception ex) { throw new VLCException("Impossible de créer un objet de type VlcMediaPlayer.", ex) { Source = App.VLCFolder }; }
            InitializeComponent();
            DataContext = this;
            LoopTypeChanged += AudioPlayer_LoopTypeChanged;
            m_player.PositionChanged += (sender, e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Position"));
            m_player.LengthChanged += (sender, e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LengthString"));
        }

        #endregion

        #region Méthodes

        #region AudioControl

        /// <summary>
        /// Lit <see cref="FileName"/> pu <see cref="Stream"/>.
        /// </summary>
        /// <param name="cancellationToken"/>
        /// <returns>true si la lecture est bien en cours; null si un chargement est déjà en cours; sinon false.</returns>
        public async Task<bool?> Play(CancellationToken cancellationToken = default)
        {
            if (Loading) return null;

            bool result = false;
            Loading = true;

            switch (m_player.State)
            {
                case MediaStates.NothingSpecial:
                case MediaStates.Stopped:
                    {
                        if (await SetMedia(cancellationToken))
                        {
                            try
                            {
                                await m_player.PlayAsync(cancellationToken);
                                positionslider.IsEnabled = true;
                                m_player.Audio.Volume = m_volume;
                                m_player.Audio.IsMute = m_mute;
                                if (CurrentPlaying != null) CurrentPlaying.FontWeight = FontWeights.Bold;
                            }
                            catch { Stop(true); }
                        }
                    }
                    break;
                case MediaStates.Paused:
                    await m_player.PlayAsync();
                    break;
            }

            if (m_player.State == MediaStates.Playing)
            {
                PlayButtonSetPause();
                App.MainWindow.TBISetPause();
                result = true;
            }
            else result = false;

            Loading = false;
            return result;
        }

        /// <summary>
        /// Arrête la lecture.
        /// </summary>
        /// <param name="end">false pour adopter le comportement défini par <see cref="LoopType"/>; sinon true.</param>
        /// <returns>true si la lecture est bien arrêtée; sinon false.</returns>
        public async Task<bool> Stop(bool end = false)
        {
            try
            {
                await m_player.StopAsync();

                m_stream = null;
                m_filename = null;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Position"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LengthString"));

                positionslider.IsEnabled = false;

                PlayButtonSetPlay(false);
                App.MainWindow.TBISetPlay(false);

                if (CurrentPlaying != null) CurrentPlaying.FontWeight = FontWeights.Normal;
                if (end)
                {
                    CurrentPlaying = null;
                    Playlist = null;
                }

                GC.Collect();

                Stopped?.Invoke(this, new AudioPlayerStopEventArgs(end));

                return m_player.State == MediaStates.Stopped;
            }
            finally { if (end) { await VGMStream.DeleteTMPFiles(); } }
        }

        /// <summary>
        /// Met la lecture en pause.
        /// </summary>
        public async Task<bool> Pause()
        {
            await m_player.PauseAsync();
            PlayButtonSetPlay(true);
            App.MainWindow.TBISetPlay(true);
            return m_player.State == MediaStates.Paused;
        }

        /// <summary>
        /// Met la lecture en pause ou la démarre.
        /// </summary>
        public async Task PlayPause()
        {
            if (m_player.State == MediaStates.Playing) await Pause();
            else await Play();
        }

        /// <summary>
        /// Modifie la position de la lecture.
        /// </summary>
        /// <param name="value">La valeur à soustraire (%).</param>
        public async Task PositionPlus(float value = 5)
        {
            await Task.Run(() =>
            {
                var postPosition = m_player.Position + value / 100;
                if (postPosition > 1) postPosition = 1;
                m_player.Position = postPosition;
            });
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Position"));
        }

        /// <summary>
        /// Modifie la position de la lecture.
        /// </summary>
        /// <param name="value">La valeur à additionner (%).</param>
        public async Task PositionMinus(float value = 5)
        {
            await Task.Run(() =>
            {
                var postPosition = m_player.Position - value / 100;
                if (postPosition < 0) postPosition = 0;
                m_player.Position = postPosition;
            });
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Position"));
        }

        #endregion

        #region Audio

        /// <summary>
        /// Définit les données audio lues par le lecteur à partir d'un fichier audio.
        /// </summary>
        /// <param name="filename">Fichier au format WAV contenant les données audio</param>
        public void SetAudio(string filename)
        {
            if (File.Exists(filename))
            {
                m_filename = null;
                m_stream = null;

                m_filename = filename;
            }
        }

        /// <summary>
        /// Définit les données audio lues par le lecteur à partir d'un stream.
        /// </summary>
        /// <param name="stream">Stream contenant les les données audio au format WAV</param>
        public void SetAudio(Stream stream)
        {
            m_stream = null;
            m_filename = null;

            m_stream = stream;
        }

        /// <summary>
        /// Définit wfr à partir du stream ou du nom de fichier contenu dans la mémoire.
        /// </summary>
        /// <param name="cancellationToken"/>
        /// <returns>true si wfr a été défini; sinon, false.</returns>
        private async Task<bool> SetMedia(CancellationToken cancellationToken = default)
        {
            try
            {
                if (m_filename != null)
                {
                    await Task.Run(() => m_player.SetMedia(new FileInfo(m_filename)), cancellationToken);
                }
                else if (m_stream != null)
                {
                    if (m_stream is FileStream fs) await Task.Run(() => m_player.SetMedia(new FileInfo(fs.Name)), cancellationToken);
                    else await Task.Run(() => m_player.SetMedia(m_stream), cancellationToken);
                }
                else return false;

                return true;
            }
            catch { return false; }
        }

        #endregion

        #region UI

        /// <summary>
        /// Transforme <see cref="PlayButton"/> en bouton de lecture.
        /// </summary>
        void PlayButtonSetPlay(bool resume)
        {
            PlayButton.Content = Application.Current.Resources["Play"];
            PlayButton.SetResourceReference(ToolTipProperty, resume ? "AP_Resume" : "AP_Play");
        }

        /// <summary>
        /// Transforme <see cref="PlayButton"/> en bouton de pause.
        /// </summary>
        void PlayButtonSetPause()
        {
            PlayButton.Content = Application.Current.Resources["Pause"];
            PlayButton.SetResourceReference(ToolTipProperty, "AP_Pause");
        }

        #endregion

        #endregion

        #region Events

        private async void PlayButton_Click(object sender, RoutedEventArgs e) => await PlayPause();

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Shift:
                    App.FreeMemory();
                    break;
                default:
                    await Stop(true);
                    break;
            }
        }

        private void positionslider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (positionslider.Value < 0) positionslider.Value = 0;
            else if (positionslider.Value > 1) positionslider.Value = 1;
            m_player.Position = (float)positionslider.Value;
        }

        private void positionslider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PositionString"));

        private async void positionslider_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta < 0) await PositionMinus();
            else await PositionPlus();
        }

        private void volumeslider_MouseWheel(object sender, MouseWheelEventArgs e) => Volume += e.Delta > 0 ? 5 : -5;

        private void VolumeIcon_PreviewMouseUp(object sender, MouseButtonEventArgs e) => Mute = !Mute;

        private async void AudioPlayer_LoopTypeChanged(object sender, PropertyChangedExtendedEventArgs<LoopTypes> e)
        {
            switch (LoopType)
            {
                case LoopTypes.None:
                    LoopButton.SetResourceReference(ToolTipProperty, "AP_LOOP_None");
                    LoopButton.Content = Application.Current.Resources["Loop"];
                    foreach (System.Windows.Shapes.Path path in ((LoopButton.Content as Viewbox).Child as Canvas).Children) path.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "RadioButtonForeground");
                    Settings.SettingsData.Global["LoopType"] = "None";
                    await Settings.TryWriteSettings();
                    break;
                case LoopTypes.All:
                    LoopButton.SetResourceReference(ToolTipProperty, "AP_LOOP_All");
                    LoopButton.Content = Application.Current.Resources["Loop"];
                    foreach (System.Windows.Shapes.Path path in ((LoopButton.Content as Viewbox).Child as Canvas).Children) path.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "ForegroundBrush");
                    Settings.SettingsData.Global["LoopType"] = "All";
                    await Settings.TryWriteSettings();
                    break;
                case LoopTypes.Random:
                    LoopButton.SetResourceReference(ToolTipProperty, "AP_LOOP_Random");
                    LoopButton.Content = Application.Current.Resources["Random"];
                    Settings.SettingsData.Global["LoopType"] = "Random";
                    await Settings.TryWriteSettings();
                    break;
            }
        }

        private void LoopButton_Click(object sender, RoutedEventArgs e)
        {
            switch (LoopType)
            {
                case LoopTypes.None:
                    LoopType = LoopTypes.All;
                    break;
                case LoopTypes.All:
                    LoopType = LoopTypes.Random;
                    break;
                case LoopTypes.Random:
                    LoopType = LoopTypes.None;
                    break;
            }
        }

        #endregion
    }

    public class AudioPlayerStopEventArgs
    {
        public AudioPlayerStopEventArgs(bool end) => End = end;

        public bool End { get; set; }
    }

    public enum LoopTypes { None, All, Random }

    public static class VLCExtensions
    {
        public static async Task StopAsync(this VlcMediaPlayer player, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                player.Stop();
                while (player.State != MediaStates.Stopped && player.State != MediaStates.Error) continue;
            }, cancellationToken);
            await Task.Delay(1);
        }
        public static async Task PlayAsync(this VlcMediaPlayer player, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                player.Play();
                while (player.State != MediaStates.Playing && player.State != MediaStates.Error) continue;
            }, cancellationToken);
            await Task.Delay(1);
        }
        public static Task PauseAsync(this VlcMediaPlayer player, CancellationToken cancellationToken = default) => Task.Run(() => player.Pause(), cancellationToken);
        public static Time PositionTime(this VlcMediaPlayer player) => new Time(player.Position * player.Length / 1000);
        public static Time LengthTime(this VlcMediaPlayer player) => new Time(player.Length / 1000);
    }

    public class VLCException : Exception
    {
        public VLCException(string message, Exception innerException) : base(message, innerException) { }
    }
}
