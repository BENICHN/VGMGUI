using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CSCore;
using CSCore.SoundOut;
using CSCore.Codecs.WAV;
using BenLib;
using System.Collections.Generic;

namespace VGMGUI
{
    /// <summary>
    /// Logique d'interaction pour AudioPlayer.xaml
    /// </summary>
    public partial class AudioPlayer : UserControl
    {
        #region Champs & Propriétés

        /// <summary>
        /// Valeur temporaire du volume de la sortie audio.
        /// </summary>
        float m_volumeTMP;

        /// <summary>
        /// Volume de la sortie audio.
        /// </summary>
        float m_volume;

        /// <summary>
        /// Volume de la sortie audio.
        /// </summary>
        public float Volume
        {
            get => m_volume;
            set
            {
                if (value > 1) value = 1;
                else if (value < 0) value = 0;

                volumeslider.Value = value * 100;
            }
        }

        /// <summary>
        /// Indique si <see cref="PlayAsync"/> est en cours.
        /// </summary>
        bool Loading { get; set; }

        /// <summary>
        /// À chaque Tick, modifie la valeur de <see cref="positionslider"/> en fonction de l'avancement de la lecture.
        /// </summary>
        DispatcherTimer dt = new DispatcherTimer() { Interval = new TimeSpan(0, 0, 0, 0, 250) };

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
        public PlaybackState PlaybackState => audioOutput.PlaybackState;

        /// <summary>
        /// Se produit qunad la lecture est arrêtée.
        /// </summary>
        public event EventHandler<PlaybackStoppedEventArgs> PlaybackStopped { add => audioOutput.Stopped += value; remove => audioOutput.Stopped -= value; }

        public event EventHandler<AudioPlayerStopEventArgs> Stopped;

        /// <summary>
        /// Se produit quand la lecture est terminée.
        /// </summary>
        public event EventHandler StreamFinished;

        /// <summary>
        /// La fenêtre hôte de ce lecteur.
        /// </summary>
        MainWindow MainWin => Window.GetWindow(this) as MainWindow;

        /// <summary>
        /// Le fichier qui est actuellement lu ou décodé pour la lecture.
        /// </summary>
        public Fichier CurrentPlaying { get; set; }

        public List<Fichier> Playlist { get; set; }

        /// <summary>
        /// Se produit quand <see cref="LoopType"/> change de valeur.
        /// </summary>
        public event PropertyChangedExtendedEventHandler<LoopTypes> LoopTypeChanged;

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

        /// <summary>
        /// Contient les données de la lecture.
        /// </summary>
        public IWaveSource WaveSource => wfr;

        /// <summary>
        /// Contient les données de la lecture.
        /// </summary>
        IWaveSource wfr;

        /// <summary>
        /// Sortie audio utilisée pour la lecture.
        /// </summary>
        public ISoundOut audioOutput;

        #endregion

        #region Constructeur

        /// <summary>
        /// Initialise une nouvelle instance de la classe AudioPlayer
        /// </summary>
        public AudioPlayer()
        {
            InitializeComponent();
            audioOutput = GetSoundOut();
            dt.Tick += Dt_Tick;
            LoopTypeChanged += AudioPlayer_LoopTypeChanged;
        }

        ~AudioPlayer()
        {
            Settings.SettingsData.Global["Volume"] = Volume.ToString();
            Settings.TryWriteSettings();
        }

        #endregion

        #region Méthodes

        #region AudioControl

        /// <summary>
        /// Arrête la lecture.
        /// </summary>
        public void Stop(bool end = false)
        {
            audioOutput.Stop();
            dt.Stop();
            if (m_stream is FileStream fs)
            {
                fs.Close();
                Threading.MultipleAttempts(() => File.Delete(fs.Name), throwEx: false);
            }
            m_stream = null;
            m_filename = null;
            if (!Loading) wfr.TryDispose();
            positionslider.Value = 0;
            positionslider.IsEnabled = false;
            totaltimelabel.Content = "00:00:00";
            currenttimelabel.Content = "00:00:00";
            positionslider.IsEnabled = false;
            PlayButtonSetPlay(false);
            MainWin.TBISetPlay(false);
            if (CurrentPlaying != null) CurrentPlaying.FontWeight = FontWeights.Normal;
            if (end)
            {
                CurrentPlaying = null;
                Playlist = null;
            }
            GC.Collect();
            Stopped?.Invoke(this, new AudioPlayerStopEventArgs(end));
        }

        /// <summary>
        /// Démarre la lecture ou la reprend là où elle en était.
        /// </summary>
        public void Play()
        {
            if (audioOutput.PlaybackState == PlaybackState.Paused)
            {
                audioOutput.Play();
            }
            else if (audioOutput.PlaybackState == PlaybackState.Stopped)
            {
                if (SetWFR())
                {
                    try
                    {
                        audioOutput.Initialize(wfr);
                        try { audioOutput.Volume = Volume; }
                        catch { }
                        audioOutput.Play();
                        positionslider.IsEnabled = true;
                        totaltimelabel.Content = wfr.GetLength().ToString(@"hh\:mm\:ss");
                        if (CurrentPlaying != null) CurrentPlaying.FontWeight = FontWeights.Bold;
                    }
                    catch { Stop(true); }
                }
            }

            if (audioOutput.PlaybackState == PlaybackState.Playing)
            {
                PlayButtonSetPause();
                MainWin.TBISetPause();
                dt.Start();
            }
        }

        /// <summary>
        /// Démarre la lecture ou la reprend là où elle en était.
        /// </summary>
        /// <param name="cancellationToken">Jeton d'annulation qui peut être utilisé par d'autres objets ou threads pour être informés de l'annulation.</param>
        /// <returns>Tâche qui représente l'opération de lecture asynchrone.</returns>
        public async Task PlayAsync(CancellationToken cancellationToken = default)
        {
            if (!Loading)
            {
                try
                {
                    Loading = true;

                    await Task.Run(() =>
                    {
                        if (audioOutput.PlaybackState == PlaybackState.Paused)
                        {
                            audioOutput.Play();
                        }
                        else if (audioOutput.PlaybackState == PlaybackState.Stopped)
                        {
                            if (SetWFR())
                            {
                                try
                                {
                                    audioOutput.Initialize(wfr);
                                    try { audioOutput.Volume = Volume; }
                                    catch { }
                                    audioOutput.Play();
                                    Dispatcher.Invoke(() =>
                                    {
                                        positionslider.IsEnabled = true;
                                        totaltimelabel.Content = wfr.GetLength().ToString(@"hh\:mm\:ss");
                                        if (CurrentPlaying != null) CurrentPlaying.FontWeight = FontWeights.Bold;
                                    });
                                }
                                catch { Dispatcher.Invoke(() => Stop(true)); }
                            }
                        }

                        if (audioOutput.PlaybackState == PlaybackState.Playing)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                PlayButtonSetPause();
                                MainWin.TBISetPause();
                            });
                            dt.Start();
                        }
                    }, cancellationToken);

                    Loading = false;
                }
                finally
                {
                    if (cancellationToken.IsCancellationRequested) Loading = false;
                }
            }
        }

        /// <summary>
        /// Met la lecture en pause ou la démarre.
        /// </summary>
        public void PlayPause()
        {
            if (audioOutput.PlaybackState == PlaybackState.Playing) Pause();
            else Play();
        }

        /// <summary>
        /// Met la lecture en pause.
        /// </summary>
        public void Pause()
        {
            audioOutput.Pause();
            PlayButtonSetPlay(true);
            MainWin.TBISetPlay(true);
        }

        /// <summary>
        /// Modifie la position de la lecture.
        /// </summary>
        /// <param name="value">La valeur à soustraire (%).</param>
        public void PositionPlus(int value = 5)
        {
            if (positionslider.IsEnabled)
            {
                positionslider.Value += value;
                try { wfr?.SetPosition(new TimeSpan(0, 0, 0, 0, (int)(positionslider.Value * wfr.GetLength().TotalMilliseconds / 100))); }
                catch (ArgumentOutOfRangeException) { Stop(LoopType == LoopTypes.None); }
            }
        }

        /// <summary>
        /// Modifie la position de la lecture.
        /// </summary>
        /// <param name="value">La valeur à additionner (%).</param>
        public void PositionMinus(int value = 5)
        {
            if (positionslider.IsEnabled)
            {
                positionslider.Value -= value;
                wfr?.SetPosition(new TimeSpan(0, 0, 0, 0, (int)(positionslider.Value * wfr.GetLength().TotalMilliseconds / 100)));
            }
        }

        /// <summary>
        /// Modifie la valeur de "volumeslider" pour ainsi augmenter le volume.
        /// </summary>
        /// <param name="value">La valeur à additionner (%).</param>
        public void VolumePlus(int value = 5) => Volume += (float)value / 100;

        /// <summary>
        /// Modifie la valeur de "volumeslider" pour ainsi diminuer le volume.
        /// </summary>
        /// <param name="value">La valeur à soustraire (%).</param>
        public void VolumeMinus(int value = 5) => Volume -= (float)value / 100;

        #endregion

        #region Audio

        /// <summary>
        /// Obtient un objet de type <see cref="ISoundOut"/> adapté au système.
        /// </summary>
        private ISoundOut GetSoundOut()
        {
            if (WasapiOut.IsSupportedOnCurrentPlatform) return new WasapiOut();
            else return new WaveOut();
        }

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
        /// <returns>true si wfr a été défini; sinon, false.</returns>
        private bool SetWFR()
        {
            try
            {
                if (m_filename != null)
                {
                    wfr = new WaveFileReader(m_filename);
                    return true;
                }
                else if (m_stream != null)
                {
                    wfr = new WaveFileReader(m_stream);
                    return true;
                }
                else return false;
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

        private void PlayButton_Click(object sender, RoutedEventArgs e) => PlayPause();

        private void StopButton_Click(object sender, RoutedEventArgs e) => Stop(true);

        private void Dt_Tick(object sender, EventArgs e)
        {
            try
            {
                if (wfr != null && wfr.Length > 0 && positionslider.IsEnabled && !positionslider.IsMouseCaptureWithin)
                {
                    positionslider.Value = wfr.GetPosition().TotalMilliseconds / wfr.GetLength().TotalMilliseconds * 100;
                    if (wfr.GetPosition().TotalMilliseconds == wfr.GetLength().TotalMilliseconds)
                    {
                        Stop(LoopType == LoopTypes.None);
                        StreamFinished?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch { }
        }

        private void positionslider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (positionslider.IsEnabled)
            {
                if (wfr != null)
                {
                    TimeSpan t = new TimeSpan(0, 0, (int)(positionslider.Value * wfr.GetLength().TotalSeconds / 100));
                    currenttimelabel.Content = t.ToString(@"hh\:mm\:ss");
                }
            }
            else
            {
                positionslider.ValueChanged -= positionslider_ValueChanged;
                positionslider.Value = e.OldValue;
                positionslider.ValueChanged += positionslider_ValueChanged;
            }
        }

        private void positionslider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            try { wfr?.SetPosition(new TimeSpan(0, 0, 0, 0, (int)(positionslider.Value * wfr.GetLength().TotalMilliseconds / 100))); }
            catch (ArgumentOutOfRangeException)
            {
                if (positionslider.Value * wfr.GetLength().TotalMilliseconds / 100 > 0) wfr.Position = wfr.Length;
                else wfr.Position = 0;
            }
        }

        private void positionslider_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if (e.Delta < 0)
                {
                    positionslider.Value--;
                    wfr?.SetPosition(new TimeSpan(0, 0, 0, 0, (int)(positionslider.Value * wfr.GetLength().TotalMilliseconds / 100)));
                }
                else
                {
                    positionslider.Value++;
                    wfr?.SetPosition(new TimeSpan(0, 0, 0, 0, (int)(positionslider.Value * wfr.GetLength().TotalMilliseconds / 100)));
                }
            }
            catch { }
        }

        private void volumeslider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try { audioOutput.Volume = m_volume = (float)volumeslider.Value / 100; }
            catch { }

            if (volumeslider.Value > 66) VolumeIcon.Content = Application.Current.Resources["High Volume"];
            else if (volumeslider.Value >= 33 && volumeslider.Value <= 66) VolumeIcon.Content = Application.Current.Resources["Medium Volume"];
            else if (volumeslider.Value < 33 && volumeslider.Value > 0) VolumeIcon.Content = Application.Current.Resources["Low Volume"];
            else VolumeIcon.Content = Application.Current.Resources["Mute"];
        }

        private void volumeslider_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) VolumePlus();
            else VolumeMinus();
        }

        private void VolumeIcon_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Volume > 0)
            {
                m_volumeTMP = Volume;
                Volume = 0;
            }
            else Volume = m_volumeTMP > 0 ? m_volumeTMP : 1;
        }

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
}
