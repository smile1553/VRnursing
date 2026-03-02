using UnityEngine;

public class LoginUI : MonoBehaviour
{
    [SerializeField] private GameObject loginPanel; // 拖 Canvas/Login_Panel 進來

    // 給 Button OnClick 用
    public void OnClickLogin()
    {
        // TODO: 這裡以後可以加你的驗證流程（帳號密碼、API、等）
        loginPanel.SetActive(false);
    }
}