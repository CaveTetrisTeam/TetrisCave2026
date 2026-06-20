using UnityEngine;

// Tetris-Form als reine Daten:
// vier Zellen im Gitter + eine Farbe. 
[CreateAssetMenu(fileName = "Tetromino_", menuName = "TetrisCave/Tetromino Definition")]
public class TetrominoDefinition : ScriptableObject
{
    [Tooltip("Name der Form, z. B. L, T, I, O ...")]
    public string shapeName = "L";

    [Tooltip("Die vier Zellen der Form als Gitter-Koordinaten (X = rechts, Y = oben). " +
             "Eine Einheit = ein Block_Unit.")]
    public Vector2Int[] cells = new Vector2Int[4];

    [Tooltip("Farbe der Form (klassische Tetris-Farbe).")]
    public Color color = Color.white;

    // Liefert die Zell-Offsets, zentriert um den Schwerpunkt der Form.
    // Dadurch taumelt der Block später sauber um SEINE MITTE statt um eine Ecke.
    public Vector3[] GetCenteredOffsets()
    {
        Vector3[] result = new Vector3[cells.Length];

        Vector2 sum = Vector2.zero;
        foreach (var c in cells)
            sum += c;
        Vector2 center = sum / cells.Length;

        for (int i = 0; i < cells.Length; i++)
            result[i] = new Vector3(cells[i].x - center.x, cells[i].y - center.y, 0f);

        return result;
    }
}