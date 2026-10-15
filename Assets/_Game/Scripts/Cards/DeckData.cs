using System.Collections.Generic;
using UnityEngine;

namespace CardDuel.Cards
{
    [CreateAssetMenu(fileName = "NewDeck", menuName = "CardDuel/Deck Data")]
    public class DeckData : ScriptableObject
    {
        public string deckName;
        public List<CardData> cards = new List<CardData>();
    }
}
