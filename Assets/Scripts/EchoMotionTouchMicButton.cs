using UnityEngine;

public class EchoMotionTouchMicButton : MonoBehaviour
{
    public EchoMotionSpeechToText speechToText;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("SPACE DOWN → Aufnahme startet");
            speechToText.StartRecording();
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            Debug.Log("SPACE UP → Aufnahme stoppt");
            speechToText.StopRecording();
        }
    }
}
