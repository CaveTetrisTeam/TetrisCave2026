using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace CaveGame.Quiz
{
    [Serializable] public sealed class OllamaQuizResult { public bool correct; public string feedback; }

    public sealed class OllamaQuizClient : MonoBehaviour
    {
        public string endpoint = "http://localhost:11434/api/chat";
        public string model = "gemma3:4b";
        [Min(1f)] public float timeoutSeconds = 10f;

        [Serializable] private sealed class Message { public string role; public string content; }
        [Serializable] private sealed class Options { public float temperature = 0.1f; }
        [Serializable] private sealed class Request { public string model; public bool stream; public string format; public Message[] messages; public Options options; }
        [Serializable] private sealed class Response { public Message message; }

        public async Task<OllamaQuizResult> EvaluateAsync(QuizQuestion question, string transcript, CancellationToken token)
        {
            string prompt = "Bewerte die Antwort auf Deutsch. Akzeptiere sinngleiche Antworten. " +
                            "Antworte ausschließlich als JSON mit correct (Boolean) und feedback (kurzer deutscher Satz).\n" +
                            "Frage: " + question.question + "\nMusterlösung: " + question.expectedAnswer +
                            "\nAkzeptierte Varianten: " + string.Join(", ", question.acceptedVariants ?? new System.Collections.Generic.List<string>()) +
                            "\nAntwort: " + transcript;
            var body = new Request
            {
                model = model, stream = false, format = "json", options = new Options(),
                messages = new[] { new Message { role = "user", content = prompt } }
            };

            using (var request = new UnityWebRequest(endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.CeilToInt(timeoutSeconds);
                using (token.Register(request.Abort))
                {
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }
                }
                token.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException(request.error);

                Response response = JsonUtility.FromJson<Response>(request.downloadHandler.text);
                if (response == null || response.message == null || string.IsNullOrWhiteSpace(response.message.content))
                    throw new FormatException("Ollama-Antwort enthält keinen Inhalt.");
                string resultJson = response.message.content;
                if (!resultJson.Contains("\"correct\"") || !resultJson.Contains("\"feedback\""))
                    throw new FormatException("Ollama-Ergebnis enthält nicht alle Pflichtfelder.");
                OllamaQuizResult result = JsonUtility.FromJson<OllamaQuizResult>(resultJson);
                if (result == null || string.IsNullOrWhiteSpace(result.feedback))
                    throw new FormatException("Ollama-Ergebnis ist kein gültiges Quiz-JSON.");
                return result;
            }
        }
    }
}
