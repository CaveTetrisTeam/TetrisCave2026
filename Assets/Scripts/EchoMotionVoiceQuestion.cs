using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Whisper.Utils;

/// <summary>
/// Question/answer voice flow for the CAVE game. Sits on top of
/// <see cref="EchoMotionSpeechToText"/> and only listens during an active
/// question window (mic is off otherwise -> no false triggers from game audio
/// or bystanders).
///
/// Usage from the game/avatar:
///   voiceQuestion.AskQuestion(new[] { "blau", "blaue" });   // arm + listen
///   voiceQuestion.OnCorrect       += () => { /* weiter im Spiel */ };
///   voiceQuestion.OnWrong         += t  => { /* Spiel entscheidet: Schaden / retry / ... */ };
///   voiceQuestion.OnNotUnderstood += n  => { /* z.B. "Bitte nochmal" einblenden */ };
///   voiceQuestion.OnGaveUp        += () => { /* nach maxAttempts aufgeben */ };
///
/// If the avatar SPEAKS the question, call AskQuestion(..., micStartDelay) with a
/// delay, or pass autoListen:false and call BeginListening() yourself once the
/// avatar audio has finished (so VAD doesn't pick up the avatar's voice).
/// </summary>
public class EchoMotionVoiceQuestion : MonoBehaviour
{
    [Header("References")]
    public EchoMotionSpeechToText speechToText;
    [Tooltip("Optional. Taken from speechToText if left empty.")]
    public MicrophoneRecord microphoneRecord;

    [Header("Retry / timeout")]
    [Tooltip("Empty/unintelligible answers before giving up.")]
    public int maxAttempts = 3;
    [Tooltip("Seconds to wait for the person to START talking (0 = unlimited). " +
             "Cancelled once speech is detected; VAD then handles the end.")]
    public float noSpeechTimeout = 8f;
    [Tooltip("Delay before the mic arms after AskQuestion. Use when the avatar " +
             "speaks the question via audio/TTS.")]
    public float micStartDelay = 0f;

    [Header("VAD (applied when a question starts)")]
    public bool configureVad = true;
    [Tooltip("Seconds of silence after speech before the answer is auto-submitted.")]
    public float vadStopTime = 1.5f;

    // The game subscribes to these.
    public event Action OnCorrect;
    public event Action<string> OnWrong;        // transcript that didn't match
    public event Action<int> OnNotUnderstood;   // attempt number just finished
    public event Action OnGaveUp;
    public event Action OnListeningStarted;     // e.g. show mic indicator
    public event Action OnListeningStopped;
    /// <summary>Feuert für jede nicht-leere Transkription, bevor sie bewertet wird.</summary>
    public event Action<string> OnAnswerTranscribed;

    [Tooltip("Wenn aktiv, übernimmt ein externer Listener von OnAnswerTranscribed die Bewertung.")]
    public bool useExternalEvaluation;

    public bool IsAsking { get; private set; }

    private string[] _accepted;
    private int _attempt;
    private Coroutine _timeoutRoutine;
    private Coroutine _startRoutine;

    private void Awake()
    {
        if (speechToText == null)
            speechToText = GetComponent<EchoMotionSpeechToText>();

        if (microphoneRecord == null && speechToText != null)
            microphoneRecord = speechToText.microphoneRecord;
    }

    private void OnEnable()
    {
        if (speechToText != null)
            speechToText.OnTranscriptionReady += HandleTranscription;

        if (microphoneRecord != null)
            microphoneRecord.OnVadChanged += HandleVadChanged;
    }

    private void OnDisable()
    {
        if (speechToText != null)
            speechToText.OnTranscriptionReady -= HandleTranscription;

        if (microphoneRecord != null)
            microphoneRecord.OnVadChanged -= HandleVadChanged;

        StopRoutines();
    }

    /// <summary>Avatar/game calls this when a question is posed.</summary>
    /// <param name="acceptedAnswers">Keywords counting as correct (case/punctuation-insensitive, substring match).</param>
    /// <param name="autoListen">If false, call <see cref="BeginListening"/> yourself (e.g. after avatar audio).</param>
    public void AskQuestion(string[] acceptedAnswers, bool autoListen = true)
    {
        if (acceptedAnswers == null || acceptedAnswers.Length == 0)
        {
            Debug.LogWarning("AskQuestion: no accepted answers given.");
            return;
        }

        if (speechToText == null)
        {
            Debug.LogError("AskQuestion: speechToText reference missing.");
            return;
        }

        CancelQuestion();   // drop any previous question that is still open

        _accepted = acceptedAnswers;
        _attempt = 0;
        IsAsking = true;

        if (configureVad && microphoneRecord != null)
        {
            microphoneRecord.useVad = true;
            microphoneRecord.vadStop = true;
            microphoneRecord.dropVadPart = true;
            microphoneRecord.vadStopTime = vadStopTime;
        }

        if (autoListen)
            BeginListening();
    }

    /// <summary>(Re)start listening for the current question. Honours micStartDelay.</summary>
    public void BeginListening()
    {
        if (!IsAsking)
            return;

        if (micStartDelay > 0f)
        {
            if (_startRoutine != null) StopCoroutine(_startRoutine);
            _startRoutine = StartCoroutine(StartListeningDelayed(micStartDelay));
        }
        else
        {
            StartListeningNow();
        }
    }

    /// <summary>Abort the current question without firing an outcome.</summary>
    public void CancelQuestion()
    {
        if (!IsAsking)
            return;

        IsAsking = false;
        StopRoutines();
        speechToText.StopRecording();
        OnListeningStopped?.Invoke();
    }

    private IEnumerator StartListeningDelayed(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (IsAsking)
            StartListeningNow();
    }

    private void StartListeningNow()
    {
        _attempt++;
        speechToText.StartRecording();
        OnListeningStarted?.Invoke();

        if (noSpeechTimeout > 0f)
        {
            if (_timeoutRoutine != null) StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = StartCoroutine(NoSpeechTimeout(noSpeechTimeout));
        }
    }

    // Person never started talking -> stop the mic. The resulting (empty)
    // transcription flows through HandleTranscription and counts as "not understood".
    private IEnumerator NoSpeechTimeout(float t)
    {
        yield return new WaitForSecondsRealtime(t);
        _timeoutRoutine = null;
        if (IsAsking)
            speechToText.StopRecording();
    }

    private void HandleVadChanged(bool isSpeechDetected)
    {
        // Speech started -> the no-speech guard is no longer needed; let VAD's
        // vadStop decide when the answer is finished.
        if (isSpeechDetected && IsAsking && _timeoutRoutine != null)
        {
            StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = null;
        }
    }

    private void HandleTranscription(string text)
    {
        if (!IsAsking)
            return;   // ignore anything recorded outside a question window

        if (_timeoutRoutine != null)
        {
            StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = null;
        }

        OnListeningStopped?.Invoke();

        var normalized = Normalize(text);

        if (string.IsNullOrEmpty(normalized))
        {
            HandleNotUnderstood();
            return;
        }

        OnAnswerTranscribed?.Invoke(text);
        if (useExternalEvaluation)
        {
            IsAsking = false;
            return;
        }

        if (Matches(normalized))
        {
            IsAsking = false;
            OnCorrect?.Invoke();
        }
        else
        {
            // Understood, but not an accepted answer. The game decides what
            // happens next (continue with penalty, ask again, ...).
            IsAsking = false;
            OnWrong?.Invoke(text);
        }
    }

    private void HandleNotUnderstood()
    {
        if (_attempt >= maxAttempts)
        {
            IsAsking = false;
            OnGaveUp?.Invoke();
            return;
        }

        OnNotUnderstood?.Invoke(_attempt);
        BeginListening();   // re-arm and try again
    }

    private bool Matches(string normalizedText)
    {
        foreach (var answer in _accepted)
        {
            var needle = Normalize(answer);
            if (needle.Length > 0 && normalizedText.Contains(needle))
                return true;
        }
        return false;
    }

    // Lowercase, strip punctuation, collapse whitespace. Keeps letters (incl.
    // umlauts) and digits so "Blau." and "die blaue" both match "blau".
    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        var sb = new StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c)) sb.Append(' ');
        }

        return Regex.Replace(sb.ToString(), "\\s+", " ").Trim();
    }

    /// <summary>Schließt eine extern bewertete Frage ab, ohne Demo-Events auszulösen.</summary>
    public void CompleteExternalEvaluation()
    {
        IsAsking = false;
        StopRoutines();
        if (speechToText != null) speechToText.StopRecording();
    }

    private void StopRoutines()
    {
        if (_timeoutRoutine != null) { StopCoroutine(_timeoutRoutine); _timeoutRoutine = null; }
        if (_startRoutine != null) { StopCoroutine(_startRoutine); _startRoutine = null; }
    }
}
