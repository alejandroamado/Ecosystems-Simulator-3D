using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EcosystemAI;

public class CarnivoreAI : MonoBehaviour
{
    private Animator animator;
    private GridManager gridManager;
    private NavMeshAgent agent;
    private AnimalManager animalManager;

    private AnimalManager.AlgorithmType algorithmType;

    // Para RL
    private RLDecisionMaker rlDecisionMaker;
    private int prevState;
    private RLTrainer rlTrainer;

    // Para SI
    private SwarmBrain brain;
    private SwarmBrain siDecisionMaker;

    // Para GA
    private ActionGenome genome;
    private IDecisionMaker decisionMaker;

    [SerializeField]
    private float[] weights = new float[3];

    public float minIdleTime = 2f;
    public float maxIdleTime = 5f;
    public float minRestTime = 5f;
    public float maxRestTime = 20f;

    public WolfStats wolfStats;

    public float growthRate = 0.02f;
    public float adultAge = 2f;
    public float hungerDecayRate = 1f;
    public float energyConsumption = 0.75f;
    public float healthDecayRate = 6f;
    public float energyDecayRate = 4f;
    public float runSpeedMultiplier = 3f;

    private GameObject lastTargetedPrey;
    private GameObject lastTargetedMate;

    private bool isInitialized = false;

    [Header("Reproduction settings")]
    [SerializeField] private float reproductionCooldownYears = 1f;
    private float lastReproductionAge = -999f;


    void Awake()
    {
        animalManager = Object.FindAnyObjectByType<AnimalManager>();
        if (animalManager == null)
            Debug.LogError("AnimalManager not found in the scene.");
    }

    public void StartCarnivore()
    {
        // 0) Leer algoritmo desde AnimalManager
        algorithmType = animalManager.carnivoreAlgorithm;

        if (wolfStats == null)
        {
            wolfStats = new WolfStats(
            6f, 12f,     // Rango para maxAge
            0.9f, 1.1f,  // Rango para size
            1.2f, 1.7f,  // Rango para speed
            80f, 110f,    // Rango para health
            80f, 110f,    // Rango para energy
            80f, 110f,    // Rango para hunger
            50f, 75f,    // Rango para strength
            40f, 60f     // Rango para deteccion
        );

        if (name == "Wolf_001")
            wolfStats.gender = WolfStats.Gender.Male;
        else if (name == "Wolf_002")
            wolfStats.gender = WolfStats.Gender.Female;
        else
            wolfStats.gender = (Random.value < 0.5f) ? WolfStats.Gender.Male : WolfStats.Gender.Female;
        }

        wolfStats.health = wolfStats.maxHealth * 0.6f;
        wolfStats.energy = wolfStats.maxEnergy * 0.9f;
        wolfStats.hunger = wolfStats.maxHunger * 0.5f;
        
        if (algorithmType == AnimalManager.AlgorithmType.Genetic || algorithmType == AnimalManager.AlgorithmType.Rand)
        {
            // GA: genoma → decisionMaker
            if (genome == null)
                genome = ActionGeneticTrainer.Instance.GetRandomGenome();
            weights = new float[] { genome.genes[0], genome.genes[1], genome.genes[2] };
            decisionMaker = new GeneticDecisionMaker(weights);

            minIdleTime = genome.genes[3];
            maxIdleTime = genome.genes[4];
            minRestTime = genome.genes[5];
            maxRestTime = genome.genes[6];
        }
        else if (algorithmType == AnimalManager.AlgorithmType.Reinforcement)
        {
            // RL: componente y trainer
            var comp = GetComponent<RLDecisionMakerComponent>()
                       ?? gameObject.AddComponent<RLDecisionMakerComponent>();
            rlDecisionMaker = comp.DecisionMaker;
            decisionMaker = rlDecisionMaker;

            rlTrainer = FindObjectOfType<RLTrainer>();
            if (rlTrainer == null)
                rlTrainer = new GameObject("RLTrainer").AddComponent<RLTrainer>();

            prevState = rlDecisionMaker.StateFromAgent(this);
        }
        else if (algorithmType == AnimalManager.AlgorithmType.Swarm)
        {
            brain = animalManager.wolfBrain;
            prevState = brain.StateFromAgent(this);
            siDecisionMaker = brain;
            decisionMaker = siDecisionMaker;
        }

        // 2) Init NavMesh, animator, gridManager…
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        gridManager = Object.FindAnyObjectByType<GridManager>();
        
        if (gridManager == null)
        {
            Debug.LogError("GridManager not found in the scene.");
        }

        NavMeshHit hit;
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else if (!agent.isOnNavMesh)
        {
            Debug.LogError($"{name} could not be placed on NavMesh at {transform.position}");
        }

        isInitialized = true;
        float initFactor = wolfStats.age >= adultAge
            ? 1f
            : Mathf.Lerp(0.5f, 1f, wolfStats.age / adultAge);
        agent.speed = wolfStats.baseSpeed * initFactor;
        StartCoroutine(IdleRoutine());
    }

    void Update()
    {
        if (!isInitialized)
            return;

        UpdateAnimation();
        UpdateGrowth();
        UpdateStats();
    }

    void UpdateAnimation()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        float speed = agent.velocity.magnitude;
        animator.SetFloat("Vert", speed);
        animator.SetFloat("State", speed > wolfStats.baseSpeed * 2 ? 1f : 0f);
    }

    void UpdateGrowth()
    {
        wolfStats.age = Mathf.MoveTowards(wolfStats.age, wolfStats.maxAge, growthRate * Time.deltaTime);

        if (wolfStats.age <= adultAge)
        {
            float factor = Mathf.Lerp(0.5f, 1f, wolfStats.age / adultAge);

            transform.localScale = Vector3.one * factor;

            bool wasAtMaxHealth = wolfStats.health == wolfStats.maxHealth;
            bool wasAtMaxEnergy = wolfStats.energy == wolfStats.maxEnergy;
            bool wasAtMaxHunger = wolfStats.hunger == wolfStats.maxHunger;

            wolfStats.maxHealth = wolfStats.baseMaxHealth * factor;
            wolfStats.maxEnergy = wolfStats.baseMaxEnergy * factor;
            wolfStats.maxHunger = wolfStats.baseMaxHunger * factor;

            if (wasAtMaxHealth) wolfStats.health = wolfStats.maxHealth;
            if (wasAtMaxEnergy) wolfStats.energy = wolfStats.maxEnergy;
            if (wasAtMaxHunger) wolfStats.hunger = wolfStats.maxHunger;
        }

        if (wolfStats.age >= wolfStats.maxAge)
        {
            Debug.Log(gameObject.name + " died of old age.");
            Destroy(gameObject);
            animalManager.activeWolfCounter--;
        }
    }

    void UpdateStats()
    {
        if (wolfStats.health <= 0)
        {
            Debug.Log(gameObject.name + " died.");
            Destroy(gameObject);
            animalManager.activeWolfCounter--;
        }
        else if (wolfStats.health < 30)
        {
            agent.speed = wolfStats.baseSpeed * 0.5f;
            wolfStats.strength = wolfStats.baseStrength * 0.5f;
            wolfStats.energy = wolfStats.baseMaxEnergy * 0.5f;
            wolfStats.hunger = wolfStats.baseMaxHunger * 0.75f;
        }

        // Actualización del hambre
        wolfStats.hunger = Mathf.MoveTowards(wolfStats.hunger, 0, hungerDecayRate * Time.deltaTime);
        if (wolfStats.hunger <= 0f)
        {
            wolfStats.health = Mathf.MoveTowards(wolfStats.health, 0, healthDecayRate * Time.deltaTime);
            wolfStats.energy = Mathf.MoveTowards(wolfStats.energy, 0, energyDecayRate * Time.deltaTime);
            wolfStats.hunger = 0;
        }

        // Actualización de la energía
        float currentSpeed = agent.velocity.magnitude;
        if (currentSpeed > 0.1f)
        {
            wolfStats.energy = Mathf.MoveTowards(wolfStats.energy, 0, currentSpeed * energyConsumption * Time.deltaTime);
        }

        if (wolfStats.energy <= 0f)
        {
            agent.speed = wolfStats.baseSpeed * 0.5f;
            wolfStats.strength = wolfStats.baseStrength * 0.5f;
            wolfStats.energy = 0;
        }
    }

    IEnumerator IdleRoutine()
    {
        while (true)
        {
            // 1) Idle / Warp
            agent.isStopped = true;
            NavMeshHit hit;
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
                agent.Warp(hit.position);

            // 2) Espera idle
            float idleTime = Random.Range(minIdleTime, maxIdleTime);
            yield return new WaitForSeconds(idleTime);
            agent.isStopped = false;

            // 3) Estado previo RL
            if (algorithmType == AnimalManager.AlgorithmType.Reinforcement)
                prevState = rlDecisionMaker.StateFromAgent(this);

            if (algorithmType == AnimalManager.AlgorithmType.Swarm)
                prevState = brain.StateFromAgent(this);

            // 4) Elegir y ejecutar acción
            DecisionActionType action = decisionMaker.ChooseAction(this);
            bool success = false;

            switch (action)
            {
                case DecisionActionType.SeekFood:
                    success = TrySeekFood();
                    if (!success)
                        break;
                    yield return StartCoroutine(ChaseAndConsume());
                    break;
                case DecisionActionType.SeekMate:
                    success = TrySeekMate();
                    if (!success)
                        break;
                    yield return StartCoroutine(WaitAndReproduce());
                    break;
                case DecisionActionType.Rest:
                    success = TryRest();
                    if (!success)
                        break;
                    agent.isStopped = true;
                    yield return StartCoroutine(Rest(minRestTime, maxRestTime));
                    agent.isStopped = false;
                    break;
            }

            if (!success)
                Debug.Log($"{name}: acción {action} fallida.");
            else
                Debug.Log($"{name}: realizando acción {action}.");

            // 5) Aprendizaje RL
            if (algorithmType == AnimalManager.AlgorithmType.Reinforcement)
            {
                float reward = rlDecisionMaker.ComputeReward(this, action, success, idleTime);
                rlTrainer.ReportStep(this, prevState, action, reward);
            }
            else if (algorithmType == AnimalManager.AlgorithmType.Swarm)
            {
                int nextState = brain.StateFromAgent(this);
                float reward = siDecisionMaker.ComputeReward(this, action, success, Time.deltaTime);
                brain.Observe(prevState, action, reward, nextState);
                prevState = nextState;
            }

            yield return null;
        }
    }

    void MoveTo(Vector3 targetPosition)
    {
        NavMeshHit hit;
        const float maxSampleDistance = 100f;
        if (NavMesh.SamplePosition(targetPosition, out hit, maxSampleDistance, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
        else
        {
            // Si ni siquiera con un radio grande lo encontramos, 
            // simplemente nos quedamos donde estamos
            agent.isStopped = true;
        }
    }

    private bool TrySeekFood()
    {
        if (wolfStats.hunger >= wolfStats.maxHunger * 0.3f)
            return false;

        lastTargetedPrey = FindNearestPrey();
        if (lastTargetedPrey == null)
            return false;

        MoveTo(lastTargetedPrey.transform.position);
        return true;
    }

    private IEnumerator ChaseAndConsume()
    {
        GameObject prey = lastTargetedPrey;

        yield return StartCoroutine(ChasePrey(prey));
        yield return StartCoroutine(AttackPrey(prey));
        yield return StartCoroutine(ConsumePrey(prey));
    }
    
    private bool TrySeekMate()
    {
        const float reproductionMinEnergy = 50f;

        if (wolfStats.age < adultAge)
            return false;

        if (wolfStats.age - lastReproductionAge < reproductionCooldownYears)
        {
            Debug.Log($"{name}: en cooldown reproductivo ({wolfStats.age - lastReproductionAge:F2}/{reproductionCooldownYears} años).");
            return false;
        }

        if (wolfStats.energy < reproductionMinEnergy)
        {
            Debug.Log($"{name}: no tiene suficiente energía para intentar reproducirse (energia={wolfStats.energy:F1}).");
            return false;
        }

        lastTargetedMate = FindNearestMate();
        if (lastTargetedMate == null)
            return false;

        MoveTo(lastTargetedMate.transform.position);
        return true;
    }

    private IEnumerator WaitAndReproduce()
    {
        GameObject mate = lastTargetedMate;
        while (agent.pathPending || agent.remainingDistance > 0.5f)
            yield return null;

        if (Vector3.Distance(transform.position, mate.transform.position) <= 1f &&
            wolfStats.gender == WolfStats.Gender.Female &&
            !wolfStats.isPregnant)
        {
            yield return StartCoroutine(Reproduce(mate));
        }
    }

    private bool TryRest()
    {
        return true;
    }

    GameObject FindNearestPrey()
    {
        List<HerbivoreAI> herbivores = animalManager.GetHerbivores();
        GameObject nearestPrey = null;
        float minDistance = Mathf.Infinity;
        Vector3 currentPosition = transform.position;
        foreach (HerbivoreAI herb in herbivores)
        {
            if (herb == null || herb.gameObject == null)
                continue;
            float distance = Vector3.Distance(currentPosition, herb.transform.position);

            if (distance > wolfStats.detectionRange)
                continue;

            if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestPrey = herb.gameObject;
                }
        }

        wolfStats.energy -= 2f;
        return nearestPrey;
    }

    IEnumerator ChasePrey(GameObject prey)
    {
        float factor = wolfStats.age >= adultAge
            ? 1f
            : Mathf.Lerp(0.5f, 1f, wolfStats.age / adultAge);
        float normalSpeed = wolfStats.baseSpeed * factor;

        agent.speed = normalSpeed * runSpeedMultiplier;

        while (prey != null && agent != null && agent.isOnNavMesh && Vector3.Distance(transform.position, prey.transform.position) > 1.0f && wolfStats.energy > 10)
        {
            MoveTo(prey.transform.position);
            yield return null;
        }

        if (agent != null && wolfStats.energy <= 0f)
        {
            agent.speed = normalSpeed;
            yield break;
        }

        agent.speed = normalSpeed;
    }

    IEnumerator AttackPrey(GameObject prey)
    {
        while (prey != null && agent != null && agent.isOnNavMesh && Vector3.Distance(transform.position, prey.transform.position) <= 1f &&
               prey.GetComponent<HerbivoreAI>().deerStats.health > 0)
        {
            float damage = wolfStats.strength;
            HerbivoreAI preyAI = prey.GetComponent<HerbivoreAI>();
            if (preyAI == null) yield break;

            preyAI.deerStats.health -= damage;
            wolfStats.energy -= 5f;

            Debug.Log($"{gameObject.name} attacks {prey.name} for {damage} damage.");

            yield return new WaitForSeconds(1f);

            // Si la presa se aleja, salimos del bucle.
            if (preyAI != null)
            {
                if (Vector3.Distance(transform.position, prey.transform.position) > 1f)
                    break;
            }
        }
        yield break;
    }

    IEnumerator ConsumePrey(GameObject prey)
    {
        Debug.Log("consume prey");
        yield return new WaitForSeconds(1f);
        if (prey != null)
        {
            wolfStats.hunger += 40f;
            if (wolfStats.hunger > wolfStats.maxHunger)
                wolfStats.hunger = wolfStats.maxHunger;

            wolfStats.energy -= 5f;
            
            HerbivoreAI preyAI = prey.GetComponent<HerbivoreAI>();
            preyAI.haunted = true;
        }
    }

    GameObject FindNearestMate()
    {
        List<CarnivoreAI> allCarnivores = animalManager.GetCarnivores();
        GameObject nearestMate = null;
        float minDistance = Mathf.Infinity;
        Vector3 currentPosition = transform.position;
        foreach (CarnivoreAI carnivore in allCarnivores)
        {
            if (carnivore == null || carnivore.gameObject == null)
                continue;
            if (carnivore == this)
                continue;
            if (carnivore.wolfStats.gender == wolfStats.gender)
                continue;
            float distance = Vector3.Distance(currentPosition, carnivore.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestMate = carnivore.gameObject;
            }
        }

        wolfStats.energy -= 2f;
        return nearestMate;
    }

    IEnumerator Reproduce(GameObject mateObject)
    {
        // 0) Protecciones iniciales
        if (!isInitialized || mateObject == null) yield break;

        CarnivoreAI mateAI = mateObject.GetComponent<CarnivoreAI>();
        if (mateAI == null || !mateAI.isInitialized) yield break;

        // 1) Debe ser hembra y no estar ya embarazada
        if (wolfStats.gender != WolfStats.Gender.Female || wolfStats.isPregnant)
            yield break;

        if ((algorithmType == AnimalManager.AlgorithmType.Genetic || algorithmType == AnimalManager.AlgorithmType.Rand) && mateAI.genome == null)
            yield break;
        if (algorithmType == AnimalManager.AlgorithmType.Reinforcement && mateAI.rlDecisionMaker == null)
            yield break;

        const float reproductionMinEnergy = 50f;
        if (wolfStats.energy < reproductionMinEnergy)
        {
            Debug.Log($"Reproduction failed for {name}: not enough energy before mating (energy={wolfStats.energy:F1})");
            yield break;
        }

        if (wolfStats.age - lastReproductionAge < reproductionCooldownYears)
        {
            Debug.Log($"Reproduction aborted for {name}: still in cooldown ({wolfStats.age - lastReproductionAge:F2}/{reproductionCooldownYears} years).");
            yield break;
        }

        Debug.Log($"Reproduction started for {name}");
        yield return new WaitForSeconds(5f);
        wolfStats.energy -= 15f;
        mateAI.wolfStats.energy -= 15f;

        // 3) Probabilidad de éxito
        if (Random.value >= 0.9f)
        {
            Debug.Log($"Reproduction failed for {name}");
            yield break;
        }

        wolfStats.isPregnant = true;
        Debug.Log($"Female {name} is now pregnant.");

        yield return new WaitForSeconds(15f);
        wolfStats.energy -= 35f;

        // 4) Crear camada
        AnimalManager mgr = Object.FindAnyObjectByType<AnimalManager>();
        if (mgr == null) { Debug.LogError("AnimalManager missing"); yield break; }

        float rawLitter = mgr.NormalRandom(2f, 1f);
        int litterSize = Mathf.Clamp(Mathf.RoundToInt(rawLitter), 1, 4);
        Debug.Log($"Litter size for {name}: {litterSize}");

        for (int i = 0; i < litterSize; i++)
        {
            // 4a) Stats y posición
            WolfStats childStats = WolfStats.CreateOffspring(wolfStats, mateAI.wolfStats);

            Vector3 spawnPos = transform.position + new Vector3(1f, 0f, 1f);
            NavMeshHit hit;

            if (NavMesh.SamplePosition(spawnPos, out hit, 5f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
                spawnPos.y += 0.1f;
            }

            GameObject child = Instantiate(mgr.carnivorePrefab, spawnPos, Quaternion.identity);

            child.name = $"Wolf_{mgr.totalWolfCounter + 1:D3}";
            mgr.totalWolfCounter++;
            mgr.activeWolfCounter++;

            mgr.DisableUnwantedScripts(child);

            // 4b) NavMeshAgent
            NavMeshAgent navAgent = child.GetComponent<NavMeshAgent>();
            if (navAgent == null)
            {
                navAgent = child.AddComponent<NavMeshAgent>();
            }
            navAgent.enabled = true;

            if (!navAgent.isOnNavMesh)
            {
                Vector3 posToFix = child.transform.position;
                if (NavMesh.SamplePosition(posToFix, out hit, 5f, NavMesh.AllAreas))
                {
                    child.transform.position = hit.position;
                }
                else
                {
                    Debug.LogWarning("No se pudo reposicionar la cría sobre la NavMesh.");
                }
            }

            // Ajustar el tamaño de la cria a la mitad (recién nacido)
            child.transform.localScale *= 0.5f;
            navAgent.baseOffset = -0.05f;

            // 4c) Añadir y configurar CarnivoreAI
            CarnivoreAI childAI = child.GetComponent<CarnivoreAI>();
            if (childAI == null)
            {
                childAI = child.AddComponent<CarnivoreAI>();
            }
            childAI.wolfStats = childStats;

            // 4d) Herencia de algoritmo
            if (algorithmType == AnimalManager.AlgorithmType.Genetic)
            {
                childAI.genome = ActionGeneticTrainer.Instance.Crossover(genome, mateAI.genome);
            }
            
            if (algorithmType == AnimalManager.AlgorithmType.Reinforcement)
            {
                if (mateAI.rlDecisionMaker == null || rlDecisionMaker == null)
                    yield break;

                var compChild = child.GetComponent<RLDecisionMakerComponent>()
                                ?? child.AddComponent<RLDecisionMakerComponent>();

                if (compChild.DecisionMaker == null)
                    compChild.DecisionMaker = new RLDecisionMaker();

                compChild.DecisionMaker.AverageFrom(rlDecisionMaker, mateAI.rlDecisionMaker);
            }

            // 4e) Iniciar al cachorro
            childAI.StartCarnivore();
            Debug.Log($"child born from {name}");
        }
        
        lastReproductionAge = wolfStats.age;
        wolfStats.isPregnant = false;
    }

    IEnumerator Rest(float minRestTime, float maxRestTime)
    {
        float restDuration = Random.Range(minRestTime, maxRestTime);
        float elapsedTime = 0f;
        while (elapsedTime < restDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        wolfStats.energy += restDuration * 2f;
        if (wolfStats.energy > wolfStats.maxEnergy)
            wolfStats.energy = wolfStats.maxEnergy;

        wolfStats.health += restDuration;
        if (wolfStats.health > wolfStats.maxHealth)
            wolfStats.health = wolfStats.maxHealth;

        if (agent.speed < wolfStats.baseSpeed)
            agent.speed = wolfStats.baseSpeed;

        if (wolfStats.strength < wolfStats.baseStrength)
            wolfStats.strength = wolfStats.baseStrength;
    }
    
    public bool IsPreyNearby()
    {
        List<HerbivoreAI> preys = animalManager.GetHerbivores();
        Vector3 currentPosition = transform.position;
        foreach (HerbivoreAI prey in preys)
        {
            if (prey == null || prey.gameObject == null)
                continue;
            if (Vector3.Distance(currentPosition, prey.transform.position) <= wolfStats.detectionRange)
            {
                return true;
            }
        }
        return false;
    }
}
