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
    }
}
