using System;
using System.Diagnostics;
using UnityEngine;
using Whisper;
using Whisper.Utils;

public class EchoMotionSpeechToText : MonoBehaviour
{
    [Header("Whisper Components")]
    public WhisperManager whisper;
    public MicrophoneRecord microphoneRecord;

    [Header("Options")]
    public bool printLanguage = true;
    public bool autoInitWhisper = true;

    public string LastText { get; private set; }

    public event Action<string> OnTranscriptionReady;
    public event Action<string> OnError;

    private void Awake()
    {
        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop += OnRecordStop;
    }

    private async void Start()
    {
        if (autoInitWhisper && whisper != null && !whisper.IsLoaded)
        {
            await whisper.InitModel();
        }
    }

    private void OnDestroy()
    {
        if (microphoneRecord != null)
            microphoneRecord.OnRecordStop -= OnRecordStop;
    }

    public void StartRecording()
    {
        if (microphoneRecord == null)
        {
            RaiseError("MicrophoneRecord missing");
            return;
        }

        if (!microphoneRecord.IsRecording)
            microphoneRecord.StartRecord();
    }

    public void StopRecording()
    {
        if (microphoneRecord == null)
        {
            RaiseError("MicrophoneRecord missing");
            return;
        }

        if (microphoneRecord.IsRecording)
            microphoneRecord.StopRecord();
    }

    public void ToggleRecording()
    {
        if (microphoneRecord == null)
        {
            RaiseError("MicrophoneRecord missing");
            return;
        }

        if (microphoneRecord.IsRecording)
            StopRecording();
        else
            StartRecording();
    }

    private async void OnRecordStop(AudioChunk recordedAudio)
    {
        if (whisper == null)
        {
            RaiseError("WhisperManager missing");
            return;
        }

        if (!whisper.IsLoaded)
        {
            RaiseError("Whisper model not loaded");
            return;
        }

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var result = await whisper.GetTextAsync(
            recordedAudio.Data,
            recordedAudio.Frequency,
            recordedAudio.Channels
        );

        stopwatch.Stop();

        if (result == null)
        {
            RaiseError("No transcription result");
            return;
        }

        LastText = result.Result;

        if (printLanguage)
        {
            UnityEngine.Debug.Log(
                $"STT: {LastText}\nLanguage: {result.Language}\nTime: {stopwatch.ElapsedMilliseconds} ms"
            );
        }
        else
        {
            UnityEngine.Debug.Log($"STT: {LastText}");
        }

        OnTranscriptionReady?.Invoke(LastText);
    }

    private void RaiseError(string message)
    {
        UnityEngine.Debug.LogError(message);
        OnError?.Invoke(message);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartRecording();
            UnityEngine.Debug.Log("Recording...");
        }

        if (Input.GetKeyUp(KeyCode.R))
        {
            StopRecording();
            UnityEngine.Debug.Log("Stopped");
        }
    }
}