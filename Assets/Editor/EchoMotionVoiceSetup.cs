#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click setup for the voice question test. Adds and wires
/// EchoMotionVoiceQuestion + Demo + MicIndicator onto the object that already
/// holds EchoMotionSpeechToText, creates a mic indicator visual, and applies the
/// recommended MicrophoneRecord settings. Safe to run multiple times (idempotent).
///
/// Menu: Tools > Echo-Motion > Sprachtest einrichten
/// </summary>
public static class EchoMotionVoiceSetup
{
    [MenuItem("Tools/Echo-Motion/Sprachtest einrichten")]
    public static void Setup()
    {
        var stt = Object.FindObjectOfType<EchoMotionSpeechToText>();
        if (stt == null)
        {
            EditorUtility.DisplayDialog(
                "Echo-Motion",
                "Kein EchoMotionSpeechToText in der offenen Szene gefunden.\nBitte test2.unity öffnen und erneut ausführen.",
                "OK");
            return;
        }

        var host = stt.gameObject;
        Undo.SetCurrentGroupName("Echo-Motion Sprachtest einrichten");
        var group = Undo.GetCurrentGroup();

        // --- 1. Controller ---
        var vq = host.GetComponent<EchoMotionVoiceQuestion>();
        if (vq == null) vq = Undo.AddComponent<EchoMotionVoiceQuestion>(host);
        Undo.RecordObject(vq, "wire voice question");
        vq.speechToText = stt;
        if (vq.microphoneRecord == null) vq.microphoneRecord = stt.microphoneRecord;

        // --- 2. Demo trigger (press Q) ---
        var demo = host.GetComponent<EchoMotionVoiceQuestionDemo>();
        if (demo == null) demo = Undo.AddComponent<EchoMotionVoiceQuestionDemo>(host);
        Undo.RecordObject(demo, "wire demo");
        demo.voiceQuestion = vq;

        // --- 3. Mic indicator ---
        var indicator = host.GetComponent<EchoMotionMicIndicator>();
        if (indicator == null) indicator = Undo.AddComponent<EchoMotionMicIndicator>(host);
        Undo.RecordObject(indicator, "wire indicator");
        indicator.voiceQuestion = vq;

        // --- 4. Indicator visual: prefer the mic sprite, fall back to a sphere ---
        var icon = GameObject.Find("MicIndicatorIcon");
        if (icon == null)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/EchoMotionMicIcon.svg");
            if (sprite != null)
            {
                icon = new GameObject("MicIndicatorIcon");
                var sr = icon.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                indicator.micIcon = sr;
                icon.transform.localScale = Vector3.one;
            }
            else
            {
                // No Vector Graphics import yet -> placeholder sphere (tinted via Renderer).
                icon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                icon.name = "MicIndicatorIcon";
                Object.DestroyImmediate(icon.GetComponent<Collider>());
                indicator.targetRenderer = icon.GetComponent<Renderer>();
                icon.transform.localScale = Vector3.one * 0.3f;
                Debug.Log("[Echo-Motion] Kein SVG-Sprite gefunden (Vector-Graphics-Package?) " +
                          "-> Platzhalter-Kugel als Indikator angelegt.");
            }

            Undo.RegisterCreatedObjectUndo(icon, "create mic indicator");

            var cam = Camera.main;
            if (cam != null)
                icon.transform.position = cam.transform.position + cam.transform.forward * 2f;
        }
        else
        {
            var sr = icon.GetComponent<SpriteRenderer>();
            if (sr != null) indicator.micIcon = sr;
            else if (icon.GetComponent<Renderer>() != null) indicator.targetRenderer = icon.GetComponent<Renderer>();
        }

        Undo.RecordObject(indicator, "wire indicator visual");
        indicator.scaleTarget = icon.transform;

        // --- 5. Recommended mic setting for a clean flow (no self-playback) ---
        if (stt.microphoneRecord != null)
        {
            Undo.RecordObject(stt.microphoneRecord, "mic echo off");
            stt.microphoneRecord.echo = false;
        }

        // --- 6. Persist ---
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(host);
        EditorUtility.SetDirty(indicator);
        if (stt.microphoneRecord != null) EditorUtility.SetDirty(stt.microphoneRecord);
        EditorSceneManager.MarkSceneDirty(host.scene);
        Selection.activeGameObject = host;

        Debug.Log("[Echo-Motion] Sprachtest eingerichtet auf '" + host.name +
                  "'. Szene speichern (Cmd/Ctrl+S), dann Play und 'Q' druecken.");
    }
}
#endif
