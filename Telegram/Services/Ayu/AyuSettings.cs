//
// Copyright (c) 2026 PrismGram / AyuGram port for Unigram
// Distributed under the GNU General Public License v3.0.
//

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Telegram.Services.Settings;

namespace Telegram.Services.Ayu
{
    public class AyuSettings : INotifyPropertyChanged
    {
        private static AyuSettings _current;
        public static AyuSettings Current => _current ??= new AyuSettings();

        private readonly ISettingsStore _store;

        public AyuSettings()
        {
            _store = SettingsStore.Local;
        }

        // --- 1. 幽灵模式 (Ghost Mode) ---
        public bool GhostModeActive
        {
            get => GetValue(nameof(GhostModeActive), false);
            set => SetValue(nameof(GhostModeActive), value);
        }

        public bool SendReadMessages
        {
            get => GetValue(nameof(SendReadMessages), true);
            set => SetValue(nameof(SendReadMessages), value);
        }

        public bool SendReadStories
        {
            get => GetValue(nameof(SendReadStories), true);
            set => SetValue(nameof(SendReadStories), value);
        }

        public bool SendOnlinePackets
        {
            get => GetValue(nameof(SendOnlinePackets), true);
            set => SetValue(nameof(SendOnlinePackets), value);
        }

        public bool SendUploadProgress
        {
            get => GetValue(nameof(SendUploadProgress), true);
            set => SetValue(nameof(SendUploadProgress), value);
        }

        public bool SendOfflinePacketAfterOnline
        {
            get => GetValue(nameof(SendOfflinePacketAfterOnline), false);
            set => SetValue(nameof(SendOfflinePacketAfterOnline), value);
        }

        public bool MarkReadAfterAction
        {
            get => GetValue(nameof(MarkReadAfterAction), true);
            set => SetValue(nameof(MarkReadAfterAction), value);
        }

        // --- 2. 防撤回与编辑记录 (Anti-Recall & History) ---
        public bool SaveDeletedMessages
        {
            get => GetValue(nameof(SaveDeletedMessages), true);
            set => SetValue(nameof(SaveDeletedMessages), value);
        }

        public bool SaveMessagesHistory
        {
            get => GetValue(nameof(SaveMessagesHistory), true);
            set => SetValue(nameof(SaveMessagesHistory), value);
        }

        public bool SemiTransparentDeletedMessages
        {
            get => GetValue(nameof(SemiTransparentDeletedMessages), true);
            set => SetValue(nameof(SemiTransparentDeletedMessages), value);
        }

        public string DeletedMark
        {
            get => GetValue(nameof(DeletedMark), "🧹");
            set => SetValue(nameof(DeletedMark), value);
        }

        public string EditedMark
        {
            get => GetValue(nameof(EditedMark), "✎");
            set => SetValue(nameof(EditedMark), value);
        }

        // --- 3. 增强功能与本地特权 (Enhancements) ---
        public bool LocalPremium
        {
            get => GetValue(nameof(LocalPremium), true);
            set => SetValue(nameof(LocalPremium), value);
        }

        public bool DisableAds
        {
            get => GetValue(nameof(DisableAds), true);
            set => SetValue(nameof(DisableAds), value);
        }

        // --- 4. 翻译引擎配置 (Translation Providers) ---
        public AyuTranslationProvider TranslationProvider
        {
            get => (AyuTranslationProvider)GetValue(nameof(TranslationProvider), (int)AyuTranslationProvider.Google);
            set => SetValue(nameof(TranslationProvider), (int)value);
        }

        public string OpenAIBaseUrl
        {
            get => GetValue(nameof(OpenAIBaseUrl), "https://api.openai.com/v1");
            set => SetValue(nameof(OpenAIBaseUrl), value);
        }

        public string OpenAIApiKey
        {
            get => GetValue(nameof(OpenAIApiKey), string.Empty);
            set => SetValue(nameof(OpenAIApiKey), value);
        }

        public string OpenAIModel
        {
            get => GetValue(nameof(OpenAIModel), "gpt-4o-mini");
            set => SetValue(nameof(OpenAIModel), value);
        }

        public bool DisableStories
        {
            get => GetValue(nameof(DisableStories), false);
            set => SetValue(nameof(DisableStories), value);
        }

        public bool StreamerMode
        {
            get => GetValue(nameof(StreamerMode), false);
            set => SetValue(nameof(StreamerMode), value);
        }

        public bool ShowMessageSeconds
        {
            get => GetValue(nameof(ShowMessageSeconds), false);
            set => SetValue(nameof(ShowMessageSeconds), value);
        }

        public bool StickerConfirmation
        {
            get => GetValue(nameof(StickerConfirmation), false);
            set => SetValue(nameof(StickerConfirmation), value);
        }

        // --- 内部持久化辅助方法 ---
        private T GetValue<T>(string key, T defaultValue)
        {
            if (_store.TryGetValue("ayu_" + key, out var val) && val is T typedVal)
            {
                return typedVal;
            }
            return defaultValue;
        }

        private void SetValue<T>(string key, T value, [CallerMemberName] string propertyName = null)
        {
            _store.SetValue("ayu_" + key, value);
            OnPropertyChanged(propertyName);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
