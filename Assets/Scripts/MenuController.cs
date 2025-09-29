using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MenuController : MonoBehaviour
{
    [Header("Paneles")]
    public GameObject mainMenuPanel;
    public GameObject configPanelGeneral;
    public GameObject configPanelTerrain;
    public GameObject configPanelHerbivores;
    public GameObject configPanelCarnivores;

    [Header("Botones")]
    public Button startSimulationButton;
    public Button configButton;
    public Button backButton;
    public Button applyChangesButton;

    public Button terrainVegetationButton;
    public Button herbivoresButton;
    public Button carnivoresButton;
    public Button exitButton;

    public ConfigController configController;

    [Header("Dropdowns configuración")]
    public TMP_Dropdown terrainSizeDropdown;
    public TMP_Dropdown treesDropdown;
    public TMP_Dropdown grassDropdown;

    public TMP_Dropdown deerCountDropdown;
    public TMP_Dropdown horseCountDropdown;
    public TMP_Dropdown herbivoreAlgoDropdown;

    public TMP_Dropdown wolfCountDropdown;
    public TMP_Dropdown carnivoreAlgoDropdown;

    [Header("Botones de vuelta desde submenús")]
    public Button backFromTerrainButton;
    public Button backFromHerbivoresButton;
    public Button backFromCarnivoresButton;

    public PauseManager pauseManager;
    public GridManager gridManager;
    public AnimalManager animalManager;
    private NavMeshUpdater navMeshUpdater;
    public PopulationLogger populationLogger;

    public bool onMenu = true;

    void Start()
    {
        mainMenuPanel.SetActive(true);
        configPanelGeneral.SetActive(false);
        configPanelTerrain.SetActive(false);
        configPanelHerbivores.SetActive(false);
        configPanelCarnivores.SetActive(false);

        configButton.onClick.AddListener(OpenConfiguration);
        backButton.onClick.AddListener(CloseConfiguration);
        applyChangesButton.onClick.AddListener(ApplyChanges);
        startSimulationButton.onClick.AddListener(StartSimulation);

        terrainVegetationButton.onClick.AddListener(OpenTerrainVegetationConfig);
        herbivoresButton.onClick.AddListener(OpenHerbivoresConfig);
        carnivoresButton.onClick.AddListener(OpenCarnivoresConfig);

        exitButton.onClick.AddListener(QuitApplication);

        SetDefaultDropdownValues();

        navMeshUpdater = GameObject.Find("Terrain")?.GetComponent<NavMeshUpdater>();

        if (backFromTerrainButton != null)
            backFromTerrainButton.onClick.AddListener(BackFromSubPanel);

        if (backFromHerbivoresButton != null)
            backFromHerbivoresButton.onClick.AddListener(BackFromSubPanel);

        if (backFromCarnivoresButton != null)
            backFromCarnivoresButton.onClick.AddListener(BackFromSubPanel);
    }

    public void StartSimulation()
    {
        ApplyChanges(); // Asegura que los cambios se apliquen antes de iniciar

        mainMenuPanel.SetActive(false);
        configPanelGeneral.SetActive(false);
        configPanelTerrain.SetActive(false);
        configPanelHerbivores.SetActive(false);
        configPanelCarnivores.SetActive(false);

        gridManager.StartGridManager();
        animalManager.StartAnimalManager();
        populationLogger.StartLogger();

        navMeshUpdater?.StartNavMeshUpdater();

        pauseManager.isPaused = false;
        pauseManager.SetPaused(false);

        Debug.Log("Simulación iniciada");
    }

    void SetDefaultDropdownValues()
    {
        terrainSizeDropdown.value = 1;
        treesDropdown.value = 2;
        grassDropdown.value = 1;

        deerCountDropdown.value = 2;
        horseCountDropdown.value = 2;
        herbivoreAlgoDropdown.value = 0;

        wolfCountDropdown.value = 2;
        carnivoreAlgoDropdown.value = 0;

        terrainSizeDropdown.RefreshShownValue();
        treesDropdown.RefreshShownValue();
        grassDropdown.RefreshShownValue();
        deerCountDropdown.RefreshShownValue();
        horseCountDropdown.RefreshShownValue();
        herbivoreAlgoDropdown.RefreshShownValue();
        wolfCountDropdown.RefreshShownValue();
        carnivoreAlgoDropdown.RefreshShownValue();
    }

    public void OpenConfiguration()
    {
        onMenu = false;
        mainMenuPanel.SetActive(false);
        configPanelGeneral.SetActive(true);
        configPanelTerrain.SetActive(false);
        configPanelHerbivores.SetActive(false);
        configPanelCarnivores.SetActive(false);
    }

    public void CloseConfiguration()
    {
        onMenu = true;
        configPanelGeneral.SetActive(false);
        configPanelTerrain.SetActive(false);
        configPanelHerbivores.SetActive(false);
        configPanelCarnivores.SetActive(false);
        mainMenuPanel.SetActive(true);
        configController.FreeTempValues();
    }

    public void BackFromSubPanel()
    {
        configPanelTerrain.SetActive(false);
        configPanelHerbivores.SetActive(false);
        configPanelCarnivores.SetActive(false);
        configPanelGeneral.SetActive(true);
    }

    public void OpenTerrainVegetationConfig()
    {
        configPanelGeneral.SetActive(false);
        configPanelTerrain.SetActive(true);
        configPanelHerbivores.SetActive(false);
        configPanelCarnivores.SetActive(false);
    }

    public void OpenHerbivoresConfig()
    {
        configPanelGeneral.SetActive(false);
        configPanelTerrain.SetActive(false);
        configPanelHerbivores.SetActive(true);
        configPanelCarnivores.SetActive(false);
    }

    public void OpenCarnivoresConfig()
    {
        configPanelGeneral.SetActive(false);
        configPanelTerrain.SetActive(false);
        configPanelHerbivores.SetActive(false);
        configPanelCarnivores.SetActive(true);
    }

    public void ApplyChanges()
    {
        configController.ApplyDropdownConfiguration(
            terrainSizeDropdown.value,
            treesDropdown.value,
            grassDropdown.value,
            deerCountDropdown.value,
            horseCountDropdown.value,
            herbivoreAlgoDropdown.value,
            wolfCountDropdown.value,
            carnivoreAlgoDropdown.value
        );
    }
    
    public void QuitApplication()
    {
        Debug.Log("Saliendo del juego...");
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }
}
