using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Records movements within a set recording duration. Replay/Echo features removed for merge.
/// </summary>
public class MovementEchoMultiple : MonoBehaviour
{
    public PositionTransferMultiple tracker; // Takes positions of the mirrored avatars
    public float recordingDuration;
    public AudioClip recordSound;

    public Dictionary<string, List<Dictionary<string, Vector3>>> recordedFrames = new();

    private bool isRecording = false;
    private float timer = 0f;

    void Start()
    {
        Invoke("StartRecording", 10f);
    }

    /// <summary>
    /// Measures the time per frame to not exceed the recording duration.
    /// (Replay-Aufruf wurde laut Vorgabe entfernt)
    /// </summary>
    void Update()
    {
        if (isRecording)
        {
            timer += Time.deltaTime;
            RecordFrame();

            if (timer >= recordingDuration)
            {
                isRecording = false;
                Debug.Log("Recording finished.");
            }
        }
    }

    /// <summary>
    /// Sets timer to zero, plays sound at the beginning of recording and clears the temporary recorded frames for the next recording.
    /// </summary>
    public void StartRecording()
    {
        recordedFrames.Clear();
        timer = 0f;
        isRecording = true;
        Debug.Log("Recording started.");

        if (recordSound == null) return;

        GameObject soundObj = new GameObject("RecordSound");
        AudioSource audioSource = soundObj.AddComponent<AudioSource>();
        audioSource.clip = recordSound;
        audioSource.Play();
        Destroy(soundObj, recordSound.length + 0.1f);
    }

    /// <summary>
    /// Takes the positions from the Avatars of PositionTransferMultiple and stores them to the temporary recordedFrames.
    /// </summary>
    void RecordFrame()
    {
        foreach (var actorId in tracker.joints.Keys)
        {
            if (!recordedFrames.ContainsKey(actorId))
                recordedFrames[actorId] = new();

            Dictionary<string, Vector3> frame = new();

            foreach (var joint in tracker.joints[actorId])
            {
                frame[joint.Key] = joint.Value.transform.position;
            }

            recordedFrames[actorId].Add(frame);
        }
    }
}