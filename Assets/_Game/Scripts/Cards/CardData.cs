using UnityEngine;

namespace CardDuel.Cards
{
    public enum CardType { Monster, Spell, Trap }

    public enum CardAttribute { None, Light, Dark, Fire, Water, Earth, Wind }

    /// <summary>
    /// Definición estática de una carta. Crea nuevas cartas desde
    /// Assets > Create > CardDuel > Card Data, sin escribir código.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCard", menuName = "CardDuel/Card Data")]
    public class CardData : ScriptableObject
    {
        [Header("Identidad")]
        public string id;
        public string displayName;
        [TextArea(3, 6)] public string description;
        public Sprite artwork;

        [Header("Tipo")]
        public CardType cardType = CardType.Monster;
        public CardAttribute attribute = CardAttribute.None;

        [Header("Monstruo")]
        [Range(1, 12)] public int level = 4;
        public int attack = 1000;
        public int defense = 1000;
    }
}
