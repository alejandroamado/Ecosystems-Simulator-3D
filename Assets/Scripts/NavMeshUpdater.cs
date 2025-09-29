using UnityEngine;
using Unity.AI.Navigation;
using System.Collections;

public class NavMeshUpdater : MonoBehaviour
{
    public NavMeshSurface navMeshSurface;

    void Start()
    {
        // Vacío
    }

    public void StartNavMeshUpdater()
    {
        StartCoroutine(RebakeNavMesh());
    }

    IEnumerator RebakeNavMesh()
    {
        yield return new WaitForSeconds(1f); // Espera 1 segundo para asegurarse de que las hierbas están en la escena
        navMeshSurface.BuildNavMesh(); // Vuelve a generar la NavMesh con las hierbas
        Debug.Log("NavMesh actualizada");
    }
}
