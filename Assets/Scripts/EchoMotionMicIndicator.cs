using UnityEngine;
using Whisper.Utils;

/// <summary>
/// Loudness-reactive mic-icon indicator for the CAVE. While a question is open,
/// the icon reacts to the live microphone volume: quiet -> small + grey, loud ->
/// big + red. Driven by the real audio amplitude (RMS of the Whisper
/// <see cref="MicrophoneRecord.OnChunkReady"/> chunks), not a binary on/off.
///
/// Assign a SpriteRenderer (e.g. the EchoMotionMicIcon sprite) and/or a Renderer,
/// plus a scaleTarget. Visibility outside a question is optional via
/// showWhileListening (leave empty to keep the icon always on screen).
///
/// Setup: put this on the SpeechManager (next to EchoMotionVoiceQuestion) and
/// assign voiceQuestion + the mic icon.
/// </summary>
public class EchoMotionMicIndicator : MonoBehaviour
{
    [Header("Reference")]
    public EchoMotionVoiceQuestion voiceQuestion;

    [Header("Visibility (optional)")]
    [Tooltip("Active only while a question is open. Put the mic icon here to hide it otherwise. Leave empty to keep it always visible.")]
    public GameObject[] showWhileListening;

    [Header("Colour (quiet -> loud)")]
    [Tooltip("World-space mic icon (white sprite). Tinted between quiet and loud.")]
    public SpriteRenderer micIcon;
    [Tooltip("Optional 3D Renderer alternative (uses _BaseColor / _Color).")]
    public Renderer targetRenderer;
    public Color quietColor = new Color(0.60f, 0.62f, 0.65f, 1f);
    public Color loudColor = new Color(0.85f, 0.22f, 0.22f, 1f);

    [Header("Reaction")]
    [Tooltip("Transform that scales with loudness (the icon).")]
    public Transform scaleTarget;
    [Tooltip("Scale multiplier at full volume.")]
    public float maxScale = 1.6f;
    [Tooltip("Higher = more sensitive (raw mic RMS is small, ~0.01-0.2).")]
    public float loudnessGain = 20f;
    [Tooltip("How fast the icon follows the volume. Higher = snappier.")]
    public float responseSpeed = 14f;
    [Tooltip("Metering resolution in seconds. Smaller = more responsive.")]
    public float chunkSeconds = 0.1f;

    private bool _windowActive;
    private bool _chunkSubscribed;
    private float _targetLevel;
    private float _level;
    private MicrophoneRecord _mic;
    private Vector3 _baseScale = Vector3.one;
    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP / HDRP Lit
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // built-in Standard / Sprite

    private void Awake()
    {
        if (voiceQuestion == null)
            voiceQuestion = GetComponent<EchoMotionVoiceQuestion>();

        if (voiceQuestion != null)
            _mic = voiceQuestion.microphoneRecord;

        if (scaleTarget != null)
            _baseScale = scaleTarget.localScale;

        _mpb = new MaterialPropertyBlock();

        // Must be set before StartRecord (the chunk size is computed there).
        if (_mic != null && chunkSeconds > 0f)
            _mic.chunksLengthSec = chunkSeconds;
    }

    private void OnEnable()
    {
        if (voiceQuestion != null)
        {
            voiceQuestion.OnListeningStarted += HandleWindowOpen;
            voiceQuestion.OnListeningStopped += HandleWindowClose;
        }

        HandleWindowClose();
    }

    private void OnDisable()
    {
        if (voiceQuestion != null)
        {
            voiceQuestion.OnListeningStarted -= HandleWindowOpen;
            voiceQuestion.OnListeningStopped -= HandleWindowClose;
        }

        SetChunkSubscribed(false);
    }

    private void Update()
    {
        var k = Mathf.Clamp01(Time.unscaledDeltaTime * responseSpeed);
        _level = Mathf.Lerp(_level, _targetLevel, k);
        Apply(_level);
    }

    private void HandleWindowOpen()
    {
        _windowActive = true;
        _targetLevel = 0f;
        SetObjects(true);
        SetChunkSubscribed(true);
    }

    private void HandleWindowClose()
    {
        _windowActive = false;
        _targetLevel = 0f;
        SetChunkSubscribed(false);
        SetObjects(false);
    }

    // RMS of the latest audio chunk -> 0..1 loudness target.
    private void HandleChunk(AudioChunk chunk)
    {
        if (!_windowActive)
            return;

        var data = chunk.Data;
        if (data == null || data.Length == 0)
            return;

        double sum = 0;
        for (var i = 0; i < data.Length; i++)
            sum += (double)data[i] * data[i];

        var rms = Mathf.Sqrt((float)(sum / data.Length));
        _targetLevel = Mathf.Clamp01(rms * loudnessGain);
    }

    private void Apply(float level)
    {
        if (scaleTarget != null)
            scaleTarget.localScale = _baseScale * Mathf.Lerp(1f, maxScale, level);

        SetColor(Color.Lerp(quietColor, loudColor, level));
    }

    private void SetChunkSubscribed(bool subscribe)
    {
        if (_mic == null || subscribe == _chunkSubscribed)
            return;

        if (subscribe) _mic.OnChunkReady += HandleChunk;
        else _mic.OnChunkReady -= HandleChunk;

        _chunkSubscribed = subscribe;
    }

    private void SetObjects(bool active)
    {
        if (showWhileListening == null)
            return;

        foreach (var go in showWhileListening)
            if (go != null)
                go.SetActive(active);
    }

    private void SetColor(Color color)
    {
        if (micIcon != null)
            micIcon.color = color;

        if (targetRenderer != null)
        {
            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(_mpb);
        }
    }
}
