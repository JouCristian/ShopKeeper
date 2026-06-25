using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class AiIntentResult
    {
        public AiIntentResult()
        {
            IntentKeys = new List<string>();
        }

        public IList<string> IntentKeys { get; private set; }

        public bool HasBusinessContext
        {
            get { return IntentKeys.Count > 0; }
        }
    }
}
