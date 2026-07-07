using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CaveGame.Quiz
{
    public sealed class AvatarQuizController : MonoBehaviour
    {
        // Anzeigedauer "bis auf Weiteres": Die Frage bleibt in der Sprechblase stehen,
        // bis sie per interrupt durch einen Hinweis oder das Ergebnis ersetzt wird.
        // (Say mit Dauer <= 0 wäre "automatisch" und damit auf maxHoldSeconds gedeckelt.)
        private const float QuestionHoldSeconds = 99999f;

        [Header("Quiz")]
        public QuizQuestionDatabase questionDatabase;
        [Min(1)] public int scoreInterval = 1000;
        [Min(0f)] public float feedbackDuration = 4f;
        [Tooltip("Antwortversuche (falsch oder nur Geräusche), bevor die Lösung verraten wird.")]
        [Min(1)] public int answerAttempts = 3;

        [Header("Referenzen")]
        public AvatarCompanion avatar;
        public EchoMotionVoiceQuestion voiceQuestion;
        public OllamaQuizClient ollama;

        public bool IsQuizActive { get; private set; }
        public int NextScoreThreshold { get; private set; } = 1000;

        private QuizQuestionDeck deck;
        private QuizQuestion currentQuestion;
        private CancellationTokenSource cancellation;
        private Coroutine finishRoutine;
        private int remainingAttempts;

        private void Awake()
        {
            ResolveReferences();
            RebuildDeck();
            NextScoreThreshold = Mathf.Max(1, scoreInterval);
        }

        private void OnEnable()
        {
            GameManager.ScoreChanged += HandleScoreChanged;
            GameManager.StateChanged += HandleStateChanged;
            SubscribeVoice();
        }

        private void OnDisable()
        {
            GameManager.ScoreChanged -= HandleScoreChanged;
            GameManager.StateChanged -= HandleStateChanged;
            UnsubscribeVoice();
            AbortQuiz();
        }

        private void ResolveReferences()
        {
            if (avatar == null) avatar = FindObjectOfType<AvatarCompanion>(true);
            if (voiceQuestion == null) voiceQuestion = FindObjectOfType<EchoMotionVoiceQuestion>(true);
            if (ollama == null) ollama = GetComponent<OllamaQuizClient>();
        }

        public void RebuildDeck()
        {
            deck = new QuizQuestionDeck(questionDatabase != null ? questionDatabase.questions : null);
        }

        private void SubscribeVoice()
        {
            if (voiceQuestion == null) return;
            voiceQuestion.OnAnswerTranscribed += HandleAnswerTranscribed;
            voiceQuestion.OnNotUnderstood += HandleNotUnderstood;
            voiceQuestion.OnGaveUp += HandleGaveUp;
        }

        private void UnsubscribeVoice()
        {
            if (voiceQuestion == null) return;
            voiceQuestion.OnAnswerTranscribed -= HandleAnswerTranscribed;
            voiceQuestion.OnNotUnderstood -= HandleNotUnderstood;
            voiceQuestion.OnGaveUp -= HandleGaveUp;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                AbortQuiz();
                NextScoreThreshold = Mathf.Max(1, scoreInterval);
                return;
            }
            AbortQuiz();
        }

        private void HandleScoreChanged(int score)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
                return;
            if (!ShouldStartAtScore(score, NextScoreThreshold, IsQuizActive)) return;

            while (NextScoreThreshold <= score) NextScoreThreshold += Mathf.Max(1, scoreInterval);
            StartQuiz();
        }

        public static bool ShouldStartAtScore(int score, int nextThreshold, bool quizActive)
        {
            return !quizActive && nextThreshold > 0 && score >= nextThreshold;
        }

        private void StartQuiz()
        {
            ResolveReferences();
            if (deck == null || deck.Count == 0)
            {
                Debug.LogWarning("[AvatarQuiz] Keine Quizfragen konfiguriert.");
                return;
            }
            if (avatar == null || voiceQuestion == null)
            {
                Debug.LogWarning("[AvatarQuiz] Avatar oder Sprachaufnahme fehlt; Quiz wird übersprungen.");
                return;
            }

            currentQuestion = deck.Next();
            IsQuizActive = true;
            remainingAttempts = Mathf.Max(1, answerAttempts);
            cancellation = new CancellationTokenSource();
            Time.timeScale = 0f;
            // Time.timeScale pausiert KEINE AudioSources: Die Musik liefe weiter, würde
            // vom Mikrofon mit aufgenommen und von Whisper als "(Geräusch)" transkribiert.
            AudioListener.pause = true;
            avatar.SetQuizMode(true);
            voiceQuestion.useExternalEvaluation = true;
            voiceQuestion.maxAttempts = 3;
            voiceQuestion.noSpeechTimeout = 8f;
            voiceQuestion.configureVad = true;
            ShowQuestion(null);
            StartListening();
        }

        /// <summary>Zeigt die Frage dauerhaft an, optional mit Hinweiszeile darüber.</summary>
        private void ShowQuestion(string hint)
        {
            string text = string.IsNullOrEmpty(hint)
                ? currentQuestion.question
                : hint + "\n" + currentQuestion.question;
            avatar.ShowMessage(text, QuestionHoldSeconds, true);
        }

        private void StartListening()
        {
            voiceQuestion.AskQuestion(currentQuestion.AllAcceptedAnswers()
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray());
        }

        private async void HandleAnswerTranscribed(string transcript)
        {
            if (!IsQuizActive || currentQuestion == null) return;

            // Whisper markiert Nicht-Sprache als "(Geräusch)", "[Musik]" o. Ä. – solche
            // Anteile zählen nicht als Antwort, sondern kosten nur einen Versuch.
            string spoken = LocalAnswerMatcher.StripNoiseAnnotations(transcript);
            if (LocalAnswerMatcher.Normalize(spoken).Length == 0)
            {
                Retry("Ich habe nur Geräusche gehört – sag deine Antwort laut und deutlich!");
                return;
            }

            string understood = spoken.Trim();
            // Sofort zeigen, was verstanden wurde – so sieht der Spieler schon während
            // der Bewertung, ob er ggf. deutlicher sprechen muss.
            ShowQuestion("Ich habe „" + understood + "“ verstanden – einen Moment …");

            bool correct;
            string feedback;
            try
            {
                if (ollama == null) throw new InvalidOperationException("Ollama-Client fehlt.");
                OllamaQuizResult result = await ollama.EvaluateAsync(currentQuestion, spoken, cancellation.Token);
                correct = result.correct;
                feedback = result.feedback;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception exception)
            {
                Debug.LogWarning("[AvatarQuiz] Ollama nicht verfügbar, lokaler Vergleich wird verwendet: " + exception.Message);
                correct = LocalAnswerMatcher.IsMatch(spoken, currentQuestion);
                feedback = null;
            }
            if (!IsQuizActive) return;

            if (correct) FinishWithFeedback(true, feedback);
            else Retry("Ich habe „" + understood + "“ verstanden – das ist leider falsch. Versuch es noch einmal!",
                       understood);
        }

        private void HandleNotUnderstood(int attempt)
        {
            // voiceQuestion hört in diesem Fall selbst erneut zu – nur den Text auffrischen,
            // damit die Frage lesbar stehen bleibt.
            if (IsQuizActive) ShowQuestion("Ich habe dich nicht gehört – sprich bitte laut und deutlich!");
        }

        private void HandleGaveUp()
        {
            if (IsQuizActive) FinishWithFeedback(false, null);
        }

        /// <summary>Falsche/unverständliche Antwort: neuer Versuch oder Auflösung.</summary>
        private void Retry(string hint, string understood = null)
        {
            remainingAttempts--;
            if (remainingAttempts > 0)
            {
                ShowQuestion(hint);
                StartListening();
            }
            else
            {
                FinishWithFeedback(false, null, understood);
            }
        }

        private void FinishWithFeedback(bool correct, string feedback, string understood = null)
        {
            voiceQuestion.CompleteExternalEvaluation();
            string message;
            if (correct)
            {
                message = string.IsNullOrWhiteSpace(feedback) ? "Richtig!" : "Richtig! " + feedback;
            }
            else
            {
                // Das Verstandene mit anzeigen, damit der Spieler nachvollziehen kann,
                // was beim Mikrofon/Whisper angekommen ist.
                message = string.IsNullOrEmpty(understood)
                    ? "Leider falsch – die richtige Antwort wäre: " + currentQuestion.expectedAnswer
                    : "Ich habe „" + understood + "“ verstanden.\nLeider falsch – die richtige Antwort wäre: " +
                      currentQuestion.expectedAnswer;
            }
            avatar.ShowMessage(message, feedbackDuration, true);
            if (finishRoutine != null) StopCoroutine(finishRoutine);
            finishRoutine = StartCoroutine(ResumeAfterFeedback());
        }

        private IEnumerator ResumeAfterFeedback()
        {
            yield return new WaitForSecondsRealtime(feedbackDuration);
            finishRoutine = null;
            EndQuiz();
        }

        public void AbortQuiz()
        {
            if (!IsQuizActive) return;
            if (finishRoutine != null) { StopCoroutine(finishRoutine); finishRoutine = null; }
            cancellation?.Cancel();
            if (voiceQuestion != null) voiceQuestion.CancelQuestion();
            EndQuiz();
        }

        private void EndQuiz()
        {
            if (!IsQuizActive) return;
            IsQuizActive = false;
            cancellation?.Dispose();
            cancellation = null;
            currentQuestion = null;
            if (voiceQuestion != null) voiceQuestion.useExternalEvaluation = false;
            if (avatar != null) avatar.SetQuizMode(false);
            AudioListener.pause = false;
            Time.timeScale = 1f;
        }
    }
}
