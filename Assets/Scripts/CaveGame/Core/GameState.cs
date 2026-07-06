namespace CaveGame
{
    /// <summary>
    /// Die vier Spielzustände laut Aufgaben-PDF.
    /// Der <see cref="GameManager"/> hält genau einen aktuellen Zustand und
    /// steuert daraus Wände, UI, Leben und Punkte.
    /// </summary>
    public enum GameState
    {
        /// <summary>Startbildschirm sichtbar, physischer Knopf aktiv, keine Wände.</summary>
        MainMenu,

        /// <summary>Es wurde Start ausgelöst – wir warten auf eine zuverlässig getrackte Person.</summary>
        WaitingForPlayerTracking,

        /// <summary>Laufendes Spiel: Wände spawnen, Punkte/Leben werden gezählt.</summary>
        Playing,

        /// <summary>0 Leben: Wände stoppen, Highscore wird gespeichert, Game-Over-UI.</summary>
        GameOver
    }
}
