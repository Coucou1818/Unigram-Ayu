//
// Copyright (c) 2026 PrismGram / AyuGram port for Unigram
// Distributed under the GNU General Public License v3.0.
//

using Telegram.Services.Ayu;
#if WINUI
using Microsoft.UI.Xaml.Controls;
#else
using Windows.UI.Xaml.Controls;
#endif

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsAyuPage : Page
    {
        public AyuSettings ViewModel => AyuSettings.Current;

        public int ProviderSelectedIndex
        {
            get => (int)ViewModel.TranslationProvider;
            set => ViewModel.TranslationProvider = (AyuTranslationProvider)value;
        }

        public SettingsAyuPage()
        {
            this.InitializeComponent();
        }
    }
}
