using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public int width = 100;
    public int height = 100;
    public float cellSize = 1f;

    public List<GameObject> rockPrefabs;
    public float rockGap = 0.5f;
    private Vector3 lastRockBounds = Vector3.zero;
    public int generationMargin = 2;
    public float rockInset = 0.2f;

    public GameObject treePrefab;
    public GameObject grassPrefabLow;
    public GameObject grassPrefabHigh;

    public GameObject mushroomPrefabRed;
    public GameObject mushroomPrefabOrange;

    public int totalTrees = 150;

    public int randomGrassCount = 200;
    public int grassPerTree = 1;
    public float grassPerMinute = 60f;
    
    public int randomMushroomsCount = 40;
    public float mushroomsPerTree = 0.3f;

    // Nueva referencia: objeto que tiene el NavMeshSurface (el terreno)
    public GameObject navMeshParent;

    // Diccionario para almacenar árboles (clave: posición en el grid)
    private Dictionary<Vector2Int, GameObject> casillas = new Dictionary<Vector2Int, GameObject>();

    // Lista de posiciones ya ocupadas por árboles
    private List<Vector2Int> placedTreePositions = new List<Vector2Int>();

    // Lista de posiciones de semillas de bosque
    private List<Vector2Int> forestSeedPositions = new List<Vector2Int>();

    private List<GameObject> grassInstancesLow = new List<GameObject>();
    private List<GameObject> grassInstancesHigh = new List<GameObject>();

    public int activeGrassCounter = 0;
    public int totalGrassCounter = 0;

    public List<Vector3> grassRespawnPositions = new List<Vector3>();
    private float grassRespawnProbability = 0.9f;

    void Start()
    {
        // Vacío
    }

    public void StartGridManager()
    {
        // 1) Si navMeshParent no es null, eliminar rápidamente todos sus hijos (rocas, árboles, hierba, setas, etc.)
        if (navMeshParent != null)
        {
            // Recorremos de atrás hacia adelante para no alterar el índice de transform.GetChild(i)
            for (int i = navMeshParent.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(navMeshParent.transform.GetChild(i).gameObject);
            }
        }

        // 2) Limpiar las colecciones internas para que no quede rastro de la generación anterior
        casillas.Clear();
        placedTreePositions.Clear();
        forestSeedPositions.Clear();
        grassInstancesLow.Clear();
        grassInstancesHigh.Clear();
        grassRespawnPositions.Clear();

        activeGrassCounter = 0;
        totalGrassCounter = 0;

        // 3) Ahora generamos todo desde cero
        GenerateRocks();
        GenerateTrees();
        GenerateGrass();
        GenerateMushrooms();

        // GenerateVisualGrid();

        StartCoroutine(GenerateGrassOverTime());
    }

    void GenerateVisualGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 start = new Vector3(x * cellSize, 0, y * cellSize);
                Vector3 endY = new Vector3(x * cellSize, 0, (y + 1) * cellSize);
                Vector3 endX = new Vector3((x + 1) * cellSize, 0, y * cellSize);
                Debug.DrawLine(start, endY, Color.green, 100f);
                Debug.DrawLine(start, endX, Color.green, 100f);
            }
        }
    }

    bool IsCellValid(Vector2Int pos, float minDispersion)
    {
        foreach (Vector2Int existing in placedTreePositions)
        {
            if (Vector2.Distance(new Vector2(pos.x, pos.y), new Vector2(existing.x, existing.y)) < minDispersion)
                return false;
        }
        return true;
    }

    void GenerateRocks()
    {
        float worldMargin = generationMargin * cellSize;
        float halfMargin = worldMargin * 0.5f - rockInset;
        float minX = halfMargin;
        float maxX = width * cellSize - halfMargin;
        float minZ = halfMargin;
        float maxZ = height * cellSize - halfMargin;

        foreach (var z in new float[] { minZ, maxZ })
        {
            float x = minX;
            while (x < maxX)
            {
                PlaceRock(new Vector3(x, 0, z), Vector3.right);
                x += lastRockBounds.x + rockGap;
            }
        }

        foreach (var x in new float[] { minX, maxX })
        {
            float z = minZ;
            while (z < maxZ)
            {
                PlaceRock(new Vector3(x, 0, z), Vector3.forward);
                z += lastRockBounds.z + rockGap;
            }
        }
    }

    void PlaceRock(Vector3 position, Vector3 edgeDirection)
    {
        int prefabIndex = Random.Range(0, rockPrefabs.Count);
        GameObject prefab = rockPrefabs[prefabIndex];

        var mf = prefab.GetComponentInChildren<MeshFilter>();
        var meshSz = mf.sharedMesh.bounds.size;
        var baseScale = prefab.transform.localScale;
        var worldSzLocal = Vector3.Scale(meshSz, baseScale);
        lastRockBounds = worldSzLocal;

        bool longerX = worldSzLocal.x > worldSzLocal.z;
        float yaw;
        if (edgeDirection == Vector3.right) yaw = longerX ? 0f : 90f;
        else                                      yaw = longerX ? 90f : 0f;

        var baseRot = prefab.transform.rotation;
        var finalRot = Quaternion.AngleAxis(yaw, Vector3.up) * baseRot;
        GameObject rock = Instantiate(prefab, position, finalRot, navMeshParent.transform);
        rock.layer = navMeshParent.layer;

        float yFactor = prefabIndex == 0 ? 4f
                    : prefabIndex == 1 ? 3f
                    : 2f;
        Vector3 ls = rock.transform.localScale;
        rock.transform.localScale = new Vector3(ls.x, ls.y * yFactor, ls.z);

        var rend = rock.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            float minY = rend.bounds.min.y;
            rock.transform.position += Vector3.up * (-minY);
        }

        var col = rock.GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    void GenerateTrees()
    {
        GenerateForestSeeds();
        GenerarClusterTrees();
    }

    void GenerateForestSeeds()
    {
        float minDispersion = 3f;
        int forestSeedCount = Mathf.FloorToInt(totalTrees * 0.10f);
        int seedsPlaced = 0;
        int maxAttempts = forestSeedCount * 5;
        int attempts = 0;

        if (forestSeedCount < 1 && totalTrees > 0)
            forestSeedCount = 1;
        else if (totalTrees == 0)
            return;

        while (seedsPlaced < forestSeedCount && attempts < maxAttempts)
        {
            attempts++;
            int randX = Random.Range(generationMargin, width  - generationMargin);
            int randY = Random.Range(generationMargin, height - generationMargin);
            Vector2Int pos = new Vector2Int(randX, randY);

            if (IsCellValid(pos, minDispersion))
            {
                Vector3 worldPos = GetWorldPosition(pos);
                GameObject treeInstance = Instantiate(treePrefab, worldPos, Quaternion.identity);
                float scaleFactor = Random.Range(0.8f, 1.5f);
                treeInstance.transform.localScale = Vector3.one * scaleFactor;

                // Asignar el árbol como hijo del terreno y su capa
                if (navMeshParent != null)
                {
                    treeInstance.transform.SetParent(navMeshParent.transform);
                    treeInstance.layer = navMeshParent.layer;
                }

                casillas[pos] = treeInstance;
                placedTreePositions.Add(pos);
                forestSeedPositions.Add(pos);
                seedsPlaced++;
            }
        }
        Debug.Log("Semillas de bosque colocadas: " + seedsPlaced);
    }

    void GenerarClusterTrees()
    {
        float minDispersion = 3f;
        float maxDispersion = 15f;
        int clusterTreeCount = totalTrees - forestSeedPositions.Count;
        int clusterTreesPlaced = 0;
        int maxAttempts = clusterTreeCount * 10;
        int attempts = 0;

        if (totalTrees == 0)
            return;

        while (clusterTreesPlaced < clusterTreeCount && attempts < maxAttempts)
        {
            attempts++;
            // Selecciona aleatoriamente una semilla
            Vector2Int seed = forestSeedPositions[Random.Range(0, forestSeedPositions.Count)];

            // Genera un radio aleatorio entre minDispersion y maxDispersion y un ángulo aleatorio
            float randomRadius = Random.Range(minDispersion, maxDispersion);
            float randomAngle = Random.Range(0f, Mathf.PI * 2f);
            int offsetX = Mathf.RoundToInt(randomRadius * Mathf.Cos(randomAngle));
            int offsetY = Mathf.RoundToInt(randomRadius * Mathf.Sin(randomAngle));

            Vector2Int candidate = new Vector2Int(seed.x + offsetX, seed.y + offsetY);

            // Verificar límites del grid
            if (candidate.x < generationMargin || candidate.x >= width  - generationMargin ||
                candidate.y < generationMargin || candidate.y >= height - generationMargin)
                continue;

            // Verificar separación mínima respecto a otros árboles
            if (IsCellValid(candidate, minDispersion))
            {
                Vector3 worldPos = GetWorldPosition(candidate);
                GameObject treeInstance = Instantiate(treePrefab, worldPos, Quaternion.identity);
                float scaleFactor = Random.Range(0.8f, 1.5f);
                treeInstance.transform.localScale = Vector3.one * scaleFactor;

                // Asignar el árbol como hijo del terreno y su capa
                if (navMeshParent != null)
                {
                    treeInstance.transform.SetParent(navMeshParent.transform);
                    treeInstance.layer = navMeshParent.layer;
                }

                casillas[candidate] = treeInstance;
                placedTreePositions.Add(candidate);
                clusterTreesPlaced++;
            }
        }
        Debug.Log("Árboles en clúster colocados: " + clusterTreesPlaced + " de " + clusterTreeCount);
    }

    // Convierte una posición del grid (Vector2Int) a posición en el mundo (Vector3)
    public Vector3 GetWorldPosition(Vector2Int gridPosition)
    {
        return new Vector3(gridPosition.x * cellSize, 0, gridPosition.y * cellSize);
    }

    void GenerateGrass()
    {
        GenerateGrassPerTree();
        GenerateRandomGrass();
    }

    void GenerateGrassPerTree()
    {
        foreach (Vector2Int treePos in placedTreePositions)
        {
            for (int i = 0; i < grassPerTree; i++)
            {
                float radius = Random.Range(1f, 5f);
                float angle = Random.Range(0f, Mathf.PI * 2f);
                int offsetX = Mathf.RoundToInt(radius * Mathf.Cos(angle));
                int offsetY = Mathf.RoundToInt(radius * Mathf.Sin(angle));
                Vector2Int grassPos = new Vector2Int(treePos.x + offsetX, treePos.y + offsetY);

                if (grassPos.x < generationMargin || grassPos.x >= width  - generationMargin ||
                    grassPos.y < generationMargin || grassPos.y >= height - generationMargin)
                    continue;

                GameObject chosenGrassPrefab = (Random.value < 0.7f) ? grassPrefabHigh : grassPrefabLow;
                GameObject grassInstance = Instantiate(chosenGrassPrefab, GetWorldPosition(grassPos), Quaternion.identity);
                float scaleFactor = Random.Range(2.5f, 3.5f);
                grassInstance.transform.localScale = Vector3.one * scaleFactor;

                activeGrassCounter++;
                totalGrassCounter++;
                grassInstance.name = "Grass_" + totalGrassCounter.ToString("D5");

                if (navMeshParent != null)
                {
                    grassInstance.transform.SetParent(navMeshParent.transform);
                    int vegetationLayer = LayerMask.NameToLayer("Vegetation");
                    SetLayer(grassInstance, vegetationLayer);
                }

                Collider grassCollider = grassInstance.GetComponent<Collider>();
                if (grassCollider != null)
                    grassCollider.enabled = false;

                if (chosenGrassPrefab == grassPrefabLow)
                    grassInstancesLow.Add(grassInstance);
                else
                    grassInstancesHigh.Add(grassInstance);
            }
        }
    }

    // Genera hierba aleatoria dividida en 5% semillas de clúster, 75% adición a clúster y 20% completamente aleatoria.
    void GenerateRandomGrass()
    {
        int totalRandomGrass = randomGrassCount;
        int clusterSeedCount = Mathf.RoundToInt(totalRandomGrass * 0.05f);
        int clusterAdditionCount = Mathf.RoundToInt(totalRandomGrass * 0.75f);
        int completelyRandomCount = totalRandomGrass - clusterSeedCount - clusterAdditionCount;

        // Generar las semillas de clúster
        List<Vector3> clusterSeedPositions = new List<Vector3>();
        for (int i = 0; i < clusterSeedCount; i++)
        {
            int x = Random.Range(generationMargin, width  - generationMargin);
            int y = Random.Range(generationMargin, height - generationMargin);

            Vector2Int pos = new Vector2Int(x, y);
            if (pos.x < generationMargin || pos.x >= width  - generationMargin ||
                pos.y < generationMargin || pos.y >= height - generationMargin)
                continue;


            Vector3 worldPos = GetWorldPosition(pos);
            GameObject chosenGrassPrefab = (Random.value < 0.5f) ? grassPrefabHigh : grassPrefabLow;
            GameObject grassInstance = Instantiate(chosenGrassPrefab, worldPos, Quaternion.identity);
            float scaleFactor = Random.Range(2.5f, 3.5f);
            grassInstance.transform.localScale = Vector3.one * scaleFactor;

            activeGrassCounter++;
            totalGrassCounter++;
            grassInstance.name = "Grass_" + totalGrassCounter.ToString("D5");

            if (navMeshParent != null)
            {
                grassInstance.transform.SetParent(navMeshParent.transform);
                int vegetationLayer = LayerMask.NameToLayer("Vegetation");
                SetLayer(grassInstance, vegetationLayer);
            }

            Collider grassCollider = grassInstance.GetComponent<Collider>();
            if (grassCollider != null)
                grassCollider.enabled = false;
            if (chosenGrassPrefab == grassPrefabLow)
                grassInstancesLow.Add(grassInstance);
            else
                grassInstancesHigh.Add(grassInstance);
            clusterSeedPositions.Add(worldPos);
        }

        // Generar hierba adicional en clúster (75%)
        for (int i = 0; i < clusterAdditionCount; i++)
        {
            if (clusterSeedPositions.Count == 0)
                break;

            // Seleccionar una semilla aleatoria
            Vector3 seedPos = clusterSeedPositions[Random.Range(0, clusterSeedPositions.Count)];
            float radius = Random.Range(1f, 5f);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float offsetX = radius * Mathf.Cos(angle);
            float offsetZ = radius * Mathf.Sin(angle);
            Vector3 worldPos = seedPos + new Vector3(offsetX, 0, offsetZ);
            
            // Comprobar que worldPos esté dentro de los límites del terreno
            float minCoord = generationMargin * cellSize;
            float maxCoordX = (width  - generationMargin) * cellSize;
            float maxCoordZ = (height - generationMargin) * cellSize;

            if (worldPos.x < minCoord || worldPos.x > maxCoordX ||
                worldPos.z < minCoord || worldPos.z > maxCoordZ)
                continue;

            GameObject chosenGrassPrefab = (Random.value < 0.5f) ? grassPrefabHigh : grassPrefabLow;
            GameObject grassInstance = Instantiate(chosenGrassPrefab, worldPos, Quaternion.identity);
            float scaleFactor = Random.Range(2.5f, 3.5f);
            grassInstance.transform.localScale = Vector3.one * scaleFactor;

            activeGrassCounter++;
            totalGrassCounter++;
            grassInstance.name = "Grass_" + totalGrassCounter.ToString("D5");

            if (navMeshParent != null)
            {
                grassInstance.transform.SetParent(navMeshParent.transform);
                int vegetationLayer = LayerMask.NameToLayer("Vegetation");
                SetLayer(grassInstance, vegetationLayer);
            }

            Collider grassCollider = grassInstance.GetComponent<Collider>();
            if (grassCollider != null)
                grassCollider.enabled = false;
            
            if (chosenGrassPrefab == grassPrefabLow)
                grassInstancesLow.Add(grassInstance);
            else
                grassInstancesHigh.Add(grassInstance);
        }

        // Generar hierba completamente aleatoria
        for (int i = 0; i < completelyRandomCount; i++)
        {
            int x = Random.Range(generationMargin, width  - generationMargin);
            int y = Random.Range(generationMargin, height - generationMargin);
            Vector2Int pos = new Vector2Int(x, y);

            if (pos.x < generationMargin || pos.x >= width  - generationMargin ||
                pos.y < generationMargin || pos.y >= height - generationMargin)
                continue;


            GameObject chosenGrassPrefab = (Random.value < 0.5f) ? grassPrefabHigh : grassPrefabLow;
            GameObject grassInstance = Instantiate(chosenGrassPrefab, GetWorldPosition(pos), Quaternion.identity);
            float scaleFactor = Random.Range(2.5f, 3.5f);
            grassInstance.transform.localScale = Vector3.one * scaleFactor;

            activeGrassCounter++;
            totalGrassCounter++;
            grassInstance.name = "Grass_" + totalGrassCounter.ToString("D5");

            if (navMeshParent != null)
            {
                grassInstance.transform.SetParent(navMeshParent.transform);
                int vegetationLayer = LayerMask.NameToLayer("Vegetation");
                SetLayer(grassInstance, vegetationLayer);
            }

            Collider grassCollider = grassInstance.GetComponent<Collider>();
            if (grassCollider != null)
                grassCollider.enabled = false;
            if (chosenGrassPrefab == grassPrefabLow)
                grassInstancesLow.Add(grassInstance);
            else
                grassInstancesHigh.Add(grassInstance);
        }
    }

    private IEnumerator GenerateGrassOverTime()
    {
        float interval = 60f / grassPerMinute;  // Intervalo en segundos entre cada hierba
        while (true)
        {
            yield return new WaitForSeconds(interval);
            
            Vector3 spawnPos;
            // Si hay posiciones de respawn y se cumple la probabilidad, usa una de ellas
            if (grassRespawnPositions.Count > 0 && Random.value < grassRespawnProbability)
            {
                int index = Random.Range(0, grassRespawnPositions.Count);
                spawnPos = grassRespawnPositions[index];
                grassRespawnPositions.RemoveAt(index);
            }
            else
            {
                int x = Random.Range(generationMargin, width  - generationMargin);
                int y = Random.Range(generationMargin, height - generationMargin);
                Vector2Int pos = new Vector2Int(x, y);
                if (pos.x < generationMargin || pos.x >= width  - generationMargin ||
                    pos.y < generationMargin || pos.y >= height - generationMargin)
                    continue;

                spawnPos = GetWorldPosition(pos);
            }
            
            GameObject chosenGrassPrefab = (Random.value < 0.5f) ? grassPrefabHigh : grassPrefabLow;
            GameObject grassInstance = Instantiate(chosenGrassPrefab, spawnPos, Quaternion.identity);
            float scaleFactor = Random.Range(2.5f, 3.5f);
            grassInstance.transform.localScale = Vector3.one * scaleFactor;

            activeGrassCounter++;
            totalGrassCounter++;
            grassInstance.name = "Grass_" + totalGrassCounter.ToString("D5");

            if (navMeshParent != null)
            {
                grassInstance.transform.SetParent(navMeshParent.transform);
                int vegetationLayer = LayerMask.NameToLayer("Vegetation");
                SetLayer(grassInstance, vegetationLayer);
            }
            
            Collider grassCollider = grassInstance.GetComponent<Collider>();
            if (grassCollider != null)
            {
                grassCollider.enabled = false;
            }
            
            if (chosenGrassPrefab == grassPrefabLow)
                grassInstancesLow.Add(grassInstance);
            else
                grassInstancesHigh.Add(grassInstance);
        }
    }



    void GenerateMushrooms()
    {
        // Generar setas cerca de los árboles, con menor probabilidad por árbol
        foreach (Vector2Int treePos in placedTreePositions)
        {
            if (Random.value < mushroomsPerTree)
            {
                float radius = Random.Range(1f, 4f);
                float angle = Random.Range(0f, Mathf.PI * 2f);
                int offsetX = Mathf.RoundToInt(radius * Mathf.Cos(angle));
                int offsetY = Mathf.RoundToInt(radius * Mathf.Sin(angle));
                Vector2Int pos = new Vector2Int(treePos.x + offsetX, treePos.y + offsetY);

                if (pos.x < generationMargin || pos.x >= width  - generationMargin ||
                    pos.y < generationMargin || pos.y >= height - generationMargin)
                    continue;


                GameObject chosenMushroomsPrefab = (Random.value < 0.5f) ? mushroomPrefabRed : mushroomPrefabOrange;
                GameObject mushroomInstance = Instantiate(chosenMushroomsPrefab, GetWorldPosition(pos), Quaternion.identity);
                float scaleFactor = Random.Range(1.0f, 1.5f);
                mushroomInstance.transform.localScale = Vector3.one * scaleFactor;

                if (navMeshParent != null)
                {
                    mushroomInstance.transform.SetParent(navMeshParent.transform);
                    int vegetationLayer = LayerMask.NameToLayer("Vegetation");
                    SetLayer(mushroomInstance, vegetationLayer);
                }
            }
        }

        for (int i = 0; i < randomMushroomsCount; i++)
        {
            int x = Random.Range(generationMargin, width  - generationMargin);
            int y = Random.Range(generationMargin, height - generationMargin);
            Vector2Int pos = new Vector2Int(x, y);
            if (pos.x < generationMargin || pos.x >= width  - generationMargin ||
                pos.y < generationMargin || pos.y >= height - generationMargin)
                continue;


            GameObject chosenMushroomsPrefab = (Random.value < 0.5f) ? mushroomPrefabRed : mushroomPrefabOrange;
            GameObject mushroomInstance = Instantiate(chosenMushroomsPrefab, GetWorldPosition(pos), Quaternion.identity);
            float scaleFactor = Random.Range(1.0f, 1.5f);
            mushroomInstance.transform.localScale = Vector3.one * scaleFactor;

            if (navMeshParent != null)
            {
                mushroomInstance.transform.SetParent(navMeshParent.transform);
                int vegetationLayer = LayerMask.NameToLayer("Vegetation");
                SetLayer(mushroomInstance, vegetationLayer);
            }
        }
    }

    private void SetLayer(GameObject obj, int layer)
    {
        if (obj == null)
            return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
                SetLayer(child.gameObject, layer);
        }
    }

    public List<GameObject> GetAllGrass()
    {
        List<GameObject> allGrass = new List<GameObject>();
        allGrass.AddRange(grassInstancesLow);
        allGrass.AddRange(grassInstancesHigh);
        return allGrass;
    }
}
