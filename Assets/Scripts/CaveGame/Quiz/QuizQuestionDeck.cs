using System;
using System.Collections.Generic;

namespace CaveGame.Quiz
{
    public sealed class QuizQuestionDeck
    {
        private readonly List<QuizQuestion> source = new List<QuizQuestion>();
        private readonly List<int> order = new List<int>();
        private readonly Random random;
        private int cursor;

        public int Count => source.Count;

        public QuizQuestionDeck(IEnumerable<QuizQuestion> questions, int? seed = null)
        {
            if (questions != null)
            {
                foreach (QuizQuestion question in questions)
                    if (question != null && !string.IsNullOrWhiteSpace(question.question)) source.Add(question);
            }
            random = seed.HasValue ? new Random(seed.Value) : new Random();
            Shuffle();
        }

        public QuizQuestion Next()
        {
            if (source.Count == 0) return null;
            if (cursor >= order.Count) Shuffle();
            return source[order[cursor++]];
        }

        private void Shuffle()
        {
            order.Clear();
            for (int i = 0; i < source.Count; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temp = order[i]; order[i] = order[j]; order[j] = temp;
            }
            cursor = 0;
        }
    }
}
