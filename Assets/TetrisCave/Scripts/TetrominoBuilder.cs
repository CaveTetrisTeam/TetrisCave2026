using UnityEngine;

// Baut/konfiguriert Tetromino-Formen aus dem Block_Unit-Prefab.
// Build() erzeugt die 4 Würfel EINMAL, Apply() prägt danach jede beliebige Form auf.
public class TetrominoBuilder : MonoBehaviour
{
    [Header("Bausteine")]
    public GameObject blockUnitPrefab;

    [Header("Leuchtkraft")]
    [Tooltip("Multiplikator für die Emission. >1 wirkt später mit Bloom als echtes Leuchten.")]
    public float emissionIntensity = 2f;

    static readonly int ColorID    = Shader.PropertyToID("_Color");
    static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock _mpb;

    // Erzeugt ein Wurzel-Objekt mit GENAU 4 Block_Units und prägt die Start-Form auf.
    // Wird vom Pool nur EINMAL pro Pool-Objekt aufgerufen.
    public GameObject Build(TetrominoDefinition def)
    {
        if (blockUnitPrefab == null)
        {
            Debug.LogError("[TetrominoBuilder] Block_Unit-Prefab fehlt!");
            return null;
        }

        GameObject root = new GameObject("Tetromino");
        for (int i = 0; i < 4; i++)
            Instantiate(blockUnitPrefab, root.transform);

        Apply(root, def);
        return root;
    }

    // Konfiguriert ein bestehendes Wurzel-Objekt (mit 4 Kindern) auf eine neue Form um.
    // Wird bei JEDEM Pool.Get() aufgerufen.
    public void Apply(GameObject root, TetrominoDefinition def)
    {
        if (root == null || def == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        Vector3[] offsets = def.GetCenteredOffsets();
        int count = Mathf.Min(offsets.Length, root.transform.childCount);

        for (int i = 0; i < count; i++)
        {
            Transform unit = root.transform.GetChild(i);
            unit.localPosition = offsets[i];
            unit.localRotation = Quaternion.identity;
            ApplyColor(unit.gameObject, def.color);
        }

        root.name = "Tetromino_" + def.shapeName;
    }

    private void ApplyColor(GameObject unit, Color color)
    {
        MeshRenderer renderer = unit.GetComponentInChildren<MeshRenderer>();
        if (renderer == null) return;

        renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(ColorID, color);
        _mpb.SetColor(EmissionID, color * emissionIntensity);
        renderer.SetPropertyBlock(_mpb);
    }
}