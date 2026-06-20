using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class BlockSpawner : MonoBehaviour
{
    [Header("Referenzen")]
    public TetrominoBuilder builder;
    public TetrominoDefinition[] definitions;   // die 7 Form-Assets

    [Header("Spawn-Timing (Sekunden)")]
    public float minSpawnDelay = 0.5f;
    public float maxSpawnDelay = 2.0f;

    [Header("Flug-Geschwindigkeit")]
    public float minSpeed = 5f;
    public float maxSpeed = 12f;

    [Header("Taumel-Geschwindigkeit (Grad/Sek)")]
    public float minTumble = 10f;
    public float maxTumble = 45f;

    [Header("Korridore (Mitte bleibt FREI!)")]
    [Tooltip("Kleinster Abstand zur Mitte (innere Korridor-Kante).")]
    public float corridorInner = 3f;
    [Tooltip("Größter Abstand zur Mitte (äußere Korridor-Kante).")]
    public float corridorOuter = 6f;
    public float spawnHeightMin = -1f;
    public float spawnHeightMax = 4f;

    [Header("Z-Achse (Start vorne / Ende hinten)")]
    public float spawnZ   = 40f;   // weit vorne
    public float despawnZ = -20f;  // hinter der Plattform

    [Header("Einblenden")]
    [Tooltip("Strecke (in Units) nach dem Spawn, über die der Block von unsichtbar auf voll einblendet.")]
    public float fadeDistance = 40f;

    [Header("Pool")]
    public int defaultCapacity = 20;
    public int maxPoolSize = 100;

    private ObjectPool<GameObject> _pool;

    void Awake()
    {
        _pool = new ObjectPool<GameObject>(
            createFunc:       CreatePooledBlock,
            actionOnGet:      obj => obj.SetActive(true),
            actionOnRelease:  obj => obj.SetActive(false),
            actionOnDestroy:  obj => Destroy(obj),
            collectionCheck:  true,
            defaultCapacity:  defaultCapacity,
            maxSize:          maxPoolSize
        );
    }

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    // Wird vom Pool nur aufgerufen, wenn KEIN freies Objekt verfügbar ist.
    private GameObject CreatePooledBlock()
    {
        GameObject root = builder.Build(definitions[0]); // Start-Form egal, wird neu geprägt
        root.AddComponent<BlockMover>();
        root.SetActive(false);
        return root;
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minSpawnDelay, maxSpawnDelay));
            SpawnOne();
        }
    }

    private void SpawnOne()
    {
        if (builder == null || definitions == null || definitions.Length == 0) return;

        GameObject block = _pool.Get();

        // Recycelt die 4 Würfel
        TetrominoDefinition def = definitions[Random.Range(0, definitions.Length)];
        builder.Apply(block, def);

        // Startposition: linker ODER rechter Korridor -> Mitte bleibt frei
        float x = Random.Range(corridorInner, corridorOuter);
        if (Random.value < 0.5f) x = -x;                 
        float y = Random.Range(spawnHeightMin, spawnHeightMax);
        block.transform.position = new Vector3(x, y, spawnZ);
        block.transform.rotation = Random.rotation;      // zufällige Start-Ausrichtung

        // Bewegung starten
        float speed  = Random.Range(minSpeed, maxSpeed);
        float tumble = Random.Range(minTumble, maxTumble);
        block.GetComponent<BlockMover>().Launch(
            speed, despawnZ, tumble,
            def.color, builder.emissionIntensity, fadeDistance,
            ReleaseBlock);
    }

    private void ReleaseBlock(GameObject block)
    {
        _pool.Release(block);
    }
}