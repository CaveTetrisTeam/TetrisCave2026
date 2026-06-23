using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Spawnt im Zustand <see cref="GameState.Playing"/> fortlaufend Wände mit
    /// posenförmiger Öffnung, die auf den Spieler zufliegen. Schwierigkeit steigt
    /// über die Zeit (schneller + dichter). Lädt die Wand-Sprites automatisch aus
    /// <c>Resources/Walls</c> (oder dem Inspector-Array). Ohne Grafiken wird eine
    /// prozedurale Platzhalter-Wand erzeugt, damit alles auch ohne Art testbar ist.
    /// </summary>
    public sealed class WallSpawner : MonoBehaviour
    {
        [Header("Wand-Grafiken")]
        [Tooltip("Optional manuell: Wand-Sprites. Leer = automatisch aus Resources/<resourcesFolder> laden.")]
        public Sprite[] wallSprites;
        [Tooltip("Resources-Unterordner mit den Wand-PNGs (als Texture2D geladen).")]
        public string resourcesFolder = "Walls";

        [Header("Platzierung (Weltkoordinaten)")]
        [Tooltip("Z-Position, an der Wände erscheinen (weit vorne).")]
        public float spawnZ = 40f;
        [Tooltip("Z-Position der Spielerebene (dort steht der Avatar).")]
        public float playerPlaneZ = 0f;
        [Tooltip("Z-Position, an der Wände hinter dem Spieler verschwinden.")]
        public float despawnZ = -20f;
        [Tooltip("Wie weit hinter der Spielerebene gewertet wird (Wand muss durch sein).")]
        public float resolveMargin = 1.0f;
        [Tooltip("Mittelpunkt der Wand (X,Y) – auf Körpermitte des getrackten Spielers kalibrieren.")]
        public Vector2 wallCenter = new Vector2(0f, 1.5f);
        [Tooltip("Gewünschte Welthöhe einer Wand (auf Spielergröße/Reichweite kalibrieren).")]
        public float targetWorldHeight = 3.2f;
        [Tooltip("Y-Drehung der Wand. Bei gespiegelter/abgewandter Darstellung 180 versuchen.")]
        public float wallYRotation = 0f;

        [Header("Collider-Generierung")]
        [Tooltip("Spalten des Abtast-Gitters. Höher = genauer, aber mehr Collider.")]
        public int gridColumns = 48;
        [Range(0f, 1f)]
        [Tooltip("Ab welcher Deckkraft ein Pixel als massive Wand gilt.")]
        public float alphaThreshold = 0.5f;
        [Tooltip("Welt-Tiefe der Wand-Collider (Z).")]
        public float colliderThickness = 0.3f;
        [Tooltip("Layer der Avatar-Körperteile. Leer = automatisch Layer 'Player'.")]
        public LayerMask playerLayers;

        [Header("Geschwindigkeit / Schwierigkeit")]
        public float baseSpeed = 5f;
        public float maxSpeed = 14f;
        public float speedGrowthPerSecond = 0.15f;

        [Header("Spawn-Frequenz")]
        public float baseInterval = 3.0f;
        public float minInterval = 1.2f;
        [Tooltip("Über wie viele Sekunden das Intervall von base auf min sinkt.")]
        public float rampSeconds = 90f;

        private readonly List<GameObject> m_ActiveWalls = new List<GameObject>();
        private Sprite[] m_Sprites;
        private Coroutine m_SpawnRoutine;
        private float m_PlayStartTime;

        private void Awake()
        {
            EnsureSprites();
        }

        // ---------------------------------------------------------------------
        // Steuerung durch den GameManager
        // ---------------------------------------------------------------------

        public void StartSpawning()
        {
            m_PlayStartTime = Time.time;
            if (m_SpawnRoutine == null)
            {
                m_SpawnRoutine = StartCoroutine(SpawnLoop());
            }
        }

        public void StopSpawning()
        {
            if (m_SpawnRoutine != null)
            {
                StopCoroutine(m_SpawnRoutine);
                m_SpawnRoutine = null;
            }
        }

        public void ClearAllWalls()
        {
            for (int i = m_ActiveWalls.Count - 1; i >= 0; i--)
            {
                if (m_ActiveWalls[i] != null)
                {
                    Destroy(m_ActiveWalls[i]);
                }
            }
            m_ActiveWalls.Clear();
        }

        public void FreezeAllWalls()
        {
            foreach (var wall in m_ActiveWalls)
            {
                if (wall != null && wall.TryGetComponent<WallMover>(out var mover))
                {
                    mover.Freeze();
                }
            }
        }

        // ---------------------------------------------------------------------
        // Spawn-Logik
        // ---------------------------------------------------------------------

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                SpawnOne();
                yield return new WaitForSeconds(CurrentInterval());
            }
        }

        private float Elapsed() => Time.time - m_PlayStartTime;

        private float CurrentSpeed()
        {
            return Mathf.Min(maxSpeed, baseSpeed + speedGrowthPerSecond * Elapsed());
        }

        private float CurrentInterval()
        {
            float t = rampSeconds > 0f ? Mathf.Clamp01(Elapsed() / rampSeconds) : 1f;
            return Mathf.Lerp(baseInterval, minInterval, t);
        }

        private void SpawnOne()
        {
            if (m_Sprites == null || m_Sprites.Length == 0)
            {
                return;
            }

            var sprite = m_Sprites[Random.Range(0, m_Sprites.Length)];
            var wall = CreateWall(sprite);
            m_ActiveWalls.Add(wall);

            wall.GetComponent<WallMover>().Launch(
                CurrentSpeed(), playerPlaneZ, despawnZ, resolveMargin, ReleaseWall);
        }

        private GameObject CreateWall(Sprite sprite)
        {
            var go = new GameObject("Wall_" + sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(wallCenter.x, wallCenter.y, spawnZ);
            go.transform.rotation = Quaternion.Euler(0f, wallYRotation, 0f);

            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer >= 0)
            {
                go.layer = wallLayer;
            }

            // Maßstab auf gewünschte Welthöhe (Sprite ist unskaliert sprite.bounds groß).
            float localHeight = Mathf.Max(0.0001f, sprite.bounds.size.y);
            float scale = targetWorldHeight / localHeight;
            go.transform.localScale = new Vector3(scale, scale, scale);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            // Kinematischer Rigidbody, damit Trigger-Events sicher feuern.
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            go.AddComponent<Wall>();
            var hitZone = go.AddComponent<WallHitZone>();
            hitZone.playerLayers = playerLayers;

            // Collider aus dem Alphakanal erzeugen (lokale Dicke = Weltdicke / Skalierung).
            float localThickness = colliderThickness / Mathf.Max(0.0001f, scale);
            WallColliderBuilder.Build(go, sprite, gridColumns, alphaThreshold, localThickness, true);

            go.AddComponent<WallMover>();
            return go;
        }

        private void ReleaseWall(GameObject wall)
        {
            m_ActiveWalls.Remove(wall);
            if (wall != null)
            {
                Destroy(wall);
            }
        }

        // ---------------------------------------------------------------------
        // Sprites laden / Platzhalter
        // ---------------------------------------------------------------------

        private void EnsureSprites()
        {
            if (wallSprites != null && wallSprites.Length > 0)
            {
                m_Sprites = wallSprites;
                return;
            }

            // Wand-Texturen aus Resources laden und Sprites erzeugen.
            var textures = Resources.LoadAll<Texture2D>(resourcesFolder);
            if (textures != null && textures.Length > 0)
            {
                System.Array.Sort(textures, (a, b) => string.CompareOrdinal(a.name, b.name));
                var sprites = new List<Sprite>(textures.Length);
                foreach (var tex in textures)
                {
                    sprites.Add(Sprite.Create(
                        tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f));
                }
                m_Sprites = sprites.ToArray();
                return;
            }

            Debug.LogWarning($"[WallSpawner] Keine Wand-Sprites in 'Resources/{resourcesFolder}' gefunden – " +
                             "nutze prozedurale Platzhalter-Wand.");
            m_Sprites = new[] { CreatePlaceholderWall() };
        }

        /// <summary>Graue Wand mit rechteckiger Öffnung – nur als Fallback ohne Grafiken.</summary>
        private static Sprite CreatePlaceholderWall()
        {
            const int w = 1024;
            const int h = 768;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "PlaceholderWall" };

            var wall = new Color32(70, 76, 92, 255);
            var hole = new Color32(0, 0, 0, 0);
            var pixels = new Color32[w * h];

            // Rechteckige Öffnung in der Mitte (Platzhalter für eine Pose).
            int holeMinX = (int)(w * 0.40f), holeMaxX = (int)(w * 0.60f);
            int holeMinY = (int)(h * 0.20f), holeMaxY = (int)(h * 0.85f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inHole = x >= holeMinX && x < holeMaxX && y >= holeMinY && y < holeMaxY;
                    pixels[y * w + x] = inHole ? hole : wall;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
