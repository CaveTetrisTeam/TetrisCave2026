using UnityEngine;
using UnityEngine.UI;

public class EchoMotionSTTLogger : MonoBehaviour
{
    public EchoMotionSpeechToText speechToText;
    public Text outputText;

    private void OnEnable()
    {
        if (speechToText != null)
        {
            speechToText.OnTranscriptionReady += HandleText;
            speechToText.OnError += HandleError;
        }
    }

    private void OnDisable()
    {
        if (speechToText != null)
        {
            speechToText.OnTranscriptionReady -= HandleText;
            speechToText.OnError -= HandleError;
        }
    }

    private void HandleText(string text)
    {
        if (outputText != null)
            outputText.text = text;

        Debug.Log("Transcribed: " + text);
    }

    private void HandleError(string error)
    {
        if (outputText != null)
            outputText.text = "STT Error: " + error;

        Debug.LogError(error);
    }
}
