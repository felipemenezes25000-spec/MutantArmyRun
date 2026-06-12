using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MutantArmy.UI
{
    /// <summary>
    /// PauseOverlay (OVL-SISTEMA, doc 09 §2.2): pausa da corrida acionada pelo botão de PAUSE
    /// no HUD (GameUIController) durante Running/BossFight. Sobe SOBRE o HUD com o fade de 150 ms
    /// do UIOverlay (em UNSCALED time, então a coreografia roda mesmo com timeScale 0). Enquanto
    /// visível, congela o jogo com Time.timeScale = 0 e restaura o valor anterior ao fechar
    /// (1 por padrão). Botões: CONTINUAR (despausa), REINICIAR FASE, CONFIGURAÇÕES, MENU.
    ///
    /// O overlay NÃO conhece GameManager/cena: ele só dispara EVENTOS. O glue (GameUIController)
    /// decide o que cada um faz (restart via StartLevel, load da cena Main, abrir Settings) E é
    /// quem controla o Time.timeScale (0 ao abrir, 1 ao fechar) — mesma fronteira da ResultScreen
    /// (UI não referencia Core direto, doc 12 §2.3). O timeScale fica no glue, não aqui, porque o
    /// UIManager.ShowOverlay chama Show() pela base UIOverlay (sem polimorfismo), então um override
    /// aqui não rodaria por esse caminho.
    ///
    /// NÃO atrapalha o AutoPilot: o autoteste NUNCA abre o pause (só o toque humano no botão do
    /// HUD chama ShowOverlay) — o rig de screenshots não toca neste overlay.
    /// </summary>
    public class PauseOverlay : UIOverlay
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _menuButton;

        /// <summary>CONTINUAR — despausa e volta à corrida (o glue fecha o overlay).</summary>
        public event System.Action ResumeRequested;
        /// <summary>REINICIAR FASE — reinicia a fase atual (soft reset via StartLevel).</summary>
        public event System.Action RestartRequested;
        /// <summary>CONFIGURAÇÕES — abre a tela de configurações (se houver na cena).</summary>
        public event System.Action SettingsRequested;
        /// <summary>MENU — abandona a corrida e carrega a cena Main.</summary>
        public event System.Action MenuRequested;

        protected override void Awake()
        {
            base.Awake();
            if (_resumeButton != null) _resumeButton.onClick.AddListener(() => Raise(ResumeRequested));
            if (_restartButton != null) _restartButton.onClick.AddListener(() => Raise(RestartRequested));
            if (_settingsButton != null) _settingsButton.onClick.AddListener(() => Raise(SettingsRequested));
            if (_menuButton != null) _menuButton.onClick.AddListener(() => Raise(MenuRequested));
        }

        protected override void OnShown()
        {
            base.OnShown();
            if (_titleText != null) _titleText.text = "PAUSADO";
        }

        private static void Raise(System.Action evt)
        {
            if (evt != null) evt();
        }
    }
}
