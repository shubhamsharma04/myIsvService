using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace myIsvService.Utilities
{
    public class ISVServiceUtilities
    {
        private static IDictionary<string, string> answersByQuestions = GetAnswersByQuestions();
        private static List<string> userNames = GetUserNames();

        private static IDictionary<string, string> GetAnswersByQuestions()
        {
            IDictionary<string, string> answersByQuestions = new Dictionary<string, string>();
            answersByQuestions.Add("What is the name of the book", "Expo low");
            answersByQuestions.Add("Who is the publisher of the book", "Problem publishing");

            return answersByQuestions;
        }

        private static List<string> GetUserNames()
        {
            List<string> userNames = new List<string>();
            userNames.Add("John Doe");
            userNames.Add("Jane Doe");
            return userNames;
        }
    }
}
