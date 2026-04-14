using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic; // 引用 List 集合
using System.Text;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 支援「系統提示詞」與「對話記憶」的 OpenRouter API 客戶端
/// </summary>
public class OpenRouterClient : MonoBehaviour {
    [Header("UI 元件綁定")]
    public TMP_InputField inputField;
    public Button sendButton;
    public TextMeshProUGUI outputText;

    [Header("OpenRouter API 設定")]
    [Tooltip("請輸入你的 OpenRouter API Key (sk-or-v1-...)")]
    public string apiKey = "在此輸入你的_OPENROUTER_API_KEY";

    [Tooltip("你想使用的模型名稱")]
    public string modelName = "google/gemini-2.5-flash";

    [Header("AI 角色與上下文設定")]
    [Tooltip("設定 AI 的人設與回答規則 (System Prompt)")]
    [TextArea(3, 5)]
    public string systemPrompt = "你是一個專業的工業設備維修助理。請使用繁體中文，並以步驟化的方式指導使用者進行故障排除。";

    [Tooltip("要記住幾回合的對話歷史？(避免對話太長導致 Token 爆表)")]
    public int maxHistoryTurns = 5;

    private string apiUrl = "https://openrouter.ai/api/v1/chat/completions";

    // --- 儲存對話歷史的清單 ---
    private List<ORMessage> chatHistory = new List<ORMessage>();

    // --- API 資料結構 ---
    [System.Serializable]
    public class ORMessage {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class ORRequest {
        public string model;
        public ORMessage[] messages;
    }

    [System.Serializable]
    public class ORResponse {
        public ORChoice[] choices;
    }

    [System.Serializable]
    public class ORChoice {
        public ORMessage message;
    }
    // -------------------------------------------------------------

    void Start() {
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendClicked);
    }

    public void OnSendClicked() {
        if (inputField != null && !string.IsNullOrEmpty(inputField.text)) {
            string userQuery = inputField.text;
            if (outputText != null)
                outputText.text = "思考中...";

            StartCoroutine(PostToOpenRouter(userQuery));
            inputField.text = "";
        }
    }

    /// <summary>
    /// 清除對話記憶（可以綁定到一個 UI 按鈕上，用來開啟新對話）
    /// </summary>
    public void ClearChatHistory() {
        chatHistory.Clear();
        if (outputText != null)
            outputText.text = "對話紀錄已清除。";
        Debug.Log("對話歷史已重置");
    }

    IEnumerator PostToOpenRouter(string query) {
        // 1. 將使用者的最新問題加入歷史紀錄
        chatHistory.Add(new ORMessage { role = "user", content = query });

        // 如果歷史紀錄太長，移除最舊的對話 (一問一答算 2 筆，所以 * 2)
        if (chatHistory.Count > maxHistoryTurns * 2) {
            // 移除最舊的一組對話 (User 和 Assistant)
            chatHistory.RemoveRange(0, 2);
        }

        // 2. 準備這次要送出的完整對話包
        List<ORMessage> messagesToSend = new List<ORMessage>();

        // 永遠把 System Prompt 塞在最前面
        if (!string.IsNullOrEmpty(systemPrompt)) {
            messagesToSend.Add(new ORMessage { role = "system", content = systemPrompt });
        }

        // 加入處理過的對話歷史
        messagesToSend.AddRange(chatHistory);

        // 3. 建構請求資料
        ORRequest requestData = new ORRequest {
            model = modelName,
            messages = messagesToSend.ToArray()
        };

        string jsonPayload = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST")) {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success) {
                string responseText = request.downloadHandler.text;
                try {
                    ORResponse res = JsonUtility.FromJson<ORResponse>(responseText);
                    string aiAnswer = res.choices[0].message.content;

                    // 4. 將 AI 的回答也加入歷史紀錄，這樣下次發問 AI 才會記得！
                    chatHistory.Add(new ORMessage { role = "assistant", content = aiAnswer });

                    aiAnswer = aiAnswer.Replace("\\n", "\n");
                    if (outputText != null)
                        outputText.text = aiAnswer;
                }
                catch (System.Exception e) {
                    Debug.LogError("JSON 解析失敗：" + e.Message);
                    // 解析失敗時，把剛剛加入歷史的使用者提問移除，避免後續錯亂
                    chatHistory.RemoveAt(chatHistory.Count - 1);
                }
            }
            else {
                Debug.LogError("連線失敗：" + request.error);
                chatHistory.RemoveAt(chatHistory.Count - 1);
            }
        }
    }
}