using GeneratedApp.Models;
using System.Threading.Tasks;

namespace GeneratedApp.Services
{
    public class BlackjackService
    {
        private readonly DeckService _deckService;
        
        public Hand PlayerHand { get; private set; }
        public Hand DealerHand { get; private set; }
        public GamePhase CurrentPhase { get; private set; }
        public GameResult GameResult { get; private set; }
        public GameStats Stats { get; private set; }
        public bool CanDoubleDown { get; private set; }
        public bool CanSplit { get; private set; }

        public event Action? GameStateChanged;

        public BlackjackService(DeckService deckService)
        {
            _deckService = deckService;
            PlayerHand = new Hand();
            DealerHand = new Hand { IsDealer = true };
            Stats = new GameStats();
            CurrentPhase = GamePhase.NotStarted;
        }

        public void StartNewGame()
        {
            // Reset hands
            PlayerHand.Clear();
            DealerHand.Clear();
            
            // Reset game state
            CurrentPhase = GamePhase.PlayerTurn;
            GameResult = GameResult.None;
            CanDoubleDown = true;
            CanSplit = false;

            // Deal initial cards
            PlayerHand.AddCard(_deckService.DrawCard());
            DealerHand.AddCard(_deckService.DrawCard());
            PlayerHand.AddCard(_deckService.DrawCard());
            DealerHand.AddCard(_deckService.DrawCard());

            // Check for split possibility
            CanSplit = PlayerHand.Cards[0].Rank == PlayerHand.Cards[1].Rank;

            // Check for blackjack
            if (PlayerHand.IsBlackjack())
            {
                if (DealerHand.IsBlackjack())
                {
                    EndGame(GameResult.Push);
                }
                else
                {
                    EndGame(GameResult.PlayerBlackjack);
                }
            }
            else if (DealerHand.IsBlackjack())
            {
                EndGame(GameResult.DealerWin);
            }

            GameStateChanged?.Invoke();
        }

        public void Hit()
        {
            if (CurrentPhase != GamePhase.PlayerTurn) return;

            PlayerHand.AddCard(_deckService.DrawCard());
            CanDoubleDown = false;
            CanSplit = false;

            if (PlayerHand.IsBust())
            {
                EndGame(GameResult.DealerWin);
            }

            GameStateChanged?.Invoke();
        }

        public void Stand()
        {
            if (CurrentPhase != GamePhase.PlayerTurn) return;

            CurrentPhase = GamePhase.DealerTurn;
            CanDoubleDown = false;
            CanSplit = false;

            // Dealer plays
            PlayDealerTurn();
        }

        public void DoubleDown()
        {
            if (CurrentPhase != GamePhase.PlayerTurn || !CanDoubleDown) return;

            Hit();
            if (CurrentPhase == GamePhase.PlayerTurn && !PlayerHand.IsBust())
            {
                Stand();
            }
        }

        private async void PlayDealerTurn()
        {
            await Task.Delay(500); // Add some delay for dramatic effect

            while (DealerHand.GetValue() < 17)
            {
                DealerHand.AddCard(_deckService.DrawCard());
                GameStateChanged?.Invoke();
                await Task.Delay(1000);
            }

            // Determine winner
            if (DealerHand.IsBust())
            {
                EndGame(GameResult.PlayerWin);
            }
            else
            {
                int playerValue = PlayerHand.GetValue();
                int dealerValue = DealerHand.GetValue();

                if (playerValue > dealerValue)
                {
                    EndGame(GameResult.PlayerWin);
                }
                else if (dealerValue > playerValue)
                {
                    EndGame(GameResult.DealerWin);
                }
                else
                {
                    EndGame(GameResult.Push);
                }
            }
        }

        private void EndGame(GameResult result)
        {
            CurrentPhase = GamePhase.GameOver;
            GameResult = result;
            
            // Update statistics
            switch (result)
            {
                case GameResult.PlayerWin:
                case GameResult.PlayerBlackjack:
                    Stats.Wins++;
                    break;
                case GameResult.DealerWin:
                    Stats.Losses++;
                    break;
                case GameResult.Push:
                    Stats.Pushes++;
                    break;
            }

            GameStateChanged?.Invoke();
        }

        public string GetGameResultMessage()
        {
            return GameResult switch
            {
                GameResult.PlayerWin => "You Win!",
                GameResult.PlayerBlackjack => "Blackjack! You Win!",
                GameResult.DealerWin => "Dealer Wins!",
                GameResult.Push => "Push - It's a Tie!",
                _ => ""
            };
        }
    }
}