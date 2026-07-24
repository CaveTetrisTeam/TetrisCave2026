using UnityEngine;

// Bewegt die Form nach -Z, lässt sie taumeln, blendet sie zu Beginn sanft ein
public class BlockMover : MonoBehaviour
{
    private float _speed;
    private float _despawnZ;
    private Vector3 _tumbleAxis;
    private float _tumbleSpeed;
    private System.Action<GameObject> _release;
    private bool _active;

    // Einblenden
    private Color _targetColor;
    private float _emissionIntensity;
    private float _fadeDistance;
    private float _startZ;
    private bool _fadeComplete;

    // Farb-Anwendung
    static readonly int ColorID    = Shader.PropertyToID("_Color");
    static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _mpb;
    private MeshRenderer[] _renderers;

    // Vom Spawner bei jedem Pool.Get() aufgerufen.
    public void Launch(float speed, float despawnZ, float tumbleSpeed,
                       Color targetColor, float emissionIntensity, float fadeDistance,
                       System.Action<GameObject> releaseCallback)
    {
        _speed             = speed;
        _despawnZ          = despawnZ;
        _tumbleSpeed       = tumbleSpeed;
        _tumbleAxis        = Random.onUnitSphere;
        _release           = releaseCallback;

        _targetColor       = targetColor;
        _emissionIntensity = emissionIntensity;
        _fadeDistance      = Mathf.Max(0.01f, fadeDistance); // schützt vor Division durch 0
        _startZ            = transform.position.z;
        _fadeComplete      = false;

        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        // Renderer einmalig cachen – der Block recycelt seine 4 Würfel, bleibt also gültig.
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<MeshRenderer>(true);

        _active = true;
        ApplyBrightness(0f); // sofort komplett unsichtbar starten -> kein Aufploppen
    }

    void Update()
    {
        if (!_active) return;

        // Bewegung & Taumeln
        transform.position += Vector3.back * (_speed * Time.deltaTime);
        transform.Rotate(_tumbleAxis, _tumbleSpeed * Time.deltaTime, Space.Self);

        // Distanz-basiertes Einblenden über die erste Strecke nach dem Spawn
        if (!_fadeComplete)
        {
            float traveled = _startZ - transform.position.z; // bewegt sich Richtung -Z
            float t = Mathf.Clamp01(traveled / _fadeDistance);
            ApplyBrightness(t);
            if (t >= 1f) _fadeComplete = true;
        }

        // Despawn über FESTE Weltkoordinate
        if (transform.position.z < _despawnZ)
        {
            _active = false;
            _release?.Invoke(gameObject);
        }
    }

    // t = 0 -> unsichtbar, t = 1 -> volle Neon-Farbe
    private void ApplyBrightness(float t)
    {
        Color albedo   = _targetColor * t;
        Color emission = _targetColor * (_emissionIntensity * t);

        foreach (MeshRenderer r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorID, albedo);
            _mpb.SetColor(EmissionID, emission);
            r.SetPropertyBlock(_mpb);
        }
    }
}