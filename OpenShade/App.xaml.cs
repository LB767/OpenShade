using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OpenShade
{
    public enum Themes
    {
        Default,
        Dark
    }

    public partial class App : Application
    {
        public App()
        {
            // NOTE: This allows for input like 0.12 into textboxes. Behavior was changed from .Net 4 to 4.5
            // See https://stackoverflow.com/questions/14600842/bind-textbox-to-float-value-unable-to-input-dot-comma and
            //     http://www.sebastianlux.net/2014/06/21/binding-float-values-to-a-net-4-5-wpf-textbox-using-updatesourcetriggerpropertychanged/
            System.Windows.FrameworkCompatibilityPreferences.KeepTextBoxDisplaySynchronizedWithTextProperty = false;


            // NOTE: Avoids 0,12 problems when writing strings (0.12 instead)
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InstalledUICulture;
        }

        private Themes _currentTheme = Themes.Default;
        public Themes CurrentTheme
        {
            get { return _currentTheme; }
            set { _currentTheme = value; }
        }

        public void ChangeTheme(Themes theme)
        {
            if (theme != _currentTheme)
            {
                _currentTheme = theme;
                switch (theme)
                {
                    default:
                    case Themes.Default:
                        this.Resources.MergedDictionaries.Clear();
                        AddResourceDictionary("UI/DefaultTheme.xaml");
                        break;
                    case Themes.Dark:
                        this.Resources.MergedDictionaries.Clear();
                        AddResourceDictionary("UI/DarkTheme.xaml");
                        break;
                }
            }
        }

        void AddResourceDictionary(string source)
        {
            ResourceDictionary resourceDictionary = Application.LoadComponent(new Uri(source, UriKind.Relative)) as ResourceDictionary;
            this.Resources.MergedDictionaries.Add(resourceDictionary);
        }
    }
}
