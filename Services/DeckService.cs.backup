using GeneratedApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneratedApp.Services
{
    public class DeckService
    {
        private List<Card> _deck = new List<Card>();
        private Random _random;

        public DeckService()
        {
            _random = new Random();
            InitializeDeck();
        }

        public void InitializeDeck()
        {
            _deck = new List<Card>();
            
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    _deck.Add(new Card(suit, rank));
                }
            }
            
            Shuffle();
        }

        public void Shuffle()
        {
            for (int i = _deck.Count - 1; i > 0; i--)
            {
                int randomIndex = _random.Next(i + 1);
                Card temp = _deck[i];
                _deck[i] = _deck[randomIndex];
                _deck[randomIndex] = temp;
            }
        }

        public Card DrawCard()
        {
            if (_deck.Count == 0)
            {
                InitializeDeck(); // Reshuffle when deck is empty
            }

            Card card = _deck[0];
            _deck.RemoveAt(0);
            return card;
        }

        public int RemainingCards => _deck.Count;
    }
}