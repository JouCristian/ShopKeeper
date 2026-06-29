using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiQuickQuestionService
    {
        private static readonly string[] BuiltInQuestions =
        {
            "分析今日收入",
            "生成本周经营小结",
            "生成本月经营月报",
            "库存补货建议",
            "赊账客户提醒",
            "热销与滞销商品"
        };

        private string SettingsFilePath
        {
            get { return Path.Combine(AppPaths.RuntimeRoot, "ai", "quick_questions.xml"); }
        }

        public IList<string> Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return new List<string>();
                }

                XDocument document = XDocument.Load(SettingsFilePath);
                List<string> questions = new List<string>();
                foreach (XElement element in document.Root.Elements("Question"))
                {
                    string text = (element.Value ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(text) && !IsBuiltInQuestion(text))
                    {
                        questions.Add(text);
                    }
                }

                return questions;
            }
            catch
            {
                return new List<string>();
            }
        }

        public void Save(IEnumerable<string> questions)
        {
            AppPaths.EnsureDirectory(Path.GetDirectoryName(SettingsFilePath));
            XElement root = new XElement("AiQuickQuestions");
            int count = 0;
            IEnumerable<string> sourceQuestions = questions ?? new string[0];
            foreach (string question in sourceQuestions)
            {
                if (count >= 8)
                {
                    break;
                }

                string text = (question ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text) || IsBuiltInQuestion(text))
                {
                    continue;
                }

                root.Add(new XElement("Question", text));
                count++;
            }

            new XDocument(root).Save(SettingsFilePath);
        }

        public void Add(string question)
        {
            string text = (question ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || IsBuiltInQuestion(text))
            {
                return;
            }

            List<string> questions = new List<string>(Load());
            foreach (string existing in questions)
            {
                if (string.Equals(existing, text, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            questions.Add(text);
            Save(questions);
        }

        public IList<string> CreateBuiltInList()
        {
            return new List<string>(BuiltInQuestions);
        }

        private static bool IsBuiltInQuestion(string question)
        {
            foreach (string builtInQuestion in BuiltInQuestions)
            {
                if (string.Equals(builtInQuestion, question, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
