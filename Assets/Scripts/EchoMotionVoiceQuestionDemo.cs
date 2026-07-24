using UnityEngine;

/// <summary>
/// Manual test harness for <see cref="EchoMotionVoiceQuestion"/> — no game logic
/// required. Press <see cref="askKey"/> to pose a sample question; every outcome
/// is logged to the Console. Use this in test2.unity to try the full voice flow
/// (question -> listen -> VAD auto-stop -> Whisper -> correct/wrong/retry) before
/// the real Tetris/avatar game exists.
///
/// Setup: put this on the SpeechManager GameObject (next to EchoMotionVoiceQuestion)
/// and assign the voiceQuestion reference. Press Play, press the key, then speak.
/// </summary>
public class EchoMotionVoiceQuestionDemo : MonoBehaviour
{
    [Header("Reference")]
    public EchoMotionVoiceQuestion voiceQuestion;

    [Header("Demo question")]
    [Tooltip("Press this key to pose the sample question.")]
    public KeyCode askKey = KeyCode.Q;
    [TextArea] public string questionText = "Welche Farbe hat die Form? (richtig: blau)";
    public string[] acceptedAnswers = { "blau", "blaue", "blauer" };

    [Tooltip("Mirror the real game: pause via Time.timeScale while a question is open.")]
    public bool pauseDuringQuestion = false;

    private bool _subscribed;

    private void Start()
    {
        if (voiceQuestion == null)
            voiceQuestion = GetComponent<EchoMotionVoiceQuestion>();

        Subscribe();
        Debug.Log($"[VoiceDemo] Bereit. Taste '{askKey}' druecken, um die Frage zu stellen.");
    }

    private void OnDestroy() => Unsubscribe();

    private void Update()
    {
        if (Input.GetKeyDown(askKey))
            Ask();
    }

    private void Ask()
    {
        if (voiceQuestion == null)
        {
            Debug.LogError("[VoiceDemo] voiceQuestion reference missing.");
            return;
        }

        if (voiceQuestion.IsAsking)
        {
            Debug.Log("[VoiceDemo] Es laeuft bereits eine Frage.");
            return;
        }

        if (pauseDuringQuestion)
            Time.timeScale = 0f;

        Debug.Log($"[VoiceDemo] FRAGE: {questionText}");
        Debug.Log("[VoiceDemo] Jetzt antworten ... (Mikro lauscht, stoppt automatisch per VAD)");
        voiceQuestion.AskQuestion(acceptedAnswers);
    }

    private void Subscribe()
    {
        if (voiceQuestion == null || _subscribed) return;
        voiceQuestion.OnListeningStarted += HandleListeningStarted;
        voiceQuestion.OnListeningStopped += HandleListeningStopped;
        voiceQuestion.OnCorrect += HandleCorrect;
        voiceQuestion.OnWrong += HandleWrong;
        voiceQuestion.OnNotUnderstood += HandleNotUnderstood;
        voiceQuestion.OnGaveUp += HandleGaveUp;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (voiceQuestion == null || !_subscribed) return;
        voiceQuestion.OnListeningStarted -= HandleListeningStarted;
        voiceQuestion.OnListeningStopped -= HandleListeningStopped;
        voiceQuestion.OnCorrect -= HandleCorrect;
        voiceQuestion.OnWrong -= HandleWrong;
        voiceQuestion.OnNotUnderstood -= HandleNotUnderstood;
        voiceQuestion.OnGaveUp -= HandleGaveUp;
        _subscribed = false;
    }

    private void HandleListeningStarted() => Debug.Log("[VoiceDemo] Mikro an -> sprich jetzt.");
    private void HandleListeningStopped() => Debug.Log("[VoiceDemo] Mikro aus -> werte aus ...");

    private void HandleCorrect()
    {
        Debug.Log("[VoiceDemo] KORREKT -> weiter im Spiel.");
        Resume();
    }

    private void HandleWrong(string transcript)
    {
        Debug.Log($"[VoiceDemo] FALSCH. Verstanden: \"{transcript}\". (Hier wuerde das Spiel entscheiden.)");
        Resume();
    }

    private void HandleNotUnderstood(int attempt)
    {
        // Controller re-arms by itself; just give feedback.
        Debug.Log($"[VoiceDemo] Nicht verstanden (Versuch {attempt}) -> nochmal, bitte sprechen.");
    }

    private void HandleGaveUp()
    {
        Debug.Log("[VoiceDemo] Aufgegeben nach max. Versuchen -> weiter.");
        Resume();
    }

    private void Resume()
    {
        if (pauseDuringQuestion)
            Time.timeScale = 1f;
    }
}
