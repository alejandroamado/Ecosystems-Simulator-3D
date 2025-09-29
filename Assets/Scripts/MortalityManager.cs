using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EcosystemAI;

public class MortalityManager : MonoBehaviour
{
    [Header("Referencias")]
    public AnimalManager animalManager;
    public GridManager gridManager;

    [Header("Tiempo / protección")]
    public float secondsPerYear = 50f;
    public int protectionYears = 4;
    private int currentYear = 0;

    [Header("Rates base (proporciones por año)")]
    [Range(0f, 1f)] public float baseMortalityDeer  = 0.05f;
    [Range(0f, 1f)] public float baseMortalityHorse = 0.04f;
    [Range(0f, 1f)] public float baseMortalityWolf  = 0.1f;

    [Header("Ajuste por ratio herbívoros:carnívoros")]
    public float targetHerbivorePerCarnivore = 5f; // objetivo (si hay más herb que esto, aumenta mortalidad de herb)
    public float ratioScale = 0.05f; // cuánto escala la mortalidad por unidad de exceso de ratio (para herbívoros)
    [Tooltip("Cuánto aumenta la mortalidad de lobos cuando hay menos herbívoros de los necesarios (proporción por debajo del objetivo).")]
    public float wolfPenaltyScale = 1.0f;

    [Header("Límites mínimos")]
    public int minDeerToKeep = 5;
    public int minHorseToKeep = 5;
    public int minWolvesToKeep = 2;

    [Header("Opciones")]
    public bool logActions = true;

    void Start()
    {
        if (animalManager == null) animalManager = FindObjectOfType<AnimalManager>();
        if (gridManager == null) gridManager = FindObjectOfType<GridManager>();

        if (animalManager == null)
        {
            Debug.LogError("MortalityManager: AnimalManager no encontrado.");
            enabled = false;
            return;
        }

        StartCoroutine(YearlyRoutine());
    }

    IEnumerator YearlyRoutine()
    {
        yield return new WaitForSeconds(secondsPerYear);

        while (true)
        {
            currentYear++;
            if (logActions) Debug.Log($"[Mortality] Año {currentYear}: ejecuto checks.");

            if (currentYear > protectionYears)
            {
                ApplyAnnualMortality();
            }
            else
            {
                if (logActions) Debug.Log($"[Mortality] Año {currentYear} está dentro del periodo de protección ({protectionYears}). No se aplican muertes.");
            }

            yield return new WaitForSeconds(secondsPerYear);
        }
    }

    void ApplyAnnualMortality()
    {
        var allHerb = animalManager.GetHerbivores();
        var allCarn = animalManager.GetCarnivores();

        int deerCount = allHerb.Count(h => h != null && h.species == HerbivoreAI.Species.Deer);
        int horseCount = allHerb.Count(h => h != null && h.species == HerbivoreAI.Species.Horse);
        int wolfCount = allCarn.Count;

        int safeWolfCount = Mathf.Max(1, wolfCount);

        // ratio herb:carn (usamos total herbívoros sobre carnívoros)
        int totalHerb = deerCount + horseCount;
        float herbToCarnRatio = totalHerb / (float)safeWolfCount;

        // factor por exceso de herbívoros respecto al objetivo (aumenta mortalidad de herbívoros)
        float excessRatio = Mathf.Max(0f, herbToCarnRatio - targetHerbivorePerCarnivore);
        float herbRatioFactor = 1f + excessRatio * ratioScale;

        // ahora penalización para lobos si H:C está por debajo del objetivo
        float ratioDeficit = Mathf.Max(0f, (targetHerbivorePerCarnivore - herbToCarnRatio) / targetHerbivorePerCarnivore);
        // wolfPenaltyScale controla la severidad; se combina con la componente previa para exceso de lobos
        float wolfDeficitFactor = 1f + ratioDeficit * wolfPenaltyScale;

        // adicional: si hay muy pocos herbívoros absolutos, forzamos una penalización fuerte
        if (totalHerb <= 2 && wolfCount > 0)
        {
            // penalización extra cuando el total de herb es casi nulo
            wolfDeficitFactor += 1.0f;
            if (logActions) Debug.Log("[Mortality] Penalización extra a lobos porque totalHerb <= 2.");
        }

        // calcular muertes (redondeo)
        int deerDeaths = Mathf.RoundToInt(deerCount * baseMortalityDeer * herbRatioFactor);
        int horseDeaths = Mathf.RoundToInt(horseCount * baseMortalityHorse * herbRatioFactor);

        // para lobos: mantenemos la lógica previa (exceso de lobos) y le aplicamos la penalización por déficit de herbívoros
        float wolfToHerbRatio = wolfCount / Mathf.Max(1, totalHerb); // si hay pocos herb, exceso de lobos es malo
        float wolfExcess = Mathf.Max(0f, wolfToHerbRatio - (1f / Mathf.Max(1f, targetHerbivorePerCarnivore)));
        float wolfExcessFactor = 1f + wolfExcess * ratioScale * 2f;

        float combinedWolfFactor = wolfExcessFactor * wolfDeficitFactor;
        int wolfDeaths = Mathf.RoundToInt(wolfCount * baseMortalityWolf * combinedWolfFactor);

        // asegurar mínimos (no matar todo)
        deerDeaths  = Mathf.Clamp(deerDeaths,  0, Mathf.Max(0, deerCount  - minDeerToKeep));
        horseDeaths = Mathf.Clamp(horseDeaths, 0, Mathf.Max(0, horseCount - minHorseToKeep));
        wolfDeaths  = Mathf.Clamp(wolfDeaths,  0, Mathf.Max(0, wolfCount  - minWolvesToKeep));

        if (logActions)
            Debug.Log($"[Mortality] Poblaciones antes: deer={deerCount}, horse={horseCount}, wolf={wolfCount} | H:C={herbToCarnRatio:F2} | factors -> herb:{herbRatioFactor:F2} wolfExcess:{wolfExcessFactor:F2} wolfDeficit:{wolfDeficitFactor:F2} combinedWolf:{combinedWolfFactor:F2} => muertes deer:{deerDeaths} horse:{horseDeaths} wolf:{wolfDeaths}");

        // ejecutar muertes
        KillRandomHerbivores(HerbivoreAI.Species.Deer, deerDeaths);
        KillRandomHerbivores(HerbivoreAI.Species.Horse, horseDeaths);
        KillRandomCarnivores(wolfDeaths);
    }

    void KillRandomHerbivores(HerbivoreAI.Species species, int count)
    {
        if (count <= 0) return;
        var list = animalManager.GetHerbivores().Where(h => h != null && h.species == species).ToList();
        if (list.Count == 0) return;

        count = Mathf.Min(count, list.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, list.Count);
            HerbivoreAI victim = list[idx];
            if (victim != null && victim.gameObject != null)
            {
                if (logActions) Debug.Log($"[Mortality] Matando {victim.name} ({species})");
                Destroy(victim.gameObject);
                if (species == HerbivoreAI.Species.Deer) animalManager.activeDeerCounter = Mathf.Max(0, animalManager.activeDeerCounter - 1);
                else animalManager.activeHorseCounter = Mathf.Max(0, animalManager.activeHorseCounter - 1);
            }
            list.RemoveAt(idx);
        }
    }

    void KillRandomCarnivores(int count)
    {
        if (count <= 0) return;
        var list = animalManager.GetCarnivores().Where(c => c != null).ToList();
        if (list.Count == 0) return;

        count = Mathf.Min(count, list.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, list.Count);
            CarnivoreAI victim = list[idx];
            if (victim != null && victim.gameObject != null)
            {
                if (logActions) Debug.Log($"[Mortality] Matando {victim.name} (wolf)");
                Destroy(victim.gameObject);
                animalManager.activeWolfCounter = Mathf.Max(0, animalManager.activeWolfCounter - 1);
            }
            list.RemoveAt(idx);
        }
    }
}
