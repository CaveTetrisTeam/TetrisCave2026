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
        [Header("Quiz")]
        public QuizQuestionDatabase questionDatabase;
        [Min(1)] public int scoreInterval = 1000;
        [Min(0f)] public float feedbackDuration = 4f;

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
            cancellation = new CancellationTokenSource();
            Time.timeScale = 0f;
            avatar.SetQuizMode(true);
            avatar.ShowMessage(currentQuestion.question, -1f, true);
            voiceQuestion.useExternalEvaluation = true;
            voiceQuestion.maxAttempts = 3;
            voiceQuestion.noSpeechTimeout = 8f;
            voiceQuestion.configureVad = true;
            voiceQuestion.AskQuestion(currentQuestion.AllAcceptedAnswers().Where(x => !string.IsNullOrWhiteSpace(x)).ToArray());
        }

        private async void HandleAnswerTranscribed(string transcript)
        {
            if (!IsQuizActive || currentQuestion == null) return;
            bool correct;
            string feedback;
            try
            {
                if (ollama == null) throw new InvalidOperationException("Ollama-Client fehlt.");
                OllamaQuizResult result = await ollama.EvaluateAsync(currentQuestion, transcript, cancellation.Token);
                correct = result.correct;
                feedback = result.feedback;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception exception)
            {
                Debug.LogWarning("[AvatarQuiz] Ollama nicht verfügbar, lokaler Vergleich wird verwendet: " + exception.Message);
                correct = LocalAnswerMatcher.IsMatch(transcript, currentQuestion);
                feedback = correct ? "Richtig!" : "Leider falsch – richtig wäre: " + currentQuestion.expectedAnswer;
            }
            if (IsQuizActive) FinishWithFeedback(correct, feedback);
        }

        private void HandleNotUnderstood(int attempt)
        {
            if (IsQuizActive) avatar.ShowMessage("Ich habe dich nicht verstanden. Bitte versuche es noch einmal.", 2.5f, true);
        }

        private void HandleGaveUp()
        {
            if (IsQuizActive)
                FinishWithFeedback(false, "Leider nicht verstanden – richtig wäre: " + currentQuestion.expectedAnswer);
        }

        private void FinishWithFeedback(bool correct, string feedback)
        {
            voiceQuestion.CompleteExternalEvaluation();
            string message = correct ? "Richtig! " + feedback : "Leider falsch – richtig wäre: " + currentQuestion.expectedAnswer;
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
            cancellation?.Dispose();
            cancellation = null;
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
            Time.timeScale = 1f;
        }
    }
}
