using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiConversationService
    {
        private const int MaxConversationCount = 10;

        private string ConversationFilePath
        {
            get { return Path.Combine(AiDataDirectory, "conversations.xml"); }
        }

        private string CleanupLogPath
        {
            get { return Path.Combine(AiDataDirectory, "conversation_cleanup.log"); }
        }

        private string AiDataDirectory
        {
            get { return Path.Combine(AppPaths.RuntimeRoot, "ai"); }
        }

        public IList<AiConversation> ListConversations()
        {
            XDocument document = LoadDocument();
            List<AiConversation> conversations = new List<AiConversation>();
            foreach (XElement element in document.Root.Elements("Conversation"))
            {
                AiConversation conversation = ReadConversationHeader(element);
                if (!conversation.IsArchived)
                {
                    conversations.Add(conversation);
                }
            }

            return conversations
                .OrderByDescending(item => item.UpdatedAt)
                .ToList();
        }

        public int CountUserRequests(DateTime startInclusive, DateTime endExclusive)
        {
            XDocument document = LoadDocument();
            int count = 0;
            foreach (XElement messageElement in document.Descendants("Message"))
            {
                string role = ReadValue(messageElement, "Role", string.Empty);
                if (!string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DateTime createdAt = ReadDate(messageElement, "CreatedAt", DateTime.MinValue);
                if (createdAt >= startInclusive && createdAt < endExclusive)
                {
                    count++;
                }
            }

            return count;
        }

        public AiConversation LoadLatestOrCreate(string model)
        {
            IList<AiConversation> conversations = ListConversations();
            if (conversations.Count > 0)
            {
                return Load(conversations[0].Id);
            }

            return Create("新对话", model);
        }

        public AiConversation Load(long id)
        {
            XDocument document = LoadDocument();
            XElement element = document.Root.Elements("Conversation")
                .FirstOrDefault(item => ReadLong(item, "Id", 0) == id);
            if (element == null)
            {
                return LoadLatestOrCreate("deepseek-v4-flash");
            }

            AiConversation conversation = ReadConversationHeader(element);
            XElement messagesElement = element.Element("Messages");
            if (messagesElement == null)
            {
                return conversation;
            }

            foreach (XElement messageElement in messagesElement.Elements("Message"))
            {
                conversation.Messages.Add(ReadMessage(messageElement));
            }

            return conversation;
        }

        public AiConversation Create(string title, string model)
        {
            XDocument document = LoadDocument();
            long nextId = ResolveNextConversationId(document);
            AiConversation conversation = new AiConversation
            {
                Id = nextId,
                Title = string.IsNullOrWhiteSpace(title) ? "新对话" : title.Trim(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Model = string.IsNullOrWhiteSpace(model) ? "deepseek-v4-flash" : model,
                IsArchived = false,
                IsPinned = false,
                Summary = string.Empty
            };

            document.Root.Add(CreateConversationElement(conversation));
            PruneConversationLimit(document, conversation.Id);
            SaveDocument(document);
            return conversation;
        }

        public long AddMessage(long conversationId, string role, string content, string messageType, string dataContextType)
        {
            XDocument document = LoadDocument();
            XElement conversationElement = document.Root.Elements("Conversation")
                .FirstOrDefault(item => ReadLong(item, "Id", 0) == conversationId);
            if (conversationElement == null)
            {
                return 0;
            }

            XElement messagesElement = conversationElement.Element("Messages");
            if (messagesElement == null)
            {
                messagesElement = new XElement("Messages");
                conversationElement.Add(messagesElement);
            }

            long nextId = ResolveNextMessageId(document);
            messagesElement.Add(new XElement("Message",
                new XElement("Id", nextId),
                new XElement("ConversationId", conversationId),
                new XElement("Role", role ?? string.Empty),
                new XElement("Content", content ?? string.Empty),
                new XElement("CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                new XElement("MessageType", messageType ?? string.Empty),
                new XElement("DataContextType", dataContextType ?? string.Empty),
                new XElement("TokenEstimate", EstimateTokens(content))));

            conversationElement.SetElementValue("UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            AutoTitleConversation(conversationElement, role, content);
            SaveDocument(document);
            return nextId;
        }

        public void UpdateMessage(long conversationId, long messageId, string content, string messageType, string dataContextType)
        {
            if (messageId <= 0)
            {
                return;
            }

            XDocument document = LoadDocument();
            XElement conversationElement = document.Root.Elements("Conversation")
                .FirstOrDefault(item => ReadLong(item, "Id", 0) == conversationId);
            if (conversationElement == null)
            {
                return;
            }

            XElement messageElement = conversationElement.Descendants("Message")
                .FirstOrDefault(item => ReadLong(item, "Id", 0) == messageId);
            if (messageElement == null)
            {
                return;
            }

            messageElement.SetElementValue("Content", content ?? string.Empty);
            messageElement.SetElementValue("MessageType", messageType ?? string.Empty);
            messageElement.SetElementValue("DataContextType", dataContextType ?? string.Empty);
            conversationElement.SetElementValue("UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            SaveDocument(document);
        }

        public void Rename(long conversationId, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            XDocument document = LoadDocument();
            XElement conversationElement = document.Root.Elements("Conversation")
                .FirstOrDefault(item => ReadLong(item, "Id", 0) == conversationId);
            if (conversationElement == null)
            {
                return;
            }

            conversationElement.SetElementValue("Title", title.Trim());
            conversationElement.SetElementValue("UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            SaveDocument(document);
        }

        public void UpdateModel(long conversationId, string model)
        {
            XDocument document = LoadDocument();
            XElement conversationElement = document.Root.Elements("Conversation")
                .FirstOrDefault(item => ReadLong(item, "Id", 0) == conversationId);
            if (conversationElement == null)
            {
                return;
            }

            conversationElement.SetElementValue("Model", model ?? string.Empty);
            conversationElement.SetElementValue("UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            SaveDocument(document);
        }

        public void Archive(long conversationId)
        {
            XDocument document = LoadDocument();
            XElement conversationElement = document.Root.Elements("Conversation")
                .FirstOrDefault(item => ReadLong(item, "Id", 0) == conversationId);
            if (conversationElement == null)
            {
                return;
            }

            conversationElement.SetElementValue("IsArchived", true);
            conversationElement.SetElementValue("UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            SaveDocument(document);
        }

        public Task UpdateConversationSummaryAsync(long conversationId)
        {
            return Task.FromResult(0);
        }

        private XDocument LoadDocument()
        {
            AppPaths.EnsureDirectory(AiDataDirectory);
            if (!File.Exists(ConversationFilePath))
            {
                return new XDocument(new XElement("AiConversations"));
            }

            return XDocument.Load(ConversationFilePath);
        }

        private void SaveDocument(XDocument document)
        {
            AppPaths.EnsureDirectory(AiDataDirectory);
            document.Save(ConversationFilePath);
        }

        private static XElement CreateConversationElement(AiConversation conversation)
        {
            return new XElement("Conversation",
                new XElement("Id", conversation.Id),
                new XElement("Title", conversation.Title ?? string.Empty),
                new XElement("CreatedAt", conversation.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                new XElement("UpdatedAt", conversation.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                new XElement("Model", conversation.Model ?? string.Empty),
                new XElement("IsArchived", conversation.IsArchived),
                new XElement("IsPinned", conversation.IsPinned),
                new XElement("Summary", conversation.Summary ?? string.Empty),
                new XElement("Messages"));
        }

        private static AiConversation ReadConversationHeader(XElement element)
        {
            return new AiConversation
            {
                Id = ReadLong(element, "Id", 0),
                Title = ReadValue(element, "Title", "新对话"),
                CreatedAt = ReadDate(element, "CreatedAt", DateTime.Now),
                UpdatedAt = ReadDate(element, "UpdatedAt", DateTime.Now),
                Model = ReadValue(element, "Model", "deepseek-v4-flash"),
                IsArchived = ReadBool(element, "IsArchived", false),
                IsPinned = ReadBool(element, "IsPinned", false),
                Summary = ReadValue(element, "Summary", string.Empty)
            };
        }

        private void PruneConversationLimit(XDocument document, long currentConversationId)
        {
            if (document == null || document.Root == null)
            {
                return;
            }

            while (document.Root.Elements("Conversation")
                .Where(item => !ReadBool(item, "IsArchived", false))
                .Count() > MaxConversationCount)
            {
                XElement oldest = document.Root.Elements("Conversation")
                    .Where(item => !ReadBool(item, "IsArchived", false)
                        && !ReadBool(item, "IsPinned", false)
                        && ReadLong(item, "Id", 0) != currentConversationId)
                    .OrderBy(item => ReadDate(item, "UpdatedAt", DateTime.MinValue))
                    .FirstOrDefault();
                if (oldest == null)
                {
                    return;
                }

                string title = ReadValue(oldest, "Title", "新对话");
                oldest.Remove();
                AppendCleanupLog("已自动清理最早 AI 会话：" + title);
            }
        }

        private void AppendCleanupLog(string message)
        {
            try
            {
                AppPaths.EnsureDirectory(AiDataDirectory);
                File.AppendAllText(CleanupLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static AiStoredMessage ReadMessage(XElement element)
        {
            return new AiStoredMessage
            {
                Id = ReadLong(element, "Id", 0),
                ConversationId = ReadLong(element, "ConversationId", 0),
                Role = ReadValue(element, "Role", string.Empty),
                Content = ReadValue(element, "Content", string.Empty),
                CreatedAt = ReadDate(element, "CreatedAt", DateTime.Now),
                MessageType = ReadValue(element, "MessageType", string.Empty),
                DataContextType = ReadValue(element, "DataContextType", string.Empty),
                TokenEstimate = (int)ReadLong(element, "TokenEstimate", 0)
            };
        }

        private static long ResolveNextConversationId(XDocument document)
        {
            long max = 0;
            foreach (XElement item in document.Root.Elements("Conversation"))
            {
                max = Math.Max(max, ReadLong(item, "Id", 0));
            }

            return max + 1;
        }

        private static long ResolveNextMessageId(XDocument document)
        {
            long max = 0;
            foreach (XElement item in document.Descendants("Message"))
            {
                max = Math.Max(max, ReadLong(item, "Id", 0));
            }

            return max + 1;
        }

        private static void AutoTitleConversation(XElement conversationElement, string role, string content)
        {
            string title = ReadValue(conversationElement, "Title", string.Empty);
            if (title != "新对话" || role != "user" || string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            string clean = content.Replace("\r", " ").Replace("\n", " ").Trim();
            if (clean.Length > 18)
            {
                clean = clean.Substring(0, 18) + "...";
            }

            conversationElement.SetElementValue("Title", clean);
        }

        private static int EstimateTokens(string content)
        {
            return string.IsNullOrEmpty(content) ? 0 : Math.Max(1, content.Length / 2);
        }

        private static string ReadValue(XElement element, string name, string defaultValue)
        {
            XElement child = element.Element(name);
            return child == null ? defaultValue : child.Value;
        }

        private static long ReadLong(XElement element, string name, long defaultValue)
        {
            long result;
            return long.TryParse(ReadValue(element, name, defaultValue.ToString()), out result) ? result : defaultValue;
        }

        private static bool ReadBool(XElement element, string name, bool defaultValue)
        {
            bool result;
            return bool.TryParse(ReadValue(element, name, defaultValue.ToString()), out result) ? result : defaultValue;
        }

        private static DateTime ReadDate(XElement element, string name, DateTime defaultValue)
        {
            DateTime result;
            return DateTime.TryParse(ReadValue(element, name, string.Empty), out result) ? result : defaultValue;
        }
    }
}
