using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using BenLib.WPF;
using static VGMGUI.Settings;

namespace VGMGUI
{
    public class StatusBar
    {
        public static BoolToValueConverter<Visibility> BoolToVisibilityConverter = new BoolToValueConverter<Visibility>() { TrueValue = Visibility.Visible, FalseValue = Visibility.Collapsed };
        public static event EventHandler<PropertyChangedEventArgs> StaticPropertyChanged;

        private static bool s_display = true;
        private static bool s_counter = true;
        private static bool s_RAM = true;
        private static bool s_samplesDisplay = true;
        private static bool s_SearchDelay = true;
        private static bool s_PreAnalyse = true;
        private static bool s_StreamingType = true;

        public static bool Display
        {
            get => s_display;
            set
            {
                if (value != s_display)
                {
                    s_display = value;
                    SettingsData["StatusBar"]["Display"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("Display"));
                }
            }
        }
        public static bool Counter
        {
            get => s_counter;
            set
            {
                if (value != s_counter)
                {
                    s_counter = value;
                    SettingsData["StatusBar"]["Counter"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("Counter"));
                }
            }
        }
        public static bool RAM
        {
            get => s_RAM;
            set
            {
                if (value != s_RAM)
                {
                    s_RAM = value;
                    SettingsData["StatusBar"]["RAM"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("RAM"));
                }
            }
        }
        public static bool SamplesDisplay
        {
            get => s_samplesDisplay;
            set
            {
                if (value != s_samplesDisplay)
                {
                    s_samplesDisplay = value;
                    SettingsData["StatusBar"]["SamplesDisplay"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("SamplesDisplay"));
                }
            }
        }
        public static bool SearchDelay
        {
            get => s_SearchDelay;
            set
            {
                if (value != s_SearchDelay)
                {
                    s_SearchDelay = value;
                    SettingsData["StatusBar"]["SearchDelay"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("SearchDelay"));
                }
            }
        }
        public static bool PreAnalyse
        {
            get => s_PreAnalyse;
            set
            {
                if (value != s_PreAnalyse)
                {
                    s_PreAnalyse = value;
                    SettingsData["StatusBar"]["PreAnalyse"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("PreAnalyse"));
                }
            }
        }
        public static bool StreamingType
        {
            get => s_StreamingType;
            set
            {
                if (value != s_StreamingType)
                {
                    s_StreamingType = value;
                    SettingsData["StatusBar"]["StreamingType"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("StreamingType"));
                }
            }
        }
    }

    public class StreamingTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StreamingType streamingType)
            {
                switch (streamingType)
                {
                    case StreamingType.Cache:
                        return Application.Current.Resources["File"];
                    case StreamingType.Live:
                        return Application.Current.Resources["Streaming"];
                    default: return null;
                }
            }
            else return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Viewbox viewbox)
            {
                switch (viewbox.Name)
                {
                    case "File":
                        return StreamingType.Cache;
                    case "Streaming":
                        return StreamingType.Live;
                    default: return null;
                }
            }
            else return null;
        }
    }
}
