using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoginController : MonoBehaviour
{
    private const string MainMenuScene = "MainMenu";

    [SerializeField] private SQL sql;
    [SerializeField] private TMP_InputField idInputField;
    [SerializeField] private Button confirmButton;

    private void OnEnable()
    {
        confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    private void OnDisable()
    {
        confirmButton.onClick.RemoveListener(OnConfirmClicked);
    }

    private void OnConfirmClicked()
    {
        string userId = idInputField.text.Trim();

        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[LoginController] The identifier field is empty.");
            return;
        }

        if (sql.UserExists(userId))
        {
            UserSession.Instance.SetUser(userId);
            SceneManager.LoadScene(MainMenuScene);
        }
        else
        {
            Debug.LogWarning($"[LoginController] No user found with identifier: {userId}");
        }
    }
}
