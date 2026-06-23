using System.Collections.Generic;
using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Erzeugt aus dem Alphakanal eines Wand-Sprites passgenaue 3D-BoxCollider:
    /// Nur die DECKENDEN (grauen) Pixel der Wand bekommen Collider – die transparente
    /// Silhouette (die "Öffnung") bleibt frei. So kann der 3D-Avatar nur durch die
    /// passende Körperhaltung hindurch, während jeder andere Körperteil-Kontakt die
    /// Wand trifft.
    ///
    /// Ein 2D-PolygonCollider würde NICHT mit dem 3D-Avatar kollidieren – deshalb das
    /// Gitter aus 3D-Boxen (zeilenweise zusammengefasst, um die Collider-Zahl gering zu halten).
    /// Das Ergebnis wird pro Sprite gecacht (die Analyse läuft nur einmal).
    /// </summary>
    public static class WallColliderBuilder
    {
        private struct BoxData
        {
            public Vector3 center;
            public Vector3 size;
        }

        private static readonly Dictionary<Sprite, BoxData[]> s_Cache = new Dictionary<Sprite, BoxData[]>();

        /// <summary>
        /// Hängt die generierten BoxCollider an <paramref name="target"/> (lokaler Raum,
        /// zentriert auf den Sprite-Mittelpunkt; skaliert mit dem Transform).
        /// </summary>
        /// <returns>Anzahl erzeugter Collider.</returns>
        public static int Build(GameObject target, Sprite sprite, int gridColumns,
                                float alphaThreshold, float localThickness, bool isTrigger)
        {
            if (target == null || sprite == null)
            {
                return 0;
            }

            var boxes = GetOrComputeBoxes(sprite, Mathf.Max(4, gridColumns),
                                          Mathf.Clamp01(alphaThreshold), localThickness);

            foreach (var box in boxes)
            {
                var collider = target.AddComponent<BoxCollider>();
                collider.center = box.center;
                collider.size = box.size;
                collider.isTrigger = isTrigger;
            }

            return boxes.Length;
        }

        private static BoxData[] GetOrComputeBoxes(Sprite sprite, int gridColumns,
                                                   float alphaThreshold, float localThickness)
        {
            if (s_Cache.TryGetValue(sprite, out var cached))
            {
                return cached;
            }

            var boxes = ComputeBoxes(sprite, gridColumns, alphaThreshold, localThickness);
            s_Cache[sprite] = boxes;
            return boxes;
        }

        private static BoxData[] ComputeBoxes(Sprite sprite, int gridColumns,
                                              float alphaThreshold, float localThickness)
        {
            // Lokale (unskalierten) Maße des Sprites, zentriert um (0,0).
            float worldW = sprite.bounds.size.x;
            float worldH = sprite.bounds.size.y;

            int gridX = gridColumns;
            int gridY = Mathf.Max(1, Mathf.RoundToInt(gridColumns * (worldH / Mathf.Max(0.0001f, worldW))));

            var pixels = ReadPixels(sprite.texture, out int texW, out int texH);
            if (pixels == null)
            {
                return new BoxData[0];
            }

            var rect = sprite.rect; // Pixelbereich des Sprites in der Textur
            byte threshold = (byte)Mathf.RoundToInt(alphaThreshold * 255f);

            // 1) Gitter abtasten: deckend (Wand) = true.
            var solid = new bool[gridX, gridY];
            for (int cy = 0; cy < gridY; cy++)
            {
                for (int cx = 0; cx < gridX; cx++)
                {
                    int px = Mathf.Clamp(
                        Mathf.RoundToInt(rect.x + (cx + 0.5f) / gridX * rect.width), 0, texW - 1);
                    int py = Mathf.Clamp(
                        Mathf.RoundToInt(rect.y + (cy + 0.5f) / gridY * rect.height), 0, texH - 1);

                    solid[cx, cy] = pixels[py * texW + px].a >= threshold;
                }
            }

            // 2) Pro Zeile zusammenhängende Wand-Zellen zu einer Box zusammenfassen.
            var result = new List<BoxData>();
            float cellW = worldW / gridX;
            float cellH = worldH / gridY;
            float halfW = worldW * 0.5f;
            float halfH = worldH * 0.5f;

            for (int cy = 0; cy < gridY; cy++)
            {
                int runStart = -1;
                for (int cx = 0; cx <= gridX; cx++)
                {
                    bool isSolid = cx < gridX && solid[cx, cy];

                    if (isSolid && runStart < 0)
                    {
                        runStart = cx;
                    }
                    else if (!isSolid && runStart >= 0)
                    {
                        int runEnd = cx - 1; // inklusiv
                        float x0 = -halfW + runStart * cellW;
                        float x1 = -halfW + (runEnd + 1) * cellW;
                        float y0 = -halfH + cy * cellH;
                        float y1 = -halfH + (cy + 1) * cellH;

                        result.Add(new BoxData
                        {
                            center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f),
                            size = new Vector3(x1 - x0, y1 - y0, localThickness)
                        });

                        runStart = -1;
                    }
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Liest die Pixel der Textur. Ist die Textur nicht "Read/Write enabled",
        /// wird per Graphics.Blit eine lesbare Kopie erzeugt (robust ohne Importeinstellung).
        /// </summary>
        private static Color32[] ReadPixels(Texture2D texture, out int width, out int height)
        {
            width = texture.width;
            height = texture.height;

            try
            {
                return texture.GetPixels32();
            }
            catch
            {
                // Fallback: über eine temporäre RenderTexture kopieren.
                var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;

                var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readable.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                var pixels = readable.GetPixels32();
                Object.Destroy(readable);
                return pixels;
            }
        }
    }
}
