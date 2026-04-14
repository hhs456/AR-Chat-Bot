using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using UnityEngine.InputSystem; // 引用新版輸入系統

/// <summary>
/// 修正後的 Dify API 介面：支援 Dify 工作流變數 userinput.query
/// 基於使用者提供的結構進行優化
/// </summary>
public class DifyUnityIntegration : MonoBehaviour {
    [Header("Dify API 設定")]
    [Tooltip("請使用 app- 開頭的應用程式金鑰")]
    public string apiKey = "在此輸入你的_DIFY_API_KEY";
    public string apiUrl = "http://localhost/v1/chat-messages";

    [Header("測試設定")]
    [TextArea(3, 10)]
    public string testQuery = "這台設備的保養週期是多久？";

    // 用於記錄對話 ID，以便連續對話
    private string conversationId = "";

    // 接收回應的資料結構
    [System.Serializable]
    public class DifyResponse {
        public string answer;
        public string conversation_id;
        public string task_id;
    }

    void Start() {
        Debug.Log("<color=yellow>【RAG 系統】已啟動。按下【空白鍵】發送測試問題。</color>");
    }

    void Update() {
        // 使用 New Input System 偵測空白鍵
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) {
            SendToDify(testQuery);
        }
    }

    public void SendToDify(string query) {
        StartCoroutine(PostRequest(query));
    }

    IEnumerator PostRequest(string query) {
        // 防止 query 包含引號導致 JSON 格式錯誤
        string escapedQuery = query.Replace("\"", "\\\"");

        // 【核心修改】：手動建構 JSON 字串
        // 這是為了符合你 Dify 開始節點中「userinput.query」這個帶有點號的變數名稱
        string json = "{" +
                      "\"inputs\": {" +
                          "\"userinput.query\": \"" + escapedQuery + "\"" +
                      "}," +
                      "\"query\": \"" + escapedQuery + "\"," +
                      "\"response_mode\": \"blocking\"," +
                      "\"user\": \"unity_tester\"," +
                      "\"conversation_id\": \"" + conversationId + "\"" +
                      "}";

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST")) {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log("<color=cyan>正在傳送請求到 Dify 工作流...</color>");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success) {
                string responseText = request.downloadHandler.text;
                Debug.Log("收到原始回應：" + responseText);

                try {
                    // 使用 JsonUtility 解析回傳結果
                    DifyResponse res = JsonUtility.FromJson<DifyResponse>(responseText);
                    conversationId = res.conversation_id;

                    // 處理回傳文字中的換行符號
                    string cleanAnswer = res.answer.Replace("\\n", "\n");
                    Debug.Log("<color=green>AI 助手回覆：</color>\n" + cleanAnswer);
                }
                catch (System.Exception e) {
                    Debug.LogError("JSON 解析失敗：" + e.Message);
                }
            }
            else {
                Debug.LogError("連線失敗：" + request.error);
                if (request.responseCode == 401)
                    Debug.LogError("提示：請檢查 API 金鑰是否為該應用程式的『app-』金鑰。");
                if (request.responseCode == 404)
                    Debug.LogError("提示：請檢查 API 地址是否正確。");
            }
        }
    }
}