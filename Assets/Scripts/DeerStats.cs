using UnityEngine;

[System.Serializable]
public class DeerStats {
    public enum Gender { Male, Female }
    
    public Gender gender;
    public float maxAge;
    public float age;
    public float size;       // 1.0 para adulto, 0.5 para recién nacido
    public float baseSpeed;  // Velocidad base para caminar
    public float baseMaxHealth;
    public float maxHealth;
    public float health;
    public float baseMaxEnergy;
    public float maxEnergy;
    public float energy;
    public float baseMaxHunger;
    public float maxHunger;
    public float hunger;
    public float baseStrength;
    public float strength;
    public float detectionRange;
    
    // Nuevo campo para indicar si una hembra está embarazada
    public bool isPregnant = false;

    // Límites físicos
    public const float MIN_MAXAGE = 3f;
    public const float MAX_MAXAGE = 30f;
    public const float MIN_SIZE = 0.7f;
    public const float MAX_SIZE = 1.5f;
    public const float MIN_SPEED = 0.6f;
    public const float MAX_SPEED = 2.5f;
    public const float MIN_HEALTH = 40f;
    public const float MAX_HEALTH = 250f;
    public const float MIN_ENERGY = 40f;
    public const float MAX_ENERGY = 250f;
    public const float MIN_HUNGER = 40f;
    public const float MAX_HUNGER = 250f;
    public const float MIN_STRENGTH = 20f;
    public const float MAX_STRENGTH = 80f;
    public const float MIN_DETECTIONRANGE = 1f;
    public const float MAX_DETECTIONRANGE = 25f;

    public DeerStats(float minMaxAge, float maxMaxAge, float minSize, float maxSize, float minSpeed, float maxSpeedRange, float minHealth, float maxHealthRange, 
                     float minEnergy, float maxEnergyRange, float minHunger, float maxHungerRange, float minStrength, float maxStrengthRange, float minDetectionRange, 
                     float maxDetectionRange) {

        isPregnant = false;
        maxAge = Mathf.Clamp(Random.Range(minMaxAge, maxMaxAge), MIN_MAXAGE, MAX_MAXAGE);
        age = 3f;
        size = Mathf.Clamp(Random.Range(minSize, maxSize), MIN_SIZE, MAX_SIZE);
        float sizeFactor = 1 + 0.5f * (size - 1f);

        baseSpeed = Mathf.Clamp(Random.Range(minSpeed, maxSpeedRange), MIN_SPEED, MAX_SPEED);
        baseMaxHealth = Mathf.Clamp(Random.Range(minHealth, maxHealthRange), MIN_HEALTH, MAX_HEALTH);
        baseMaxEnergy = Mathf.Clamp(Random.Range(minEnergy, maxEnergyRange), MIN_ENERGY, MAX_ENERGY) / sizeFactor;
        baseMaxHunger = Mathf.Clamp(Random.Range(minHunger, maxHungerRange), MIN_HUNGER, MAX_HUNGER) * sizeFactor;
        baseStrength = Mathf.Clamp(Random.Range(minStrength, maxStrengthRange), MIN_STRENGTH, MAX_STRENGTH) * sizeFactor;
        detectionRange = Mathf.Clamp(Random.Range(minDetectionRange, maxDetectionRange), MIN_DETECTIONRANGE, MAX_DETECTIONRANGE);

        maxHealth = baseMaxHealth;
        maxEnergy = baseMaxEnergy;
        maxHunger = baseMaxHunger;
        strength = baseStrength;
        health = baseMaxHealth;
        energy = baseMaxEnergy;
        hunger = baseMaxHunger;
    }
    
    // Método estático para crear descendencia a partir de dos padres.
    public static DeerStats CreateOffspring(DeerStats parent1, DeerStats parent2) {
        DeerStats offspring = new DeerStats(
            10f, 20f,    // Rango para maxAge
            0.9f, 1.1f,  // Rango para size
            1f, 1.5f,    // Rango para speed
            90f, 120f,   // Rango para health
            90f, 120f,   // Rango para energy
            90f, 120f,   // Rango para hunger
            40f, 60f,    // Rango para strength
            8f, 13f      // Rango para deteccion
        );
        // Elegir género aleatoriamente
        offspring.gender = (Random.value < 0.5f) ? Gender.Male : Gender.Female;
        offspring.isPregnant = false;
        
        // Promediar los atributos y clampear para asegurar límites físicos:
        offspring.maxAge = (parent1.maxAge + parent2.maxAge) / 2f;
        offspring.age = 0f;
        offspring.size = Mathf.Clamp((parent1.size + parent2.size) / 2f, MIN_SIZE, MAX_SIZE) * 0.5f;
        float sizeFactor = 1 + 0.5f * (offspring.size - 1f);

        offspring.baseSpeed = Mathf.Clamp((parent1.baseSpeed + parent2.baseSpeed) / 2f, MIN_SPEED, MAX_SPEED);
        offspring.baseMaxHealth = Mathf.Clamp((parent1.baseMaxHealth + parent2.baseMaxHealth) / 2f, MIN_HEALTH, MAX_HEALTH);
        offspring.baseMaxEnergy = Mathf.Clamp((parent1.baseMaxEnergy + parent2.baseMaxEnergy) / 2f, MIN_ENERGY, MAX_ENERGY) / sizeFactor;
        offspring.baseMaxHunger = Mathf.Clamp((parent1.baseMaxHunger + parent2.baseMaxHunger) / 2f, MIN_HUNGER, MAX_HUNGER) * sizeFactor;
        offspring.baseStrength = Mathf.Clamp((parent1.baseStrength + parent2.baseStrength) / 2f, MIN_STRENGTH, MAX_STRENGTH) * sizeFactor;
        offspring.detectionRange = Mathf.Clamp((parent1.detectionRange + parent2.detectionRange) / 2f, MIN_DETECTIONRANGE, MAX_DETECTIONRANGE);
        
        offspring.maxHealth = offspring.baseMaxHealth;
        offspring.maxEnergy = offspring.baseMaxEnergy;
        offspring.maxHunger = offspring.baseMaxHunger;
        offspring.strength = offspring.baseStrength;
        offspring.health = offspring.baseMaxHealth * 0.6f;
        offspring.energy = offspring.baseMaxEnergy * 0.8f;
        offspring.hunger = offspring.baseMaxHunger * 0.3f;
        return offspring;
    }
}
