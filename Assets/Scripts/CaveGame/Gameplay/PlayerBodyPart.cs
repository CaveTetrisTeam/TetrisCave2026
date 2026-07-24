using UnityEngine;

namespace CaveGame
{
    /// <summary>
    /// Marker an jedem Avatar-Körperteil. Damit erkennt <see cref="WallHitZone"/> einen
    /// Treffer zuverlässig – unabhängig davon, ob der Layer "Player" bereits angelegt ist.
    /// Wird von <c>PositionTransferMultiple</c> automatisch an die Körperteile gehängt.
    /// </summary>
    public sealed class PlayerBodyPart : MonoBehaviour
    {
    }
}
