using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class AiPurchaseDraftReview
    {
        public AiPurchaseDraftReview()
        {
            MissingRequiredFields = new List<string>();
            OptionalReminders = new List<string>();
        }

        public AiPurchaseDraft Draft { get; set; }

        public Product MatchedProduct { get; set; }

        public Category MatchedCategory { get; set; }

        public IList<string> MissingRequiredFields { get; private set; }

        public IList<string> OptionalReminders { get; private set; }

        public bool IsReady
        {
            get { return MissingRequiredFields.Count == 0; }
        }
    }
}
