using System;
using System.Collections.Generic;
using UnityEngine;
using MutantArmy.Core;
using MutantArmy.Domain;

namespace MutantArmy.UI
{
    /// <summary>
    /// Gerência de UI com DUAS pilhas (doc 12 §4.13): telas (SCR-01..09, com history)
    /// e overlays (OVL-01..06) que sobem SOBRE a tela atual sem destruí-la. A pilha de
    /// overlays espelha a pilha de estados do GameManager (push de ReviveOffer no
    /// estado = push do OVL-05 na UI). Safe area resolvida UMA vez, no root.
    /// Init() é chamado pelo GameBootstrap na ordem canônica (doc 12 §3.3) —
    /// nunca auto-registro em Awake.
    /// </summary>
    public class UIManager : MonoBehaviour, IInitializable
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private RectTransform _root;           // safe area aplicada aqui, 1×
        [SerializeField] private BossScoutOverlay _bossScout;   // OVL-01 (CANON §3.1)
        [SerializeField] private UIOverlay _reviveOffer;        // OVL-05 — revive no boss (CANON §11)

        private const float BossScoutSeconds = 2f;              // cartão de ~2 s (CANON §3.1)

        private readonly Stack<UIScreen> _screens = new Stack<UIScreen>();
        private readonly Stack<UIOverlay> _overlays = new Stack<UIOverlay>();

        public UIScreen CurrentScreen
        {
            get { return _screens.Count > 0 ? _screens.Peek() : null; }
        }

        public UIOverlay CurrentOverlay
        {
            get { return _overlays.Count > 0 ? _overlays.Peek() : null; }
        }

        public int ScreenDepth
        {
            get { return _screens.Count; }
        }

        public int OverlayDepth
        {
            get { return _overlays.Count; }
        }

        public void Init()
        {
            Instance = this;
            if (_root != null)
            {
                // Safe area resolvida UMA vez, no root — nunca por tela e nunca por frame
                // (notch/punch-hole, doc 12 §4.13 / doc 09 P10).
                UIUtils.ResizeToSafeArea(_root);
            }

            if (GameManager.Instance != null)
            {
                // Contrato doc 12 §4.1 (EnterState): BossScout mostra o cartão (e o onDone
                // dele é quem leva a Running); ReviveOffer abre o OVL-05. Core não enxerga
                // UI, então é o manager que assina; -= antes de += p/ re-Init não duplicar.
                GameManager.Instance.StateEntered -= HandleStateEntered;
                GameManager.Instance.StateEntered += HandleStateEntered;
                GameManager.Instance.StateExited -= HandleStateExited;
                GameManager.Instance.StateExited += HandleStateExited;
            }
        }

        private void HandleStateEntered(GameState s)
        {
            GameManager gm = GameManager.Instance;
            switch (s)
            {
                case GameState.BossScout:
                    // sem o onDone → ChangeState(Running) o jogo ficaria preso em BossScout
                    ShowBossScout(gm.CurrentLevel != null ? gm.CurrentLevel.boss : null,
                                  BossScoutSeconds, () => gm.ChangeState(GameState.Running));
                    break;

                case GameState.ReviveOffer:
                    if (_reviveOffer != null)
                    {
                        ShowOverlay(_reviveOffer);   // OVL-05 sobe SOBRE a luta (pilha espelha estados)
                    }
                    else
                    {
                        // sem overlay o estado ficaria preso sobre o BossFight: recusa explícita
                        Debug.LogWarning("UIManager: overlay de revive (OVL-05) não atribuído — recusando o revive.");
                        gm.ResolveRevive(false);
                    }
                    break;
            }
        }

        private void HandleStateExited(GameState s)
        {
            // o Pop do estado (ResolveRevive) fecha o OVL-05 junto — a pilha de UI
            // espelha a pilha de estados (doc 12 §4.13)
            if (s == GameState.ReviveOffer && _reviveOffer != null
                && ReferenceEquals(CurrentOverlay, _reviveOffer))
                PopOverlay();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.StateEntered -= HandleStateEntered;
            GameManager.Instance.StateExited -= HandleStateExited;
        }

        /// <summary>Empilha uma tela; o topo anterior fica no history (slide 200 ms).</summary>
        public void Push(UIScreen screen)
        {
            if (screen == null)
            {
                Debug.LogWarning("UIManager.Push chamado com tela nula.");
                return;
            }
            if (_screens.Count > 0) _screens.Peek().Hide();
            _screens.Push(screen);
            screen.Show();
        }

        /// <summary>Volta à tela anterior do history.</summary>
        public void Pop()
        {
            if (_screens.Count == 0)
            {
                Debug.LogWarning("UIManager.Pop sem tela na pilha.");
                return;
            }
            _screens.Pop().Hide();
            if (_screens.Count > 0) _screens.Peek().Show();
        }

        /// <summary>Mostra um overlay SOBRE a tela atual (fade 150 ms) — a tela não sai da pilha.</summary>
        public void ShowOverlay(UIOverlay overlay)
        {
            if (overlay == null)
            {
                Debug.LogWarning("UIManager.ShowOverlay chamado com overlay nulo.");
                return;
            }
            _overlays.Push(overlay);
            overlay.Show();
        }

        /// <summary>Fecha o overlay do topo; a tela (e overlays de baixo) seguem intactos.</summary>
        public void PopOverlay()
        {
            if (_overlays.Count == 0)
            {
                Debug.LogWarning("UIManager.PopOverlay sem overlay na pilha.");
                return;
            }
            _overlays.Pop().Hide();
        }

        /// <summary>
        /// OVL-01 Boss Scout (CANON §3.1): cartão de ~2 s com boss/elemento/fraqueza,
        /// auto-dismiss, qualquer toque pula. onDone dispara após o fechamento —
        /// o GameManager usa para transicionar BossScout→Running (doc 12 §4.1).
        /// </summary>
        public void ShowBossScout(BossConfigSO boss, float seconds, Action onDone)
        {
            if (_bossScout == null)
            {
                Debug.LogWarning("UIManager: BossScoutOverlay não atribuído — pulando o cartão.");
                if (onDone != null) onDone();
                return;
            }

            _overlays.Push(_bossScout);
            _bossScout.Play(boss, seconds, () =>
            {
                if (_overlays.Count > 0 && ReferenceEquals(_overlays.Peek(), _bossScout))
                    _overlays.Pop();
                if (onDone != null) onDone();
            });
        }
    }
}
