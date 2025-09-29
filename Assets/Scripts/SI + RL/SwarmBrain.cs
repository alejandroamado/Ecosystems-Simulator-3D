using UnityEngine;
using System;
using System.Collections.Generic;
using EcosystemAI;

public class SwarmBrain : IDecisionMaker
{
    public enum Species { Herbivore, Carnivore }
    private Species _species;
    private float alpha = 0.1f, gamma = 0.9f, epsilon = 0.5f;
    private System.Random rnd = new System.Random();
    private Dictionary<int, float[]> qTable = new();

    public SwarmBrain(Species species)
    {
        _species = species;
    }

    public DecisionActionType ChooseAction(MonoBehaviour agent)
    {
        int state = StateFromAgent(agent);
        var q = GetOrInitQValues(state);

        // 1) Si todas las Q(s,a) son iguales (fase inicial), acción aleatoria
        bool allEqual = true;
        for (int i = 1; i < q.Length; i++)
        {
            if (Mathf.Abs(q[i] - q[0]) > 1e-6f) { allEqual = false; break; }
        }
        if (allEqual)
        {
            return (DecisionActionType)rnd.Next(q.Length);
        }

        // 2) ε-greedy estándar
        if (rnd.NextDouble() < epsilon)
        {
            return (DecisionActionType)rnd.Next(q.Length);
        }

        // 3) selección de la acción con mayor Q
        int best = 0;
        for (int i = 1; i < q.Length; i++)
            if (q[i] > q[best]) best = i;
        return (DecisionActionType)best;
    }

    public void Observe(int prevState, DecisionActionType action, float reward, int newState)
    {
        var qPrev = GetOrInitQValues(prevState);
        var qNew  = GetOrInitQValues(newState);
        int a = (int)action;
        qPrev[a] += alpha * (reward + gamma * Max(qNew) - qPrev[a]);
        qTable[prevState] = qPrev;
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

        // Recompensa/penalización por la acción
        switch (action)
        {
            case DecisionActionType.SeekFood:
                if (actionSuccess) r += 10f;
                break;
            case DecisionActionType.SeekMate:
                if (actionSuccess) r += 10f;
                break;
            case DecisionActionType.Rest:
                r += 5f;
                break;
        }

        // Penalizaciones críticas
        if (isHerb)
        {
            var h = (HerbivoreAI)agentMono;

            if (h.species == HerbivoreAI.Species.Deer)
            {
                if (h.deerStats.health <= 20f) r -= 8f;
                if (h.deerStats.hunger <= 20f) r -= 4f;
                if (h.deerStats.energy <= 20f) r -= 4f;
            }
            else
            {
                if (h.horseStats.health <= 20f) r -= 8f;
                if (h.horseStats.hunger <= 20f) r -= 4f;
                if (h.horseStats.energy <= 20f) r -= 4f;
            }
            
        }
        else if (isCarn)
        {
            var c = (CarnivoreAI)agentMono;
            if (c.wolfStats.health <= 20f) r -= 8f;
            if (c.wolfStats.hunger <= 20f) r -= 4f;
            if (c.wolfStats.energy <= 20f) r -= 4f;
        }

        // Penalización por muerte prematura
        // Si el agente ha muerto (o está a punto de hacerlo) y no alcanzó su mitad de vida,
        // penalizamos en proporción a cuán lejos estaba de su maxAge.
        bool justDied = false;
        if (isHerb)
        {
            var h = (HerbivoreAI)agentMono;
            if (h.species == HerbivoreAI.Species.Deer)
            {
                if (h.deerStats.health <= 0)
                    justDied = true;
            }
            else
            {
                if (h.horseStats.health <= 0)
                    justDied = true;
            }
        }
            
        if (isCarn && ((CarnivoreAI)agentMono).wolfStats.health <= 0f)
                justDied = true;

        if (justDied && lifeFraction < 1f)
        {
            // la penalización crece cuanto menor es lifeFraction
            float penalty = (1f - lifeFraction) * 20f;
            r -= penalty;
        }

        return r;
    }

    private float Max(float[] arr)
    {
        float m = arr[0];
        for (int i = 1; i < arr.Length; i++)
            if (arr[i] > m) m = arr[i];
        return m;
    }

    private float[] GetOrInitQValues(int s)
    {
        if (!qTable.TryGetValue(s, out var qs))
        {
            int nActions = Enum.GetNames(typeof(DecisionActionType)).Length;
            qs = new float[nActions];
            // Puedes inicializar con valores pequeños aleatorios para romper simetría:
            for (int i = 0; i < nActions; i++)
                qs[i] = (float)(rnd.NextDouble() * 0.01);
            qTable[s] = qs;
        }
        return qs;
    }

    public int StateFromAgent(MonoBehaviour agentMono)
    {
        float hungerPerc = 0f, energyPerc = 0f;
        bool threat = false;

        if (_species == Species.Herbivore && agentMono is HerbivoreAI h)
        {
            if (h.species == HerbivoreAI.Species.Deer)
            {
                hungerPerc = h.deerStats.hunger / h.deerStats.maxHunger;
                energyPerc = h.deerStats.energy / h.deerStats.maxEnergy;
            }
            else
            {
                hungerPerc = h.horseStats.hunger / h.horseStats.maxHunger;
                energyPerc = h.horseStats.energy / h.horseStats.maxEnergy; 
            }
            threat = h.IsPredatorNearby();
        }
        else if (_species == Species.Carnivore && agentMono is CarnivoreAI c)
        {
            hungerPerc = c.wolfStats.hunger / c.wolfStats.maxHunger;
            energyPerc = c.wolfStats.energy / c.wolfStats.maxEnergy;
            threat     = c.IsPreyNearby();
        }

        int hState = hungerPerc < 0.3f ? 0 : hungerPerc < 0.7f ? 1 : 2;
        int eState = energyPerc < 0.3f ? 0 : energyPerc < 0.7f ? 1 : 2;
        int tState = threat ? 1 : 0;

        return hState + 3 * eState + 9 * tState; // rango 0..17
    }
}
