using BenLib.WPF;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        private static bool s_searchDelay = true;
        private static bool s_preAnalyse = true;
        private static bool s_streamingType = true;
        private static bool s_size = true;
        private static bool s_conversion = true;

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
            get => s_searchDelay;
            set
            {
                if (value != s_searchDelay)
                {
                    s_searchDelay = value;
                    SettingsData["StatusBar"]["SearchDelay"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("SearchDelay"));
                }
            }
        }
        public static bool PreAnalyse
        {
            get => s_preAnalyse;
            set
            {
                if (value != s_preAnalyse)
                {
                    s_preAnalyse = value;
                    SettingsData["StatusBar"]["PreAnalyse"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("PreAnalyse"));
                }
            }
        }
        public static bool StreamingType
        {
            get => s_streamingType;
            set
            {
                if (value != s_streamingType)
                {
                    s_streamingType = value;
                    SettingsData["StatusBar"]["StreamingType"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("StreamingType"));
                }
            }
        }
        public static bool Size
        {
            get => s_size;
            set
            {
                if (value != s_size)
                {
                    s_size = value;
                    SettingsData["StatusBar"]["Size"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("Size"));
                }
            }
        }
        public static bool Conversion
        {
            get => s_conversion;
            set
            {
                if (value != s_conversion)
                {
                    s_conversion = value;
                    SettingsData["StatusBar"]["Conversion"] = value.ToString();
                    StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs("Conversion"));
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
