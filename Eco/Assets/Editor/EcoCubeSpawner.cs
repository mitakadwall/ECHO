using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EcoCubeSpawner
{
    static EcoCubeSpawner()
    {
        EditorApplication.delayCall += SpawnCubeOnce;
    }

    private static void SpawnCubeOnce()
    {
        if (GameObject.Find("EcoTestCube") != null) return;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "EcoTestCube";
        cube.transform.position = new Vector3(0, 0.5f, 0);

        // Marca a cena como alterada para salvar
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
        );
        Debug.Log("[Eco] Cubo de teste inserido com sucesso na cena via Antigravity!");
    }
}
