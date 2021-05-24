using myIsvService.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace myIsvService.Utilities
{
    public class ISVServiceUtilities
    {
        private static IDictionary<string, string> answersByQuestions = new ConcurrentDictionary<string, string>();
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

        internal static void LoadData()
        {
            if(answersByQuestions != null && answersByQuestions.Count() > 0)
            {
                return;
            }

            List<QnA> dataset = File.ReadAllLines(Path.Combine(Directory.GetCurrentDirectory(), "data.json"))
                .Select(line => JsonConvert.DeserializeObject<QnA>(line))
                .ToList();

            foreach (QnA qna in dataset)
            {
                answersByQuestions.Add(qna.Question, qna.Answer[0]);
            }
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
            userNames.Add("Rajesh Gautam");
            userNames.Add("Bradley Stone");
            userNames.Add("Shifa Masood");
            userNames.Add("K. Narayan");
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

        internal static string AddAdditionalData(string input)
        {
            int questionIndex = new Random().Next(answersByQuestions.Count);
            int userIndex = new Random().Next(userNames.Count);

            var content = "\n" + answersByQuestions.ElementAt(questionIndex).Key + answersByQuestions.ElementAt(questionIndex).Value;
            string response = string.Format(input, answersByQuestions.ElementAt(questionIndex).Key, answersByQuestions.ElementAt(questionIndex).Value, userNames[userIndex], content);

            return response;
        }

        internal static string GetContentData(IDictionary<string, object> additionalData)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach(KeyValuePair<string, object> entry in additionalData)
            {
                stringBuilder.Append("\n" + entry.Key + "   " + entry.Value);
            }
            return stringBuilder.ToString();
        }

        internal static Item GetItem()
        {
            Item item = new Item();
            item.acl = GetAcls();

            int questionIndex = new Random().Next(answersByQuestions.Count);
            int userIndex = new Random().Next(userNames.Count);

            Properties properties = new Properties
            {
                Question = answersByQuestions.ElementAt(questionIndex).Key,
                Answer = answersByQuestions.ElementAt(questionIndex).Value,
                UserName = userNames[userIndex]
            };

            Content content = new Content
            {
                value = answersByQuestions.ElementAt(questionIndex).Key +"    "+ answersByQuestions.ElementAt(questionIndex).Value,
                type = "text"
            };

            item.properties = properties;
            item.content = content;
            return item;
        }

        private static List<Acl> GetAcls()
        {
            Acl first_acl = new Acl
            {
                type = "user",
                value = "cbb6d774-d245-4927-9a4f-eea22c3f7ff4",
                accessType = "grant",
                identitySource = "azureActiveDirectory"
            };

            Acl second_acl = new Acl
            {
                type = "user",
                value = "06085933-cbc3-4cc0-bdc6-9ff75933ef97",
                accessType = "grant",
                identitySource = "azureActiveDirectory"
            };

            List<Acl> acls = new List<Acl>();
            acls.Add(first_acl);
            acls.Add(second_acl);
            return acls;
        }
    }
}
