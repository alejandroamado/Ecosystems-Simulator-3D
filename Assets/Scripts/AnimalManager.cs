using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;
using Controller;

namespace EcosystemAI
{
    public enum DecisionActionType
    {
        SeekFood,
        SeekMate,
        Rest
    }
}

public class AnimalManager : MonoBehaviour
{
    [Header("Deer Prefab & Count")]
    public GameObject deerPrefab;
    public int numberOfDeers = 20;

    [Header("Deer Prefab & Count")]
    public GameObject horsePrefab;
    public int numberOfHorses = 20;

    [Header("Carnivore Prefab & Count")]
    public GameObject carnivorePrefab;
    public int numberOfCarnivores = 5;

    public enum AlgorithmType { Genetic, Reinforcement, Swarm, Rand }

    [Header("Algoritmo de IA")]
    public AlgorithmType herbivoreAlgorithm;
    public AlgorithmType carnivoreAlgorithm;

    [HideInInspector] public SwarmBrain herbBrain;
    [HideInInspector] public SwarmBrain wolfBrain;

    public int activeDeerCounter = 0;
    public int totalDeerCounter = 0;

    public int activeHorseCounter = 0;
    public int totalHorseCounter = 0;

    public int activeWolfCounter = 0;
    public int totalWolfCounter = 0;

    private HerbivoreAI[] herbivores;
    private CarnivoreAI[] carnivores;
    public GridManager gridManager;
    public int effectiveMargin = 2;

    void Awake()
    {
        herbivores = Object.FindObjectsByType<HerbivoreAI>(FindObjectsSortMode.None);
        carnivores = Object.FindObjectsByType<CarnivoreAI>(FindObjectsSortMode.None);
    }

    public void StartAnimalManager()
    {
        ClearAllAnimals();

        if (herbivoreAlgorithm == AlgorithmType.Genetic || carnivoreAlgorithm == AlgorithmType.Genetic
            || herbivoreAlgorithm == AlgorithmType.Rand || carnivoreAlgorithm == AlgorithmType.Rand)
        {
            ActionGeneticTrainer.Instance.Initialize();
        }

        if (herbivoreAlgorithm == AlgorithmType.Swarm)
            herbBrain = new SwarmBrain(SwarmBrain.Species.Herbivore);
        if (carnivoreAlgorithm  == AlgorithmType.Swarm)
            wolfBrain = new SwarmBrain(SwarmBrain.Species.Carnivore);

        SpawnDeers();
        SpawnHorses();
        SpawnCarnivores();
    }

    private void ClearAllAnimals()
    {
        // Destruir todos los ciervos
        foreach (var h in Object.FindObjectsByType<HerbivoreAI>(FindObjectsSortMode.None))
            if (h != null && h.gameObject != null)
                Destroy(h.gameObject);

        // Destruir todos los lobos
        foreach (var c in Object.FindObjectsByType<CarnivoreAI>(FindObjectsSortMode.None))
            if (c != null && c.gameObject != null)
                Destroy(c.gameObject);

        // Resetear contadores
        activeDeerCounter = 0;
        totalDeerCounter = 0;
        activeHorseCounter = 0;
        totalHorseCounter = 0;
        activeWolfCounter = 0;
        totalWolfCounter = 0;
    }

    void SpawnDeers()
    {
        int minX = effectiveMargin;
        int minZ = effectiveMargin;
        int maxX = gridManager.width - effectiveMargin;
        int maxZ = gridManager.height - effectiveMargin;

        for (int i = 0; i < numberOfDeers; i++)
        {
            Vector3 randomPosition;

            if (gridManager.width > 50 && gridManager.height > 50)
            {
                randomPosition = new Vector3(Random.Range(minX, 50), 0, Random.Range(minZ, 50));
            }
            else
            {
                randomPosition = new Vector3(Random.Range(minX, maxX), 0, Random.Range(minZ, maxZ));

            }

            GameObject herbivore = Instantiate(deerPrefab, randomPosition, Quaternion.identity);

            activeDeerCounter++;
            totalDeerCounter++;
            herbivore.name = "Deer_" + totalDeerCounter.ToString("D3");

            // Desactivar scripts que no se deben usar (por ejemplo, CreatureMover y MovePlayerInput)
            DisableUnwantedScripts(herbivore);

            // Asegurar que el herbívoro tenga un NavMeshAgent
            EnsureNavMeshAgent(herbivore);

            // Agregar y configurar HerbivoreAI
            HerbivoreAI herbivoreAI = herbivore.GetComponent<HerbivoreAI>();
            if (herbivoreAI == null)
            {
                herbivoreAI = herbivore.AddComponent<HerbivoreAI>();
            }

            if (herbivoreAI != null)
            {
                herbivoreAI.species = HerbivoreAI.Species.Deer;
                herbivoreAI.StartHerbivore();
            }
            else
            {
                Debug.LogWarning("Failed to add HerbivoreAI to the instantiated herbivore.");
            }
        }
    }

    void SpawnHorses()
    {
        int minX = effectiveMargin;
        int minZ = effectiveMargin;
        int maxX = gridManager.width - effectiveMargin;
        int maxZ = gridManager.height - effectiveMargin;

        for (int i = 0; i < numberOfHorses; i++)
        {
            Vector3 randomPosition;

            if (gridManager.width > 50 && gridManager.height > 50)
            {
                randomPosition = new Vector3(Random.Range(minX, 50), 0, Random.Range(minZ, 50));
            }
            else
            {
                randomPosition = new Vector3(Random.Range(minX, maxX), 0, Random.Range(minZ, maxZ));

            }

            GameObject herbivore = Instantiate(horsePrefab, randomPosition, Quaternion.identity);

            activeHorseCounter++;
            totalHorseCounter++;
            herbivore.name = "Horse_" + totalHorseCounter.ToString("D3");

            // Desactivar scripts que no se deben usar (por ejemplo, CreatureMover y MovePlayerInput)
            DisableUnwantedScripts(herbivore);

            // Asegurar que el herbívoro tenga un NavMeshAgent
            EnsureNavMeshAgent(herbivore);

            // Agregar y configurar HerbivoreAI
            HerbivoreAI herbivoreAI = herbivore.GetComponent<HerbivoreAI>();
            if (herbivoreAI == null)
            {
                herbivoreAI = herbivore.AddComponent<HerbivoreAI>();
            }

            if (herbivoreAI != null)
            {
                herbivoreAI.species = HerbivoreAI.Species.Horse;
                herbivoreAI.StartHerbivore();
            }
            else
            {
                Debug.LogWarning("Failed to add HerbivoreAI to the instantiated herbivore.");
            }
        }
    }

    void SpawnCarnivores()
    {
        int minX = effectiveMargin;
        int minZ = effectiveMargin;
        int maxX = gridManager.width - effectiveMargin;
        int maxZ = gridManager.height - effectiveMargin;

        for (int i = 0; i < numberOfCarnivores; i++)
        {
            Vector3 randomPosition;

            if (gridManager.width > 50 && gridManager.height > 50)
            {
                randomPosition = new Vector3(Random.Range(gridManager.width - 50, maxX), 0, Random.Range(gridManager.height - 50, maxZ));
            }
            else
            {
                randomPosition = new Vector3(Random.Range(minX, maxX), 0, Random.Range(minZ, maxZ));

            }

            GameObject carnivore = Instantiate(carnivorePrefab, randomPosition, Quaternion.identity);

            activeWolfCounter++;
            totalWolfCounter++;
            carnivore.name = "Wolf_" + totalWolfCounter.ToString("D3");

            // Desactivar scripts que no se deben usar (por ejemplo, CreatureMover y MovePlayerInput)
            DisableUnwantedScripts(carnivore);

            // Asegurar que el herbívoro tenga un NavMeshAgent
            EnsureNavMeshAgent(carnivore);

            // Agregar y configurar HerbivoreAI
            CarnivoreAI carnivoreAI = carnivore.GetComponent<CarnivoreAI>();
            if (carnivoreAI == null)
            {
                carnivoreAI = carnivore.AddComponent<CarnivoreAI>();
            }

            if (carnivoreAI != null)
            {
                carnivoreAI.StartCarnivore();
            }
            else
            {
                Debug.LogWarning("Failed to add CarnivoreAI to the instantiated carnivore.");
            }
        }
    }

    // Método para desactivar CreatureMover y MovePlayerInput en el herbívoro
    public void DisableUnwantedScripts(GameObject animal)
    {
        Destroy(animal.GetComponent<MovePlayerInput>());
        Destroy(animal.GetComponent<CreatureMover>());
    }

    // Método para asegurarse de que el herbívoro tenga un NavMeshAgent y configurarlo
    private void EnsureNavMeshAgent(GameObject animal)
    {
        NavMeshAgent navAgent = animal.GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            navAgent = animal.AddComponent<NavMeshAgent>();
        }
        navAgent.baseOffset = -0.05f;
        // Configurar parámetros básicos
        navAgent.speed = 1.5f;
        navAgent.angularSpeed = 200f;
        navAgent.acceleration = 8f;
        navAgent.stoppingDistance = 0.5f;
    }

    public List<HerbivoreAI> GetHerbivores()
    {
        // Cada vez que se llame, obtengo todos los HerbivoreAI actualmente en escena
        return new List<HerbivoreAI>(Object.FindObjectsByType<HerbivoreAI>(FindObjectsSortMode.None));
    }

    public List<CarnivoreAI> GetCarnivores()
    {
        return new List<CarnivoreAI>(Object.FindObjectsByType<CarnivoreAI>(FindObjectsSortMode.None));
    }

    public float NormalRandom(float mean, float std)
    {
        float u1 = Random.value;
        float u2 = Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return mean + std * randStdNormal;
    }
}
