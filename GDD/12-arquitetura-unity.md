# 12 — Arquitetura Unity & Código (Entregáveis 16, 17 e 18)

> **Mutant Army Run** · Unity 2022 LTS + URP · C# · Android-first (iOS depois) · Portrait 9:16.
> Este documento segue estritamente o `CANON.md` (§13 Tecnologia, §4 Elementos, §5 Tropas, §8 Economia, §11 Monetização). Conteúdo de design referenciado: doc 03 (unidades), doc 04 (portais), doc 05 (bosses), doc 08 (economia), doc 14 (monetização).

## 1. Princípios de arquitetura

1. **Data-driven total:** todo conteúdo (tropas, bosses, fases, portais, mutações) vive em ScriptableObjects. Designer cria fase nova sem abrir código; Remote Config sobrescreve números em produção.
2. **Desacoplamento por eventos:** sistemas nunca se referenciam diretamente para notificar; publicam em um event bus central. Só existe referência direta no sentido "orquestrador → serviço" (ex.: `GameManager` chama `SaveSystem.Save()`).
3. **Local-first:** o jogo funciona 100% offline. Rede (Firebase, MAX, RevenueCat) é camada opcional que degrada silenciosamente.
4. **Orçamento de performance é requisito, não otimização tardia:** 60 fps em device mediano com 1000 unidades é meta de arquitetura (ver §6).
5. **Simulação centralizada e orientada a dados:** a multidão é dado (structs/arrays) tickado por UM manager num único loop. É **proibido** 1 MonoBehaviour/`Update` por unidade, `Rigidbody` dinâmico por unidade e `NavMeshAgent` por unidade — consenso de todos os repos de crowd estudados (ver `15-referencias-e-recursos.md` §3.1).
6. **Composition root explícito:** managers nascem registrados pelo `GameBootstrap` em ordem declarada (§3.3) — banido o auto-singleton `public static X i` setado em `Awake`, banida string mágica de estado/label (sempre enum, const ou `AssetReference` tipado).

---

## 2. Estrutura de projeto (Entregável 16)

### 2.1 Árvore de pastas

Tudo do jogo vive em `Assets/_Project/` (o underscore mantém no topo e isola de SDKs de terceiros, que poluem a raiz de `Assets/`).

```text
Assets/
├── _Project/
│   ├── Scenes/
│   │   ├── Boot.unity              # init de serviços, sem visual além do splash
│   │   ├── Main.unity              # menu, mapa, tropas, upgrades, loja
│   │   └── Game.unity              # pista + arena de boss (conteúdo via LevelConfigSO)
│   ├── Scripts/
│   │   ├── Core/                   # GameManager, event bus, state machine, utils
│   │   ├── Gameplay/               # Crowd, Gate, Unit, Boss, Combat, Level, VFX hooks
│   │   ├── Meta/                   # Economy, Upgrade, Reward, Save, progressão
│   │   ├── Services/               # Ads, IAP, Analytics, RemoteConfig, Firebase
│   │   ├── UI/                     # UIManager, telas, HUD, feedbacks (NICE/INSANE...)
│   │   └── Editor/                 # ferramentas internas (validador de SO, level preview)
│   ├── ScriptableObjects/
│   │   ├── Units/                  # UnitConfigSO (Soldado.asset, Arqueiro.asset...)
│   │   ├── Bosses/                 # BossConfigSO
│   │   ├── Levels/                 # LevelConfigSO (Level_001.asset ... Level_020.asset)
│   │   ├── Worlds/                 # WorldConfigSO (W01_CampoInicial.asset...)
│   │   ├── Gates/                  # GateConfigSO (os 8 do MVP + pós-MVP)
│   │   ├── Mutations/              # MutationConfigSO
│   │   ├── Upgrades/               # UpgradeConfigSO (1 por trilha)
│   │   ├── Rewards/                # RewardConfigSO (baús, vitória, boss)
│   │   └── Balance/                # ElementChartSO, RarityConfigSO
│   ├── Prefabs/
│   │   ├── Units/                  # 1 prefab "casca" por tropa (mesh + dados de pool)
│   │   ├── Gates/                  # portal pareado (par L/R num único prefab)
│   │   ├── Bosses/
│   │   ├── Track/                  # segmentos modulares de pista, obstáculos
│   │   └── UI/
│   ├── Art/
│   │   ├── Models/  Materials/  Textures/  Animations/
│   │   └── VAT/                    # vertex animation textures geradas (ver §6)
│   ├── Audio/
│   │   ├── Music/  SFX/            # SFX de multiplicação, hit, fanfarra de Supply
│   ├── VFX/                        # partículas, shaders (Shader Graph), trails
│   ├── Settings/
│   │   ├── URP/                    # URP-Asset-High.asset, URP-Asset-Low.asset, Renderer
│   │   └── Input/                  # InputActions (touch/drag)
│   └── Resources/                  # SOMENTE bootstrap (GameSettings.asset); nada mais
├── Plugins/Android/                # manifests, gradle templates (MAX, Firebase)
└── (raiz) MaxSdk/, Firebase/, RevenueCat/   # SDKs de terceiros, intocados
```

**Regras de higiene:** nada em `Resources/` além do bootstrap (mata o build size e o startup); texturas com ASTC e mipmaps off em UI; `Addressables` fica fora do MVP (20 fases cabem no build) e entra no pós-MVP para mundos 4–10 via download remoto — quando entrar, a disciplina é **1 label por mundo** (carrega só os configs/assets do mundo atual num Dictionary com flag `loaded` consultada pela UI) e **`ReleaseInstance` em TODO caminho de despawn**, sem exceção — instância addressable que volta ao pool sem release é memory leak garantido (modelo do Trash Dash, ver `15-referencias-e-recursos.md` §3.5).

### 2.2 Cenas e fluxo

| Cena | Conteúdo | Quando carrega |
|---|---|---|
| `Boot` | Canvas de splash + `GameBootstrap` (composition root, §3.3) | uma vez, no launch |
| `Main` | menu inicial, mapa de mundos, tropas, upgrades, loja | após Boot; retorno pós-fase |
| `Game` | controlador de corrida + arena; pista montada de segmentos conforme `LevelConfigSO` | ao apertar Jogar |

- Carregamento por `SceneManager.LoadSceneAsync` (modo Single). Managers persistentes vivem num prefab `[Services]` instanciado no Boot com `DontDestroyOnLoad`.
- **Regra de produto "jogar em ≤5 s":** Boot tem orçamento de **2,5 s** (init paralelo, ver §3.3); `Main` aparece com botão Jogar ativo mesmo se Ads/Firebase ainda estiverem inicializando em background.
- `Game` é **cena única para as 20+ fases** (níveis = `LevelConfigSO`, nunca 1 cena por nível — anti-exemplo do Count-Master-Clone, ver `15-referencias-e-recursos.md` §3.3) e não recarrega entre fases nem no retry: vitória re-gera a pista in-place (re-pool de segmentos, §4.11) e derrota faz **soft reset** — pools drenados, âncoras repopuladas, estado de corrida zerado, **sem `SceneManager.LoadScene`**. Encadeamento sem loading é essencial para "≥6 fases/sessão" (CANON §12); num retry de fase de 60–90 s, reload de cena é fricção inaceitável.

### 2.3 Assemblies (asmdef)

| Assembly | Conteúdo | Referencia |
|---|---|---|
| `MutantArmy.Core` | event bus, state machine, tipos compartilhados (enums, structs), SOs | — |
| `MutantArmy.Gameplay` | Crowd, Gate, Boss, Combat, Level, Unit | Core |
| `MutantArmy.Meta` | Economy, Upgrade, Reward, Save | Core |
| `MutantArmy.Services` | Ads, IAP, Analytics, RemoteConfig (+ wrappers de SDK) | Core |
| `MutantArmy.UI` | telas e HUD | Core, Meta |
| `MutantArmy.Editor` | ferramentas (Editor only) | todos |

Direção de dependência: tudo aponta para `Core`; **ninguém referencia `UI`**. Gameplay não conhece Services — fala com Ads/Analytics só via eventos. Benefícios: compilação incremental rápida, impossibilita acoplamento acidental (ex.: `CrowdManager` chamando `MaxSdk` direto não compila).

**Regras duras de compliance técnico (verificadas em CI, quebram o build se violadas):**

1. **Código de editor SÓ em `MutantArmy.Editor`** (asmdef marcado *Editor-only*) ou em pasta `Editor/`. Nunca `using UnityEditor` em assembly de runtime — 2 dos repos estudados quebram o build de device exatamente assim (ver `15-referencias-e-recursos.md` §2). Guard de CI: grep por `UnityEditor` fora de `Editor/` falha o pipeline antes do build.
2. **`MutantArmy.Gameplay` é fronteira contra SDKs:** o asmdef não referencia `MutantArmy.Services` nem os assemblies de MAX/Firebase/RevenueCat. A fronteira não é convenção — é compilação: gameplay só enxerga `Core`.
3. **ScriptableObjects são read-only em runtime:** SO é config; estado vivo mora no manager (detalhe e racional em §5.1).
4. **Addressables (pós-MVP):** 1 label por mundo + `ReleaseInstance` em todo despawn (§2.1).

### 2.4 Pipeline URP

- **Forward Renderer**, SRP Batcher **on**, HDR **off**, MSAA off (anti-alias visual vem do estilo flat/cores chapadas), Depth/Opaque texture off.
- Dois assets de qualidade trocados em runtime pelo `GameManager` conforme device tier (RAM + GPU name + benchmark de 2 s no primeiro boot):
  - **High:** render scale 1.0 · sombra de 1 cascade, distância 30 m, hard shadows · Bloom leve via Volume.
  - **Low:** render scale 0.85 · sombras off (blob shadow projetado no shader da unidade) · sem post-processing.
- Luz: 1 directional realtime; ambiente por gradiente. Sem luzes adicionais em gameplay (boss usa emissive + VFX para "brilhar").
- Shaders autorais em Shader Graph compatíveis com SRP Batcher; o shader da multidão é custom HLSL (VAT + instancing, §6).

### 2.5 Build Android

| Configuração | Valor | Racional |
|---|---|---|
| Scripting backend | **IL2CPP** | obrigatório p/ ARM64 + performance de simulação da multidão |
| Target architectures | **ARM64 + ARMv7** | AAB divide por ABI na Play; ARMv7 cobre low-end BR/SEA |
| Formato | **AAB** (App Bundle) | exigência da Play + entrega por ABI/density |
| Min API | 24 (Android 7.0) | cobre ≈97% do parque BR/LatAm/SEA |
| Target API | mais recente exigida pela Play (35+) | compliance |
| Compressão do build | **LZ4** (dev) / **LZ4HC** (release) | descompressão rápida no startup (meta de 5 s) |
| Texturas | **ASTC 6x6** (UI 4x4) | qualidade/peso ideal em Vulkan/GLES3 |
| Graphics API | **Vulkan, fallback GLES3** (Auto off) | Vulkan reduz overhead de draw call em mid-range |
| Managed Stripping | **High** + Strip Engine Code | APK menor; manter `link.xml` p/ Firebase/MAX (reflection) |
| GC | **Incremental** | evita spikes na corrida |
| Minify (release) | R8 ativo | tamanho + ofuscação básica |
| Alvo de tamanho | **≤ 60 MB** no download inicial | CPI ≤ US$ 0,40 exige instalação sem fricção |

---

## 3. Arquitetura de código

### 3.1 Managers (CANON §13) — responsabilidades

| Manager | Responsabilidade (resumo) | Assembly |
|---|---|---|
| `GameManager` | máquina de estados **em pilha** (Boot→Menu→BossScout→Running; BossFight/ReviveOffer/GameOver entram por *push* sobre a corrida — §4.1); transições validadas por tabela; device tier. | Core |
| `LevelManager` | monta pista de segmentos-prefab com âncoras a partir de `LevelConfigSO` + **seed determinística**; spawn/reciclagem por distância; progresso (0–100%); dispara a arena (§4.11). | Gameplay |
| `GateManager` | gera e posiciona pares de portais conforme a fase **e o boss** (rota ótima + armadilha), reconcilia o efeito via contrato GatePair→CrowdManager (§4.3). | Gameplay |
| `CrowdManager` | dono do exército: dados SoA tickados num único loop (§4.2), contagem, formação filotáxica, Supply (cap 60→300), conversão de excedente em moedas, mutações (3 slots). | Gameplay |
| `UnitManager` | catálogo de `UnitConfigSO`, níveis/fragmentos por tropa, stats efetivos (base × nível × upgrades). | Gameplay |
| `BossManager` | spawn do boss, entrada ≤2 s, fases de vida, telegraph do ataque especial, fraqueza no HUD, morte em slow motion. | Gameplay |
| `CombatSystem` | resolução de dano por **agregados HP/DPS com ticks** (crowd↔boss, crowd↔inimigos), aquisição de alvo centralizada, chart elemental via `ElementChartSO`, crítico, DoTs (§4.4). | Gameplay |
| `UpgradeSystem` | 8 trilhas de meta (+5%/nível, custo 100×1,35^n), aplica modificadores no início da fase. | Meta |
| `EconomySystem` | carteira (Coins/Gems/Shards/XP), ganhos/gastos transacionais, recompensa de fase = base×1,10^(fase−1). | Meta |
| `RewardSystem` | baús, drops de boss (cartas/fragmentos), missões diárias, multiplicador x2 por rewarded. | Meta |
| `AdsManager` | AppLovin MAX: rewarded (5 placements do CANON §11) e interstitial com pacing por Remote Config. | Services |
| `IAPManager` | RevenueCat: ofertas, compra, restore, entitlement `no_ads` e `season_pass`. | Services |
| `AnalyticsManager` | fila local + Firebase Analytics; todos os eventos do BRIEF; flush resiliente offline. | Services |
| `SaveSystem` | JSON local com checksum + backup + sync Firestore (merge por timestamp). | Meta |
| `UIManager` | **duas pilhas** (telas SCR + overlays OVL-01..06), safe area resolvida 1× no root, HUD por evento, feedbacks textuais (NICE→GODLIKE), Boss Scout card (§4.13). | UI |
| `AudioManager` | música por mundo, SFX por evento do bus (multiplicação, fanfarra de Supply, hit de boss). | Services |
| `VFXManager` | pools de partículas com orçamento global (§6.3), slow motion canônico do golpe final: `Time.timeScale = 0,3` por 0,8 s, com `Time.fixedDeltaTime` escalado na mesma proporção (0,02 → 0,006 s) para a física acompanhar sem engasgo — técnica única do pacote (doc 01, Pilar 3). | Gameplay |
| `RemoteConfigManager` | fetch com timeout, cache, defaults embutidos; fonte de verdade de tuning em produção. | Services |

### 3.2 Comunicação: event bus — decisão

**Decisão: event bus estático em C# (`GameEvents`), tipado, com payload em struct — implementado sobre Signals (MIT, ~200 linhas absorvidas no projeto com notice em `THIRD-PARTY-NOTICES.md`; ver `15-referencias-e-recursos.md` §4).** Signals dá o hub tipado por assinatura sem alocação e sem dependência de pacote externo; tratamos o código como nosso. ScriptableObject events (estilo "SO Architecture") ficam restritos ao **fluxo de telas de UI e a reações cosméticas (áudio/VFX)**, onde designers ganham ao religar respostas sem código.

| Critério | C# static events | SO events |
|---|---|---|
| Performance/alocação | zero alloc com structs | boxing/listas de listeners em asset |
| Rastreabilidade | "Find References" acha tudo | referência via asset = invisível ao IDE |
| Ordem de execução | determinística por assinatura | dependente de ordem de serialização |
| Autonomia do designer | nenhuma | alta (liga SFX/VFX no Inspector) |

Num jogo onde a simulação roda a 60 Hz com até 1000 unidades, debugabilidade e custo zero vencem. Regra prática: **estado de jogo = C# events (Signals); fluxo de telas e reação cosmética = SO events.**

**Regras de uso do bus (lições do estudo — ver `15-referencias-e-recursos.md` §3.5):**

- **Listeners síncronos de DADOS rodam antes da transição de tela.** Commit de economia, save e analytics assinam o evento e executam no mesmo frame do Raise; a troca de tela é deferida para o frame seguinte — ordem garantida "dados prontos → tela mostra" (padrão `EventLink` do Runner Template, reimplementado). Tela de vitória que lê o RunWallet ANTES do commit é a classe de bug que essa regra elimina.
- **Alerta Reset-pós-Raise:** em SO events, chamar `Reset()` imediatamente após o Raise **zera o payload para qualquer listener assíncrono** (armadilha documentada do `AbstractGameEvent` do estudo). Payload de SO event é imutável por disparo; listener que precisa do dado depois guarda cópia própria.
- **UI atualiza por evento, nunca por frame.** Nenhum `Update()` de UI lendo contadores (anti-exemplo: HUD por polling do InfiniteRunner3D). HUD assina `OnCrowdChanged`/`OnCurrencyChanged` e só redesenha quando algo muda.

```csharp
// Core/GameEvents.cs — bus central. Sempre limpar inscrições em OnDisable.
public static class GameEvents
{
    public static event Action<GateResult>   OnGateConsumed;      // 1 por exército por par (§4.3)
    public static event Action<int, int>     OnCrowdChanged;      // (count, supplyUsed)
    public static event Action<SupplyOverflow> OnSupplyOverflow;  // excedente → moedas (fanfarra)
    public static event Action<MutationConfigSO> OnMutationGained;
    public static event Action<UnitDeath>    OnUnitDied;          // ponto de extensão (§4.2): VFX/analytics
    public static event Action<BossPhase>    OnBossPhaseChanged;
    public static event Action<LevelResult>  OnLevelFinished;     // vitória ou derrota + stats
    public static event Action<CurrencyChange> OnCurrencyChanged;

    public static void RaiseGateConsumed(GateResult r) => OnGateConsumed?.Invoke(r);
    public static void RaiseCrowdChanged(int c, int s) => OnCrowdChanged?.Invoke(c, s);
    public static void RaiseSupplyOverflow(SupplyOverflow o) => OnSupplyOverflow?.Invoke(o);
    public static void RaiseLevelFinished(LevelResult r) => OnLevelFinished?.Invoke(r);
    // ... demais Raise* seguem o padrão
}
```

### 3.3 Bootstrap / composition root com fallback offline

O `GameBootstrap` é o **composition root** do projeto: o ÚNICO lugar que registra e inicializa managers, em ordem explícita. **Banido o auto-singleton `public static X i` setado em `Awake`** — com 18 managers (CANON §13), ordem de `Awake` é loteria de serialização e a falha aparece como NullReference intermitente em device (anti-exemplos catalogados: singletons sem ordem, `instance = new MonoBehaviour()`, `FindObjectsOfType` no getter — ver `15-referencias-e-recursos.md` §3.5). Cada manager expõe `Instance` somente-leitura atribuído dentro de `Init()`, chamado AQUI.

Ordem canônica dos serviços persistentes: **Save → RemoteConfig → Analytics → Ads/IAP → Economy → Upgrade → Unit → Reward → Audio/VFX → UI.** Racional: o save define quem é o jogador (e flags como `adsRemoved` e consentimento) antes de qualquer SDK; Remote Config precisa vir antes de Ads para ditar pacing; economia/meta antes da UI; UI só abre quando o estado do jogador é confiável. A cena `Game` tem um `GameSceneBootstrap` com a mesma regra para os managers de gameplay: **Level → Crowd → Gate → Boss → Combat.**

```csharp
// Core/GameBootstrap.cs — composition root. Vive no prefab [Services] da cena Boot.
public class GameBootstrap : MonoBehaviour
{
    [Header("Managers do prefab [Services] — a ordem dos campos É a ordem de init")]
    [SerializeField] private SaveSystem _save;
    [SerializeField] private RemoteConfigManager _remoteConfig;
    [SerializeField] private AnalyticsManager _analytics;
    [SerializeField] private AdsManager _ads;
    [SerializeField] private IAPManager _iap;
    [SerializeField] private EconomySystem _economy;
    /* ... demais managers persistentes (CANON §13), na ordem canônica acima ... */

    private async void Start()
    {
        DontDestroyOnLoad(gameObject);                    // 1 chamada, no root [Services]

        // 1. SAVE (local, síncrono-rápido, NUNCA depende de rede)
        _save.Init();                                     // checksum + backup fallback

        // 2. FIREBASE CORE + REMOTE CONFIG (timeout 3 s; falhou → cache → defaults)
        bool online = await FirebaseBootstrap.InitAsync(timeoutMs: 3000);
        await _remoteConfig.InitAsync(online, timeoutMs: 3000);

        // 3. ANALYTICS (consentimento lido do save; eventos pré-init ficam na fila)
        _analytics.Init(online, _save.Data.consentStatus);

        // 4. ADS — MAX dispara o fluxo de consent (Google UMP) na 1ª sessão em região GDPR
        if (!_save.Data.adsRemoved) _ads.Init();
        _iap.Init();                                      // RevenueCat (não bloqueia)

        // 5. META (economia, upgrades, catálogo de tropas, recompensas)
        _economy.Init(); /* ... */

        // 6. UI — Main abre mesmo que 2–4 ainda estejam pendentes em background
        await SceneManager.LoadSceneAsync("Main");
    }
}
```

**Strings mágicas são banidas junto:** estados de jogo, placements de ads, chaves de Remote Config e ids de tela vivem em enums/`const`/`AssetReference` tipados (`GameState`, `AdPlacement`, `RcKeys`) — string espalhada é o bug latente clássico dos repos estudados (Trash Dash, ver doc 15 §2).

**Sem rede:** o jogo abre normalmente; Remote Config usa o último fetch em cache (ou defaults compilados); botões de rewarded somem (nunca botão morto); loja IAP mostra estado "reconectando"; Firestore sync e fila de analytics drenam quando a conexão voltar. Nenhum passo do Boot bloqueia além do timeout de 3 s.

---

## 4. Classes principais em C# (Entregável 17)

> Esqueletos reais e compiláveis (lógica central comentada; corpos triviais omitidos com `/* ... */`). Managers expõem `Instance` somente-leitura atribuído no `Init()` chamado pelo bootstrap em ordem explícita (§3.3) — **nunca auto-registro em `Awake`**. Sem framework de DI no MVP.

### 4.1 GameManager — máquina de estados em PILHA

**Decisão: máquina em pilha, não enum plano.** O motivo é o revive no boss (CANON §11): `ReviveOffer` e `GameOver` entram por **push SOBRE o estado da corrida** — exército, mutações, RunWallet e progresso da pista ficam intactos embaixo; aceitar o revive é um `Pop()` que devolve EXATAMENTE a corrida. Mesmo mecanismo serve para `BossArena` sobre a corrida e para pausa. Padrão validado no Trash Dash (ver `15-referencias-e-recursos.md` §3.5), reimplementado do zero. Transições são **validadas por tabela** — transição ilegal loga erro e é ignorada, nunca corrompe o fluxo (anti-exemplo: `CheckFail` setando estado todo frame sem checar o atual).

```csharp
public enum GameState { Boot, MainMenu, BossScout, Running, BossFight, ReviveOffer, Victory, Defeat }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState State => _stack.Peek();
    public LevelConfigSO CurrentLevel { get; private set; }

    private readonly Stack<GameState> _stack = new();
    private bool _endSelectionDone;                       // guard anti duplo-clique no fim de fase

    // Tabela de transições válidas — tudo fora dela é bug e loga erro (sem string mágica: só enum).
    private static readonly Dictionary<GameState, GameState[]> Allowed = new()
    {
        [GameState.Boot]        = new[] { GameState.MainMenu },
        [GameState.MainMenu]    = new[] { GameState.BossScout },
        [GameState.BossScout]   = new[] { GameState.Running },
        [GameState.Running]     = new[] { GameState.BossFight, GameState.Defeat },
        [GameState.BossFight]   = new[] { GameState.Victory, GameState.Defeat },
        [GameState.Victory]     = new[] { GameState.MainMenu, GameState.BossScout },  // próxima fase sem loading (§2.2)
        [GameState.Defeat]      = new[] { GameState.MainMenu, GameState.BossScout },
    };

    public void StartLevel(LevelConfigSO level)
    {
        CurrentLevel = level;
        _endSelectionDone = false;
        AnalyticsManager.Instance.LogLevelStart(level.levelIndex);
        ChangeState(GameState.BossScout);        // cartão de ~2 s ANTES da corrida (CANON §3.1)
    }

    public void ChangeState(GameState next)      // troca o TOPO da pilha (transição lateral)
    {
        if (!Allowed.TryGetValue(State, out var ok) || Array.IndexOf(ok, next) < 0)
        { Debug.LogError($"Transição ilegal {State}→{next}"); return; }
        ExitState(_stack.Pop());
        _stack.Push(next);
        EnterState(next);
    }

    // PUSH: estado temporário SOBRE a corrida — quem está embaixo congela, não morre.
    public void PushState(GameState overlay) { _stack.Push(overlay); EnterState(overlay); }
    public void PopState() { ExitState(_stack.Pop()); /* estado de baixo retoma intacto */ }

    // Derrota no boss com revive disponível: PUSH preserva a luta (CANON §11: 1×/fase).
    public void OfferRevive()
    {
        if (SaveSystem.Instance.Data.usedReviveThisLevel) { ChangeState(GameState.Defeat); return; }
        PushState(GameState.ReviveOffer);                  // BossFight continua vivo embaixo
    }
    public void ResolveRevive(bool revived)
    {
        PopState();                                        // sai do ReviveOffer
        if (revived) CrowdManager.Instance.Revive();       // segunda chance + invencibilidade breve
        else ChangeState(GameState.Defeat);
    }

    private void EnterState(GameState s)
    {
        switch (s)
        {
            case GameState.BossScout: UIManager.Instance.ShowBossScout(CurrentLevel.boss, 2f,
                                       onDone: () => ChangeState(GameState.Running)); break;
            case GameState.Running:   LevelManager.Instance.BeginRun(CurrentLevel); break;
            case GameState.BossFight: BossManager.Instance.BeginFight(CurrentLevel.boss); break;
            case GameState.Victory:   ResolveEnd(won: true);  break;
            case GameState.Defeat:    ResolveEnd(won: false); break;
        }
    }

    private void ResolveEnd(bool won)
    {
        if (_endSelectionDone) return;                     // duplo-clique/duplo-evento: ignora
        _endSelectionDone = true;
        var result = LevelManager.Instance.BuildResult(won);   // sobreviventes, dano, moedas
        EconomySystem.Instance.CommitRun(won);             // RunWallet → carteira (§4.6); XP sempre
        if (won) EconomySystem.Instance.GrantLevelReward(CurrentLevel.levelIndex);
        SaveSystem.Instance.RecordLevelEnd(result);        // save imediato pós-fase
        GameEvents.RaiseLevelFinished(result);             // UI/Ads/Analytics reagem
    }
    private void ExitState(GameState s) { /* cleanup por estado */ }
    public void Init() { Instance = this; _stack.Push(GameState.Boot); }   // chamado pelo GameBootstrap (§3.3)
}
```

### 4.2 CrowdManager — simulação SoA, formação, contagem, Supply

**Arquitetura: manager central com dados em SoA (Structure of Arrays), tickados num ÚNICO loop.** Unidades não são GameObjects pensantes — são índices em arrays paralelos (`positions`, `velocities`, `typeIds`, `hp`, `flags`). **Proibições explícitas** (anti-exemplos medidos no estudo — ver `15-referencias-e-recursos.md` §3.1): 1 MonoBehaviour/`Update` por unidade; `Rigidbody` dinâmico por unidade; `NavMeshAgent` por unidade; collider de física por unidade. O layout já nasce **Jobs/Burst-ready**: no MVP (Supply 60) roda single-thread managed; quando o profiling pedir (teto 300 pós-MVP), migrar é trocar o contêiner por `NativeArray` + `IJobParallelFor` sem tocar no layout — ciclo schedule-cedo no `Update` / `Complete` no `LateUpdate` / `Dispose` em `OnDestroy` (template de Jobs do estudo, doc 15 §3.1). "Refatorar para manager depois" mata a feature — nasce centralizado.

```csharp
public class CrowdManager : MonoBehaviour
{
    public static CrowdManager Instance { get; private set; }

    // ---- Dados SoA: arrays paralelos, índice = unidade; [0.._count) = vivas ----
    private Vector3[] _positions;  private Vector3[] _velocities;
    private byte[]    _typeIds;    private float[]  _hp;
    private byte[]    _flags;                                 // bit0 alive · bit1 dying (anim de desmonte)
    private int[]     _slot;                                  // ledger: unidade i ocupa o slot de formação _slot[i]
    private int       _count;
    private SpatialGridXZ _grid;                              // vizinhança p/ separação (§6.2); reusada pelo CombatSystem
    private readonly MutationConfigSO[] _mutationSlots = new MutationConfigSO[3]; // CANON §3.3
    private int _nextMutationSlot;

    public int Count => _count;
    public int SupplyCap { get; private set; } = 60;          // MVP fixo; meta eleva até 300
    public int SupplyUsed { get; private set; }
    public Vector3 Centroid { get; private set; }             // consumido pela CameraRig (§4.12)

    public void Init()                                        // chamado pelo GameSceneBootstrap (§3.3)
    {
        Instance = this;
        int max = 300 + 32;                                   // teto canônico de Supply + folga de transição
        _positions = new Vector3[max]; _velocities = new Vector3[max];
        _typeIds = new byte[max]; _hp = new float[max]; _flags = new byte[max]; _slot = new int[max];
        _grid = new SpatialGridXZ(cellSize: 0.9f, capacity: max);
    }
    private void OnDestroy() { /* quando o passo Jobs/Burst entrar: Dispose de todo NativeArray AQUI */ }

    // FUNIL ÚNICO de mutação de contagem: TODO portal entrega um TOTAL-ALVO (§4.3) e o
    // manager reconcilia atual→alvo aqui — spawn/despawn da diferença + Supply no mesmo funil.
    public void ReconcileTo(int targetCount, UnitConfigSO spawnType)
    {
        int delta = targetCount - _count;
        if (delta > 0) SpawnUnits(spawnType, delta);          // pool por tipo + Reset() no Get (§6.4)
        else if (delta < 0) RemoveUnits(-delta);              // lote: de trás pra frente, 1 VFX agregado
        EnforceSupplyCap();
        GameEvents.RaiseCrowdChanged(_count, SupplyUsed);
    }

    // CANON §3.2: excedente vira moedas COM FANFARRA — nunca parece punição. A conversão
    // usa METERING (1 unidade a cada ~80 ms): espetáculo sequencial, nunca frame-spike.
    private void EnforceSupplyCap()
    {
        if (SupplyUsed <= SupplyCap) return;
        // converte as unidades MAIS BARATAS primeiro; nunca zera o exército (mínimo 1)
        /* ordena índices por supplyCost, enfileira o excedente no ConversionMeter;
           cada "pop" do meter: RemoveUnits(1) + Earn(coins) + moeda voadora (exceção do
           RunWallet: overflow credita NA HORA — §4.6) */
        GameEvents.RaiseSupplyOverflow(new SupplyOverflow(/* removidas, moedas */));
    }

    // Formação: slot filotáxico (girassol/Vogel) calculado O(1) POR ÍNDICE — n arbitrário
    // até o teto de Supply, sem capacidade precomputada (capacidade fixa dessincroniza a
    // UI quando estoura — anti-exemplo do estudo). Ledger _slot[] permite reordenar:
    // tropas grandes (Gigante, Supply 12) recebem índices baixos = centro da formação.
    public static Vector2 GetSlotOffset(int slotIndex)
    {
        const float spacing = 0.45f, golden = 2.39996f;       // 137,5° em rad
        float r = spacing * Mathf.Sqrt(slotIndex + 1);
        return new Vector2(r * Mathf.Cos(slotIndex * golden), r * Mathf.Sin(slotIndex * golden));
    }

    private void Update()                                     // O ÚNICO loop da multidão no jogo
    {
        _grid.Rebuild(_positions, _count);                    // 1×/frame, zero alloc
        float dt = Time.deltaTime;
        Vector3 anchor = CrowdAnchor.Position;                // líder: segue o input lateral (drag)
        Vector3 sum = default;
        for (int i = 0; i < _count; i++)
        {
            Vector2 slot = GetSlotOffset(_slot[i]);
            Vector3 target = anchor + new Vector3(slot.x, 0f, slot.y);
            Vector3 steer  = (target - _positions[i]) * ConvergeGain   // converge ao slot (auto-curativa)
                           + Separation(i);                            // SÓ separação local — coesão e
                                                                       // alinhamento vêm de graça do corredor+slots
            _velocities[i] = Vector3.Lerp(_velocities[i], steer, 1f - Mathf.Exp(-12f * dt));
            _positions[i] += _velocities[i] * dt;
            sum += _positions[i];
        }
        Centroid = _count > 0 ? sum / _count : Centroid;      // cache: mantém último valor se zerar
        CrowdRenderer.Submit(_positions, _typeIds, _flags, _count);   // instancing por tipo (§6.2)
    }

    // Separação com falloff linear Clamp01(1 − d/raio); vizinhos via grid uniforme em XZ —
    // NUNCA Physics.OverlapSphere por unidade. Guards: d mínimo e 0 vizinhos (anti-NaN).
    private Vector3 Separation(int i)
    {
        Vector3 force = default; int n = 0;
        foreach (int j in _grid.Neighbors(i))
        {
            Vector3 away = _positions[i] - _positions[j];
            float d = away.magnitude;
            if (d < 1e-4f) { away = JitterFor(i); d = 1e-4f; }         // sobrepostas: desempate estável
            force += (away / d) * Mathf.Clamp01(1f - d / SeparationRadius); n++;
        }
        return n == 0 ? default : force * (SeparationGain / n);
    }

    public void ApplyMutation(MutationConfigSO m)             // 4ª mutação substitui a mais antiga
    {
        _mutationSlots[_nextMutationSlot] = m;
        _nextMutationSlot = (_nextMutationSlot + 1) % 3;
        GameEvents.RaiseMutationGained(m);                    // shader flags + UI (§6.2)
    }

    // Morte: estado "dying" + pool — NUNCA Destroy. OnDeath é ponto de extensão (doc 15 §3.4):
    // Supply liberado aqui; VFX de desmonte em peças e analytics assinam o evento, sem acoplamento.
    public void KillUnit(int i)
    {
        _flags[i] |= 0b10;                                    // dying: anima o desmonte antes do Release
        SupplyUsed -= UnitManager.Instance.GetSupplyCost(_typeIds[i]);
        GameEvents.RaiseUnitDied(new UnitDeath(_typeIds[i], _positions[i]));
    }

    public void ConvertClass(UnitConfigSO target, float fraction) { /* portal de classe: muda typeId, Supply re-checado no funil */ }
    public void SetElement(ElementType e) { /* portal de elemento: aplica ao exército inteiro */ }
    public float GetTotalDps(ElementType vsElement) { /* soma DPS × chart × mutações, p/ CombatSystem */ return 0f; }
    public void DamageArea(float area, float damage) { /* especial do boss: dano por índice via grid (§4.4) */ }
    public void EnterArenaFormation() { /* handoff sim→cinemática: congela separação, easing p/ slots de combate (§4.5) */ }
    public void Revive() { /* revive do rewarded: restaura fração do exército + invencibilidade breve (§4.1) */ }
    private void SpawnUnits(UnitConfigSO type, int n) { /* pool Get + Reset; scatter ao redor do líder → converge ao slot */ }
    private void RemoveUnits(int n) { /* de trás pra frente (swap-back nos arrays), 1 VFX agregado p/ o lote */ }
}
```

**Contrato de colisão do exército:** zero colliders de física nas unidades. Portais e gatilhos de arena detectam o exército com **1 trigger contra o AABB/centroid do grupo** — exatamente 1 evento por exército por portal (nunca N `OnTriggerEnter` × N unidades — §4.3). Obstáculos testam overlap diretamente contra `_positions` com broadphase por faixa de pista (§6.2).

### 4.3 GateManager — pares honestos gerados contra o boss

**Contrato GatePair → CrowdManager** (formalizado a partir do estudo — ver `15-referencias-e-recursos.md` §3.2):

1. **Par com referências serializadas.** O `GatePairView` referencia os dois meios-portais por campo `[SerializeField]` — nunca `GetChild(0)/GetChild(1)` (índice de filho quebra ao reordenar o prefab).
2. **Consumo one-shot com flag no manager DO PAR.** A 1ª unidade/trigger que toca consome o par inteiro; toques subsequentes (inclusive no irmão) retornam cedo. Callbacks de física são síncronos — sem race. Resultado: **exatamente 1 evento `OnGateConsumed` por exército por par.** Como pares são pooled (§6.4), `Setup()` SEMPRE reseta a flag (flag que nunca reseta = portal morto no chunk reciclado).
3. **Efeito = função pura `int Apply(int current)` com semântica de TOTAL-ALVO.** O portal calcula o total-alvo; o `CrowdManager.ReconcileTo()` reconcilia atual→alvo num único funil — onde o Supply check também mora (§4.2). Nunca aplicar multiplicador como delta por fora.
4. **Rótulo é RENDERIZADO do dado** (`GateConfigSO.displayLabel` + ícone + cor) via `OnValidate` — designer vê a fórmula na cena sem play mode; texto digitado à mão na cena é a antítese do pilar "portais honestos" (CANON §3.4). **Contra-escala do texto:** quando o mesh do portal escala (largura por mundo), o rótulo aplica escala inversa — legível em 3 s em qualquer largura (Pilar 1).

```csharp
public class GatePairView : MonoBehaviour
{
    [SerializeField] private GateView _left, _right;          // referências SERIALIZADAS (regra 1)
    private bool _consumed;                                   // one-shot do PAR (regra 2)

    public void Setup(GateConfigSO l, GateConfigSO r, float trackPos)
    {
        _consumed = false;                                    // pool reusa: SEMPRE resetar a flag
        _left.Bind(l); _right.Bind(r);
        /* posiciona na âncora do segmento (§4.11) */
    }

    public void OnArmyTouched(GateView touched)               // 1 trigger contra o AABB do exército (§4.2)
    {
        if (_consumed) return;
        _consumed = true;
        _left.DisableCollider(); _right.DisableCollider();    // ambos no MESMO frame; descarte anima depois
        GateManager.Instance.Consume(touched.Config, OtherOf(touched).Config);
    }
    public GateView OtherOf(GateView g) => g == _left ? _right : _left;
}

public class GateView : MonoBehaviour
{
    [SerializeField] private GateConfigSO _config;
    [SerializeField] private TMP_Text _label;
    public GateConfigSO Config => _config;

    public void Bind(GateConfigSO c) { _config = c; RenderLabel(); }

    private void RenderLabel()                                // rótulo SEMPRE derivado do dado (regra 4)
    {
        _label.text = _config.displayLabel;                   // "+10" · "x2" · "70% x10 / 30% −½"
        _label.transform.localScale = Vector3.one / transform.lossyScale.x;   // contra-escala
        /* material/cor do portal a partir do SO */
    }
#if UNITY_EDITOR
    private void OnValidate() { if (_config != null && _label != null) RenderLabel(); }  // preview sem play
#endif
}

public class GateManager : MonoBehaviour
{
    public static GateManager Instance { get; private set; }
    [SerializeField] private GatePairView _pairPrefab;        // 1 prefab = par L/R

    // CANON §3.1/§3.4: geração considera o boss; sempre ≥1 rota ótima e ≥1 armadilha plausível.
    // Chamado pelo LevelManager ao popular as âncoras de cada segmento (§4.11), com a MESMA
    // seed determinística da fase — pares reproduzíveis em QA.
    public void SpawnGates(LevelConfigSO level, System.Random rng)
    {
        foreach (var slot in level.gateSlots)                 // posições ao longo da pista
        {
            GateConfigSO left = slot.leftGate, right = slot.rightGate;
            if (slot.autoBalance)                             // fases geradas: escolhe par coerente
                (left, right) = PickPairForBoss(level.boss, slot.depth01, rng);
            var pair = Pool.Get(_pairPrefab);
            pair.Setup(left, right, slot.trackPosition);      // número/ícone/% SEMPRE visíveis
        }
    }

    private (GateConfigSO, GateConfigSO) PickPairForBoss(BossConfigSO boss, float depth01, System.Random rng)
    {
        // Rota ótima: portal cujo elemento/classe explora boss.weaknesses.
        // Armadilha: número maior bruto, mas elemento resistido ou estouro de Supply.
        /* seleção ponderada a partir do pool de GateConfigSO da fase, via rng (nunca UnityEngine.Random) */
        return default;
    }

    // Funil único do consumo: efeito puro → total-alvo → CrowdManager reconcilia (regra 3).
    public void Consume(GateConfigSO gate, GateConfigSO rejected)
    {
        var crowd = CrowdManager.Instance;
        switch (gate.gateType)
        {
            case GateType.AddFlat:
            case GateType.Multiply:     crowd.ReconcileTo(gate.Apply(crowd.Count), gate.unitToAdd); break;
            case GateType.ClassConvert: crowd.ConvertClass(gate.unitToAdd, gate.value); break;
            case GateType.Element:      crowd.SetElement(gate.element); break;
            case GateType.Mutation:     crowd.ApplyMutation(gate.mutation); break;
            case GateType.Risk:         RiskResolver.Begin(gate); break;   // "x10 se sobreviver à zona"
        }
        GameEvents.RaiseGateConsumed(new GateResult(gate, crowd.Count));
        AnalyticsManager.Instance.LogGateSelected(gate.gateId, rejected.gateId);
    }
    public void Init() => Instance = this;
}
```

**Suíte de unit tests obrigatória (MVP, roda no CI e bloqueia merge):** os 8 portais do CANON §10 testados como funções puras, sem cena — `x2 → f(n) = 2n` · `x3 → f(n) = 3n` · `+10 → f(n) = n+10` · `+25 → f(n) = n+25` · `÷2 → f(n) = ⌈n/2⌉` (arredondamento de ímpar conforme doc 04) · Virar Arqueiro, Elemento Fogo e Risco com RNG injetado. **Caso negativo obrigatório:** afirmar que aplicar x2 como DELTA sobre o atual (resultado 3n) FALHA — é o bug real "x2 que triplica" encontrado no estudo (ver doc 15 §3.2). Mais: par consumido 2× no mesmo frame gera 1 só evento; par reciclado do pool nasce com `_consumed = false`.

### 4.4 CombatSystem + ElementChart como dado

```csharp
public class CombatSystem : MonoBehaviour
{
    public static CombatSystem Instance { get; private set; }
    [SerializeField] private ElementChartSO _chart;           // NUNCA switch hardcoded

    // Tick de 10 Hz contra o boss — suficiente p/ combate de 10–20 s, barato em CPU.
    private const float TickRate = 0.1f;
    private float _accum;

    private void Update()
    {
        if (GameManager.Instance.State != GameState.BossFight) return;
        _accum += Time.deltaTime;
        while (_accum >= TickRate)
        {
            _accum -= TickRate;
            float dmg = ComputeCrowdDamage(BossManager.Instance.Current, TickRate);
            BossManager.Instance.ApplyDamage(dmg);
        }
    }

    private float ComputeCrowdDamage(BossRuntime boss, float dt)
    {
        float dps = CrowdManager.Instance.GetTotalDps(boss.Config.element);
        dps *= 1f + UpgradeSystem.Instance.GetBonus(UpgradeTrack.BossDamage);     // +5%/nível
        if (UnityEngine.Random.value < UpgradeSystem.Instance.GetBonus(UpgradeTrack.CritChance))
            dps *= 2f;                                                            // crítico x2
        return dps * dt;
    }

    // Multiplicador elemental consultado por TODO dano do jogo (crowd, boss, obstáculo).
    public float GetElementMultiplier(ElementType attacker, ElementType defender)
        => _chart.GetMultiplier(attacker, defender);

    public void ApplyPoison(IDamageable target) { /* DoT 3% HP/s por 4 s; 0% vs máquina/morto-vivo (chart) */ }
    public void ApplyIceSlow(IDamageable target) { /* lentidão 30% por 2 s, não acumula */ }
    public void ChainLightning(Vector3 origin, float dmg) { /* 50% do dano p/ até 2 inimigos próximos */ }
    public void Init() => Instance = this;
}
```

**Combate é agregado, não unidade-a-unidade.** O modelo é HP/DPS agregados resolvidos por tick: o crowd ataca como soma de DPS (× chart elemental × mutações × upgrades), e dano recebido distribui-se pelos índices das unidades. **Aquisição de alvo é centralizada no `CombatSystem`** — reusa a grid XZ do CrowdManager (§4.2) para achar inimigos/alvos por proximidade em lote; nenhuma unidade "procura" alvo sozinha. Anti-exemplos que confirmam a decisão (ver `15-referencias-e-recursos.md` §3.4): `Find` por tag em `Update` de cada unidade, pareamento 1v1 por coroutine (bug admitido em TODO no próprio repo) e cooldown de contato espelhado por par. O chart elemental entra **uma vez, no cálculo do tick agregado** — nunca por colisão individual.

### 4.5 BossManager — fases de vida, waves como dados, telegraph testável

Três decisões vindas do estudo (ver `15-referencias-e-recursos.md` §3.4):

1. **Waves da arena são DADOS no `BossConfigSO`:** lista ordenada por tempo, consumida com **ponteiro de próximo evento** (`while (t >= waves[next].time) Spawn(waves[next++])`) — nunca polling `(int)timer == x`, que pula ou duplica eventos conforme o framerate.
2. **Telegraphs e cooldowns são timers puros C#** (classe sem MonoBehaviour, `Tick(dt)` externo) — unit-testáveis sem cena: "especial dispara após o windup", "cooldown encurta por fase" viram asserts, não playtests.
3. **Entrada na arena = handoff sim→cinemática:** a separação local congela, as unidades fazem easing até os slots de formação de combate e o movimento vira determinístico durante a entrada do boss (≤2 s, CANON §6) — espetáculo controlado, sem o crowd "fervendo" atrás da cinemática.

```csharp
// Core/Countdown.cs — timer puro, sem MonoBehaviour: testável com Tick(dt) sintético.
public sealed class Countdown
{
    public float Remaining { get; private set; }
    public bool Done => Remaining <= 0f;
    public void Set(float seconds) => Remaining = seconds;
    public void Tick(float dt) => Remaining = Mathf.Max(0f, Remaining - dt);
}

public class BossManager : MonoBehaviour
{
    public static BossManager Instance { get; private set; }
    public BossRuntime Current { get; private set; }
    private static readonly float[] PhaseThresholds = { 0.5f, 0.25f };   // 3 fases de agressividade (limiares canônicos do doc 05 §2.3, idênticos aos movesets, ao stagger e à barra segmentada do doc 09 §4.3)

    private readonly Countdown _specialCooldown = new(), _telegraph = new();
    private bool _telegraphing;
    private int _nextWaveEvent;                                          // ponteiro: nunca varre a lista

    public void BeginFight(BossConfigSO config)
    {
        Current = new BossRuntime(config, RemoteConfigManager.Instance
                       .GetFloat($"boss_hp_mult_{config.bossId}", 1f));
        _nextWaveEvent = 0; _telegraphing = false;
        CrowdManager.Instance.EnterArenaFormation();                     // handoff sim→cinemática (decisão 3)
        _specialCooldown.Set(config.specialBaseCooldown + config.entranceSeconds);  // entrada ≤ 2 s (CANON §6)
        /* animação de entrada do boss roda em paralelo; luta começa após entranceSeconds */
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.BossFight) return;
        float dt = Time.deltaTime;
        Current.FightTime += dt;

        // Waves da arena: lista ORDENADA + ponteiro (decisão 1).
        var waves = Current.Config.arenaWaves;
        while (_nextWaveEvent < waves.Length && Current.FightTime >= waves[_nextWaveEvent].time)
            SpawnWave(waves[_nextWaveEvent++]);

        // Especial: cooldown → telegraph (decal + windup + som) → golpe (decisão 2).
        _specialCooldown.Tick(dt);
        if (_specialCooldown.Done && !_telegraphing)
        {
            _telegraphing = true;
            _telegraph.Set(Current.Config.telegraphSeconds);             // padrão 1,0 s
            VFXManager.Instance.ShowTelegraph(Current.Config.specialAttackArea,
                                              Current.Config.telegraphSeconds);
        }
        if (_telegraphing) { _telegraph.Tick(dt); if (_telegraph.Done) FireSpecial(); }
    }

    private void FireSpecial()
    {
        _telegraphing = false;
        CrowdManager.Instance.DamageArea(Current.Config.specialAttackArea,
                                         Current.Config.specialAttackDamage);
        _specialCooldown.Set(Current.SpecialCooldown());                 // diminui por fase
    }

    public void ApplyDamage(float raw)
    {
        float final = raw;                                               // chart já aplicado no DPS
        Current.Hp -= final;
        int phase = CurrentPhase();
        if (phase != Current.Phase)
        {
            Current.Phase = phase;                                       // fase nova = especial mais frequente
            if (Current.Config.rotatingWeakness) Current.RotateWeakness();  // Alien Supremo (M8)
            GameEvents.RaiseBossPhaseChanged(new BossPhase(phase, Current.ActiveWeakness));
        }
        if (Current.Hp <= 0f) Die();
    }

    private void Die()
    {
        VFXManager.Instance.SlowMotion(0.3f, 0.8f);                      // golpe final: timeScale global 0,3 por 0,8 s (fixedDeltaTime escala junto)
        RewardSystem.Instance.GrantBossReward(Current.Config);           // gemas/carta/fragmento
        GameManager.Instance.ChangeState(GameState.Victory);
    }
    private void SpawnWave(ArenaWaveEvent w) { /* spawn pooled (§6.4); fim de onda por contagem de OnDeath */ }
    private int CurrentPhase() { /* compara Hp/MaxHp com PhaseThresholds */ return 0; }
    public void Init() => Instance = this;
}
```

### 4.6 EconomySystem — carteira persistente + RunWallet

```csharp
public enum CurrencyType { Coin, Gem, Xp }

public class EconomySystem : MonoBehaviour
{
    public static EconomySystem Instance { get; private set; }
    public long Coins  => SaveSystem.Instance.Data.coins;
    public int  Gems   => SaveSystem.Instance.Data.gems;

    // ---- RunWallet: moeda TEMPORÁRIA da corrida (ver 15-referencias-e-recursos.md §3.5) ----
    // Ganhos da fase acumulam aqui e só COMITAM na vitória; "DOBRAR x2" do rewarded = comitar 2×;
    // derrota descarta as moedas do wallet, mas XP é SEMPRE comitada (derrota nunca zera aprendizado).
    // Exceção canônica: moedas do overflow de Supply são creditadas NA HORA, com fanfarra (§4.2).
    public int RunCoins { get; private set; }
    public int RunXp    { get; private set; }

    public void EarnInRun(int coins, int xp = 0) { RunCoins += coins; RunXp += xp; /* HUD via evento */ }

    public void CommitRun(bool won, int multiplier = 1)       // chamado pelo GameManager.ResolveEnd (§4.1)
    {
        if (won && RunCoins > 0) Earn(CurrencyType.Coin, (long)RunCoins * multiplier, "run_commit");
        if (RunXp > 0) Earn(CurrencyType.Xp, RunXp, "run_xp");    // XP comitada SEMPRE, vitória ou derrota
        RunCoins = 0; RunXp = 0;
    }

    // CANON §8: fase 1 = 100 moedas; cresce 1,10^(fase−1); recalibrável por Remote Config.
    public int GetLevelReward(int levelIndex)
    {
        float baseReward = RemoteConfigManager.Instance.GetFloat("level_reward_base", 100f);
        float growth     = RemoteConfigManager.Instance.GetFloat("level_reward_growth", 1.10f);
        float mult       = 1f + UpgradeSystem.Instance.GetBonus(UpgradeTrack.RewardMultiplier);
        return Mathf.RoundToInt(baseReward * Mathf.Pow(growth, levelIndex - 1) * mult);
    }

    public void GrantLevelReward(int levelIndex) => Earn(CurrencyType.Coin, GetLevelReward(levelIndex), "level_win");

    public void Earn(CurrencyType type, long amount, string source)
    {
        if (amount <= 0) return;
        var d = SaveSystem.Instance.Data;
        switch (type)
        {
            case CurrencyType.Coin: d.coins += amount; break;
            case CurrencyType.Gem:  d.gems += (int)amount; break;
            case CurrencyType.Xp:   d.playerXp += (int)amount; /* level-up check do nv de jogador */ break;
        }
        SaveSystem.Instance.MarkDirty();
        GameEvents.RaiseCurrencyChanged(new CurrencyChange(type, amount, source));
    }

    public bool TrySpend(CurrencyType type, long amount, string sink)
    {
        var d = SaveSystem.Instance.Data;
        long balance = type == CurrencyType.Coin ? d.coins : d.gems;
        if (balance < amount) return false;
        if (type == CurrencyType.Coin) d.coins -= amount; else d.gems -= (int)amount;
        SaveSystem.Instance.MarkDirty();                       // save em batch, não por transação
        GameEvents.RaiseCurrencyChanged(new CurrencyChange(type, -amount, sink));
        return true;
    }
    public void Init() => Instance = this;
}
```

### 4.7 SaveSystem — modelo de dados, checksum, migração e flush

**Base de código: SaveGameFree (MIT, adaptado com notice — ver `15-referencias-e-recursos.md` §4) + nossa camada de checksum + merge Firestore.** O "JSON com checksum" do CANON §13 ganha aqui quatro garantias de produção: **(a)** `schemaVersion` com **migração incremental por gates de versão** (cada `if (ver < N)` roda em sequência — um save da v1 passa por v2 e v3 até o atual; nunca `switch` exclusivo, que pula etapas); **(b)** **gravação atômica** (escreve em `.tmp`, depois rename — queda de energia no meio do write nunca corrompe o save principal); **(c)** **flush garantido** em `OnApplicationFocus(false)`, `OnApplicationPause(true)` e `OnApplicationQuit` — os três, porque Android e iOS divergem em qual callback chega primeiro (e em swipe-kill só alguns chegam); **(d)** **save assíncrono com dirty flag centralizado**: durante a corrida só se marca `MarkDirty()`; o `Save()` real acontece nas transições de estado do GameManager (§4.1) — nunca I/O síncrono no meio da corrida a 60 fps.

```csharp
[Serializable] public class SaveData
{
    public int schemaVersion = 1;                 // migração entre versões do app
    public string playerId;                       // Firebase anon UID (vazio offline)
    public long firstLaunchUnixUtc, lastSaveUnixUtc;

    // Progresso
    public int playerLevel = 1, playerXp;         // nv2 Upgrades, nv3 Baús... (CANON §8)
    public int highestLevelCleared;               // gate de desbloqueio do mapa
    public List<LevelRecord> levelRecords = new();

    // Moedas e tropas
    public long coins; public int gems;
    public List<UnitProgress> units = new();      // nível 1–10 + fragmentos por tropa
    public List<string> ownedSkinIds = new(); public string equippedSkinId = "soldier_default";

    // Meta
    public List<TrackProgress> upgradeTracks = new();   // 4 trilhas no MVP
    public int supplyCap = 60;                          // fixo no MVP (CANON §15)

    // Monetização / ads pacing (regras do CANON §11 dependem destes campos)
    public bool adsRemoved; public bool seasonPassActive; public long seasonPassExpiryUnix;
    public string starterOfferState = "eligible";       // eligible|shown|purchased|expired (48 h)
    public int levelsSinceInterstitial; public int consecutiveDefeats; public bool usedReviveThisLevel;

    // Retenção / sessão
    public long lastDailyChestUnix; public int loginStreak; public int sessionCount;

    // Configurações e consentimento
    public bool sfxOn = true, musicOn = true, hapticsOn = true;
    public string consentStatus = "unknown";            // resultado UMP cacheado
}
[Serializable] public class UnitProgress  { public string unitId; public int level = 1; public int shards; public bool unlocked; }
[Serializable] public class TrackProgress { public string trackId; public int level; }
[Serializable] public class LevelRecord   { public int levelIndex; public bool won; public int bestSurvivors; public float bestTime; }

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }
    public SaveData Data { get; private set; }
    private string MainPath   => Path.Combine(Application.persistentDataPath, "save.json");
    private string BackupPath => Path.Combine(Application.persistentDataPath, "save.bak");
    private bool _dirty;

    public void Init() { Instance = this; Load(); }          // chamado 1º pelo GameBootstrap (§3.3)

    public void Load()
    {
        Data = TryLoad(MainPath) ?? TryLoad(BackupPath) ?? new SaveData
               { firstLaunchUnixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        Migrate(Data);                                       // schemaVersion antigo → atual
    }

    private SaveData TryLoad(string path)
    {
        if (!File.Exists(path)) return null;
        string raw = File.ReadAllText(path);
        int split = raw.IndexOf('\n');                       // linha 1 = checksum, resto = JSON
        string checksum = raw[..split], json = raw[(split + 1)..];
        if (Checksum(json) != checksum) return null;         // corrompido/adulterado → backup
        return JsonUtility.FromJson<SaveData>(json);
    }

    public void Save()                                       // atômico: tmp → replace; main → bak
    {
        Data.lastSaveUnixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string json = JsonUtility.ToJson(Data);              // serializa NA main thread (snapshot)
        string payload = Checksum(json) + "\n" + json;
        string tmp = MainPath + ".tmp";
        File.WriteAllText(tmp, payload);
        if (File.Exists(MainPath)) File.Copy(MainPath, BackupPath, overwrite: true);
        File.Delete(MainPath); File.Move(tmp, MainPath);
        _dirty = false;
        CloudSync.PushAsync(Data);                           // Firestore; no-op offline (re-tenta depois)
    }

    // Save assíncrono: snapshot serializado na main thread, I/O em Task — usado nas
    // transições de estado; o Save() síncrono fica só para os callbacks de saída do app.
    public Task SaveAsync()
    {
        string json = JsonUtility.ToJson(Data);
        string payload = Checksum(json) + "\n" + json;
        _dirty = false;
        return Task.Run(() => WriteAtomic(payload));         // mesma sequência tmp→bak→rename
    }

    public void MarkDirty() => _dirty = true;                // ÚNICO ponto de "preciso salvar";
                                                             // flush centralizado nas transições de estado (§4.1)
    private static string Checksum(string json)
    { /* SHA256(json + SALT estático) em hex — barra tampering casual de moedas */ return ""; }

    private static void Migrate(SaveData d)
    {   // Gates INCREMENTAIS: executam em sequência — save v1 atravessa v2, v3... até o atual.
        if (d.schemaVersion < 2) { /* v2: novo campo com default seguro, patch de legados */ d.schemaVersion = 2; }
        if (d.schemaVersion < 3) { /* v3: ... */ d.schemaVersion = 3; }
        // NUNCA switch exclusivo por versão (pularia etapas); NUNCA remover um gate antigo.
    }

    public void RecordLevelEnd(LevelResult r) { /* atualiza records, streaks, consecutiveDefeats; SaveAsync() */ }

    // Flush triplo: Android/iOS divergem em qual callback chega (e swipe-kill entrega só alguns).
    private void OnApplicationFocus(bool focused) { if (!focused && _dirty) Save(); }
    private void OnApplicationPause(bool paused)  { if (paused && _dirty) Save(); }
    private void OnApplicationQuit()              { if (_dirty) Save(); }
    private static void WriteAtomic(string payload) { /* tmp → bak → rename */ }
}
```

**Testes obrigatórios do SaveSystem** (cobrem bugs reais encontrados no estudo — ver `15-referencias-e-recursos.md` §3.5): **(a)** chave duplicada no payload JSON não corrompe o load (vence a última, com warning); **(b)** cast de tipos no round-trip — int salvo/float lido, `is T` sobre objeto desserializado — coberto para TODOS os campos do `SaveData`; **(c)** checksum inválido cai para o backup; **(d)** save de cada `schemaVersion` antiga migra até a atual sem perda de moedas/progresso; **(e)** kill do app entre `tmp` e `rename` preserva o save anterior.

**Conflito de sync (local vs Firestore):** vence o maior `lastSaveUnixUtc`, com merge aditivo de moedas premium comprovadas por recibo (RevenueCat) para nunca perder compra. Detalhe do fluxo no doc 08.

### 4.8 AdsManager — AppLovin MAX

```csharp
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }
    private const string SdkKey = "<MAX_SDK_KEY>";
    private const string RewardedId = "<MAX_REWARDED_UNIT>", InterstitialId = "<MAX_INTER_UNIT>";
    private Action<bool> _pendingReward;                      // callback do placement atual

    public void Init()                                        // chamado pelo GameBootstrap (§3.3)
    {
        Instance = this;
        MaxSdkCallbacks.OnSdkInitializedEvent += _ => { LoadRewarded(); LoadInterstitial(); };
        MaxSdk.SetSdkKey(SdkKey);
        MaxSdk.InitializeSdk();                               // dispara UMP/consent automático (§7.3)
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (_, _, _) => _pendingReward?.Invoke(true);
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent        += (_, _) => { _pendingReward = null; LoadRewarded(); };
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent    += (_, _) => Invoke(nameof(LoadRewarded), 5f); // retry backoff
    }

    public bool IsRewardedReady => MaxSdk.IsRewardedAdReady(RewardedId);   // UI esconde botão se false

    public void ShowRewarded(string placement, Action<bool> onResult)     // double_reward, revive_boss,
    {                                                                      // daily_chest, try_legendary, speed_upgrade
        if (!IsRewardedReady) { onResult(false); return; }
        _pendingReward = onResult;
        AnalyticsManager.Instance.LogRewardedShown(placement);
        MaxSdk.ShowRewardedAd(RewardedId, placement);
    }

    // CANON §11: fase ≥ 6 · máx. 1 a cada 3 fases · NUNCA após 2 derrotas seguidas · tudo via Remote Config.
    public void MaybeShowInterstitial()
    {
        var d = SaveSystem.Instance.Data; var rc = RemoteConfigManager.Instance;
        if (d.adsRemoved) return;
        if (d.highestLevelCleared < rc.GetInt("inter_min_level", 6)) return;
        if (d.levelsSinceInterstitial < rc.GetInt("inter_level_gap", 3)) return;
        if (d.consecutiveDefeats >= 2) return;
        if (!MaxSdk.IsInterstitialReady(InterstitialId)) return;
        d.levelsSinceInterstitial = 0;
        MaxSdk.ShowInterstitial(InterstitialId);
        AnalyticsManager.Instance.LogInterstitialShown();
    }
    private void LoadRewarded() => MaxSdk.LoadRewardedAd(RewardedId);
    private void LoadInterstitial() => MaxSdk.LoadInterstitial(InterstitialId);
}
```

### 4.9 AnalyticsManager — fila offline + Firebase

```csharp
public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }
    private readonly Queue<(string name, Dictionary<string, object> p)> _preInitQueue = new();
    private bool _ready;

    public void Init(bool online, string consent)            // chamado pelo GameBootstrap (§3.3)
    {
        Instance = this;
        bool allowed = consent != "denied";                  // UMP: analytics consent-gated
        FirebaseAnalytics.SetAnalyticsCollectionEnabled(allowed);
        _ready = allowed;
        while (_ready && _preInitQueue.Count > 0) { var e = _preInitQueue.Dequeue(); Send(e.name, e.p); }
    }

    public void Log(string name, Dictionary<string, object> p = null)
    { if (_ready) Send(name, p); else _preInitQueue.Enqueue((name, p)); }   // Firebase persiste offline sozinho

    // Açúcar tipado p/ TODOS os eventos do BRIEF (level_start, gate_selected, boss_defeated...)
    public void LogLevelStart(int lvl) => Log("level_start", new() { ["level"] = lvl });
    public void LogGateSelected(string chosen, string rejected)
        => Log("gate_selected", new() { ["gate"] = chosen, ["rejected"] = rejected }); // mede rota ótima vs armadilha
    public void LogRewardedShown(string placement) => Log("rewarded_ad_shown", new() { ["placement"] = placement });
    public void LogInterstitialShown() => Log("interstitial_shown");
    private void Send(string n, Dictionary<string, object> p) { /* converte p/ Parameter[] do Firebase */ }
}
```

### 4.10 RemoteConfigManager — defaults embutidos

```csharp
public class RemoteConfigManager : MonoBehaviour
{
    public static RemoteConfigManager Instance { get; private set; }

    private static readonly Dictionary<string, object> Defaults = new()
    {   // espelham o CANON — o jogo é jogável de fábrica, sem rede, com estes valores
        ["level_reward_base"] = 100f, ["level_reward_growth"] = 1.10f,
        ["upgrade_cost_base"] = 100f, ["upgrade_cost_growth"] = 1.35f,
        ["inter_min_level"] = 6, ["inter_level_gap"] = 3,
        ["supply_overflow_coin_rate"] = 2, ["supply_cap"] = 60,
        ["boss_hp_global_mult"] = 1f, ["chest_rare_gem_price"] = 300,
    };

    public async Task InitAsync(bool online, int timeoutMs)   // chamado pelo GameBootstrap (§3.3)
    {
        Instance = this;
        await FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(Defaults);
        if (!online) return;                                  // fica nos defaults + último cache ativado
        var fetch = FirebaseRemoteConfig.DefaultInstance.FetchAndActivateAsync();
        await Task.WhenAny(fetch, Task.Delay(timeoutMs));     // nunca segura o boot além do timeout
    }

    public int   GetInt(string k, int fb)     => (int)FirebaseRemoteConfig.DefaultInstance.GetValue(k).LongValue;
    public float GetFloat(string k, float fb) => (float)FirebaseRemoteConfig.DefaultInstance.GetValue(k).DoubleValue;
    public bool  GetBool(string k, bool fb)   => FirebaseRemoteConfig.DefaultInstance.GetValue(k).BooleanValue;
}
```

### 4.11 LevelManager — pista por segmentos com âncoras + seed determinística

A pista é montada de **segmentos-prefab com âncoras**: cada segmento expõe `Transform[]` de pontos de interesse (âncora de GatePair esquerda/direita, âncoras de obstáculo) e é "burro" — quem decide o conteúdo é o `LevelManager`, populando as âncoras a partir do `LevelConfigSO` (contrato de âncoras visto no estudo; população por config, nunca `Random` no `Start` — ver `15-referencias-e-recursos.md` §3.3). A geração usa a **seed determinística da fase**: a mesma fase produz SEMPRE a mesma pista — é isso que viabiliza o contrato do Boss Scout (1 rota ótima + 1 armadilha PLANEJADAS contra o boss, CANON §3.1) e a reprodução exata de bugs em QA ("fase 12, seed do asset").

```csharp
public class TrackSegment : MonoBehaviour
{   // prefab "burro": geometria + âncoras; zero decisão de conteúdo
    public Transform[] gatePairAnchors;       // onde um GatePair L/R pode nascer
    public Transform[] obstacleAnchors;
    public float length = 30f;
    public float EndZ { get; set; }
}

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    private System.Random _rng;               // seed do LevelConfigSO — NUNCA UnityEngine.Random aqui
    private float _furthestZ;
    private readonly Queue<TrackSegment> _liveSegments = new();

    public void BeginRun(LevelConfigSO level)
    {
        _rng = new System.Random(level.seed); // determinístico: mesma fase = mesma pista
        _furthestZ = 0f;
        /* spawna segmentos iniciais (pool §6.4) + GateManager.SpawnGates(level, _rng)
           + popula âncoras de obstáculo respeitando a zona de segurança pós-portal */
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Running) return;
        // SPAWN POR DISTÂNCIA à frente do líder; RECICLAGEM POR DISTÂNCIA atrás — NUNCA
        // por timer (com velocidade variável, o chão some sob o player; anti-exemplo do
        // despawn por 10 s fixos no estudo, doc 15 §3.3).
        float leaderZ = CrowdAnchor.Position.z;
        while (_furthestZ < leaderZ + SpawnAheadMeters) SpawnNextSegment();
        while (_liveSegments.Count > 0 && _liveSegments.Peek().EndZ < leaderZ - RecycleBehindMeters)
            Pool.Release(_liveSegments.Dequeue());            // segmento volta ao pool (§6.4)
    }

    // Soft reset: retry e fase seguinte acontecem na MESMA cena (§2.2) — drena pools,
    // repopula âncoras, zera RunWallet/estado de corrida; nunca SceneManager.LoadScene.
    public void ResetRun() { /* Release de tudo + BeginRun(próximo LevelConfigSO) */ }

    public LevelResult BuildResult(bool won) { /* sobreviventes, dano, moedas da run */ return default; }
    private void SpawnNextSegment() { /* Pool.Get(segmento do WorldConfigSO) + popula âncoras */ }
    public void Init() => Instance = this;
}
```

**Zona de segurança pós-portal:** nenhum obstáculo nos primeiros metros depois de cada GatePair — o exército recém-multiplicado precisa de ~0,5 s para a separação local (§4.2) espalhar os clones; obstáculo nesse intervalo pune a escolha certa do jogador, o oposto do pilar "escolha inteligente" (CANON §2). A regra é aplicada na população das âncoras, não no prefab.

### 4.12 CameraRig — rig independente, nunca filho do player

A câmera vive em rig próprio na cena `Game` — **jamais filha do `CrowdAnchor`/player**, ou herda cada solavanco lateral do drag (anti-exemplo real: Main Camera filha do player numa cena do estudo — ver `15-referencias-e-recursos.md` §3.3). Três regras do rig:

```csharp
public class CameraRig : MonoBehaviour
{
    [SerializeField] private float _damping = 4f;             // k do damping exponencial
    [SerializeField] private Vector3 _baseOffset = new(0f, 9f, -7f);   // retrato 9:16
    private Vector3 _lastCentroid;                            // cache anti-NaN do frame anterior

    private void LateUpdate()                                 // SEMPRE depois da sim do crowd (§4.2)
    {
        // 1) Segue o CENTRÓIDE da multidão, não o líder: multiplicações deslocam a massa.
        //    Se a lista zerar num frame (morte/transição), usa o cache — nunca NaN na câmera.
        Vector3 centroid = CrowdManager.Instance.Count > 0 ? CrowdManager.Instance.Centroid
                                                           : _lastCentroid;
        _lastCentroid = centroid;

        // 2) Damping exponencial Exp(−k·dt): framerate-independente — mesmo enquadramento
        //    a 30 ou 60 fps (Low tier capa em 30, §6.3). Lerp com fator fixo NÃO é.
        float t = 1f - Mathf.Exp(-_damping * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, centroid + DynamicOffset(), t);
    }

    // 3) Enquadramento dinâmico: o raio da formação cresce com √n (§4.2) — a câmera recua
    //    e sobe na mesma proporção para o exército inteiro caber na tela (Pilar 3: espetáculo).
    private Vector3 DynamicOffset()
    {
        float radius = 0.45f * Mathf.Sqrt(CrowdManager.Instance.Count + 1);
        return _baseOffset + new Vector3(0f, radius * 0.6f, -radius * 0.8f);
    }
}
```

### 4.13 UIManager — duas pilhas, safe area única, resultado passivo

```csharp
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    [SerializeField] private RectTransform _root;
    private readonly Stack<UIScreen>  _screens  = new();      // SCR-01..09: telas, com history
    private readonly Stack<UIOverlay> _overlays = new();      // OVL-01..06: Boss Scout, ReviveOffer, pausa...

    public void Init()                                        // chamado pelo GameBootstrap (§3.3)
    {
        Instance = this;
        UIUtils.ResizeToSafeArea(_root);    // safe area resolvida UMA vez, no root —
                                            // nunca por tela e nunca por frame (notch/punch-hole)
    }

    public void Push(UIScreen s)        { /* slide 200 ms; topo anterior fica no history */ }
    public void Pop()                   { /* volta ao anterior */ }
    public void ShowOverlay(UIOverlay o){ /* fade 150 ms SOBRE a tela atual — a tela não sai da pilha */ }
    public void ShowBossScout(BossConfigSO boss, float seconds, Action onDone) { /* OVL-01 */ }
}
```

- **Duas pilhas, não uma:** overlays (OVL-01 Boss Scout, OVL-05 ReviveOffer...) sobem SOBRE SCR-02/03/04 sem destruir a tela de baixo — um `Show<T>` de View única não representa o doc 09 (anti-exemplo do Runner Template — ver `15-referencias-e-recursos.md` §3.5). A pilha de overlays espelha a pilha de estados do GameManager (§4.1): push de `ReviveOffer` no estado = push do OVL-05 na UI.
- **Tela de resultado é PASSIVA e mostra o DELTA.** "+100 moedas" como número principal — nunca o total da carteira (total não comunica o ganho; anti-exemplo do template). Todas as propriedades são preenchidas por sistema externo (EconomySystem/RunWallet, via evento `OnLevelFinished`) ANTES de exibir — a tela não calcula nada, só mostra.
- **Nunca congelar `timeScale` no resultado.** A coreografia da vitória (confete, contagem rolando, voo de moedas — sequência de ~2 s pulável) exige tempo correndo: ou anima com **unscaled time**, ou o gameplay já foi desligado pela transição de estado (§4.1) e o `timeScale` fica em 1. `timeScale = 0` mataria a coreografia e brigaria com o slow motion canônico do VFXManager (§3.1).
- **RunWallet na UI (§4.6):** o HUD da corrida mostra `RunCoins` (temporária, visual distinto da carteira); a vitória comita via `CommitRun(won: true)`; o botão "DOBRAR x2" chama `CommitRun` com `multiplier: 2` após o rewarded; a derrota chama `CommitRun(won: false)` — moedas descartadas, XP creditada sempre.
- **UI por evento, nunca por frame (§3.2):** contadores assinam `OnCurrencyChanged`/`OnCrowdChanged`; zero `Update()` de polling em telas.

---

## 5. ScriptableObjects (Entregável 18)

### 5.1 Definições dos 10 SOs canônicos

```csharp
public enum ElementType { None, Fire, Ice, Lightning, Poison, Light, Shadow, Metal, Alien }
public enum Rarity { Common, Rare, Epic, Legendary }
public enum GateType { AddFlat, Multiply, ClassConvert, Element, Mutation, Risk }
public enum BodyType { Organic, Machine, Undead }            // alvo do Veneno/Luz (CANON §4)
public enum UpgradeTrack { StartDamage, StartHealth, Speed, RewardMultiplier,
                           StartArmy, CritChance, BossDamage, ObstacleResist }

[CreateAssetMenu(menuName = "MutantArmy/Unit")]
public class UnitConfigSO : ScriptableObject
{
    public string unitId;                  // "soldier", "archer"... (chave de save/analytics)
    public string displayNameKey;          // localização
    public Rarity rarity;
    public int supplyCost;                 // Soldado 1 · Mago 4 · Gigante 12 (CANON §5)
    public float baseHp, baseDps, moveSpeed, attackRange;     // baseline Soldado: 10/2/5
    public ElementType element;            // dano nativo (Lança-Chamas = Fire)
    public BodyType bodyType;
    public string specialAbilityId;        // "heal_allies", "dodge_traps", "build_turret"...
    public Mesh mesh; public Material material;               // material com VAT (§6)
    public Texture2D vatTexture;           // animação assada (idle/run/attack)
    public Sprite cardIcon;
    public AnimationCurve levelHpCurve, levelDpsCurve;        // escala nv 1–10
}

[CreateAssetMenu(menuName = "MutantArmy/Boss")]
public class BossConfigSO : ScriptableObject
{
    public string bossId, displayNameKey;
    public ElementType element;
    public ElementType[] weaknesses;       // Zumbi Titã: Fire + Light
    public ElementType[] immunities;       // Zumbi Titã: Poison
    public bool rotatingWeakness;          // Alien Supremo: troca a cada 25% de HP
    public BodyType bodyType;
    public float maxHp, contactDps;
    public float entranceSeconds = 2f;     // CANON §6: ≤ 2 s
    public float telegraphSeconds = 1f;    // janela de leitura do especial
    public float specialAttackDamage; public float specialAttackArea; public float specialBaseCooldown;
    public ArenaWaveEvent[] arenaWaves;    // lista ORDENADA por tempo; consumida por ponteiro (§4.5)
    public GameObject prefab; public Sprite scoutCardArt;     // arte do Boss Scout
    public RewardConfigSO killReward;      // gemas + chance de carta/fragmento
}
[Serializable] public class ArenaWaveEvent { public float time; public UnitConfigSO enemyType; public int count; }

[CreateAssetMenu(menuName = "MutantArmy/Level")]
public class LevelConfigSO : ScriptableObject
{
    public int levelIndex;                 // 1–20 no MVP
    public int seed;                       // pista determinística: mesma fase = mesma pista (§4.11); QA reproduz bug por seed
    public WorldConfigSO world;
    public float trackLength = 220f;       // ≈45–75 s a 4 m/s base
    public GateSlot[] gateSlots;           // posição + par de portais (ou autoBalance)
    public ObstacleSlot[] obstacles;
    public BossConfigSO boss;              // TODA fase termina em boss (CANON §6)
    public float bossHpMultiplier = 1f;    // escala da variante regional
    public RewardConfigSO winReward;
    public int startingUnits = 1;          // fase sempre começa com 1 + bônus de meta
}
[Serializable] public class GateSlot { public float trackPosition; public float depth01;
    public bool autoBalance; public GateConfigSO leftGate, rightGate; }
[Serializable] public class ObstacleSlot { public float trackPosition; public GameObject prefab; }

[CreateAssetMenu(menuName = "MutantArmy/Gate")]
public class GateConfigSO : ScriptableObject
{
    public string gateId; public GateType gateType;
    public float value;                    // +10/+25 → 10/25 · x2 → 2 · ÷2 → 0.5
    public UnitConfigSO unitToAdd;         // AddFlat/ClassConvert
    public ElementType element;            // Element gate
    public MutationConfigSO mutation;      // Mutation gate
    [Range(0f, 1f)] public float riskSuccessChance;  // Risco: ex. 0.7 → "70% x10 / 30% perde metade"
    public float riskRewardMult, riskFailPenalty;    // ex. 10 e 0.5
    public string displayLabel;            // texto honesto exibido no portal (CANON §3.4); renderizado via OnValidate (§4.3)
    public Sprite icon; public Color portalColor;

    // Efeito como FUNÇÃO PURA int→int com semântica de TOTAL-ALVO (§4.3): testável sem
    // cena. O CrowdManager reconcilia atual→alvo; aplicar como delta é o bug "x2 que triplica".
    public int Apply(int current) => gateType switch
    {
        GateType.AddFlat  => current + (int)value,
        GateType.Multiply => Mathf.CeilToInt(current * value),   // ÷2 = value 0.5 (ímpar: ⌈n/2⌉, doc 04)
        _ => current                                             // Element/Mutation/ClassConvert não mudam contagem
    };
}

[CreateAssetMenu(menuName = "MutantArmy/Upgrade")]
public class UpgradeConfigSO : ScriptableObject
{
    public UpgradeTrack track; public string displayNameKey; public Sprite icon;
    public float bonusPerLevel = 0.05f;    // +5%/nível (StartArmy: +1 unidade a cada 2 níveis)
    public int maxLevel = 50;
    public float costBase = 100f, costGrowth = 1.35f;         // custo(n) = 100 × 1,35^n
    public bool inMvp;                     // 4 trilhas true no MVP (CANON §9)
}

[CreateAssetMenu(menuName = "MutantArmy/Reward")]
public class RewardConfigSO : ScriptableObject
{
    public int coins, gems, playerXp;
    public ChestType chest;                // None/Common/Rare/Epic
    [Range(0f, 1f)] public float cardDropChance;
    public UnitConfigSO[] cardPool;        // de qual pool a carta/fragmento sai
    public int shardAmount;
    public bool allowAdDouble = true;      // "dobrar com anúncio" na tela de vitória
}
public enum ChestType { None, Common, Rare, Epic }

[CreateAssetMenu(menuName = "MutantArmy/World")]
public class WorldConfigSO : ScriptableObject
{
    public int worldIndex; public string displayNameKey;      // "Campo Inicial", "Cidade Zumbi"...
    public LevelConfigSO[] levels;
    public BossConfigSO worldBoss;         // fase 10 (fase 7 no MVP p/ M1)
    public Material skyboxMaterial; public GameObject[] trackSegmentPrefabs;  // tema visual
    public AudioClip musicTrack;
    public RewardConfigSO worldClearReward;                   // boss de mundo: 10 gemas + baú
}

[CreateAssetMenu(menuName = "MutantArmy/Rarity")]
public class RarityConfigSO : ScriptableObject
{   // cores canônicas: Comum cinza/azul claro · Raro azul · Épico roxo · Lendário dourado
    public Rarity rarity; public Color frameColor; public Color glowColor;
    public float statPremium = 1.15f;      // +10–20% por raridade sobre o baseline/Supply (CANON §5)
    public int chestWeight;                // peso de sorteio em baús
}

[CreateAssetMenu(menuName = "MutantArmy/ElementChart")]
public class ElementChartSO : ScriptableObject
{
    [Serializable] public struct Entry { public ElementType attacker, defender; public float multiplier; }
    [SerializeField] private Entry[] _entries;   // Fire>Ice 1.5 · Ice>Lightning 1.5 · Lightning>Fire 1.5
                                                 // mesmo elemento 0.5 · Poison vs Machine/Undead 0.0 ...
    private float[,] _matrix;                    // cache N×N construído em OnEnable

    public float GetMultiplier(ElementType atk, ElementType def)
    { if (_matrix == null) Build(); return _matrix[(int)atk, (int)def]; }

    private void Build()
    {
        int n = Enum.GetValues(typeof(ElementType)).Length;
        _matrix = new float[n, n];
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) _matrix[i, j] = 1f;  // neutro = 1.0
        foreach (var e in _entries) _matrix[(int)e.attacker, (int)e.defender] = e.multiplier;
    }
    private void OnEnable() => Build();
}

[CreateAssetMenu(menuName = "MutantArmy/Mutation")]
public class MutationConfigSO : ScriptableObject
{
    public string mutationId, displayNameKey;    // "wings", "laser", "armor", "size"...
    public Rarity rarity;
    public float dpsMult = 1f, hpMult = 1f, speedMult = 1f, sizeMult = 1f;
    public bool grantsFlight;                    // asas: ignora obstáculos de chão
    public ElementType addsElement;              // laser pode adicionar dano de Raio
    public int shaderVariantFlag;                // bit usado pelo shader VAT p/ trocar visual (§6.2)
    public GameObject attachmentPrefab;          // acessório instanciado só em unidades "hero" próximas à câmera
    public Sprite hudIcon;                       // slot de mutação no HUD (máx. 3)
}
```

**Regra dura: ScriptableObjects são READ-ONLY em runtime.** SO é config; estado vivo (HP do boss, contagem, cooldowns, fraqueza rotativa) mora no manager ou em structs de runtime (`BossRuntime`, arrays do crowd — por isso `BossRuntime` existe separado do `BossConfigSO`). Mutar um SO em play mode persiste a sujeira no asset do editor, dessincroniza o tuning via Remote Config e produz bug irreproduzível em build (anti-exemplo do estudo: settings mutados por sliders de debug — ver `15-referencias-e-recursos.md` §4). O painel de tuning em device (§8) escreve em overrides de runtime, nunca no SO.

### 5.2 Workflow do designer (zero código)

1. **Nova tropa:** duplicar um `UnitConfigSO`, preencher stats coerentes com o Supply (doc 03), apontar mesh/material/VAT exportados pela arte → a tropa já aparece no catálogo, em baús e em portais de classe.
2. **Nova fase:** criar `LevelConfigSO` (a janela "MAR Tools" gera com slots padrão e seed única — §8), arrastar boss e marcar `autoBalance` nos `GateSlot` (o `GateManager` monta pares honestos contra o boss, reproduzíveis pela seed) ou montar pares à mão para momentos autorais (fase 1 "impossível perder", CANON §16). O preview dos rótulos dos portais aparece na cena via `OnValidate`, sem play mode (§4.3).
3. **Balanceamento elemental:** editar entradas do `ElementChartSO` — nenhum dano no jogo tem multiplicador fora desse asset.
4. **Validação automática:** `MutantArmy.Editor` roda validador no build: SO com campo nulo, fase sem boss, par de portais sem rota ótima, ou tropa com DPS+HP/Supply fora da banda do CANON §5 → erro de build com link para o asset.
5. **Tuning em produção:** Remote Config sobrescreve os números dos SOs por chave (`boss_hp_mult_<id>` etc.) sem novo build.

---

## 6. Performance da multidão (200–1000 unidades, device mediano)

### 6.1 Comparativo de técnicas

Device de referência: Android mediano 2024–2026 (classe Snapdragon 680 / Helio G88, 4–6 GB RAM, GPU Adreno 610/Mali-G52). Meta: **60 fps (frame ≤16,6 ms)**, sem thermal throttling em sessão de 15 min.

| Técnica | 200 un. | 1000 un. | Custo de implementação | Veredito |
|---|---|---|---|---|
| GameObject + `Animator` por unidade | ~35 fps (CPU bound: Animator + transforms) | inviável (<10 fps) | baixo | reprovado |
| **GameObject pooled + GPU instancing + VAT** | 60 fps folgado | 60 fps (sim ≈2–3 ms, render 1–3 draw calls/tipo) | médio | **escolhido** |
| `Graphics.DrawMeshInstanced` puro (sem GO) | 60 fps | 60 fps, melhor ainda em CPU | médio-alto (perde raycast/trigger/hierarquia; tudo vira matemática manual) | overkill p/ ≤1000 |
| DOTS/ECS + Entities Graphics | 60 fps | 60 fps com sobra p/ 10k+ | alto (curva da equipe, ecossistema 2022 LTS ainda áspero, SDKs de ads/IAP são MonoBehaviour-land) | reprovado p/ MVP |

**Decisão: pooling + GPU instancing com animação por shader (Vertex Animation Texture).** Racional: entrega a meta com margem, mantém o time em território MonoBehaviour conhecido (velocidade de iteração é o recurso mais escasso num MVP de 30 dias) e deixa a porta aberta — a simulação já nasce orientada a dados (arrays SoA do §4.2), então migrar o update de posição para `NativeArray` + `IJobParallelFor` + Burst (sem ECS completo) é trocar o contêiner, não o layout: schedule cedo no `Update`, `Complete` no `LateUpdate`, `Dispose` em `OnDestroy` (ciclo do template de Jobs estudado — ver `15-referencias-e-recursos.md` §3.1). É o passo 2 se o profiling pedir; no MVP (Supply 60) o single-thread basta.

### 6.2 Implementação da técnica escolhida

- **1 GameObject por unidade? Não.** Unidades são índices nos arrays SoA do CrowdManager (§4.2: `positions`, `velocities`, `typeIds`, `hp`, `flags`). O render é feito com `Graphics.RenderMeshInstanced` por tipo de tropa, alimentado por `Matrix4x4[]` reusados (sem alloc por frame). GameObjects existem apenas para: portais, obstáculos, boss, e ~10 "hero units" próximas à câmera (com acessórios de mutação 3D e skin equipada — o resto da multidão mostra mutação via shader). Proibições da §4.2 valem aqui: zero Animator, zero Rigidbody, zero NavMeshAgent, zero collider por unidade.
- **Animação:** VAT assada do rig (idle/run/attack, 8–12 fps de sample) em textura RGBA Half; o vertex shader lê a pose por `_TimeOffset` por instância → **zero Animators, zero skinning na CPU**. Variação de fase aleatória por unidade evita "exército robótico".
- **Mutações visíveis (CANON §3.3):** `shaderVariantFlag` da mutação vai num float packed por instância (via instanced property) — o shader troca cor de emissão, escala, adiciona "asas" por vertex offset de submesh. 3 slots = 3 bits.
- **Colisão/dano:** sem PhysX por unidade. Obstáculos testam overlap contra os arrays de posição (broadphase por faixa de pista, ~0,3 ms p/ 1000 unidades); portais usam **1 trigger contra o AABB/centroid do grupo** — 1 evento por exército, contrato do §4.3.
- **Vizinhança:** grid espacial uniforme em XZ reconstruída 1×/frame (zero alloc) alimenta a separação local da formação (§4.2) e a aquisição de alvo do CombatSystem (§4.4) — nunca `Physics.OverlapSphere` por unidade (GC + custo O(n) por agente, anti-exemplo medido no estudo).
- **LOD:** além de 25 m, unidades usam mesh de ~150 tris e VAT de 4 fps; multidão >600 visualmente agrupa fileiras traseiras (o contador no HUD segue exato — espetáculo preservado, custo não).

### 6.3 Orçamentos de frame (device mediano, cena de pior caso: 1000 unidades + boss + VFX)

| Recurso | Orçamento | Notas |
|---|---|---|
| Frame total | ≤ 16,6 ms (60 fps) | Low tier degrada p/ 30 fps cap + render scale 0,85 |
| CPU simulação do crowd | ≤ 3,0 ms | update batch das posições + formação |
| Draw calls (SetPass) | ≤ 120 total; **crowd ≤ 12** | 1–2 por tipo de tropa × LOD; SRP Batcher no resto |
| Triângulos em tela | ≤ 200k | unidade LOD0 ≈ 600 tris, LOD1 ≈ 150 |
| Partículas vivas | ≤ 500; ≤ 8 sistemas ativos | `VFXManager` enfileira pedidos acima do teto (drop silencioso das de menor prioridade) |
| Overdraw de VFX | ≤ 2,5x em ¼ da tela | fanfarras usam mesh particles, não billboards gigantes |
| Alocação GC em gameplay | 0 B/frame em regime | pools p/ tudo; strings de UI via `StringBuilder` cacheado |
| Memória total | ≤ 900 MB PSS | textura VAT ≈ 256×256 Half por tropa (~0,5 MB) |

### 6.4 Pooling — requisito transversal, não detalhe da multidão

Padrão único no projeto: **`UnityEngine.Pool.ObjectPool<T>` nativo** (Unity 2021+, zero dependência — decisão de stack do `15-referencias-e-recursos.md` §4). Vale para TUDO que nasce e morre em gameplay: unidades, projéteis, partículas, moedas voadoras da fanfarra e segmentos de pista. `Instantiate`/`Destroy` durante a corrida é defeito de code review, não estilo.

| Regra | Detalhe |
|---|---|
| **Pool POR TIPO de unidade** | 1 `ObjectPool<UnitView>` por `UnitConfigSO` (Soldado, Arqueiro, Mago...), indexado por id — nunca pool genérico com busca linear por tag/prefab (anti-exemplo corrigido do estudo, doc 15 §3.1) |
| **`Reset()` de estado no Get** | HP, elemento, flags de mutação visual, escala, trail e VFX residual zerados ao sair do pool — estado vazado de unidade reciclada (o Soldado que volta "envenenado") é o bug clássico do gênero |
| **Pré-alocação do pico na carga da fase** | aquecer o pool durante o loading com o pico esperado (maior contagem alcançável da fase — calculável aplicando `GateConfigSO.Apply` em cadeia sobre os `gateSlots` do `LevelConfigSO`) — zero `Instantiate` durante a corrida |
| **Remoção em LOTE, de trás pra frente** | iterar do fim para o início (swap-back nos arrays SoA, §4.2), com **1 VFX agregado para o lote inteiro** — nunca 1 partícula por unidade removida (anti-exemplo: remoção O(n) com 2 partículas por unidade, doc 15 §3.1) |
| **Mesmo padrão para tudo** | projéteis e partículas via `VFXManager` (orçamento §6.3), moedas voadoras do overflow de Supply, segmentos de pista (§4.11) — sempre `Release`, nunca `Destroy` |
| **Flags one-shot resetam no Get** | par de portais reciclado volta com `_consumed = false` no `Setup` (§4.3) — pool sem Reset reintroduz o bug do "portal morto" em chunk reaproveitado |

---

## 7. Integrações de terceiros

### 7.1 Ordem de inicialização consolidada (com consent)

```text
SaveSystem.Load (local)                                    [0 ms rede]
  └─ Firebase CheckDependencies + Auth anônimo             [timeout 3 s]
       └─ RemoteConfig FetchAndActivate                    [timeout 3 s, cache fallback]
            └─ Analytics (gated pelo consentStatus salvo)
                 └─ MAX InitializeSdk ─ dispara Google UMP se região GDPR e consent desconhecido
                      └─ RevenueCat configure(apiKey, appUserID: firebaseUid)
                           └─ Load Main (UI) — pode ocorrer ANTES de MAX/RC terminarem
```

### 7.2 Firebase

- **Pacotes:** Auth (anônimo no 1º boot; uid amarra Firestore + RevenueCat), Firestore (sync de save, doc único `players/{uid}` ≤ 8 KB), Analytics, Remote Config, Crashlytics (init automático, primeiro de todos via `ReportUncaughtExceptionsAsFatal`), FCM (pós-MVP: tokens registrados só após consentimento).
- **Sem rede:** Auth devolve uid cacheado; Firestore enfileira writes (persistência local ligada); Analytics persiste eventos no device; Remote Config ativa o último fetch. Nenhuma feature de gameplay depende de resposta do Firebase.

### 7.3 AppLovin MAX (mediação: AdMob, Meta, Unity Ads)

- SDK key no `Plugins/Android` via Integration Manager; adapters AdMob/Meta/Unity Ads fixados em versões testadas (atualização só em release dedicado).
- **Consent/UMP:** MAX aciona o fluxo do Google UMP automaticamente (Terms & Privacy Policy Flow habilitado) na primeira sessão em regiões GDPR; o resultado é cacheado em `SaveData.consentStatus` e replicado para `FirebaseAnalytics.SetConsent`. Em "denied", ads viram non-personalized e Analytics fica off — o jogo segue 100% funcional.
- Rewarded: pré-load contínuo com retry exponencial (4.8); botão de rewarded só renderiza com `IsRewardedReady` (nunca prometer e falhar — protege a conversão ≥35% DAU do CANON §12).
- Interstitial: toda a regra de pacing mora em `MaybeShowInterstitial` (4.8) lendo Remote Config — frequência 100% operável sem build novo.

### 7.4 RevenueCat (IAP)

- Entitlements: `no_ads` (US$ 4,99, inclui 200 gemas — concessão única via flag no save), `season_pass` (US$ 6,99/mês, renovação verificada no boot), oferta inicial US$ 2,99 controlada por `starterOfferState` + janela de 48 h desde `firstLaunchUnixUtc`.
- `appUserID` = uid do Firebase → compra sobrevive a reinstalação e sync de save; `Purchases.RestorePurchases` exposto na loja (obrigatório iOS, boa prática Android).
- **Sem rede:** loja exibe cache de ofertas se houver; compra exige conexão (fluxo nativo já trata); entitlements usam último estado conhecido (jogador pagante nunca vê ads por falha de rede).

---

## 8. Ferramentas de editor — entregáveis da semana 1

Tooling não é luxo de fim de projeto: num MVP de 30 dias, cada clique manual repetido 200 vezes é um dia perdido. Os padrões abaixo foram vistos funcionando nos repos estudados (ver `15-referencias-e-recursos.md` §3.5, item 11) e entram no backlog da **semana 1**, junto com o core loop. Tudo vive em `MutantArmy.Editor` (§2.3) — jamais em assembly de runtime.

| Ferramenta | O que faz | Quando paga o investimento |
|---|---|---|
| **Janela "MAR Tools"** (`EditorWindow`) | setup de fase por código **com `Undo.RecordObject`** (cria `LevelConfigSO` com slots padrão + seed única); limpar save + PlayerPrefs em 1 clique; abrir `persistentDataPath` no Explorer; **simular Remote Config** localmente (override dos defaults do §4.10 sem rede) | todo dia, do dia 1 ao último |
| **Custom inspector do `LevelConfigSO`** | sliders de `trackPosition`/`depth01` dos `GateSlot` com preview dos pares na Scene view (gizmos + rótulo renderizado via `OnValidate`, §4.3) | design das 20 fases sem play mode |
| **Pintor de prefabs** | pintar obstáculos/decoração nos segmentos com **preview fantasma + grid snap**, respeitando as âncoras do `TrackSegment` (§4.11) e a zona de segurança pós-portal | greybox dos 3 mundos |
| **Painel de tuning em runtime (device)** | overlay de debug no development build: sliders de Supply cap, taxas de economia, velocidade e cooldowns de boss — tuning na mão, no celular, sem rebuild; escreve em overrides de runtime, **nunca no SO** (§5.1) | calibragem das metas do CANON §12 |
| **`[MenuItem]` de cheats** | `MAR/Cheats/Dar 10k moedas` · `Vencer fase atual` · `Pular para fase N` · `Resetar consentimento` · `Forçar interstitial` | QA e gravação de demo/criativos |

Regras: ferramentas usam os MESMOS funis de mutação do jogo (cheat de moedas chama `EconomySystem.Earn`, nunca escreve no save direto — senão o cheat vira fonte de bug que o jogo real não tem); toda operação destrutiva pede confirmação; toda criação/edição de asset registra Undo.

---

## 9. Checklist de aceitação técnica (Definition of Done do MVP)

- [ ] Boot → Main em ≤ 2,5 s em device mediano; jogar em ≤ 5 s do tap no ícone.
- [ ] 60 fps com 1000 unidades + boss + VFX no device de referência (§6.1); 0 B GC/frame em regime.
- [ ] Avião ligado: 20 fases jogáveis, save íntegro, sem botão morto, sem exception.
- [ ] Matar o app durante a tela de vitória: nenhuma moeda/fragmento perdido (save pós-fase atômico).
- [ ] Todos os eventos de analytics do BRIEF disparando com parâmetros corretos (validados no DebugView).
- [ ] Interstitial jamais antes da fase 6, jamais 2 fases seguidas, jamais após 2 derrotas (testes automatizados sobre `MaybeShowInterstitial`).
- [ ] Todo número de balanceamento alterável por Remote Config sem rebuild.
- [ ] Designer cria uma fase nova completa (portais + boss + recompensa) sem tocar em código, e o validador de SO passa no build.
- [ ] Suíte de unit tests verde no CI: os 8 portais como funções puras (incl. o caso negativo "x2 que triplica", §4.3) + round-trip do SaveData (chave duplicada, cast de tipos, migração de schema, §4.7) + timers de telegraph/cooldown (§4.5).
- [ ] Zero `using UnityEditor` fora de `MutantArmy.Editor` (guard de CI, §2.3); zero `Instantiate`/`Destroy` em gameplay — tudo via pool (§6.4).
- [ ] Mesma fase + mesma seed = mesma pista, byte a byte (§4.11) — pré-requisito de repro de QA.

---

## 10. Lições do estudo de código de referência

Este documento incorpora as conclusões do estudo de código real de 14 repositórios open-source do gênero (runners de multidão, clones de portais, boids/crowd sim), consolidado em **`15-referencias-e-recursos.md`** — de lá vêm a arquitetura SoA do CrowdManager (§4.2), o contrato one-shot do GatePair com efeito puro de total-alvo (§4.3), a máquina de estados em pilha (§4.1), os timers e waves testáveis do boss (§4.5), o save com migração incremental (§4.7), a pista por âncoras com seed (§4.11), o rig de câmera independente (§4.12), as duas pilhas de UI com RunWallet (§4.13/§4.6), o pooling transversal (§6.4), o tooling de editor da semana 1 (§8) e as regras duras de compliance (§2.3) — além do catálogo de antipadrões que funciona, na prática, como a especificação invertida do nosso código. O doc 15 também registra a política de licenças que governa cada padrão citado aqui (o que pode ser adaptado com notice MIT, como Signals e SaveGameFree, e o que é apenas-estudo e deve ser reimplementado do zero) e o aviso anti-cópia que vale para toda linha deste documento.
