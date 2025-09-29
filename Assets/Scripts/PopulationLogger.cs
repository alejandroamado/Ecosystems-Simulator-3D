using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EcosystemAI;

public class PopulationLogger : MonoBehaviour
{
    private string filePath;
    public int timeStep;
    public AnimalManager animalManager;
    public GridManager gridManager;

    public void StartLogger()
    {
        animalManager = FindObjectOfType<AnimalManager>();
        gridManager = FindObjectOfType<GridManager>();

        timeStep = 0;

        if (animalManager == null || gridManager == null)
        {
            Debug.LogError("AnimalManager o GridManager no encontrados.");
            enabled = false;
            return;
        }

        string herbAlgo = animalManager.herbivoreAlgorithm.ToString().ToLower();
        string carnAlgo = animalManager.carnivoreAlgorithm.ToString().ToLower();
        string date = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"run_h-{herbAlgo}_c-{carnAlgo}_{date}.csv";
        string folderPath = Application.dataPath + "/Logs";

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        filePath = Path.Combine(folderPath, fileName);

        // Cabecera del CSV
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("tiempo,n. individuos,especie");
        File.WriteAllText(filePath, sb.ToString());

        StartCoroutine(LogDataEverySeconds(10f));
    }

    IEnumerator LogDataEverySeconds(float interval)
    {
        // Primera escritura al instante
        LogPopulationData(); // timeStep = 0, así que tiempo = 1

        while (true)
        {
            yield return new WaitForSeconds(interval);
            LogPopulationData();
        }
    }

    void LogPopulationData()
    {
        int currentTime = timeStep; // Para que empiece en 1
        timeStep++;

        int wolves = animalManager.GetCarnivores().Count;
        int deer = 0;
        int horses = 0;

        foreach (var herb in animalManager.GetHerbivores())
        {
            if (herb.species == HerbivoreAI.Species.Deer) deer++;
            else if (herb.species == HerbivoreAI.Species.Horse) horses++;
        }

        int grass = gridManager.activeGrassCounter;

        List<string> lines = new List<string>
        {
            $"{currentTime},{wolves},lobo",
            $"{currentTime},{deer},ciervo",
            $"{currentTime},{horses},caballo",
            $"{currentTime},{grass},hierba"
        };

        File.AppendAllLines(filePath, lines);
    }
}
