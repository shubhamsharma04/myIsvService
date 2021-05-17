using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace myIsvService.Utilities
{
    public class ISVServiceUtilities
    {
        private static IDictionary<string, string> answersByQuestions = GetAnswersByQuestions();
        private static List<string> userNames = GetUserNames();

        private static IDictionary<string, string> GetAnswersByQuestions()
        {
            IDictionary<string, string> answersByQuestions = new Dictionary<string, string>();
            answersByQuestions.Add("What is the name of the book?", "Expo low");
            answersByQuestions.Add("Who is the publisher of the book?", "Penguin publishing");
            answersByQuestions.Add("Typically, how many projects do you own?", "4");
            answersByQuestions.Add("What is your most favorite item of office pantry?", "Green Tea");
            answersByQuestions.Add("How much time do you spend on commute?", "15 mins");
            answersByQuestions.Add("Do you prefer typing or writing for note taking?", "I prefer writing");
            answersByQuestions.Add("Can you find all relevant documents for all your project?", "No");
            answersByQuestions.Add("Do you wear glasses?", "Maybe");
            answersByQuestions.Add("How many people do you typically work with?", "8");
            answersByQuestions.Add("Why did chicken cross the road?", "ask the chicken");

            return answersByQuestions;
        }

        private static List<string> GetUserNames()
        {
            List<string> userNames = new List<string>();
            userNames.Add("Sumit Roy");
            userNames.Add("Alex G.");
            userNames.Add("Hannah Kirby");
            userNames.Add("Steven Dillon");
            userNames.Add("Ashish Jha");
            userNames.Add("Gabriel");
            return userNames;
        }

        internal static IDictionary<string, object> GetAdditionalData()
        {
            IDictionary<string, object> additionalData = new Dictionary<string, object>();
            int questionIndex = new Random().Next(answersByQuestions.Count);
            int userIndex = new Random().Next(userNames.Count);

            additionalData.Add("Question", answersByQuestions.ElementAt(questionIndex).Key);
            additionalData.Add("Answer", answersByQuestions.ElementAt(questionIndex).Value);
            additionalData.Add("UserName", userNames[userIndex]);
            return additionalData;
        }

        internal static string GetContentData(IDictionary<string, object> additionalData)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach(KeyValuePair<string, object> entry in additionalData)
            {
                stringBuilder.Append(entry.Key + "\t" + entry.Value + "\n");
            }
            return stringBuilder.ToString();
        }
    }
}
