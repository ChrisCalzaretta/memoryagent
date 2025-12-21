namespace GeneratedApp.Models
{
    public enum GamePhase
    {
        NotStarted,
        PlayerTurn,
        DealerTurn,
        GameOver
    }

    public enum GameResult
    {
        None,
        PlayerWin,
        DealerWin,
        Push,
        PlayerBlackjack
    }

    public class GameStats
    {
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Pushes { get; set; }
        public int GamesPlayed => Wins + Losses + Pushes;
        public double WinPercentage => GamesPlayed > 0 ? (double)Wins / GamesPlayed * 100 : 0;
    }
}