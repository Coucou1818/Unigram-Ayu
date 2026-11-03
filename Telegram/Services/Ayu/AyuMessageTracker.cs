//
// Copyright (c) 2026 PrismGram / AyuGram port for Unigram
// Distributed under the GNU General Public License v3.0.
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.Services.Ayu
{
    public class DeletedMessageRecord
    {
        public long ChatId { get; set; }
        public long MessageId { get; set; }
        public DateTime DeleteTime { get; set; }
    }

    public class EditHistoryRecord
    {
        public long ChatId { get; set; }
        public long MessageId { get; set; }
        public int EditDate { get; set; }
        public MessageContent Content { get; set; }
    }

    public static class AyuMessageTracker
    {
        // 缓存被撤回的消息映射: ChatId -> (MessageId -> DeleteRecord)
        private static readonly ConcurrentDictionary<long, ConcurrentDictionary<long, DeletedMessageRecord>> _deletedMessages = new();

        // 缓存历史编辑版本: (ChatId, MessageId) -> List<EditHistoryRecord>
        private static readonly ConcurrentDictionary<string, List<EditHistoryRecord>> _editHistory = new();

        public static void MarkDeleted(long chatId, long messageId)
        {
            var chatDict = _deletedMessages.GetOrAdd(chatId, _ => new ConcurrentDictionary<long, DeletedMessageRecord>());
            chatDict[messageId] = new DeletedMessageRecord
            {
                ChatId = chatId,
                MessageId = messageId,
                DeleteTime = DateTime.UtcNow
            };
        }

        public static bool IsMessageDeleted(long chatId, long messageId)
        {
            if (_deletedMessages.TryGetValue(chatId, out var chatDict))
            {
                return chatDict.ContainsKey(messageId);
            }
            return false;
        }

        public static void RecordEdit(long chatId, long messageId, int editDate, MessageContent oldContent)
        {
            if (oldContent == null) return;

            string key = $"{chatId}_{messageId}";
            var list = _editHistory.GetOrAdd(key, _ => new List<EditHistoryRecord>());
            lock (list)
            {
                list.Add(new EditHistoryRecord
                {
                    ChatId = chatId,
                    MessageId = messageId,
                    EditDate = editDate,
                    Content = oldContent
                });
            }
        }

        public static IReadOnlyList<EditHistoryRecord> GetEditHistory(long chatId, long messageId)
        {
            string key = $"{chatId}_{messageId}";
            if (_editHistory.TryGetValue(key, out var list))
            {
                lock (list)
                {
                    return list.ToArray();
                }
            }
            return Array.Empty<EditHistoryRecord>();
        }

        // 缓存最后真实上线时间: UserId -> UnixTimestamp
        private static readonly ConcurrentDictionary<long, int> _lastOnlineTimes = new();

        public static void RecordLastOnline(long userId, int unixTime)
        {
            if (unixTime > 0)
            {
                _lastOnlineTimes[userId] = unixTime;
            }
        }

        public static int GetLastOnline(long userId)
        {
            return _lastOnlineTimes.TryGetValue(userId, out int time) ? time : 0;
        }

        // 根据 Telegram 用户 ID 算法估算注册年份与月份
        public static (string DateEstimate, int DcId) GetUserCreationEstimate(long userId)
        {
            // Telegram DC 估算通常取决于用户分配 (通过 ID 范围和所属数据中心大致分布)
            // ID 增长基线经验表 (根据 Telegram 历史 ID 发展)
            int dc = 0;
            string date = "未知";

            if (userId < 100_000_000) { date = "2013-2015 年 (早期元老)"; dc = 1; }
            else if (userId < 300_000_000) { date = "2016-2017 年初"; dc = 2; }
            else if (userId < 500_000_000) { date = "2017 年"; dc = 4; }
            else if (userId < 700_000_000) { date = "2018 年"; dc = 4; }
            else if (userId < 1_000_000_000) { date = "2019 年"; dc = 4; }
            else if (userId < 1_500_000_000) { date = "2020 年"; dc = 4; }
            else if (userId < 2_000_000_000) { date = "2021 年"; dc = 5; }
            else if (userId < 5_000_000_000) { date = "2022 年 - 2023 年"; dc = 5; }
            else if (userId < 6_500_000_000) { date = "2023 年 - 2024 年"; dc = 5; }
            else if (userId < 7_500_000_000) { date = "2024 年 - 2025 年"; dc = 5; }
            else { date = "2025 年 - 2026 年"; dc = 5; }

            return (date, dc);
        }
    }
}
