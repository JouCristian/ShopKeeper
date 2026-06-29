using System;
using System.IO;
using System.Xml.Linq;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiOperationLogService
    {
        private string LogFilePath
        {
            get { return Path.Combine(AiDataDirectory, "operation-log.xml"); }
        }

        private string AiDataDirectory
        {
            get { return Path.Combine(AppPaths.RuntimeRoot, "ai"); }
        }

        public void Append(AiActionDraft draft, AiActionDraftItem item, AiActionExecutionResult result)
        {
            AppPaths.EnsureDirectory(AiDataDirectory);
            XDocument document = LoadDocument();
            document.Root.Add(new XElement("Operation",
                new XElement("CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                new XElement("DraftId", draft == null ? string.Empty : draft.Id),
                new XElement("ConversationId", draft == null ? 0 : draft.ConversationId),
                new XElement("ActionType", item == null ? string.Empty : item.ActionType),
                new XElement("ProductName", item == null ? string.Empty : item.ProductName),
                new XElement("Success", result != null && result.Success),
                new XElement("Message", result == null ? string.Empty : result.Message),
                new XElement("BusinessRecordType", result == null ? string.Empty : result.BusinessRecordType),
                new XElement("BusinessRecordId", result == null ? 0 : result.BusinessRecordId)));
            document.Save(LogFilePath);
        }

        private XDocument LoadDocument()
        {
            if (!File.Exists(LogFilePath))
            {
                return new XDocument(new XElement("AiOperationLog"));
            }

            return XDocument.Load(LogFilePath);
        }
    }
}
