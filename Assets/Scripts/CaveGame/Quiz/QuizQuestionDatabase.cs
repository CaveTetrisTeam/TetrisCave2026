using System.Collections.Generic;
using UnityEngine;

namespace CaveGame.Quiz
{
    /// <summary>
    /// Im Project-Fenster über Create > Cave Game > Quiz Questions anlegbar.
    /// Neue Fragen und Antwortvarianten können vollständig im Inspector gepflegt werden.
    /// </summary>
    [CreateAssetMenu(fileName = "QuizQuestions", menuName = "Cave Game/Quiz Questions")]
    public sealed class QuizQuestionDatabase : ScriptableObject
    {
        public List<QuizQuestion> questions = new List<QuizQuestion>();
    }
}
