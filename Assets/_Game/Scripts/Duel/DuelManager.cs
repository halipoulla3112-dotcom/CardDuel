using System;
using UnityEngine;

namespace CardDuel.Duel
{
    /// <summary>
    /// Controla el flujo de turnos y fases. Las reglas irán creciendo aquí
    /// (o en clases separadas) sin depender de la parte visual.
    /// </summary>
    public class DuelManager : MonoBehaviour
    {
        [SerializeField] private int startingLifePoints = 8000;

        public int CurrentPlayer { get; private set; }
        public int TurnNumber { get; private set; }
        public TurnPhase Phase { get; private set; }
        public int[] LifePoints { get; private set; }

        public event Action<TurnPhase> PhaseChanged;
        public event Action<int> TurnStarted;

        private void Start()
        {
            LifePoints = new[] { startingLifePoints, startingLifePoints };
            CurrentPlayer = 0;
            TurnNumber = 0;
            StartTurn();
        }

        public void NextPhase()
        {
            if (Phase == TurnPhase.End)
            {
                CurrentPlayer = 1 - CurrentPlayer;
                StartTurn();
                return;
            }

            SetPhase(Phase + 1);
        }

        private void StartTurn()
        {
            TurnNumber++;
            TurnStarted?.Invoke(CurrentPlayer);
            SetPhase(TurnPhase.Draw);
        }

        private void SetPhase(TurnPhase phase)
        {
            Phase = phase;
            Debug.Log($"Turno {TurnNumber} - Jugador {CurrentPlayer + 1} - Fase {phase}");
            PhaseChanged?.Invoke(phase);
        }
    }
}
