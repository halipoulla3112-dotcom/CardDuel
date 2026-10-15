using System.Collections.Generic;
using CardDuel.Cards;
using UnityEngine;

namespace CardDuel.Prototype
{
    /// <summary>
    /// Mazo de prueba creado por código. Cuando tengas cartas como assets
    /// (Create > CardDuel > Card Data) podrás sustituirlo por un DeckData.
    /// </summary>
    public static class SampleDeck
    {
        public static List<CardData> Build()
        {
            var cards = new List<CardData>();

            void Monster(string name, int level, int atk, int def, CardAttribute attribute, string description, int copies = 1)
            {
                for (int i = 0; i < copies; i++)
                {
                    var card = Create(name, CardType.Monster, description);
                    card.level = level;
                    card.attack = atk;
                    card.defense = def;
                    card.attribute = attribute;
                    cards.Add(card);
                }
            }

            void Spell(string name, string description, int copies = 1)
            {
                for (int i = 0; i < copies; i++) cards.Add(Create(name, CardType.Spell, description));
            }

            void Trap(string name, string description, int copies = 1)
            {
                for (int i = 0; i < copies; i++) cards.Add(Create(name, CardType.Trap, description));
            }

            Monster("Caballero de Ceniza", 4, 1800, 1200, CardAttribute.Fire, "Un guerrero forjado en las brasas de un volcán dormido.", 2);
            Monster("Lobo Rúnico", 4, 1600, 1000, CardAttribute.Earth, "Las runas de su pelaje brillan antes de cada ataque.", 2);
            Monster("Guardiana del Alba", 4, 1500, 1900, CardAttribute.Light, "Protege a sus aliados con un escudo de luz.", 2);
            Monster("Golem de Musgo", 3, 1000, 2000, CardAttribute.Earth, "Lento pero casi imposible de derribar.", 2);
            Monster("Espectro Errante", 3, 1300, 800, CardAttribute.Dark, "Vaga entre las sombras buscando un cuerpo nuevo.", 2);
            Monster("Sirena de Coral", 4, 1400, 1600, CardAttribute.Water, "Su canto confunde a los monstruos enemigos.", 2);
            Monster("Halcón Tormenta", 4, 1700, 900, CardAttribute.Wind, "Cae en picado envuelto en relámpagos.", 2);
            Monster("Duende Chispa", 2, 800, 600, CardAttribute.Fire, "Pequeño, travieso y explosivo.", 2);
            Monster("Paladín Solar", 5, 2100, 1500, CardAttribute.Light, "Requiere 1 tributo. Su espada nunca se apaga.");
            Monster("Titán de las Mareas", 6, 2300, 1800, CardAttribute.Water, "Requiere 1 tributo. Surge del fondo del océano.");
            Monster("Dragón de Obsidiana", 7, 2600, 2100, CardAttribute.Dark, "Requiere 2 tributos. Sus escamas son de cristal volcánico.");

            Spell("Poción de Vida", "Ganas 1000 puntos de vida.", 2);
            Spell("Tormenta de Rayos", "Destruye un monstruo del rival.");
            Spell("Espada Encantada", "Un monstruo gana 500 ATK.");
            Spell("Portal Arcano", "Roba 2 cartas.");

            Trap("Escudo Espejo", "Cuando el rival ataque: destruye al atacante.");
            Trap("Foso Sin Fondo", "Cuando el rival invoque un monstruo: destiérralo.");
            Trap("Cadenas de Sombra", "Un monstruo rival no puede atacar.");

            return cards;
        }

        private static CardData Create(string name, CardType type, string description)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.name = name;
            card.id = name.ToLowerInvariant().Replace(' ', '_');
            card.displayName = name;
            card.cardType = type;
            card.description = description;
            return card;
        }
    }
}
