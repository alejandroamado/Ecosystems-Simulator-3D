using TMPro;
using UnityEngine;

public class DeerCounterUI : MonoBehaviour
{
    public TextMeshProUGUI deerCounterText;
    public TextMeshProUGUI horseCounterText;
    public TextMeshProUGUI wolfCounterText;
    public TextMeshProUGUI grassCounterText;
    
    private AnimalManager animalManager;
    private GridManager gridManager;

    void Start()
    {
        animalManager = Object.FindFirstObjectByType<AnimalManager>();
        gridManager = Object.FindFirstObjectByType<GridManager>();

        UpdateDeerCounter();
        UpdateHorseCounter();
        UpdateWolfCounter();
        UpdateGrassCounter();
    }

    void Update()
    {
        UpdateDeerCounter();
        UpdateHorseCounter();
        UpdateWolfCounter();
        UpdateGrassCounter();
    }

    void UpdateDeerCounter()
    {
        if (deerCounterText != null && animalManager != null)
        {
            deerCounterText.text = "Total ciervos: " + animalManager.activeDeerCounter;
        }
    }

    void UpdateHorseCounter()
    {
        if (horseCounterText != null && animalManager != null)
        {
            horseCounterText.text = "Total caballos: " + animalManager.activeHorseCounter;
        }
    }

    void UpdateWolfCounter()
    {
        if (wolfCounterText != null && animalManager != null)
        {
            wolfCounterText.text = "Total lobos: " + animalManager.activeWolfCounter;
        }
    }


    void UpdateGrassCounter()
    {
        if (grassCounterText != null && gridManager != null)
        {
            grassCounterText.text = "Total hierba: " + gridManager.activeGrassCounter;
        }
    }
}
