# Fundação Técnica do MVP — Mutant Army Run · Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Criar o projeto Unity completo do MVP (scaffold abrível no Unity 2022.3 LTS) com o núcleo de lógica de jogo implementado em C# puro, 100% coberto por testes xUnit verificados via `dotnet test` HOJE (Unity não está instalado nesta máquina).

**Architecture:** Assembly `MutantArmy.Domain` (C# puro, `noEngineReferences`) contém toda a lógica determinística (portais, Supply, elementos, estados, economia, save). Os mesmos fontes são compilados pelo Unity via asmdef e por 3 projetos de teste .NET 8 via `<Compile Include>` com lista explícita de arquivos (paralelismo seguro). A camada Unity (Core/Gameplay/Meta/Services/UI/Editor) implementa os contratos do **GDD doc 12** (`C:\Users\Felipe\Downloads\jogo test\GDD\12-arquitetura-unity.md`) — que é parte integrante deste plano: todo executor DEVE ler as seções citadas em cada task. Serviços de SDK (MAX/Firebase/RevenueCat) ficam atrás de interfaces com implementação Null — o projeto compila sem nenhum SDK importado.

**Tech Stack:** Unity 2022.3 LTS + URP 14 · C# 9 (`LangVersion 9.0` nos csproj de teste — paridade com Unity 2022) · xUnit + .NET 8 SDK · git.

**Verificação:** Domain = `dotnet test` verde (verificado nesta máquina). Camada Unity = scaffold não-compilável até o Unity ser instalado (login na conta Unity é interativo) — mitigado por revisão cruzada de contratos e pelos EditMode tests já escritos para rodar na primeira abertura.

**Caminhos canônicos:**
- Raiz do repo: `C:\Users\Felipe\Downloads\jogo test\`
- Projeto Unity: `MutantArmyRun\`
- Domain (fontes compartilhados): `MutantArmyRun\Assets\_Project\Scripts\Domain\`
- Testes .NET: `tests\Domain.Gameplay.Tests\`, `tests\Domain.Flow.Tests\`, `tests\Domain.Persistence.Tests\`

---

## Estrutura de arquivos (visão completa)

```text
jogo test/
├── .gitignore                                  (Task 1)
├── GDD/                                        (existente — spec)
├── _research/                                  (existente — NUNCA commitado)
├── docs/superpowers/plans/                     (este plano)
├── tests/
│   ├── Domain.Gameplay.Tests/Domain.Gameplay.Tests.csproj   (Tasks 2–6)
│   │   ├── GateMathTests.cs  SupplyLedgerTests.cs  ElementChartTests.cs
│   │   ├── CombatMathTests.cs  FormationMathTests.cs  RiskGateTests.cs
│   ├── Domain.Flow.Tests/Domain.Flow.Tests.csproj           (Tasks 7–9)
│   │   ├── GameStateStackTests.cs  CountdownTests.cs  RunWalletTests.cs
│   │   └── InterstitialPolicyTests.cs
│   └── Domain.Persistence.Tests/Domain.Persistence.Tests.csproj  (Tasks 10–11)
│       ├── SaveModelTests.cs  SaveMigrationTests.cs  ChecksumTests.cs
│       └── EconomyMathTests.cs
└── MutantArmyRun/
    ├── README.md                               (Task 17)
    ├── THIRD-PARTY-NOTICES.md                  (Task 17)
    ├── Packages/manifest.json                  (Task 12)
    ├── ProjectSettings/ProjectVersion.txt      (Task 12)
    └── Assets/_Project/
        ├── Scripts/
        │   ├── Domain/MutantArmy.Domain.asmdef (noEngineReferences:true)
        │   │   ├── Enums.cs            (Task 2)  ElementType, Rarity, GateType, BodyType, UpgradeTrack, GameState, CurrencyType, ChestType
        │   │   ├── GateMath.cs         (Task 3)  SupplyLedger.cs (Task 4)  ElementChart.cs (Task 5)
        │   │   ├── CombatMath.cs FormationMath.cs RiskGate.cs (Task 6)
        │   │   ├── GameStateStack.cs   (Task 7)  Countdown.cs RunWallet.cs (Task 8)
        │   │   ├── InterstitialPolicy.cs (Task 9)
        │   │   ├── SaveData.cs SaveMigration.cs SaveChecksum.cs (Task 10)
        │   │   └── EconomyMath.cs      (Task 11)
        │   ├── Core/MutantArmy.Core.asmdef      → refs: Domain        (Task 13)
        │   │   ├── GameEvents.cs EventStructs.cs GameManager.cs
        │   │   ├── GameBootstrap.cs GameSceneBootstrap.cs RcKeys.cs AdPlacement.cs
        │   │   └── SO/ (os 10 ScriptableObjects do doc 12 §5.1)
        │   ├── Gameplay/MutantArmy.Gameplay.asmdef → refs: Domain, Core (Task 14)
        │   │   ├── CrowdManager.cs SpatialGridXZ.cs CrowdRenderer.cs CrowdAnchor.cs
        │   │   ├── GateManager.cs GatePairView.cs GateView.cs RiskResolver.cs
        │   │   ├── LevelManager.cs TrackSegment.cs BossManager.cs BossRuntime.cs
        │   │   ├── CombatSystem.cs VFXManager.cs CameraRig.cs
        │   ├── Meta/MutantArmy.Meta.asmdef      → refs: Domain, Core   (Task 15)
        │   │   ├── EconomySystem.cs UpgradeSystem.cs RewardSystem.cs
        │   │   ├── SaveSystem.cs UnitManager.cs
        │   ├── Services/MutantArmy.Services.asmdef → refs: Domain, Core (Task 15)
        │   │   ├── IAdsProvider.cs NullAdsProvider.cs AdsManager.cs
        │   │   ├── IAnalyticsProvider.cs NullAnalyticsProvider.cs AnalyticsManager.cs
        │   │   ├── IRemoteConfigProvider.cs NullRemoteConfigProvider.cs RemoteConfigManager.cs
        │   │   ├── IAPManager.cs AudioManager.cs
        │   ├── UI/MutantArmy.UI.asmdef          → refs: Domain, Core, Meta (Task 16)
        │   │   ├── UIManager.cs UIScreen.cs UIOverlay.cs UIUtils.cs HudController.cs
        │   │   └── BossScoutOverlay.cs ResultScreen.cs FeedbackTextController.cs
        │   ├── Editor/MutantArmy.Editor.asmdef  (Editor-only, refs: todos) (Task 16)
        │   │   ├── MarToolsWindow.cs ProjectSetup.cs MvpContentFactory.cs
        │   │   ├── CheatsMenu.cs SoValidator.cs EditorGuards.cs
        │   └── Tests/EditMode/MutantArmy.Tests.EditMode.asmdef (Task 16)
        │       └── JsonRoundTripTests.cs GateConfigTests.cs
        ├── Scenes/.gitkeep   Prefabs/{Units,Gates,Bosses,Track,UI}/.gitkeep
        ├── ScriptableObjects/{Units,Bosses,Levels,Worlds,Gates,Mutations,Upgrades,Rewards,Balance}/.gitkeep
        ├── Art/.gitkeep  Audio/.gitkeep  VFX/.gitkeep  Settings/.gitkeep  Resources/.gitkeep
```

**Regras transversais (valem para TODAS as tasks):**
1. Antes de codar, ler `GDD\CANON.md` e as seções do `GDD\12-arquitetura-unity.md` citadas na task. Os nomes de tipos/métodos do doc 12 são CONTRATO — não renomear.
2. Domain: ZERO `using UnityEngine`. `System.Math`/`MathF` no lugar de `Mathf`. Tipos próprios (`Float2`) no lugar de `Vector2`.
3. Camada Unity: nenhuma referência a `MaxSdk`/`Firebase`/`Purchases` — sempre via interface + Null provider (SDKs entram pós-instalação do Unity).
4. TDD nos módulos Domain: teste falhando → rodar (`dotnet test tests\<proj> -v minimal`) → implementar → verde → próximo. Commits por task.
5. `_research/` é apenas-estudo: PROIBIDO copiar qualquer linha de lá (repos sem licença). Padrões MIT citados no doc 15 são reimplementados com notice em `THIRD-PARTY-NOTICES.md`.

---

### Task 1: Repositório git

**Files:** Create: `.gitignore` (raiz `jogo test\`)

- [ ] **Step 1:** Criar `.gitignore` na raiz:

```gitignore
# pesquisa local (1,15 GB de clones de estudo — nunca commitar)
_research/

# Unity
MutantArmyRun/[Ll]ibrary/
MutantArmyRun/[Tt]emp/
MutantArmyRun/[Oo]bj/
MutantArmyRun/[Bb]uild*/
MutantArmyRun/[Ll]ogs/
MutantArmyRun/[Uu]serSettings/
MutantArmyRun/*.csproj
MutantArmyRun/*.sln
*.apk
*.aab

# .NET
tests/**/bin/
tests/**/obj/

# OS/IDE
.vs/
.idea/
Thumbs.db
```

- [ ] **Step 2:** `git init`; configurar `core.autocrlf=true`; commit inicial com `GDD/`, `docs/` e `.gitignore`. Mensagem: `chore: repo inicial com pacote de GDD e plano de implementação`.
- [ ] **Step 3:** Verificar com `git status` que `_research/` NÃO aparece.

### Task 2: Scaffold de testes + Enums do Domain

**Files:** Create: `MutantArmyRun\Assets\_Project\Scripts\Domain\Enums.cs`, `Domain\MutantArmy.Domain.asmdef`, os 3 csproj de teste.

- [ ] **Step 1:** Criar `Enums.cs` (contrato do doc 12 §5.1 + §4.1 — copiar exato):

```csharp
namespace MutantArmy.Domain
{
    public enum ElementType { None, Fire, Ice, Lightning, Poison, Light, Shadow, Metal, Alien }
    public enum Rarity { Common, Rare, Epic, Legendary }
    public enum GateType { AddFlat, Multiply, ClassConvert, Element, Mutation, Risk }
    public enum BodyType { Organic, Machine, Undead }
    public enum UpgradeTrack { StartDamage, StartHealth, Speed, RewardMultiplier,
                               StartArmy, CritChance, BossDamage, ObstacleResist }
    public enum GameState { Boot, MainMenu, BossScout, Running, BossFight, ReviveOffer, Victory, Defeat }
    public enum CurrencyType { Coin, Gem, Xp }
    public enum ChestType { None, Common, Rare, Epic }
}
```

- [ ] **Step 2:** Criar `MutantArmy.Domain.asmdef`: `{"name":"MutantArmy.Domain","rootNamespace":"MutantArmy.Domain","references":[],"noEngineReferences":true,"autoReferenced":true}`.
- [ ] **Step 3:** Criar os 3 csproj (modelo abaixo; cada um inclui `Enums.cs` + SOMENTE os fontes dos próprios módulos — lista explícita, sem glob, para isolamento entre agentes):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="..\..\MutantArmyRun\Assets\_Project\Scripts\Domain\Enums.cs" Link="Domain\Enums.cs" />
    <!-- + fontes do grupo, ex.: GateMath.cs, SupplyLedger.cs ... -->
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="2.8.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4:** Em cada csproj, 1 teste sanity (`Assert.True(Enum.IsDefined(typeof(GateType), 0))`). Rodar `dotnet test` nos 3 → verde. Commit: `chore: scaffold de testes .NET + enums canônicos do Domain`.

### Task 3: GateMath (função pura de total-alvo)

**Files:** Create: `Domain\GateMath.cs`, Test: `tests\Domain.Gameplay.Tests\GateMathTests.cs` · **Ler:** doc 12 §4.3 (contrato), §5.1 (`GateConfigSO.Apply`), doc 04 (piso de 1 unidade).

- [ ] **Step 1:** Testes (casos obrigatórios — tabela do doc 12 §4.3):

| Caso | Entrada | Esperado |
|---|---|---|
| +10 | `Apply(AddFlat, 10, 7)` | 17 |
| +25 | `Apply(AddFlat, 25, 1)` | 26 |
| x2 | `Apply(Multiply, 2, 21)` | 42 |
| x3 | `Apply(Multiply, 3, 5)` | 15 |
| ÷2 ímpar | `Apply(Multiply, 0.5, 7)` | 4 (⌈7/2⌉) |
| ÷2 par | `Apply(Multiply, 0.5, 8)` | 4 |
| piso | `Apply(Multiply, 0.5, 1)` | 1 (nunca zera) |
| piso negativo | `Apply(AddFlat, -10, 4)` | 1 (nunca < 1) |
| identidade | `Apply(Element, 0, 9)` e Mutation/ClassConvert | 9 (não muda contagem) |
| **caso negativo "x2 que triplica"** | total-alvo de x2 sobre 21 | `Assert.NotEqual(63, …)` e `Assert.Equal(42, …)` — multiplicador NUNCA é delta |

```csharp
public static class GateMath
{
    /// Semântica de TOTAL-ALVO (doc 12 §4.3): retorna o novo total; nunca < 1.
    public static int Apply(GateType type, float value, int current) => type switch
    {
        GateType.AddFlat  => System.Math.Max(1, current + (int)value),
        GateType.Multiply => System.Math.Max(1, (int)System.Math.Ceiling(current * (double)value)),
        _ => current
    };
}
```

- [ ] **Step 2–4:** red → implementar → verde (`dotnet test tests\Domain.Gameplay.Tests`). Commit: `feat(domain): GateMath com semântica de total-alvo e piso de 1`.

### Task 4: SupplyLedger

**Files:** Create: `Domain\SupplyLedger.cs`, Test: `SupplyLedgerTests.cs` · **Ler:** doc 12 §4.2 (`EnforceSupplyCap`), CANON §3.2/§8.

API: `SupplyLedger(int cap)` · `int Used` · `int Cap` · `bool CanAdd(int cost)` · `void Add(int cost)` / `Remove(int cost)` · `OverflowPlan EnforceCap(IReadOnlyList<(int index, int cost)> unitsSorted)` retornando `struct OverflowPlan { int[] RemoveIndices; int CoinsGranted; }`.

- [ ] Testes: sem estouro → plano vazio; estouro remove **as mais baratas primeiro**; nunca remove a última unidade (mínimo 1 no exército); moedas = unidadesRemovidas × `coinPerSupplyRate` (param, default 2 — chave RC `supply_overflow_coin_rate`); cap 60 (CANON §15: fixo no MVP). Red → green → commit `feat(domain): SupplyLedger com conversão de excedente (mais baratas primeiro, piso 1)`.

### Task 5: ElementChart

**Files:** Create: `Domain\ElementChart.cs`, Test: `ElementChartTests.cs` · **Ler:** CANON §4, doc 12 §5.1 (`ElementChartSO`).

API: `ElementChart.Default()` (entradas canônicas embutidas) · `float GetMultiplier(ElementType atk, ElementType def)` · `float GetBodyMultiplier(ElementType atk, BodyType def)` · construtor por entradas (mesmo formato `Entry` do SO — o SO delega para esta classe).

- [ ] Testes canônicos: Fire>Ice = 1.5 · Ice>Lightning = 1.5 · Lightning>Fire = 1.5 · inverso do ciclo = 1.0 (neutro) · mesmo elemento = 0.5 (Fire vs Fire) · Poison vs Machine = 0 · Poison vs Undead = 0 · Poison vs Organic = 1.5 · None vs qualquer = 1.0 · matriz N×N completa (nenhuma célula sem valor). Commit `feat(domain): ElementChart com ciclo Fogo>Gelo>Raio e regras de Veneno`.

### Task 6: CombatMath + FormationMath + RiskGate

**Files:** Create: `Domain\CombatMath.cs`, `Domain\FormationMath.cs`, `Domain\RiskGate.cs` + 3 arquivos de teste · **Ler:** doc 12 §4.2 (`GetSlotOffset`), §4.4, §4.5 (waves por ponteiro, `PhaseThresholds`), §4.3 (Risk com RNG injetado).

- [ ] `FormationMath.GetSlotOffset(int slotIndex)` → `Float2` (struct própria, doc 12 §4.2: espiral de Vogel, `spacing 0.45f`, `golden 2.39996f`). Testes: determinístico; raio cresce ~√n (slot 99 ≈ 4.5f de raio ±5%); distância mínima entre 200 slots consecutivos > 0.3f.
- [ ] `CombatMath`: `CrowdDps(float baseDps, float chartMult, float bossDamageBonus, bool crit)` (crítico ×2, doc 12 §4.4) · `BossPhase(float hp, float maxHp)` → 0/1/2 com limiares 0.5/0.25 · `WavePointer.Consume(float t, ArenaWave[] sorted, ref int next)` → eventos disparados exatamente 1× cada (teste: dt gigante dispara todos em ordem; dt pequeno nunca duplica).
- [ ] `RiskGate.Resolve(System.Random rng, float successChance, float rewardMult, float failPenalty, int current)`: com seed fixa, sucesso → `GateMath`-style total-alvo ×rewardMult; falha → ×failPenalty com piso 1; 10.000 amostras com chance 0.7 → proporção 0.7 ±0.02. Commit `feat(domain): combate agregado, formação filotáxica e risco com RNG injetado`.

### Task 7: GameStateStack

**Files:** Create: `Domain\GameStateStack.cs`, Test: `tests\Domain.Flow.Tests\GameStateStackTests.cs` · **Ler:** doc 12 §4.1 (tabela `Allowed` — copiar EXATA).

API: `GameState Current` · `bool ChangeState(GameState next)` (false + sem efeito se ilegal) · `void Push(GameState overlay)` / `GameState Pop()` · `int Depth`.

- [ ] Testes: caminho feliz Boot→MainMenu→BossScout→Running→BossFight→Victory→BossScout (próxima fase sem voltar ao menu, doc 12 §4.1); ilegal `Running→Victory` retorna false e mantém estado; **cenário revive**: em BossFight, `Push(ReviveOffer)` → `Current==ReviveOffer`, `Pop()` → volta EXATO a BossFight (pilha preserva); pop em pilha de 1 lança `InvalidOperationException`. Commit `feat(domain): máquina de estados em pilha com tabela de transições`.

### Task 8: Countdown + RunWallet

**Files:** Create: `Domain\Countdown.cs`, `Domain\RunWallet.cs` + testes · **Ler:** doc 12 §4.5 (Countdown — adaptar `Mathf.Max`→`Math.Max`), §4.6 (semântica RunWallet).

- [ ] `Countdown`: `Set/Tick/Done/Remaining`; nunca negativo; `Tick` antes de `Set` é no-op seguro.
- [ ] `RunWallet`: `EarnCoins/EarnXp` · `(long coins, int xp) BuildCommit(bool won, int multiplier = 1)` → vitória: coins×mult; derrota: coins 0; **XP SEMPRE integral**; após commit, zera. Teste do "dobrar x2": `BuildCommit(true, 2)` com 100 → 200. Commit `feat(domain): Countdown puro e RunWallet (XP sempre comitada)`.

### Task 9: InterstitialPolicy

**Files:** Create: `Domain\InterstitialPolicy.cs`, Test: `InterstitialPolicyTests.cs` · **Ler:** doc 12 §4.8 (`MaybeShowInterstitial`), CANON §11 — item do checklist §9 do doc 12 ("testes automatizados").

```csharp
public static class InterstitialPolicy
{
    public static bool ShouldShow(bool adsRemoved, int highestLevelCleared, int levelsSinceInterstitial,
                                  int consecutiveDefeats, int minLevel = 6, int levelGap = 3)
        => !adsRemoved && highestLevelCleared >= minLevel
           && levelsSinceInterstitial >= levelGap && consecutiveDefeats < 2;
}
```

- [ ] Testes: nunca antes da fase 6; nunca com gap < 3; **nunca após 2 derrotas seguidas**; nunca com adsRemoved; caso liberado retorna true; params de Remote Config substituíveis. Commit `feat(domain): política de interstitial do CANON §11 testável`.

### Task 10: SaveData + Migração + Checksum

**Files:** Create: `Domain\SaveData.cs`, `Domain\SaveMigration.cs`, `Domain\SaveChecksum.cs` + 3 testes em `tests\Domain.Persistence.Tests\` · **Ler:** doc 12 §4.7 (modelo COMPLETO — copiar todos os campos; `[Serializable]` é `System`, permitido no Domain).

- [ ] `SaveData`: POCO exato do doc 12 §4.7 (schemaVersion, playerId, moedas, units, upgradeTracks, supplyCap 60, campos de ads pacing, consentStatus...). `UnitProgress`, `TrackProgress`, `LevelRecord` juntos.
- [ ] `SaveChecksum.Compute(string json)` → SHA256 hex de `json + SALT` const · `string Pack(string json)` → `"<checksum>\n<json>"` · `bool TryUnpack(string payload, out string json)`. Testes: round-trip; adulterar 1 char → TryUnpack false; payload sem `\n` → false (não lança).
- [ ] `SaveMigration.Migrate(SaveData d)` com gates incrementais (`if (ver < 2) {...}`); v1 atravessa todos até atual; **teste**: save v1 com moedas → migrado preserva moedas e termina na versão atual; gate nunca é `switch` exclusivo. Commit `feat(domain): modelo de save com checksum e migração incremental`.

### Task 11: EconomyMath

**Files:** Create: `Domain\EconomyMath.cs`, Test: `EconomyMathTests.cs` · **Ler:** CANON §8/§9, doc 12 §4.6.

- [ ] `LevelReward(int levelIndex, float baseReward = 100, float growth = 1.10f, float mult = 1)` → fase 1 = 100; fase 10 ≈ 236 (`100×1.10^9`, arredondado) · `UpgradeCost(int level, float costBase = 100, float growth = 1.35f)` → nível 0 = 100 · `TrackBonus(UpgradeTrack t, int level)` → +5%/nível; `StartArmy` → +1 unidade a cada 2 níveis (retorna unidades, não %) · `ShardsToLevel(int n)` → `10 × 2^(n−1)` (CANON §8). Commit `feat(domain): curvas canônicas de economia e upgrades`.

### Task 12: Esqueleto do projeto Unity

**Files:** Create: `MutantArmyRun\Packages\manifest.json`, `MutantArmyRun\ProjectSettings\ProjectVersion.txt`, árvore de pastas com `.gitkeep`.

- [ ] `manifest.json`: `com.unity.render-pipelines.universal: 14.0.11`, `com.unity.textmeshpro: 3.0.9`, `com.unity.inputsystem: 1.7.0`, `com.unity.test-framework: 1.1.33`, módulos default. `ProjectVersion.txt`: `m_EditorVersion: 2022.3.62f1`.
- [ ] Criar a árvore completa de `Assets\_Project\` (ver Estrutura de Arquivos). Commit `chore(unity): esqueleto do projeto 2022.3 LTS + URP`.

### Task 13: Core — eventos, GameManager, bootstrap, SOs

**Files:** Create: tudo de `Scripts\Core\` (ver árvore) · **Ler:** doc 12 §3.2, §3.3, §4.1, §5.1 — os códigos de lá são o CONTRATO; expandir os `/* ... */` para código real.

- [ ] `EventStructs.cs`: `GateResult`, `SupplyOverflow`, `UnitDeath`, `BossPhase`, `LevelResult`, `CurrencyChange` (structs, campos do doc 12 §3.2 + §4.x).
- [ ] `GameEvents.cs`: bus estático exato do doc 12 §3.2, com TODOS os `Raise*`.
- [ ] `GameManager.cs`: delega a `Domain.GameStateStack`; tabela `Allowed` vive no Domain (Task 7) — o manager só orquestra `EnterState/ExitState/ResolveEnd` (doc 12 §4.1), guard `_endSelectionDone` incluído.
- [ ] Os 10 SOs do doc 12 §5.1, com `GateConfigSO.Apply` delegando a `GateMath.Apply` e `ElementChartSO.GetMultiplier` delegando a `Domain.ElementChart`.
- [ ] `GameBootstrap`/`GameSceneBootstrap` (doc 12 §3.3) usando providers Null da Task 15 — compila sem Firebase. `RcKeys.cs`/`AdPlacement.cs`: todas as strings mágicas viram `const` (doc 12 §3.3).
- [ ] Commit `feat(core): event bus, GameManager em pilha, bootstrap e os 10 SOs canônicos`.

### Task 14: Gameplay — crowd, gates, level, boss, combate, câmera

**Files:** Create: tudo de `Scripts\Gameplay\` · **Ler:** doc 12 §4.2–§4.5, §4.11, §4.12, §6.2, §6.4 — expandir os esqueletos para código completo.

- [ ] `CrowdManager`: arrays SoA, `ReconcileTo` (funil único), `EnforceSupplyCap` delegando a `Domain.SupplyLedger` + metering de conversão, formação via `Domain.FormationMath`, `Separation` com `SpatialGridXZ`, `KillUnit` (dying+pool), `ApplyMutation` (3 slots rotativos), `Centroid` cacheado.
- [ ] `SpatialGridXZ`: grid uniforme, `Rebuild` 1×/frame zero-alloc, `Neighbors(i)` por células vizinhas.
- [ ] `CrowdRenderer`: `Graphics.RenderMeshInstanced` por tipo, `Matrix4x4[]` reusados.
- [ ] `GatePairView`/`GateView`/`GateManager`: contrato one-shot completo do doc 12 §4.3 (flag reset no `Setup`, rótulo via `OnValidate`, contra-escala, `Consume` por `GateType`, `PickPairForBoss` com `System.Random` — rota ótima explora `boss.weaknesses`, armadilha = número maior com elemento resistido).
- [ ] `LevelManager`/`TrackSegment`: âncoras, seed, spawn/recicla por distância, zona de segurança pós-portal, `ResetRun` soft (sem LoadScene).
- [ ] `BossManager`/`BossRuntime`: waves por ponteiro, telegraph com `Domain.Countdown`, fases via `Domain.CombatMath.BossPhase`, fraqueza rotativa, `Die()` com slow motion.
- [ ] `CombatSystem`: tick 10 Hz, `ComputeCrowdDamage` via `Domain.CombatMath`, chart via `ElementChartSO`.
- [ ] `VFXManager` (orçamento de partículas + `SlowMotion(0.3f, 0.8f)` escalando `fixedDeltaTime`) e `CameraRig` (doc 12 §4.12 completo).
- [ ] Commit `feat(gameplay): crowd SoA, portais one-shot, pista por âncoras, boss e câmera`.

### Task 15: Meta + Services (providers Null)

**Files:** Create: tudo de `Scripts\Meta\` e `Scripts\Services\` · **Ler:** doc 12 §4.6–§4.10, §7.

- [ ] `EconomySystem` (RunWallet via `Domain.RunWallet`, curvas via `Domain.EconomyMath`), `UpgradeSystem`, `RewardSystem`, `UnitManager`, `SaveSystem` (gravação atômica tmp→bak→rename, flush triplo, `MarkDirty`, `SaveAsync` com snapshot na main thread, checksum/migração via Domain).
- [ ] Interfaces: `IAdsProvider { bool IsRewardedReady; void ShowRewarded(string placement, Action<bool>); void ShowInterstitial(); void Init(); }` + `NullAdsProvider` (rewarded sempre não-pronto → botões somem, doc 12 §7.3); idem Analytics (loga no console em DEV) e RemoteConfig (retorna os `Defaults` embutidos do doc 12 §4.10).
- [ ] `AdsManager.MaybeShowInterstitial` delega a `Domain.InterstitialPolicy` (mesma assinatura de parâmetros).
- [ ] Commit `feat(meta,services): economia, save atômico e serviços atrás de providers Null`.

### Task 16: UI + Editor tooling + EditMode tests

**Files:** Create: tudo de `Scripts\UI\`, `Scripts\Editor\`, `Scripts\Tests\EditMode\` · **Ler:** doc 12 §4.13, §8, §9; doc 09 (telas SCR/OVL).

- [ ] `UIManager` (duas pilhas, safe area 1× no root), `UIScreen`/`UIOverlay` base, `HudController` (assina eventos — zero polling), `BossScoutOverlay`, `ResultScreen` (passiva, mostra DELTA), `FeedbackTextController` (NICE→BOSS BREAKER por thresholds de portal).
- [ ] `ProjectSetup.cs` (menu `MAR Tools/Setup Project`): cria as 3 cenas (Boot/Main/Game) por código com `[Services]` + managers wireados, salva em `Scenes\`; `MvpContentFactory.cs` (menu `MAR Tools/Create MVP Content`): gera por código os assets — 8 `GateConfigSO` (CANON §10), 5 `UnitConfigSO` (stats doc 03/CANON §5), 5 `BossConfigSO`, `ElementChartSO` default, 4 `RarityConfigSO`, 4 `UpgradeConfigSO` (trilhas MVP), 3 `WorldConfigSO`, 20 `LevelConfigSO` com seeds determinísticas — tudo com `Undo` e `AssetDatabase.SaveAssets`.
- [ ] `MarToolsWindow.cs` (limpar save/PlayerPrefs, abrir persistentDataPath, overrides locais de RC), `CheatsMenu.cs` (moedas via `EconomySystem.Earn` — nunca save direto), `SoValidator.cs` (campo nulo, fase sem boss → erro), `EditorGuards.cs` (scan `using UnityEditor` fora de Editor/).
- [ ] EditMode tests: `JsonRoundTripTests` (JsonUtility round-trip do SaveData completo, checksum→backup) e `GateConfigTests` (os 8 assets aplicam `GateMath` corretamente) — rodarão na primeira abertura do Unity.
- [ ] Commit `feat(ui,editor): UI de duas pilhas, MAR Tools com setup por código e validadores`.

### Task 17: README + notices + verificação final

**Files:** Create: `MutantArmyRun\README.md`, `MutantArmyRun\THIRD-PARTY-NOTICES.md`, `licencas-de-assets.csv` (planilha de assets, colunas: nome/URL/licença/autor/uso).

- [ ] README: pré-requisitos (Unity Hub + 2022.3 LTS — passo a passo), abrir projeto, rodar `MAR Tools/Setup Project` + `Create MVP Content`, rodar testes (Test Runner + `dotnet test`), status do que está verificado vs pendente de Unity.
- [ ] THIRD-PARTY-NOTICES.md: notices MIT (padrão Signals reimplementado, SaveGameFree como referência — ver doc 15) + política (CC0 livre, CC-BY com crédito, sem-licença = proibido copiar).
- [ ] Rodar `dotnet test` nos 3 projetos → TUDO verde; `git log --oneline` mostra 1+ commit por task. Commit final `docs: README de onboarding e notices`.

---

## Self-review (executado na escrita do plano)

- **Cobertura da spec:** checklist §9 do doc 12 → portais puros ✓ (T3), save round-trip/migração ✓ (T10 + EditMode T16), timers ✓ (T6/T8), interstitial ✓ (T9), guard UnityEditor ✓ (T16), seed determinística ✓ (T14), pooling ✓ (T14). Itens que EXIGEM Unity/device (60 fps, boot ≤2,5 s, DebugView) ficam para pós-instalação — registrados no README (T17).
- **Tipos consistentes:** `GateMath.Apply(GateType,float,int)` usado em T3/T13/T16; `GameStateStack` em T7/T13; `SupplyLedger` em T4/T14; enums únicos em T2.
- **Sem placeholders:** tasks de camada Unity referenciam código CONCRETO do doc 12 (que contém os esqueletos) — doc 12 é parte do plano; executores devem lê-lo antes de cada task.
