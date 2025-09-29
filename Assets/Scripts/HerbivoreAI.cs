using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EcosystemAI;

public class HerbivoreAI : MonoBehaviour
{
    private Animator animator;
    private GridManager gridManager;
    private NavMeshAgent agent;
    private AnimalManager animalManager;

    private AnimalManager.AlgorithmType algorithmType;

    public enum Species { Deer, Horse }
    [Header("Species selection")]
    public Species species;

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

    public DeerStats deerStats;
    public HorseStats horseStats;

    public float growthRate = 0.02f;
    public float adultAge;
    public float hungerDecayRate = 1f;
    public float energyConsumption = 0.75f;
    public float healthDecayRate = 6f;
    public float energyDecayRate = 4f;
    public float runSpeedMultiplier = 3f;
    public float fleeDistance = 20f;
    private bool isFleeing = false;

    private GameObject lastTargetedGrass;
    private GameObject lastTargetedMate;

    private bool isInitialized = false;
    public bool haunted = false;

    [Header("Reproduction settings")]
    [SerializeField] private float reproductionCooldownYears = 1f; // años
    private float lastReproductionAge = -999f;

    void Awake()
    {
        animalManager = Object.FindAnyObjectByType<AnimalManager>();
        if (animalManager == null)
        {
            Debug.LogError("AnimalManager not found in the scene.");
        }
    }

    // Ahora se reciben minIdle y maxIdle y se inician los atributos.
    public void StartHerbivore()
    {
        // 0) Leer algoritmo
        algorithmType = animalManager.herbivoreAlgorithm;

        // 1) Inicializar stats según la especie
        switch (species)
        {
            case Species.Deer:
                if (deerStats == null)
                {
                    deerStats = new DeerStats(10f, 20f, 0.9f, 1.1f, 1f, 1.5f, 90f, 120f, 90f, 120f, 90f, 120f, 40f, 60f, 8f, 13f);
                    if (name == "Deer_001")
                        deerStats.gender = DeerStats.Gender.Male;
                    else if (name == "Deer_002")
                        deerStats.gender = DeerStats.Gender.Female;
                    else
                        deerStats.gender = (Random.value < 0.5f) ? DeerStats.Gender.Male : DeerStats.Gender.Female;
                }
                deerStats.health = deerStats.maxHealth * 0.6f;
                deerStats.energy = deerStats.maxEnergy * 0.8f;
                deerStats.hunger = deerStats.maxHunger * 0.3f;
                adultAge = 3f;
                break;

            case Species.Horse:
                if (horseStats == null)
                {
                    horseStats = new HorseStats(15f, 30f, 0.9f, 1.1f, 1.1f, 1.6f, 80f, 110f, 80f, 110f, 80f, 110f, 50f, 75f, 7f, 12f);
                    if (name == "Horse_001")
                        horseStats.gender = HorseStats.Gender.Male;
                    else if (name == "Horse_002")
                        horseStats.gender = HorseStats.Gender.Female;
                    else
                        horseStats.gender = (Random.value < 0.5f) ? HorseStats.Gender.Male : HorseStats.Gender.Female;
                }
                horseStats.health = horseStats.maxHealth * 0.6f;
                horseStats.energy = horseStats.maxEnergy * 0.8f;
                horseStats.hunger = horseStats.maxHunger * 0.4f;
                adultAge = 4f;
                break;
        }

        if (algorithmType == AnimalManager.AlgorithmType.Genetic || algorithmType == AnimalManager.AlgorithmType.Rand)
        {
            // Genético: obtener genoma y rangos
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
            var comp = GetComponent<RLDecisionMakerComponent>()
                    ?? gameObject.AddComponent<RLDecisionMakerComponent>();

            rlDecisionMaker = comp.DecisionMaker;
            decisionMaker = rlDecisionMaker;

            rlTrainer = FindObjectOfType<RLTrainer>();
            if (rlTrainer == null)
            {
                rlTrainer = new GameObject("RLTrainer").AddComponent<RLTrainer>();
            }

            prevState = rlDecisionMaker.StateFromAgent(this);
        }
        else if (algorithmType == AnimalManager.AlgorithmType.Swarm)
        {
            brain = animalManager.herbBrain;
            prevState = brain.StateFromAgent(this);
            siDecisionMaker = brain;
            decisionMaker = siDecisionMaker;
        }

        // 2) Inicializar NavMesh, animator, gridManager, etc.
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
        StartCoroutine(IdleRoutine());
    }

    void Update()
    {
        if (!isInitialized) 
            return;
        
        UpdateAnimation();
        UpdateGrowth();
        UpdateStats();

        if (haunted)
        {
            if (species == Species.Deer)
                animalManager.activeDeerCounter--;
            if (species == Species.Horse)
                animalManager.activeHorseCounter--;

            Destroy(gameObject);
        }

        if (IsPredatorNearby() && !isFleeing)
            {
                isFleeing = true;
                StartCoroutine(FleeFromPredator());
            }
    }

    void UpdateAnimation()
    {
        if (!isInitialized || animator == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        float speed = agent.velocity.magnitude;
        animator.SetFloat("Vert", speed);

        switch (species)
        {
            case Species.Deer: animator.SetFloat("State", speed > deerStats.baseSpeed * 2 ? 1f : 0f); break;
            case Species.Horse: animator.SetFloat("State", speed > horseStats.baseSpeed * 2 ? 1f : 0f); break;
        }
        
    }

    void UpdateGrowth()
    {
        switch (species)
        {
            case Species.Deer:
                deerStats.age = Mathf.MoveTowards(deerStats.age, deerStats.maxAge, growthRate * Time.deltaTime);

                if (deerStats.age <= adultAge)
                {
                    float factor = Mathf.Lerp(0.5f, 1f, deerStats.age / adultAge);

                    transform.localScale = Vector3.one * factor;

                    agent.speed = deerStats.baseSpeed * factor;

                    bool wasAtMaxHealth = deerStats.health == deerStats.maxHealth;
                    bool wasAtMaxEnergy = deerStats.energy == deerStats.maxEnergy;
                    bool wasAtMaxHunger = deerStats.hunger == deerStats.maxHunger;

                    deerStats.maxHealth = deerStats.baseMaxHealth * factor;
                    deerStats.maxEnergy = deerStats.baseMaxEnergy * factor;
                    deerStats.maxHunger = deerStats.baseMaxHunger * factor;

                    if (wasAtMaxHealth) deerStats.health = deerStats.maxHealth;
                    if (wasAtMaxEnergy) deerStats.energy = deerStats.maxEnergy;
                    if (wasAtMaxHunger) deerStats.hunger = deerStats.maxHunger;
                }

                if (deerStats.age >= deerStats.maxAge)
                {
                    Debug.Log(gameObject.name + " died of old age.");
                    Destroy(gameObject);
                    animalManager.activeDeerCounter--;
                }
                break;

            case Species.Horse: 
                horseStats.age = Mathf.MoveTowards(horseStats.age, horseStats.maxAge, growthRate * Time.deltaTime);

                if (horseStats.age <= adultAge)
                {
                    float factor = Mathf.Lerp(0.5f, 1f, horseStats.age / adultAge);

                    transform.localScale = Vector3.one * factor;

                    agent.speed = horseStats.baseSpeed * factor;

                    bool wasAtMaxHealth = horseStats.health == horseStats.maxHealth;
                    bool wasAtMaxEnergy = horseStats.energy == horseStats.maxEnergy;
                    bool wasAtMaxHunger = horseStats.hunger == horseStats.maxHunger;

                    horseStats.maxHealth = horseStats.baseMaxHealth * factor;
                    horseStats.maxEnergy = horseStats.baseMaxEnergy * factor;
                    horseStats.maxHunger = horseStats.baseMaxHunger * factor;

                    if (wasAtMaxHealth) horseStats.health = horseStats.maxHealth;
                    if (wasAtMaxEnergy) horseStats.energy = horseStats.maxEnergy;
                    if (wasAtMaxHunger) horseStats.hunger = horseStats.maxHunger;
                }

                if (horseStats.age >= horseStats.maxAge)
                {
                    Debug.Log(gameObject.name + " died of old age.");
                    Destroy(gameObject);
                    animalManager.activeHorseCounter--;
                }
                break;
        }
    }

    void UpdateStats()
    {
        float currentSpeed;

        switch (species)
        {
            case Species.Deer:
                if (deerStats.health <= 0)
                {
                    Debug.Log(gameObject.name + " died.");
                    Destroy(gameObject);
                    animalManager.activeDeerCounter--;
                }
                else if (deerStats.health < 30)
                {
                    agent.speed = deerStats.baseSpeed * 0.5f;
                    deerStats.strength = deerStats.baseStrength * 0.5f;
                    deerStats.energy = deerStats.baseMaxEnergy * 0.5f;
                    deerStats.hunger = deerStats.baseMaxHunger * 0.75f;
                }

                // Actualización del hambre
                deerStats.hunger = Mathf.MoveTowards(deerStats.hunger, 0, hungerDecayRate * Time.deltaTime);
                if (deerStats.hunger <= 0f)
                {
                    deerStats.health = Mathf.MoveTowards(deerStats.health, 0, healthDecayRate * Time.deltaTime);
                    deerStats.energy = Mathf.MoveTowards(deerStats.energy, 0, energyDecayRate * Time.deltaTime);
                    deerStats.hunger = 0;
                }

                // Actualización de la energía
                currentSpeed = agent.velocity.magnitude;
                if (currentSpeed > 0.1f)
                {
                    deerStats.energy = Mathf.MoveTowards(deerStats.energy, 0, currentSpeed * energyConsumption * Time.deltaTime);
                }

                if (deerStats.energy <= 0f)
                {
                    agent.speed = deerStats.baseSpeed * 0.5f;
                    deerStats.strength = deerStats.baseStrength * 0.5f;
                    deerStats.energy = 0;
                }
                break;

            case Species.Horse:
                if (horseStats.health <= 0)
                {
                    Debug.Log(gameObject.name + " died.");
                    Destroy(gameObject);
                    animalManager.activeHorseCounter--;
                }
                else if (horseStats.health < 30)
                {
                    agent.speed = horseStats.baseSpeed * 0.5f;
                    horseStats.strength = horseStats.baseStrength * 0.5f;
                    horseStats.energy = horseStats.baseMaxEnergy * 0.5f;
                    horseStats.hunger = horseStats.baseMaxHunger * 0.75f;
                }

                // Actualización del hambre
                horseStats.hunger = Mathf.MoveTowards(horseStats.hunger, 0, hungerDecayRate * Time.deltaTime);
                if (horseStats.hunger <= 0f)
                {
                    horseStats.health = Mathf.MoveTowards(horseStats.health, 0, healthDecayRate * Time.deltaTime);
                    horseStats.energy = Mathf.MoveTowards(horseStats.energy, 0, energyDecayRate * Time.deltaTime);
                    horseStats.hunger = 0;
                }

                // Actualización de la energía
                currentSpeed = agent.velocity.magnitude;
                if (currentSpeed > 0.1f)
                {
                    horseStats.energy = Mathf.MoveTowards(horseStats.energy, 0, currentSpeed * energyConsumption * Time.deltaTime);
                }

                if (horseStats.energy <= 0f)
                {
                    agent.speed = horseStats.baseSpeed * 0.5f;
                    horseStats.strength = horseStats.baseStrength * 0.5f;
                    horseStats.energy = 0;
                }
                break;
        }
    }

    public bool IsPredatorNearby()
    {
        List<CarnivoreAI> predators = animalManager.GetCarnivores();
        Vector3 currentPosition = transform.position;
        foreach (CarnivoreAI predator in predators)
        {
            if (predator == null || predator.gameObject == null)
                continue;

            switch (species)
            {
                case Species.Deer:
                    if (Vector3.Distance(currentPosition, predator.transform.position) <= deerStats.detectionRange)
                    {
                        return true;
                    }
                    break;

                case Species.Horse:
                    if (Vector3.Distance(currentPosition, predator.transform.position) <= horseStats.detectionRange)
                    {
                        return true;
                    }
                    break;
            }

            
        }
        return false;
    }

    IEnumerator IdleRoutine()
    {
        while (true)
        {
            // 1) Mantenemos al agente detenido mientras “idle”
            NavMeshHit hit;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            else if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else if (!agent.isOnNavMesh)
            {
                Debug.LogError($"{name} could not be placed on NavMesh at {transform.position}");
            }

            // 2) Esperamos un tiempo Idle aleatorio entre minIdleTime..maxIdleTime
            float idleTime = Random.Range(minIdleTime, maxIdleTime);
            yield return new WaitForSeconds(idleTime);
            agent.isStopped = false;

            if (algorithmType == AnimalManager.AlgorithmType.Reinforcement)
                prevState = rlDecisionMaker.StateFromAgent(this);

            if (algorithmType == AnimalManager.AlgorithmType.Swarm)
                prevState = brain.StateFromAgent(this);

            // 3) Pedimos al DecisionMaker que elija la acción ahora:
            DecisionActionType action = decisionMaker.ChooseAction(this);
            bool success = false;

            switch (action)
            {
                case DecisionActionType.SeekFood:
                    success = TrySeekFood();
                    if (!success)
                        break;
                    yield return StartCoroutine(WaitAndConsume());
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

            if (algorithmType == AnimalManager.AlgorithmType.Reinforcement)
            {
                float reward = rlDecisionMaker.ComputeReward(this, action, success, Time.deltaTime);
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

    private bool TrySeekFood()
    {
        switch (species)
        {
            case Species.Deer:
                if (deerStats.hunger >= deerStats.maxHunger * 0.8)
                    return false;
                break;

            case Species.Horse:
                if (horseStats.hunger >= horseStats.maxHunger * 0.8)
                    return false;
                break;
        }

        lastTargetedGrass = FindNearestGrass();
        if (lastTargetedGrass == null)
            return false;

        MoveTo(lastTargetedGrass.transform.position);
        return true;
    }

    private IEnumerator WaitAndConsume()
    {
        GameObject grass = lastTargetedGrass;
        while (agent.pathPending || agent.remainingDistance > 0.5f)
            yield return null;

        if (grass != null)
            yield return StartCoroutine(ConsumeGrass(grass));
    }

    private bool TrySeekMate()
    {
        const float reproductionMinEnergy = 45f;

        switch (species)
        {
            case Species.Deer:
                if (deerStats.age < adultAge)
                {
                    Debug.Log($"{name}: aún no es adulto para reproducirse.");
                    return false;
                }
                if (deerStats.age - lastReproductionAge < reproductionCooldownYears)
                {
                    Debug.Log($"{name}: en cooldown reproductivo.");
                    return false;
                }
                if (deerStats.energy < reproductionMinEnergy)
                {
                    Debug.Log($"{name}: no tiene suficiente energía para intentar reproducirse (energia={deerStats.energy:F1}).");
                    return false;
                }
                break;

            case Species.Horse:
                if (horseStats.age < adultAge)
                {
                    Debug.Log($"{name}: aún no es adulto para reproducirse.");
                    return false;
                }
                if (horseStats.age - lastReproductionAge < reproductionCooldownYears)
                {
                    Debug.Log($"{name}: en cooldown reproductivo.");
                    return false;
                }
                if (horseStats.energy < reproductionMinEnergy)
                {
                    Debug.Log($"{name}: no tiene suficiente energía para intentar reproducirse (energia={horseStats.energy:F1}).");
                    return false;
                }
                break;
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

        switch (species)
        {
            case Species.Deer:
                if (Vector3.Distance(transform.position, mate.transform.position) <= 1f &&
                    deerStats.gender == DeerStats.Gender.Female &&
                    !deerStats.isPregnant)
                {
                    yield return StartCoroutine(Reproduce(mate));
                }
                break;

            case Species.Horse:
                if (Vector3.Distance(transform.position, mate.transform.position) <= 1f &&
                    horseStats.gender == HorseStats.Gender.Female &&
                    !horseStats.isPregnant)
                {
                    yield return StartCoroutine(Reproduce(mate));
                }
                break;
        }   
    }

    private bool TryRest()
    {
        return true;
    }

    void MoveTo(Vector3 targetPosition)
    {
        if (agent == null || !agent.enabled)
            return;

        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"{name}: no pude colocar el agente sobre la NavMesh.");
                return;
            }
        }

        agent.isStopped = false;
        agent.SetDestination(targetPosition);
    }

    GameObject FindNearestGrass()
    {
        List<GameObject> grassList = gridManager.GetAllGrass();
        GameObject nearest = null;
        float minDistance = Mathf.Infinity;
        Vector3 currentPosition = transform.position;

        foreach (GameObject grass in grassList)
        {
            if (grass == null) // Verificamos que el objeto no sea nulo
                continue;

            float distance = Vector3.Distance(currentPosition, grass.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = grass;
            }
        }

        switch (species)
        {
            case Species.Deer: deerStats.energy -= 2f; break;
            case Species.Horse: horseStats.energy -= 2f; break;
        }

        return nearest;
    }

    private IEnumerator ConsumeGrass(GameObject grass)
    {
        // Espera 2 segundos antes de consumir la hierba
        yield return new WaitForSeconds(5f);

        if (grass != null)
        {
            gridManager.grassRespawnPositions.Add(grass.transform.position);
            Destroy(grass);
            gridManager.activeGrassCounter--;
        }

        switch (species)
        {
            case Species.Deer:
                deerStats.hunger += 10;
                if (deerStats.hunger > deerStats.maxHunger)
                {
                    deerStats.hunger = deerStats.maxHunger;
                }
                deerStats.energy -= 3f;
                break;

            case Species.Horse:
                horseStats.hunger += 10;
                if (horseStats.hunger > horseStats.maxHunger)
                {
                    horseStats.hunger = horseStats.maxHunger;
                }
                horseStats.energy -= 3f;
                break;
        }   
    }

    GameObject FindNearestMate()
    {
        List<HerbivoreAI> allHerbivores = animalManager.GetHerbivores();
        GameObject nearestMate = null;
        float minDistance = Mathf.Infinity;
        Vector3 currentPosition = transform.position;

        foreach (HerbivoreAI herbivore in allHerbivores)
        {
            if (herbivore == null || herbivore.gameObject == null)
                continue;
            if (herbivore == this)
                continue;
            if (herbivore.species != species)
                continue;

            switch (species)
                {
                    case Species.Deer: if (herbivore.deerStats.gender == deerStats.gender) continue; break;
                    case Species.Horse: if (herbivore.horseStats.gender == horseStats.gender) continue; break;
                }

            float distance = Vector3.Distance(currentPosition, herbivore.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestMate = herbivore.gameObject;
            }
        }

        switch (species)
        {
            case Species.Deer: deerStats.energy -= 2f; break;
            case Species.Horse: horseStats.energy -= 2f; break;
        }

        return nearestMate;
    }

    IEnumerator Rest(float minRestTime, float maxRestTime)
    {
        if (species == Species.Deer)
        {
            float restDuration = Random.Range(minRestTime, maxRestTime);
            float elapsedTime = 0f;
            while (elapsedTime < restDuration)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            deerStats.energy += restDuration * 2f;
            if (deerStats.energy > deerStats.maxEnergy)
                deerStats.energy = deerStats.maxEnergy;

            deerStats.health += restDuration;
            if (deerStats.health > deerStats.maxHealth)
                deerStats.health = deerStats.maxHealth;

            if (agent.speed < deerStats.baseSpeed)
                agent.speed = deerStats.baseSpeed;

            if (deerStats.strength < deerStats.baseStrength)
                deerStats.strength = deerStats.baseStrength;

        }
        else if (species == Species.Horse)
        {
            float restDuration = Random.Range(minRestTime, maxRestTime);
            float elapsedTime = 0f;
            while (elapsedTime < restDuration)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            horseStats.energy += restDuration * 2f;
            if (horseStats.energy > horseStats.maxEnergy)
                horseStats.energy = horseStats.maxEnergy;

            horseStats.health += restDuration;
            if (horseStats.health > horseStats.maxHealth)
                horseStats.health = horseStats.maxHealth;

            if (agent.speed < horseStats.baseSpeed)
                agent.speed = horseStats.baseSpeed;

            if (horseStats.strength < horseStats.baseStrength)
                horseStats.strength = horseStats.baseStrength;
        }

        
    }

    IEnumerator Reproduce(GameObject mateObject)
    {
        if (species == Species.Deer)
        {
            if (!isInitialized || mateObject == null)
                yield break;

            // Solo procede si este herbívoro es hembra y no está embarazada
            if (deerStats.gender == DeerStats.Gender.Female && !deerStats.isPregnant)
            {
                Debug.Log("Reproduction process started for " + gameObject.name);
                HerbivoreAI mateAI = mateObject.GetComponent<HerbivoreAI>();
                if (mateAI == null || mateAI.deerStats == null || !mateAI.isInitialized)
                    yield break;

                if ((algorithmType == AnimalManager.AlgorithmType.Genetic || algorithmType == AnimalManager.AlgorithmType.Rand) && mateAI.genome == null)
                    yield break;
                if (algorithmType == AnimalManager.AlgorithmType.Reinforcement && mateAI.rlDecisionMaker == null)
                    yield break;

                const float reproductionMinEnergy = 45f;
                if (deerStats.energy < reproductionMinEnergy)
                {
                    Debug.Log($"Reproduction failed for {name}: not enough energy before mating (energy={deerStats.energy:F1})");
                    yield break;
                }

                if (deerStats.age - lastReproductionAge < reproductionCooldownYears)
                {
                    Debug.Log($"Reproduction aborted for {name}: still in cooldown ({deerStats.age - lastReproductionAge:F2}/{reproductionCooldownYears} years).");
                    yield break;
                }

                // Esperar 3 segundos para simular el apareamiento
                yield return new WaitForSeconds(5f);
                deerStats.energy -= 10f;
                mateAI.deerStats.energy -= 10f;

                if (Random.value >= 0.85f)
                {
                    Debug.Log($"Reproduction failed for {name}");
                    yield break;
                }

                deerStats.isPregnant = true;
                Debug.Log("Female " + gameObject.name + " is now pregnant.");

                yield return new WaitForSeconds(30f);
                deerStats.energy -= 35f;

                AnimalManager animalManager = Object.FindAnyObjectByType<AnimalManager>();
                if (animalManager != null)
                {
                    if (mateAI != null)
                    {
                        // Crear descendencia con la media de los valores de los padres; la cría nace con atributos a la mitad
                        DeerStats offspringStats = DeerStats.CreateOffspring(deerStats, mateAI.deerStats);

                        // Instanciar la cría cerca de la madre
                        Vector3 spawnPos = transform.position + new Vector3(1f, 0, 1f);
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(spawnPos, out hit, 5f, NavMesh.AllAreas))
                        {
                            spawnPos = hit.position;
                            spawnPos.y += 0.1f;
                        }

                        GameObject offspring = Instantiate(animalManager.deerPrefab, spawnPos, Quaternion.identity);

                        animalManager.activeDeerCounter++;
                        animalManager.totalDeerCounter++;
                        offspring.name = "Deer_" + animalManager.totalDeerCounter.ToString("D3");

                        animalManager.DisableUnwantedScripts(offspring);

                        // Aseguramos que el offspring tenga un NavMeshAgent
                        NavMeshAgent navAgent = offspring.GetComponent<NavMeshAgent>();
                        if (navAgent == null)
                        {
                            navAgent = offspring.AddComponent<NavMeshAgent>();
                        }
                        navAgent.enabled = true;

                        // Forzar reposicionar el offspring en la NavMesh si es necesario:
                        if (!navAgent.isOnNavMesh)
                        {
                            Vector3 posToFix = offspring.transform.position;
                            if (NavMesh.SamplePosition(posToFix, out hit, 5f, NavMesh.AllAreas))
                            {
                                offspring.transform.position = hit.position;
                            }
                            else
                            {
                                Debug.LogWarning("No se pudo reposicionar la cría sobre la NavMesh.");
                            }
                        }

                        // Ajustar el tamaño del offspring a la mitad (recién nacido)
                        offspring.transform.localScale *= 0.5f;
                        navAgent.baseOffset = -0.05f;

                        // Agregar HerbivoreAI al offspring y asignar sus estadísticas
                        HerbivoreAI offspringAI = offspring.GetComponent<HerbivoreAI>();
                        if (offspringAI == null)
                        {
                            offspringAI = offspring.AddComponent<HerbivoreAI>();
                        }
                        offspringAI.deerStats = offspringStats;

                        if (algorithmType == AnimalManager.AlgorithmType.Genetic)
                        {
                            if (mateAI.genome == null || genome == null)
                                yield break;

                            var offspringGenome = ActionGeneticTrainer.Instance.Crossover(genome, mateAI.genome);
                            offspringAI.genome = offspringGenome;
                        }

                        if (algorithmType == AnimalManager.AlgorithmType.Reinforcement)
                        {
                            if (mateAI.rlDecisionMaker == null || rlDecisionMaker == null)
                                yield break;

                            var compChild = offspringAI.GetComponent<RLDecisionMakerComponent>()
                                            ?? offspringAI.gameObject.AddComponent<RLDecisionMakerComponent>();

                            if (compChild.DecisionMaker == null)
                                compChild.DecisionMaker = new RLDecisionMaker();

                            compChild.DecisionMaker.AverageFrom(rlDecisionMaker, mateAI.rlDecisionMaker);
                        }

                        offspringAI.species = Species.Deer;
                        offspringAI.StartHerbivore();
                        Debug.Log("Offspring born from " + gameObject.name);
                    }
                    else
                    {
                        Debug.LogError("Mate does not have HerbivoreAI.");
                    }
                }
                else
                {
                    Debug.LogError("AnimalManager not found. Offspring cannot be spawned.");
                }
                lastReproductionAge = deerStats.age;
                // Resetear el estado de embarazo para permitir futuras reproducciones
                deerStats.isPregnant = false;
            }
            else
            {
                Debug.Log("Reproduction process not applicable for " + gameObject.name);
            }
            yield break;

        }
        else if (species == Species.Horse)
        {
            if (!isInitialized || mateObject == null)
                yield break;

            // Solo procede si este herbívoro es hembra y no está embarazada
            if (horseStats.gender == HorseStats.Gender.Female && !horseStats.isPregnant)
            {
                Debug.Log("Reproduction process started for " + gameObject.name);
                HerbivoreAI mateAI = mateObject.GetComponent<HerbivoreAI>();
                if (mateAI == null || mateAI.horseStats == null || !mateAI.isInitialized)
                    yield break;

                if ((algorithmType == AnimalManager.AlgorithmType.Genetic || algorithmType == AnimalManager.AlgorithmType.Rand) && mateAI.genome == null)
                    yield break;
                if (algorithmType == AnimalManager.AlgorithmType.Reinforcement && mateAI.rlDecisionMaker == null)
                    yield break;

                const float reproductionMinEnergy = 45f;
                if (horseStats.energy < reproductionMinEnergy)
                {
                    Debug.Log($"Reproduction failed for {name}: not enough energy before mating (energy={horseStats.energy:F1})");
                    yield break;
                }

                if (horseStats.age - lastReproductionAge < reproductionCooldownYears)
                {
                    Debug.Log($"Reproduction aborted for {name}: still in cooldown ({horseStats.age - lastReproductionAge:F2}/{reproductionCooldownYears} years).");
                    yield break;
                }

                // Esperar 3 segundos para simular el apareamiento
                yield return new WaitForSeconds(3f);
                horseStats.energy -= 10f;
                mateAI.horseStats.energy -= 10f;

                if (Random.value >= 0.9f)
                {
                    Debug.Log($"Reproduction failed for {name}");
                    yield break;
                }

                horseStats.isPregnant = true;
                Debug.Log("Female " + gameObject.name + " is now pregnant.");

                yield return new WaitForSeconds(40f);
                horseStats.energy -= 35f;

                AnimalManager animalManager = Object.FindAnyObjectByType<AnimalManager>();
                if (animalManager != null)
                {
                    if (mateAI != null)
                    {
                        // Crear descendencia con la media de los valores de los padres; la cría nace con atributos a la mitad
                        HorseStats offspringStats = HorseStats.CreateOffspring(horseStats, mateAI.horseStats);

                        // Instanciar la cría cerca de la madre
                        Vector3 spawnPos = transform.position + new Vector3(1f, 0, 1f);
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(spawnPos, out hit, 5f, NavMesh.AllAreas))
                        {
                            spawnPos = hit.position;
                            spawnPos.y += 0.1f;
                        }

                        GameObject offspring = Instantiate(animalManager.horsePrefab, spawnPos, Quaternion.identity);

                        animalManager.activeHorseCounter++;
                        animalManager.totalHorseCounter++;
                        offspring.name = "Horse_" + animalManager.totalHorseCounter.ToString("D3");

                        animalManager.DisableUnwantedScripts(offspring);

                        // Aseguramos que el offspring tenga un NavMeshAgent
                        NavMeshAgent navAgent = offspring.GetComponent<NavMeshAgent>();
                        if (navAgent == null)
                        {
                            navAgent = offspring.AddComponent<NavMeshAgent>();
                        }
                        navAgent.enabled = true;

                        // Forzar reposicionar el offspring en la NavMesh si es necesario:
                        if (!navAgent.isOnNavMesh)
                        {
                            Vector3 posToFix = offspring.transform.position;
                            if (NavMesh.SamplePosition(posToFix, out hit, 5f, NavMesh.AllAreas))
                            {
                                offspring.transform.position = hit.position;
                            }
                            else
                            {
                                Debug.LogWarning("No se pudo reposicionar la cría sobre la NavMesh.");
                            }
                        }

                        // Ajustar el tamaño del offspring a la mitad (recién nacido)
                        offspring.transform.localScale *= 0.5f;
                        navAgent.baseOffset = -0.05f;

                        // Agregar HerbivoreAI al offspring y asignar sus estadísticas
                        HerbivoreAI offspringAI = offspring.GetComponent<HerbivoreAI>();
                        if (offspringAI == null)
                        {
                            offspringAI = offspring.AddComponent<HerbivoreAI>();
                        }
                        offspringAI.horseStats = offspringStats;

                        if (algorithmType == AnimalManager.AlgorithmType.Genetic)
                        {
                            if (mateAI.genome == null || genome == null)
                                yield break;

                            var offspringGenome = ActionGeneticTrainer.Instance.Crossover(genome, mateAI.genome);
                            offspringAI.genome = offspringGenome;
                        }

                        if (algorithmType == AnimalManager.AlgorithmType.Reinforcement)
                        {
                            if (mateAI.rlDecisionMaker == null || rlDecisionMaker == null)
                                yield break;

                            var compChild = offspringAI.GetComponent<RLDecisionMakerComponent>()
                                            ?? offspringAI.gameObject.AddComponent<RLDecisionMakerComponent>();

                            if (compChild.DecisionMaker == null)
                                compChild.DecisionMaker = new RLDecisionMaker();

                            compChild.DecisionMaker.AverageFrom(rlDecisionMaker, mateAI.rlDecisionMaker);
                        }

                        offspringAI.species = Species.Horse;
                        offspringAI.StartHerbivore();
                        Debug.Log("Offspring born from " + gameObject.name);
                    }
                    else
                    {
                        Debug.LogError("Mate does not have HerbivoreAI.");
                    }
                }
                else
                {
                    Debug.LogError("AnimalManager not found. Offspring cannot be spawned.");
                }
                lastReproductionAge = horseStats.age;
                // Resetear el estado de embarazo para permitir futuras reproducciones
                horseStats.isPregnant = false;
            }
            else
            {
                Debug.Log("Reproduction process not applicable for " + gameObject.name);
            }
            yield break;
        }
    }

    IEnumerator FleeFromPredator()
    {
        List<CarnivoreAI> predators = animalManager.GetCarnivores();
        Vector3 fleeDirection = Vector3.zero;
        int predatorCount = 0;

        switch (species)
        {
            case Species.Deer: agent.speed = deerStats.baseSpeed * runSpeedMultiplier; break;
            case Species.Horse: agent.speed = horseStats.baseSpeed * runSpeedMultiplier; break;
        }

        foreach (CarnivoreAI predator in predators)
        {
            if (predator == null || predator.gameObject == null)
                continue;

            float distance = Vector3.Distance(transform.position, predator.transform.position);

            switch (species)
            {
                case Species.Deer:
                    if (distance <= deerStats.detectionRange)
                    {
                        fleeDirection += (transform.position - predator.transform.position).normalized;
                        predatorCount++;
                    }
                    break;

                case Species.Horse:
                    if (distance <= horseStats.detectionRange)
                    {
                        fleeDirection += (transform.position - predator.transform.position).normalized;
                        predatorCount++;
                    }
                    break;
            }
        }

        if (predatorCount > 0)
        {
            fleeDirection /= predatorCount;
            fleeDirection.Normalize();

            Vector3 destination = transform.position + fleeDirection * fleeDistance;

            MoveTo(destination);

            while (agent != null && agent.enabled && agent.isOnNavMesh && (agent.pathPending || agent.remainingDistance > 0.5f))
            {
                yield return null;
            }
        }

        switch (species)
        {
            case Species.Deer: agent.speed = deerStats.baseSpeed; break;
            case Species.Horse: agent.speed = horseStats.baseSpeed; break;
        }

        isFleeing = false;
        yield break;
    }
}
