using System;
using System.Collections.Generic;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Máquina de estados em PILHA (doc 12 §4.1). Overlays temporários (ReviveOffer, pausa)
    /// entram por Push SOBRE o estado da corrida — exército, mutações, RunWallet e progresso
    /// da pista ficam intactos embaixo; Pop devolve EXATAMENTE o estado anterior.
    /// Transições laterais são validadas por tabela: transição ilegal retorna false e não
    /// tem efeito algum — nunca corrompe o fluxo.
    /// </summary>
    public sealed class GameStateStack
    {
        // Tabela de transições válidas — cópia EXATA do doc 12 §4.1. Tudo fora dela é bug.
        private static readonly Dictionary<GameState, GameState[]> Allowed = new Dictionary<GameState, GameState[]>
        {
            [GameState.Boot]        = new[] { GameState.MainMenu },
            [GameState.MainMenu]    = new[] { GameState.BossScout },
            [GameState.BossScout]   = new[] { GameState.Running },
            [GameState.Running]     = new[] { GameState.BossFight, GameState.Defeat },
            [GameState.BossFight]   = new[] { GameState.Victory, GameState.Defeat },
            [GameState.Victory]     = new[] { GameState.MainMenu, GameState.BossScout },  // próxima fase sem loading (§2.2)
            [GameState.Defeat]      = new[] { GameState.MainMenu, GameState.BossScout },
        };

        private readonly Stack<GameState> _stack = new Stack<GameState>();

        public GameStateStack()
        {
            _stack.Push(GameState.Boot);
        }

        public GameState Current => _stack.Peek();

        public int Depth => _stack.Count;

        /// <summary>Troca o TOPO da pilha (transição lateral). Retorna false sem efeito se ilegal.</summary>
        public bool ChangeState(GameState next)
        {
            // Estados sem linha na tabela (ex.: overlay ReviveOffer no topo) não saem por
            // transição lateral — a única saída é Pop().
            if (!Allowed.TryGetValue(Current, out GameState[] ok) || Array.IndexOf(ok, next) < 0)
                return false;
            _stack.Pop();
            _stack.Push(next);
            return true;
        }

        /// <summary>Empilha um estado temporário SOBRE o atual — quem está embaixo congela, não morre.</summary>
        public void Push(GameState overlay)
        {
            _stack.Push(overlay);
        }

        /// <summary>Remove e retorna o topo; o estado de baixo retoma intacto.</summary>
        public GameState Pop()
        {
            // O estado base do fluxo nunca sai da pilha — pop no último elemento é bug do chamador.
            if (_stack.Count <= 1)
                throw new InvalidOperationException(
                    "Pop em pilha de profundidade 1: o estado base nunca sai da pilha.");
            return _stack.Pop();
        }
    }
}
