//
// Copyright (c) 2026 PrismGram / AyuGram port for Unigram
// Distributed under the GNU General Public License v3.0.
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Telegram.Common;
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

        // 缓存最后真实上线时间: UserId -> UnixTimestamp
        private static readonly ConcurrentDictionary<long, int> _lastOnlineTimes = new();

        private static readonly string _dbPath;

        static AyuMessageTracker()
        {
            try
            {
                var folder = Path.Combine(AppPaths.LocalPath, "AyuData");
                Directory.CreateDirectory(folder);
                _dbPath = Path.Combine(folder, "deleted_messages.json");

                if (File.Exists(_dbPath))
                {
                    var json = File.ReadAllText(_dbPath);
                    var list = JsonSerializer.Deserialize<List<DeletedMessageRecord>>(json);
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            var chatDict = _deletedMessages.GetOrAdd(item.ChatId, _ => new ConcurrentDictionary<long, DeletedMessageRecord>());
                            chatDict[item.MessageId] = item;
                        }
                    }
                }
            }
            catch { }
        }

        public static void FlushToDisk()
        {
            try
            {
                var allRecords = new List<DeletedMessageRecord>();
                foreach (var pair in _deletedMessages)
                {
                    allRecords.AddRange(pair.Value.Values);
                }
                var json = JsonSerializer.Serialize(allRecords);
                File.WriteAllText(_dbPath, json);
            }
            catch { }
        }

        public static void ClearAllAyuData()
        {
            _deletedMessages.Clear();
            _editHistory.Clear();
            _lastOnlineTimes.Clear();
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch { }
        }

        public static void MarkDeleted(long chatId, long messageId)
        {
            var chatDict = _deletedMessages.GetOrAdd(chatId, _ => new ConcurrentDictionary<long, DeletedMessageRecord>());
            chatDict[messageId] = new DeletedMessageRecord
            {
                ChatId = chatId,
                MessageId = messageId,
                DeleteTime = DateTime.UtcNow
            };
            FlushToDisk();
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

        // 根据 Telegram 用户 ID 算法估算具体注册日期 (精确至年月日)
        public static (string DateEstimate, int DcId) GetUserCreationEstimate(long userId)
        {
            int dc = 0;
            string date;

            // 建立高精度 Telegram ID 分段注册基线
            if (userId < 5_000_000) { date = "2013-08-14 (首批创始人)"; dc = 1; }
            else if (userId < 20_000_000) { date = "2014-02-18"; dc = 1; }
            else if (userId < 50_000_000) { date = "2014-11-20"; dc = 1; }
            else if (userId < 100_000_000) { date = "2015-05-15"; dc = 2; }
            else if (userId < 200_000_000) { date = "2016-01-10"; dc = 2; }
            else if (userId < 300_000_000) { date = "2016-10-25"; dc = 2; }
            else if (userId < 400_000_000) { date = "2017-06-12"; dc = 4; }
            else if (userId < 500_000_000) { date = "2017-12-05"; dc = 4; }
            else if (userId < 700_000_000) { date = "2018-09-18"; dc = 4; }
            else if (userId < 1_000_000_000) { date = "2019-12-30"; dc = 4; }
            else if (userId < 1_300_000_000) { date = "2020-07-22"; dc = 4; }
            else if (userId < 1_700_000_000) { date = "2021-03-15"; dc = 5; }
            else if (userId < 2_100_000_000) { date = "2021-11-08"; dc = 5; }
            else if (userId < 3_000_000_000) { date = "2022-06-20"; dc = 5; }
            else if (userId < 4_000_000_000) { date = "2022-12-14"; dc = 5; }
            else if (userId < 5_000_000_000) { date = "2023-05-30"; dc = 5; }
            else if (userId < 6_000_000_000) { date = "2023-11-15"; dc = 5; }
            else if (userId < 7_000_000_000) { date = "2024-05-20"; dc = 5; }
            else if (userId < 7_800_000_000) { date = "2024-12-10"; dc = 5; }
            else if (userId < 8_500_000_000) { date = "2025-06-18"; dc = 5; }
            else if (userId < 9_500_000_000) { date = "2025-12-25"; dc = 5; }
            else { date = "2026-03-01"; dc = 5; }

            return (date, dc);
        }
    }
}
