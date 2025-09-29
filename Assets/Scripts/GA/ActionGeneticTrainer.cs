using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un genoma genérico: un vector de floats de longitud fija (7 en este caso).
/// </summary>
[Serializable]
public class ActionGenome
{
    // 7 genes:
    // 0: prob. comer
    // 1: prob. reproducirse
    // 2: prob. descansar
    // 3: idleMin (1–10)
    // 4: idleMax (5–20)
    // 5: restMin (1–15)
    // 6: restMax (10–30)
    public float[] genes;

    public ActionGenome(int length)
    {
        genes = new float[length];
    }

    /// <summary> Clona profundamente. </summary>
    public ActionGenome Clone()
    {
        var copy = new ActionGenome(genes.Length);
        Array.Copy(genes, copy.genes, genes.Length);
        return copy;
    }
}

public class ActionGeneticTrainer : MonoBehaviour
{
    public static ActionGeneticTrainer Instance { get; private set; }

    [Tooltip("Tamaño inicial de la población")]
    public int initialPopulationSize = 40;
    [Tooltip("Magnitud máxima de mutación por gen")]
    public float mutationRate = 0.1f;

    private List<ActionGenome> population;
    private const int GENOME_LENGTH = 7;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(this);
    }

    /// <summary> Inicializa población con genomas aleatorios válidos. </summary>
    public void Initialize()
    {
        population = new List<ActionGenome>(initialPopulationSize);
        for (int i = 0; i < initialPopulationSize; i++)
        {
            var g = new ActionGenome(GENOME_LENGTH);

            // 1–3: probabilidades, generamos 3 valores aleatorios y luego normalizamos
            g.genes[0] = UnityEngine.Random.value;
            g.genes[1] = UnityEngine.Random.value;
            g.genes[2] = UnityEngine.Random.value;
            NormalizeProbs(g);

            // 4–5: idleMin (1–10), idleMax (5–20) y asegurar min<max
            g.genes[3] = UnityEngine.Random.Range(1f, 3f);
            g.genes[4] = UnityEngine.Random.Range(3f, 7f);
            if (g.genes[4] <= g.genes[3]) g.genes[4] = g.genes[3] + 1f;

            // 6–7: restMin (1–15), restMax (10–30) y asegurar min<max
            g.genes[5] = UnityEngine.Random.Range(1f, 10f);
            g.genes[6] = UnityEngine.Random.Range(15f, 20f);
            if (g.genes[6] <= g.genes[5]) g.genes[6] = g.genes[5] + 1f;

            population.Add(g);
        }
    }

    /// <summary> Obtiene un genoma aleatorio de la población. </summary>
    public ActionGenome GetRandomGenome()
    {
        if (population == null || population.Count == 0) Initialize();
        return population[UnityEngine.Random.Range(0, population.Count)].Clone();
    }

    /// <summary> Cruza dos genomas padre y produce un hijo válido. </summary>
    public ActionGenome Crossover(ActionGenome a, ActionGenome b)
    {
        var child = new ActionGenome(GENOME_LENGTH);

        // 0–2: proba → promedio + mutación → normalizar
        for (int i = 0; i < 3; i++)
        {
            child.genes[i] = (a.genes[i] + b.genes[i]) * 0.5f;
            if (UnityEngine.Random.value < 0.15f)
                child.genes[i] += UnityEngine.Random.Range(-mutationRate, mutationRate);
        }
        NormalizeProbs(child);

        // 3 (idleMin): promedio + mutación, luego clamp y redondeo
        child.genes[3] = (a.genes[3] + b.genes[3]) * 0.5f;
        if (UnityEngine.Random.value < 0.15f)
            child.genes[3] += UnityEngine.Random.Range(-mutationRate * 10f, mutationRate * 10f);
        child.genes[3] = Mathf.Clamp(child.genes[3], 1f, 10f);

        // 4 (idleMax): igual, pero aseguramos > idleMin
        child.genes[4] = (a.genes[4] + b.genes[4]) * 0.5f;
        if (UnityEngine.Random.value < 0.15f)
            child.genes[4] += UnityEngine.Random.Range(-mutationRate * 15f, mutationRate * 15f);
        child.genes[4] = Mathf.Clamp(child.genes[4], 5f, 20f);
        if (child.genes[4] <= child.genes[3]) child.genes[4] = child.genes[3] + 1f;

        // 5 (restMin)
        child.genes[5] = (a.genes[5] + b.genes[5]) * 0.5f;
        if (UnityEngine.Random.value < 0.15f)
            child.genes[5] += UnityEngine.Random.Range(-mutationRate * 15f, mutationRate * 15f);
        child.genes[5] = Mathf.Clamp(child.genes[5], 1f, 15f);

        // 6 (restMax)
        child.genes[6] = (a.genes[6] + b.genes[6]) * 0.5f;
        if (UnityEngine.Random.value < 0.15f)
            child.genes[6] += UnityEngine.Random.Range(-mutationRate * 20f, mutationRate * 20f);
        child.genes[6] = Mathf.Clamp(child.genes[6], 10f, 30f);
        if (child.genes[6] <= child.genes[5]) child.genes[6] = child.genes[5] + 1f;

        population.Add(child);
        return child;
    }

    /// <summary> Normaliza genes 0–2 para que sumen 1. </summary>
    private void NormalizeProbs(ActionGenome g)
    {
        float sum = g.genes[0] + g.genes[1] + g.genes[2];
        if (sum <= 0f) sum = 1f;
        g.genes[0] /= sum;
        g.genes[1] /= sum;
        g.genes[2] /= sum;
    }
}
