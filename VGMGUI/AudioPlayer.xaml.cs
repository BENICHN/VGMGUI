using BenLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vlc.DotNet.Core;
using Vlc.DotNet.Core.Interops.Signatures;

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

        private CancellationTokenSource m_cts;

        /// <summary>
        /// Fichier au format WAV contenant les données audio.
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// Fichier au format WAV contenant les données audio.
        /// </summary>
        public Stream Stream { get; private set; }

        /// <summary>
        /// Se produit qunad la lecture est arrêtée.
        /// </summary>
        public event EventHandler<VlcMediaPlayerEndReachedEventArgs> EndReached { add => Player.EndReached += value; remove => Player.EndReached -= value; }

        public event EventHandler<EventArgs<bool>> Stopped;

        /// <summary>
        /// Le fichier qui est actuellement lu ou décodé pour la lecture.
        /// </summary>
        public Fichier CurrentPlaying { get; set; }

        public IList<Fichier> Playlist { get; set; }

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

        public VlcMediaPlayer Player => App.VlcMediaPlayer;

        public double Position => positionslider.IsEnabled && !positionslider.IsMouseCaptureWithin ? Player.Position : positionslider.Value;

        private bool m_mute;
        public bool Mute
        {
            get => m_mute;
            set
            {
                Player.Audio.IsMute = m_mute = value;
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
                Player.Audio.Volume = m_volume = value;
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

        public string PositionString => new Time(Player.Length * positionslider.Value / 1000).ToString("hh:mm:ss");
        public string LengthString => Player.LengthTime().ToString("hh:mm:ss");

        #endregion

        #region Constructeur

        /// <summary>
        /// Initialise une nouvelle instance de la classe AudioPlayer
        /// </summary>
        public AudioPlayer()
        {
            InitializeComponent();
            DataContext = this;
            LoopTypeChanged += AudioPlayer_LoopTypeChanged;
            Player.PositionChanged += (sender, e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Position"));
            Player.LengthChanged += (sender, e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LengthString"));
        }

        ~AudioPlayer() => Player.TryDispose();

        #endregion

        #region Méthodes

        #region AudioControl

        /// <summary>
        /// Lit <see cref="FileName"/> pu <see cref="Stream"/>.
        /// </summary>
        /// <param name="cancellationToken"/>
        /// <returns>true si la lecture est bien en cours; null si un chargement est déjà en cours; sinon false.</returns>
        public async Task<bool?> Play()
        {
            if (Loading) return null;

            try
            {
                m_cts = new CancellationTokenSource();

                bool result = false;
                Loading = true;

                switch (Player.State)
                {
                    case MediaStates.NothingSpecial:
                    case MediaStates.Stopped:
                        {
                            if (await SetMedia(m_cts.Token))
                            {
                                try { await Player.PlayAsync(5000, m_cts.Token); }
                                catch { await Stop(true); }
                            }
                        }
                        break;
                    case MediaStates.Paused:
                        await Player.PlayAsync(5000);
                        break;
                }

                if (Player.State == MediaStates.Playing)
                {
                    positionslider.IsEnabled = true;
                    if (CurrentPlaying != null) CurrentPlaying.FontWeight = FontWeights.Bold;
                    PlayButtonSetPause();
                    App.VGMainWindow.TBISetPause();
                    Player.Audio.Volume = Volume;
                    Player.Audio.IsMute = Mute;
                    result = true;
                }
                else
                {
                    await Stop();
                    result = false;
                }

                return result;
            }
            catch { return false; }
            finally { Loading = false; }
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
                await Player.StopAsync();

                Stream = null;
                FileName = null;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Position"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LengthString"));

                m_cts?.Cancel();
                positionslider.IsEnabled = Loading = false;

                PlayButtonSetPlay(false);
                App.VGMainWindow.TBISetPlay(false);

                if (CurrentPlaying != null) CurrentPlaying.FontWeight = FontWeights.Normal;
                if (end)
                {
                    CurrentPlaying = null;
                    Playlist = null;
                }

                GC.Collect();

                Stopped?.Invoke(this, new EventArgs<bool>(end));

                return Player.State == MediaStates.Stopped;
            }
            finally { await VGMStream.DeleteTempFilesByType(VGMStreamProcessTypes.Streaming); }
        }

        /// <summary>
        /// Met la lecture en pause.
        /// </summary>
        public async Task<bool> Pause()
        {
            if (Player.State == MediaStates.Paused) return true;
            await Player.PauseAsync();
            PlayButtonSetPlay(true);
            App.VGMainWindow.TBISetPlay(true);
            return Player.State == MediaStates.Paused;
        }

        /// <summary>
        /// Met la lecture en pause ou la démarre.
        /// </summary>
        public async Task PlayPause()
        {
            if (Player.State == MediaStates.Playing) await Pause();
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
                var postPosition = Player.Position + value / 100;
                if (postPosition > 1) postPosition = 1;
                Player.Position = postPosition;
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
                var postPosition = Player.Position - value / 100;
                if (postPosition < 0) postPosition = 0;
                Player.Position = postPosition;
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
                FileName = null;
                Stream = null;

                FileName = filename;
            }
        }

        /// <summary>
        /// Définit les données audio lues par le lecteur à partir d'un stream.
        /// </summary>
        /// <param name="stream">Stream contenant les les données audio au format WAV</param>
        public void SetAudio(Stream stream)
        {
            Stream = null;
            FileName = null;

            Stream = stream;
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
                if (FileName != null) await Task.Run(() => Player.SetMedia(new FileInfo(FileName)), cancellationToken);
                else if (Stream != null)
                {
                    if (Player.Manager.VlcVersionNumber.Major < 3 && Stream is FileStream fs)
                    {
                        SetAudio(fs.Name);
                        return await SetMedia();
                    }
                    else await Task.Run(() => Player.SetMedia(Stream), cancellationToken);
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
        private void PlayButtonSetPlay(bool resume)
        {
            PlayButton.Content = Application.Current.Resources["Play"];
            PlayButton.SetResourceReference(ToolTipProperty, resume ? "AP_Resume" : "AP_Play");
        }

        /// <summary>
        /// Transforme <see cref="PlayButton"/> en bouton de pause.
        /// </summary>
        private void PlayButtonSetPause()
        {
            PlayButton.Content = Application.Current.Resources["Pause"];
            PlayButton.SetResourceReference(ToolTipProperty, "AP_Pause");
        }

        #endregion

        #endregion

        #region Events

        private async void PlayButton_Click(object sender, RoutedEventArgs e) { if (CurrentPlaying != null) await PlayPause(); }

        private void positionslider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (positionslider.Value < 0) positionslider.Value = 0;
            else if (positionslider.Value > 1) positionslider.Value = 1;
            Player.Position = (float)positionslider.Value;
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
                    LoopButton.Content = Application.Current.Resources["LoopB"];
                    Settings.SettingsData.Global["LoopType"] = "None";
                    await Settings.TryWriteSettings();
                    break;
                case LoopTypes.All:
                    LoopButton.SetResourceReference(ToolTipProperty, "AP_LOOP_All");
                    LoopButton.Content = Application.Current.Resources["LoopF"];
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
        public static async Task<bool> PlayAsync(this VlcMediaPlayer player, int millisecondsTimeout, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                player.Play();
                while (player.State != MediaStates.Playing && player.State != MediaStates.Error) continue;
            }, cancellationToken).WithTimeout(millisecondsTimeout);
            await Task.Delay(1);
            return player.State == MediaStates.Playing;
        }
        public static Task PauseAsync(this VlcMediaPlayer player, CancellationToken cancellationToken = default) => Task.Run(() => player.Pause(), cancellationToken);
        public static Time PositionTime(this VlcMediaPlayer player) => new Time(player.Position * player.Length / 1000);
        public static Time LengthTime(this VlcMediaPlayer player) => new Time(player.Length / 1000);
    }
}
