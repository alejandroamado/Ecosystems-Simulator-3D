using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("Referencia al panel de pausa (UI)")]
    public GameObject pausePanel;

    [Header("Referencia al panel principal (Main Menu)")]
    public GameObject mainMenuPanel;

    [Header("Botones UI")]
    public Button resumeButton;
    public Button pauseButton;
    public Button exitToMenuButton;

    [Header("Control de cámara")]
    public SimpleCameraController cameraController;

    public bool isPaused = false;

    void Start()
    {
        // Arrancamos sin pausa
        SetPaused(false);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(() => SetPaused(false));
        if (pauseButton != null)
            pauseButton.onClick.AddListener(() => TogglePause());
        if (exitToMenuButton != null)
            exitToMenuButton.onClick.AddListener(() => ExitToMenu());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
        SetPaused(!isPaused);
    }

    public void SetPaused(bool pause)
    {
        isPaused = pause;

        // Congelar / descongelar tiempo
        Time.timeScale = isPaused ? 0f : 1f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // Activar o desactivar controles de cámara
        if (cameraController != null)
            cameraController.enabled = !isPaused;

        // Mostrar / ocultar menú de pausa
        if (pausePanel != null)
            pausePanel.SetActive(isPaused);
    }

    public void ExitToMenu()
    {
        SetPaused(true);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
    }
}
