using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaveGame.Quiz
{
    [Serializable]
    public sealed class QuizQuestion
    {
        [TextArea(2, 5)] public string question;
        public string expectedAnswer;
        public List<string> acceptedVariants = new List<string>();
        public string category;

        public IEnumerable<string> AllAcceptedAnswers()
        {
            yield return expectedAnswer;
            if (acceptedVariants == null) yield break;
            foreach (string variant in acceptedVariants) yield return variant;
        }
    }

}
