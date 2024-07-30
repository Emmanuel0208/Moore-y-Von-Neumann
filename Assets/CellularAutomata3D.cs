using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CellularAutomata3D : MonoBehaviour
{
    // Variables públicas para ser ajustadas en el Inspector
    public Vector3Int gridSize; // Tamaño del grid en 3D
    public float cubeSize = 1f; // Tamaño de cada cubo
    public Material cubeMaterial; // Material del cubo
    public Transform cubesParent; // Objeto padre que contendrá todos los cubos
    public int[] reproduceThresholds; // Umbrales para la reproducción
    public int[] surviveThresholds; // Umbrales para la supervivencia
    public int[] dyingThresholds; // Umbrales para la muerte
    public Color aliveColor = Color.blue; // Color para cubos vivos
    public Color dyingColor = Color.gray; // Color para cubos muriendo
    public NeighborMethod neighborMethod = NeighborMethod.Moore; // Método de vecinos: Moore o Von Neumann
    public float updateInterval = 1f; // Intervalo de actualización en segundos
    public float distanceFromCamera = 100f; // Distancia para alejar el centro de los cubos
    public Camera mainCamera; // Cámara principal

    // Elementos de la UI
    public TMP_InputField inputX;
    public TMP_InputField inputY;
    public TMP_InputField inputZ;
    public TMP_InputField inputReproduceThreshold;
    public TMP_InputField inputSurviveThreshold;
    public TMP_InputField inputDyingThreshold;
    public TMP_Dropdown dropdownNeighborMethod;
    public Toggle centerOnlyToggle; 
    public Button playButton;

    private GameObject[,,] cubes; // Array  para los cubos
    private int[,,] states; // Array para estados de los cubos (0: muerto, 1: vivo, 2: muriendo)
    private int[,,] lifeTimes; // Array para los cubos que estan muriendo
    private float timer;
    private bool isSimulationRunning = false;

    // Enum
    public enum NeighborMethod
    {
        Moore,
        VonNeumann
    }

    void Start()
    {
        playButton.onClick.AddListener(StartSimulation); // Agrega la función StartSimulation al botón de play
    }

    void InitializeCubes()
    {
        if (gridSize.x <= 0 || gridSize.y <= 0 || gridSize.z <= 0)
        {
            Debug.LogError("Grid size must be greater than zero.");
            return;
        }

        ClearCubes(); // Limpia los cubos anteriores
        cubes = new GameObject[gridSize.x, gridSize.y, gridSize.z]; // Inicializa el array de cubos
        states = new int[gridSize.x, gridSize.y, gridSize.z]; // Inicializa el array de estados
        lifeTimes = new int[gridSize.x, gridSize.y, gridSize.z]; // Inicializa el array de tiempos de vida

        // Calcular el centro del grid y ajustar la posición en base a la distancia de alejamiento
        Vector3 gridCenter = new Vector3((gridSize.x - 1) * cubeSize / 2, (gridSize.y - 1) * cubeSize / 2, (gridSize.z - 1) * cubeSize / 2);
        Vector3 offset = mainCamera.transform.forward * distanceFromCamera; // Calcula el offset para alejar la grilla

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    Vector3 position = new Vector3(x * cubeSize, y * cubeSize, z * cubeSize) - gridCenter + offset; // Calcula la posición de cada cubo
                    GameObject cube = CreateCube(position); // Crea el cubo en la posición calculada
                    cube.transform.SetParent(cubesParent); // Asigna el cubo al padre
                    cube.SetActive(false); // Desactiva el cubo por defecto
                    cubes[x, y, z] = cube; // Almacena el cubo en el array
                }
            }
        }
    }

    GameObject CreateCube(Vector3 position)
    {
        GameObject cube = new GameObject("Cube", typeof(MeshFilter), typeof(MeshRenderer));
        cube.transform.position = position; // Asigna la posición al cubo
        cube.transform.localScale = Vector3.one * cubeSize; // Ajusta la escala del cubo
        MeshFilter meshFilter = cube.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = cube.GetComponent<MeshRenderer>();
        meshRenderer.material = cubeMaterial; // Asigna el material al cubo

        // Definición de los vértices y triángulos para el cubo
        Mesh mesh = new Mesh();
        Vector3[] vertices = {
            new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 1), new Vector3(0, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 0, 1)
        };
        int[] triangles = {
            0, 1, 2, 0, 2, 3, 1, 0, 5, 1, 5, 4, 2, 6, 3, 6, 7, 3, 1, 4, 2, 4, 6, 2,
            5, 0, 3, 5, 3, 7, 4, 5, 7, 4, 7, 6
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh; // Asigna la  al cubo
        return cube;
    }

    void ClearCubes()
    {
        if (cubesParent != null)
        {
            foreach (Transform child in cubesParent)
            {
                Destroy(child.gameObject); // Destruye todos los cubos hijos del padre
            }
        }
    }

    void ActivateInitialCubes()
    {
        if (centerOnlyToggle.isOn)
        {
            int centerX = gridSize.x / 2;
            int centerY = gridSize.y / 2;
            int centerZ = gridSize.z / 2;

            // Activa los cubos en el centro de la grilla
            for (int x = centerX - 1; x <= centerX + 1; x++)
            {
                for (int y = centerY - 1; y <= centerY + 1; y++)
                {
                    for (int z = centerZ - 1; z <= centerZ + 1; z++)
                    {
                        if (Random.value < 0.3f) // probabilidad de estar vivo
                        {
                            cubes[x, y, z].SetActive(true);
                            states[x, y, z] = 1;
                            lifeTimes[x, y, z] = GetRandomDyingThreshold(); // Inicializar tiempo de vida para la transición
                            UpdateCubeColor(x, y, z); // Actualiza el color del cubo
                        }
                    }
                }
            }
        }
        else
        {
            // Activa cubos aleatoriamente en toda la grilla
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int z = 0; z < gridSize.z; z++)
                    {
                        if (Random.value < 0.3f) // probabilidad de estar vivo
                        {
                            cubes[x, y, z].SetActive(true);
                            states[x, y, z] = 1;
                            lifeTimes[x, y, z] = GetRandomDyingThreshold(); // Inicializar tiempo de vida para la transición
                            UpdateCubeColor(x, y, z); // Actualiza el color del cubo
                        }
                    }
                }
            }
        }
    }

    void Update()
    {
        if (isSimulationRunning)
        {
            timer -= Time.deltaTime; // reduce el temporizador
            if (timer <= 0f)
            {
                UpdateCubes(); // Actualiza los cubos cuando el temporizador llega a 0
                timer = updateInterval; // Reinicia el temporizador
            }
        }
    }

    void UpdateCubes()
    {
        int[,,] newStates = new int[gridSize.x, gridSize.y, gridSize.z]; // Array para los nuevos estados de los cubos

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    // Cuenta los vecinos vivos
                    int aliveNeighbors = (neighborMethod == NeighborMethod.Moore)
                                         ? CountAliveNeighborsMoore(x, y, z)
                                         : CountAliveNeighborsVonNeumann(x, y, z);

                    if (states[x, y, z] == 1) // Vivo
                    {
                        if (!IsInThresholdRange(aliveNeighbors, surviveThresholds))
                        {
                            newStates[x, y, z] = 2; // Muriendo
                            lifeTimes[x, y, z] = GetRandomDyingThreshold(); // Reiniciar tiempo de vida para la transición
                        }
                        else
                        {
                            newStates[x, y, z] = 1; // Sobrevive
                        }
                    }
                    else if (states[x, y, z] == 2) // Muriendo
                    {
                        lifeTimes[x, y, z] -= 1; // Reducir tiempo de vida en cada frame
                        if (lifeTimes[x, y, z] <= 0)
                        {
                            newStates[x, y, z] = 0; // Muere
                        }
                        else
                        {
                            newStates[x, y, z] = 2; // Sigue muriendo
                        }
                    }
                    else // Muerto
                    {
                        if (IsInThresholdRange(aliveNeighbors, reproduceThresholds))
                        {
                            newStates[x, y, z] = 1; // Revive
                        }
                        else
                        {
                            newStates[x, y, z] = 0; // Permanece muerto
                        }
                    }
                }
            }
        }

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    states[x, y, z] = newStates[x, y, z]; // Actualiza los estados
                    bool isActive = states[x, y, z] != 0; // Verifica si el cubo debe estar activo
                    if (cubes[x, y, z].activeSelf != isActive)
                    {
                        cubes[x, y, z].SetActive(isActive); // Activa o desactiva el cubo según su estado
                    }
                    if (isActive)
                    {
                        UpdateCubeColor(x, y, z); // Actualiza el color del cubo
                    }
                }
            }
        }
    }

    // Método Moore
    int CountAliveNeighborsMoore(int x, int y, int z)
    {
        int aliveCount = 0;
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                for (int k = -1; k <= 1; k++)
                {
                    if (i == 0 && j == 0 && k == 0) continue;

                    int nx = x + i;
                    int ny = y + j;
                    int nz = z + k;

                    if (nx >= 0 && ny >= 0 && nz >= 0 && nx < gridSize.x && ny < gridSize.y && nz < gridSize.z)
                    {
                        if (states[nx, ny, nz] == 1 || states[nx, ny, nz] == 2) // Contar vivos y muriendo
                        {
                            aliveCount++;
                        }
                    }
                }
            }
        }
        return aliveCount;
    }

    // Método Von Neumann
    int CountAliveNeighborsVonNeumann(int x, int y, int z)
    {
        int aliveCount = 0;
        int[] dx = { -1, 1, 0, 0, 0, 0 };
        int[] dy = { 0, 0, -1, 1, 0, 0 };
        int[] dz = { 0, 0, 0, 0, -1, 1 };

        for (int i = 0; i < 6; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];
            int nz = z + dz[i];

            if (nx >= 0 && ny >= 0 && nz >= 0 && nx < gridSize.x && ny < gridSize.y && nz < gridSize.z)
            {
                if (states[nx, ny, nz] == 1 || states[nx, ny, nz] == 2) // Contar vivos y muriendo
                {
                    aliveCount++;
                }
            }
        }

        return aliveCount;
    }

    // Método para verificar si un valor está dentro del rango de umbrales
    bool IsInThresholdRange(int value, int[] thresholds)
    {
        foreach (int threshold in thresholds)
        {
            if (value == threshold)
            {
                return true;
            }
        }
        return false;
    }

    // Método para obtener un umbral de muerte aleatorio
    int GetRandomDyingThreshold()
    {
        return dyingThresholds[Random.Range(0, dyingThresholds.Length)];
    }

    // Método para actualizar el color del cubo según su estado
    void UpdateCubeColor(int x, int y, int z)
    {
        Renderer renderer = cubes[x, y, z].GetComponent<Renderer>();
        if (states[x, y, z] == 1) // Vivo
        {
            renderer.material.color = aliveColor;
        }
        else if (states[x, y, z] == 2) // Muriendo
        {
            float lifeRatio = Mathf.Clamp01((float)lifeTimes[x, y, z] / GetRandomDyingThreshold()); // Proporción de vida restante
            Color color = Color.Lerp(dyingColor, aliveColor, lifeRatio);
            color.a = lifeRatio; // Ajustar alfa
            renderer.material.color = color;
        }
    }

    // Método para iniciar la simulación
    public void StartSimulation()
    {
        // Leer valores de los campos de entrada
        int x = int.Parse(inputX.text);
        int y = int.Parse(inputY.text);
        int z = int.Parse(inputZ.text);
        gridSize = new Vector3Int(x, y, z);

        if (gridSize.x <= 0 || gridSize.y <= 0 || gridSize.z <= 0)
        {
            Debug.LogError("Grid size must be greater than zero.");
            return;
        }

        reproduceThresholds = ParseThresholds(inputReproduceThreshold.text);
        surviveThresholds = ParseThresholds(inputSurviveThreshold.text);
        dyingThresholds = ParseThresholds(inputDyingThreshold.text);

        // Leer el método de vecinos seleccionado
        neighborMethod = (NeighborMethod)dropdownNeighborMethod.value;

        // Inicializar y comenzar la simulación
        InitializeCubes();
        ActivateInitialCubes();
        isSimulationRunning = true;
        timer = updateInterval;
    }

    // Método para parsear umbrales de entrada
    int[] ParseThresholds(string input)
    {
        string[] parts = input.Split(',');
        int[] thresholds = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            thresholds[i] = int.Parse(parts[i]);
        }
        return thresholds;
    }
}
