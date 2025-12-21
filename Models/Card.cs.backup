namespace GeneratedApp.Models
{
    public enum Suit
    {
        Hearts,
        Diamonds,
        Clubs,
        Spades
    }

    public enum Rank
    {
        Ace = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13
    }

    public class Card
    {
        public Suit Suit { get; set; }
        public Rank Rank { get; set; }

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public int GetValue()
        {
            return Rank switch
            {
                Rank.Ace => 11, // Default to 11, will be adjusted in hand calculation
                Rank.Jack or Rank.Queen or Rank.King => 10,
                _ => (int)Rank
            };
        }

        public string GetDisplayValue()
        {
            return Rank switch
            {
                Rank.Ace => "A",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                _ => ((int)Rank).ToString()
            };
        }

        public string GetSuitSymbol()
        {
            return Suit switch
            {
                Suit.Hearts => "♥",
                Suit.Diamonds => "♦",
                Suit.Clubs => "♣",
                Suit.Spades => "♠",
                _ => ""
            };
        }

        public string GetSuitColor()
        {
            return Suit switch
            {
                Suit.Hearts or Suit.Diamonds => "red",
                Suit.Clubs or Suit.Spades => "black",
                _ => "black"
            };
        }
    }
}