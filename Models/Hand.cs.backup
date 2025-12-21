using System.Collections.Generic;
using System.Linq;

namespace GeneratedApp.Models
{
    public class Hand
    {
        public List<Card> Cards { get; set; } = new List<Card>();
        public bool IsDealer { get; set; }
        public bool IsFinished { get; set; }

        public int GetValue()
        {
            int value = 0;
            int aces = 0;

            foreach (var card in Cards)
            {
                if (card.Rank == Rank.Ace)
                {
                    aces++;
                    value += 11;
                }
                else
                {
                    value += card.GetValue();
                }
            }

            // Adjust for aces if value is over 21
            while (value > 21 && aces > 0)
            {
                value -= 10;
                aces--;
            }

            return value;
        }

        public bool IsBust()
        {
            return GetValue() > 21;
        }

        public bool IsBlackjack()
        {
            return Cards.Count == 2 && GetValue() == 21;
        }

        public void AddCard(Card card)
        {
            Cards.Add(card);
        }

        public void Clear()
        {
            Cards.Clear();
            IsFinished = false;
        }

        public List<Card> GetVisibleCards(bool gameInProgress)
        {
            if (IsDealer && gameInProgress && Cards.Count > 0)
            {
                // Show only first card when game is in progress
                return new List<Card> { Cards[0] };
            }
            return Cards;
        }

        public int GetVisibleValue(bool gameInProgress)
        {
            var visibleCards = GetVisibleCards(gameInProgress);
            if (visibleCards.Count == 0) return 0;
            
            if (IsDealer && gameInProgress && Cards.Count > 1)
            {
                // Only show first card value
                return visibleCards[0].GetValue();
            }
            
            return GetValue();
        }
    }
}