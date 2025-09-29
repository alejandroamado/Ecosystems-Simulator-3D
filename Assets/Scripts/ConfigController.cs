using UnityEngine;

public class ConfigController : MonoBehaviour
{
    [Header("Referencias al mundo")]
    public GridManager gridManager;
    public AnimalManager animalManager;
    public Terrain terrain;

    [HideInInspector] public int tempGridWidth;
    [HideInInspector] public int tempGridHeight;
    [HideInInspector] public int tempTreeCount;
    [HideInInspector] public int tempRandomGrassCount;

    [HideInInspector] public int tempDeerCount;
    [HideInInspector] public int tempHorseCount;
    [HideInInspector] public AnimalManager.AlgorithmType tempHerbAlgo;

    [HideInInspector] public int tempWolfCount;
    [HideInInspector] public AnimalManager.AlgorithmType tempCarnAlgo;

    private int backupGridWidth, backupGridHeight, backupTreeCount, backupRandomGrassCount;
    private int backupDeerCount, backupHorseCount, backupWolfCount;
    private AnimalManager.AlgorithmType backupHerbAlgo, backupCarnAlgo;

    void Awake()
    {
        BackupCurrentValues();
    }

    public void FreeTempValues()
    {
        tempGridWidth = backupGridWidth;
        tempGridHeight = backupGridHeight;
        tempTreeCount = backupTreeCount;
        tempRandomGrassCount = backupRandomGrassCount;
        tempDeerCount = backupDeerCount;
        tempHorseCount = backupHorseCount;
        tempWolfCount = backupWolfCount;
        tempHerbAlgo = backupHerbAlgo;
        tempCarnAlgo = backupCarnAlgo;
    }

    public void ApplyConfiguration()
    {
        gridManager.width = tempGridWidth;
        gridManager.height = tempGridHeight;
        gridManager.totalTrees = tempTreeCount;
        gridManager.randomGrassCount = tempRandomGrassCount;

        if (terrain != null)
        {
            var size = terrain.terrainData.size;
            size.x = tempGridWidth;
            size.z = tempGridHeight;
            terrain.terrainData.size = size;
        }

        animalManager.numberOfDeers = tempDeerCount;
        animalManager.numberOfHorses = tempHorseCount;
        animalManager.herbivoreAlgorithm = tempHerbAlgo;

        animalManager.numberOfCarnivores = tempWolfCount;
        animalManager.carnivoreAlgorithm = tempCarnAlgo;

        BackupCurrentValues();

        Debug.Log($"Configuración aplicada:\n" +
                  $"Grid: {tempGridWidth}x{tempGridHeight}, Trees: {tempTreeCount}, Grass: {tempRandomGrassCount}\n" +
                  $"Deers: {tempDeerCount} ({tempHerbAlgo}), Horses: {tempHorseCount}\n" +
                  $"Wolves: {tempWolfCount} ({tempCarnAlgo})");
    }

    private void BackupCurrentValues()
    {
        backupGridWidth = gridManager.width;
        backupGridHeight = gridManager.height;
        backupTreeCount = gridManager.totalTrees;
        backupRandomGrassCount = gridManager.randomGrassCount;

        backupDeerCount = animalManager.numberOfDeers;
        backupHorseCount = animalManager.numberOfHorses;
        backupWolfCount = animalManager.numberOfCarnivores;

        backupHerbAlgo = animalManager.herbivoreAlgorithm;
        backupCarnAlgo = animalManager.carnivoreAlgorithm;

        FreeTempValues();
    }

    /// <summary>
    /// Traduce los valores de dropdowns a configuración interna
    /// </summary>
    public void ApplyDropdownConfiguration(
        int terrainSize,
        int trees,
        int grass,
        int deerCount,
        int horseCount,
        int herbivoreAlgo,
        int wolfCount,
        int carnivoreAlgo)
    {
        // Tamaño del terreno
        switch (terrainSize)
        {
            case 0: tempGridWidth = 80; tempGridHeight = 80; break;      // Pequeño
            case 1: tempGridWidth = 100; tempGridHeight = 100; break;    // Mediano
            case 2: tempGridWidth = 130; tempGridHeight = 130; break;    // Grande
            default: tempGridWidth = 100; tempGridHeight = 100; break;
        }

        // Cantidad de árboles
        switch (trees)
        {
            case 0: tempTreeCount = 0; break;       // Nada
            case 1: tempTreeCount = 30; break;      // Baja
            case 2: tempTreeCount = 60; break;      // Media
            case 3: tempTreeCount = 100; break;     // Alta
            default: tempTreeCount = 60; break;
        }

        // Cantidad de hierba aleatoria
        switch (grass)
        {
            case 0: tempRandomGrassCount = 100; break;
            case 1: tempRandomGrassCount = 150; break;
            case 2: tempRandomGrassCount = 200; break;
            default: tempRandomGrassCount = 150; break;
        }

        // Ciervos
        switch (deerCount)
        {
            case 0: tempDeerCount = 0; break;
            case 1: tempDeerCount = 10; break;
            case 2: tempDeerCount = 20; break;
            case 3: tempDeerCount = 30; break;
            default: tempDeerCount = 20; break;
        }

        // Caballos
        switch (horseCount)
        {
            case 0: tempHorseCount = 0; break;
            case 1: tempHorseCount = 10; break;
            case 2: tempHorseCount = 20; break;
            case 3: tempHorseCount = 30; break;
            default: tempHorseCount = 20; break;
        }

        // Lobos
        switch (wolfCount)
        {
            case 0: tempWolfCount = 0; break;
            case 1: tempWolfCount = 2; break;
            case 2: tempWolfCount = 4; break;
            case 3: tempWolfCount = 8; break;
            default: tempWolfCount = 4; break;
        }

        switch (herbivoreAlgo)
        {
            case 0: tempHerbAlgo = AnimalManager.AlgorithmType.Genetic; break;
            case 1: tempHerbAlgo = AnimalManager.AlgorithmType.Reinforcement; break;
            case 2: tempHerbAlgo = AnimalManager.AlgorithmType.Swarm; break;
            case 3: tempHerbAlgo = AnimalManager.AlgorithmType.Rand; break;
            default: tempHerbAlgo = AnimalManager.AlgorithmType.Genetic; break;
        }

        switch (carnivoreAlgo)
        {
            case 0: tempCarnAlgo = AnimalManager.AlgorithmType.Genetic; break;
            case 1: tempCarnAlgo = AnimalManager.AlgorithmType.Reinforcement; break;
            case 2: tempCarnAlgo = AnimalManager.AlgorithmType.Swarm; break;
            case 3: tempCarnAlgo = AnimalManager.AlgorithmType.Rand; break;
            default: tempCarnAlgo = AnimalManager.AlgorithmType.Genetic; break;
        }

        ApplyConfiguration();
    }
}
