using UnityEngine;
using System;
using System.Collections.Generic;
using EcosystemAI; // Para IDecisionMaker y DecisionActionType

public class RLDecisionMaker : IDecisionMaker
{
    // Parámetros Q-Learning
    private float alpha      = 0.1f;   // tasa de aprendizaje
    private float gamma      = 0.9f;   // factor de descuento
    private float epsilon    = 0.9f;   // prob. de exploración ε‑greedy
    private float minEpsilon = 0.01f;  // valor mínimo de ε
    private float decay      = 0.995f; // factor de decaimiento por episodio

    // Q-table: estado → Q(s,a)[]
    private Dictionary<int, float[]> qTable = new();

    private System.Random rnd = new();

    public DecisionActionType ChooseAction(MonoBehaviour agentMono)
    {
        int state   = StateFromAgent(agentMono);
        float[] qs  = GetOrInitQValues(state);
        int nActions = qs.Length;

        // ε-greedy
        if ((float)rnd.NextDouble() < epsilon)
        {
            // exploración pura
            return (DecisionActionType)rnd.Next(nActions);
        }

        // explotación: argmax con desempate aleatorio
        float maxQ = float.NegativeInfinity;
        for (int i = 0; i < nActions; i++)
            if (qs[i] > maxQ) maxQ = qs[i];

        // recopilar todos los índices que coinciden con el máximo
        var best = new List<int>();
        for (int i = 0; i < nActions; i++)
            if (Mathf.Approximately(qs[i], maxQ))
                best.Add(i);

        // romper el empate al azar
        int choice = best[rnd.Next(best.Count)];
        return (DecisionActionType)choice;
    }

    public void Learn(
        MonoBehaviour agentMono,
        int prevState,
        DecisionActionType action,
        float reward)
    {
        int newState  = StateFromAgent(agentMono);
        float[] qPrev = GetOrInitQValues(prevState);
        float[] qNew  = GetOrInitQValues(newState);

        int a = (int)action;
        qPrev[a] = qPrev[a]
                   + alpha
                   * (reward + gamma * Max(qNew) - qPrev[a]);
        qTable[prevState] = qPrev;

        // decaer epsilon, pero sin bajar de minEpsilon
        epsilon = Mathf.Max(minEpsilon, epsilon * decay);
    }

    private float Max(float[] arr)
    {
        float m = arr[0];
        for (int i = 1; i < arr.Length; i++)
            if (arr[i] > m) m = arr[i];
        return m;
    }

    public float[] GetOrInitQValues(int state)
    {
        if (!qTable.TryGetValue(state, out var qs))
        {
            // inicializa con ruido pequeño para romper simetría
            int n = Enum.GetNames(typeof(DecisionActionType)).Length;
            qs = new float[n];
            for (int i = 0; i < n; i++)
                qs[i] = UnityEngine.Random.Range(0f, 0.01f);
            qTable[state] = qs;
        }
        return qs;
    }

    public int StateFromAgent(MonoBehaviour agentMono)
    {
        // Hambre y energía iguales para ambos:
        float hungerPerc = 0f, energyPerc = 0f;
        bool isThreatNear = false;

        if (agentMono is HerbivoreAI h)
        {
            if (h.species == HerbivoreAI.Species.Deer)
            {
                hungerPerc = h.deerStats.hunger / h.deerStats.maxHunger;
                energyPerc = h.deerStats.energy / h.deerStats.maxEnergy;
                isThreatNear = h.IsPredatorNearby();
            }
            else
            {
                hungerPerc = h.horseStats.hunger / h.horseStats.maxHunger;
                energyPerc = h.horseStats.energy / h.horseStats.maxEnergy;
                isThreatNear = h.IsPredatorNearby();
            }
            
        }
        else if (agentMono is CarnivoreAI c)
        {
            hungerPerc = c.wolfStats.hunger / c.wolfStats.maxHunger;
            energyPerc = c.wolfStats.energy / c.wolfStats.maxEnergy;
            isThreatNear = c.IsPreyNearby();  // Debes implementar este método
        }
        else
        {
            Debug.LogWarning("RLDecisionMaker recibido un agente inesperado.");
        }

        // 3 niveles de hambre (0..2)
        int hState = hungerPerc < 0.3f ? 0 : hungerPerc < 0.7f ? 1 : 2;
        // 3 niveles de energía (0..2)
        int eState = energyPerc < 0.3f ? 0 : energyPerc < 0.7f ? 1 : 2;
        // 2 niveles de amenaza/objetivo (0..1)
        int tState = isThreatNear ? 1 : 0;

        // Codificar un único int:
        return hState       // 0..2
             + 3 * eState   // 0,3,6
             + 9 * tState;  // +0 o +9
        // rango total: 0..17
    }

    public float ComputeReward(
        MonoBehaviour agentMono,
        DecisionActionType action,
        bool actionSuccess,
        float deltaTime)
    {
        float r = 0f;
        bool isHerb = agentMono is HerbivoreAI;
        bool isCarn = agentMono is CarnivoreAI;

        // Recompensa por estar vivo, escalonada según edad
        float age, maxAge;
        if (isHerb)
        {
            var h = (HerbivoreAI)agentMono;

            if (h.species == HerbivoreAI.Species.Deer)
            {
                age = h.deerStats.age;
                maxAge = h.deerStats.maxAge;
            }
            else
            {
                age = h.horseStats.age;
                maxAge = h.horseStats.maxAge;
            }
        }
        else if (isCarn)
        {
            var c = (CarnivoreAI)agentMono;
            age = c.wolfStats.age;
            maxAge = c.wolfStats.maxAge;
        }
        else
        {
            age = maxAge = 1f; // fallback
        }

        float lifeFraction = age / maxAge;
        if (lifeFraction < 0.5f)
            r += deltaTime * 0.005f;
        else if (lifeFraction < 0.8f)
            r += deltaTime * 0.01f;
        else
            r += deltaTime * 0.02f;

        switch (action)
        {
            case DecisionActionType.SeekFood:
                if (actionSuccess)
                    r += 10f;
                break;

            case DecisionActionType.SeekMate:
                if (actionSuccess)
                    r += 10f;
                break;

            case DecisionActionType.Rest:
                r += 5f;               // recompensa suave por descansar
                break;
        }

        // Penalizar estado crítico:
        if (isHerb)
        {
            var h = (HerbivoreAI)agentMono;

            if (h.species == HerbivoreAI.Species.Deer)
            {
                if (h.deerStats.health <= 20f) r -= 8f;
                if (h.deerStats.hunger <= 10f) r -= 5f;
                if (h.deerStats.energy <= 10f) r -= 5f;
            }
            else
            {
                if (h.horseStats.health <= 20f) r -= 8f;
                if (h.horseStats.hunger <= 10f) r -= 5f;
                if (h.horseStats.energy <= 10f) r -= 5f;
            }
        }
        else if (isCarn)
        {
            var c = (CarnivoreAI)agentMono;
            if (c.wolfStats.health <= 20f) r -= 8f;
            if (c.wolfStats.hunger <= 10f) r -= 5f;
            if (c.wolfStats.energy <= 10f) r -= 5f;
        }

        return r;
    }
    
    public void AverageFrom(RLDecisionMaker parentA, RLDecisionMaker parentB)
    {
        var newTable = new Dictionary<int, float[]>();
        // recorrer todos los estados que aparezcan en A o B
        var allStates = new HashSet<int>(parentA.qTable.Keys);
        allStates.UnionWith(parentB.qTable.Keys);

        int actionCount = Enum.GetNames(typeof(DecisionActionType)).Length;

        foreach (int s in allStates)
        {
            float[] aVals = parentA.qTable.TryGetValue(s, out var av) ? av : new float[actionCount];
            float[] bVals = parentB.qTable.TryGetValue(s, out var bv) ? bv : new float[actionCount];
            float[] avg = new float[actionCount];
            for (int i = 0; i < actionCount; i++)
                avg[i] = (aVals[i] + bVals[i]) * 0.5f;
            newTable[s] = avg;
        }

        this.qTable = newTable;
    }
}
