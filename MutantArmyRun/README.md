# Mutant Army Run — Projeto Unity

> Runner de multidão hybrid-casual com camada estratégica (Boss Scout + Supply + elementos).
> Spec completa em `..\GDD\` — a fonte da verdade é `..\GDD\CANON.md`; a arquitetura é `..\GDD\12-arquitetura-unity.md`.

Este repositório contém o **scaffold completo do MVP** para Unity **6 (6000.4) + URP 17**, com toda a
lógica determinística de jogo isolada no assembly `MutantArmy.Domain` (C# puro, sem `UnityEngine`),
coberta por testes xUnit que rodam **sem o Unity instalado** via `dotnet test`.

> **Nota de migração (2026-06-11):** o projeto foi concebido para 2022.3 LTS (CANON §13) e migrado
> para **Unity 6000.4.8f1** — a versão instalada nesta máquina. Mudanças: URP 14→17.4, TextMeshPro
> agora vem embutido no pacote `com.unity.ugui` 2.0 (o assembly `Unity.TextMeshPro` continua existindo,
> então os asmdefs não mudaram). Compilação, cenas, conteúdo MVP e testes EditMode já foram validados
> no 6000.4.8f1 em batchmode.

---

## 1. Pré-requisitos

### 1.1 Unity Hub

1. Baixe o Unity Hub na página oficial: <https://unity.com/download>
2. Instale e faça login com uma conta Unity (gratuita — a licença Personal é suficiente).

### 1.2 Unity 6 (6000.4) com módulo Android

O projeto está fixado em **`6000.4.8f1`** (ver `ProjectSettings\ProjectVersion.txt`) — já instalado
nesta máquina em `D:\6000.4.8f1\Editor\Unity.exe`.

Para o build Android (ainda não necessário para abrir/jogar no editor), confirme no Hub que a
instalação `6000.4.8f1` tem os módulos:
- **Android Build Support** (com **OpenJDK** e **Android SDK & NDK Tools**)

Se faltar: Hub → **Installs → ⚙ da versão → Add modules**. Documentação oficial:
<https://docs.unity3d.com/6000.4/Documentation/Manual/android-sdksetup.html>

### 1.3 .NET 8 SDK (testes do Domain — funciona SEM Unity)

1. Baixe e instale o SDK (não apenas o runtime): <https://dotnet.microsoft.com/download/dotnet/8.0>
2. Verifique no PowerShell: `dotnet --version` (deve reportar `8.x`).

---

## 2. Abrindo o projeto

1. Unity Hub → **Projects → Open** → selecione a pasta **`MutantArmyRun`** (esta pasta, a que contém `Assets\` e `Packages\`).
2. A primeira abertura importa os pacotes (URP 14, TextMeshPro, uGUI, Test Framework) e compila
   os asmdefs `MutantArmy.Domain / Core / Gameplay / Meta / Services / UI / Editor` — pode levar alguns minutos.
3. O MVP usa o **Input Manager clássico** (`Input.GetMouseButton`/`Input.mousePosition` no `CrowdAnchor`)
   e o pacote `com.unity.inputsystem` foi **removido do manifest** de propósito. Se algum pacote/janela
   oferecer ativar o backend novo de input ("Input System only"), **recuse** — com o backend novo ativo,
   toda chamada `Input.*` lança `InvalidOperationException` em runtime e o drag lateral morre.

O projeto **não depende de nenhum SDK externo** (AppLovin MAX, Firebase, RevenueCat): todos os serviços
ficam atrás de interfaces (`IAdsProvider`, `IAnalyticsProvider`, `IRemoteConfigProvider`) com
implementações **Null** — compila e roda limpo logo após o clone. Os SDKs reais entram nas semanas
S1–S4 do plano de integração (ver `..\GDD\15-referencias-e-recursos.md` §6.2).

---

## 3. Setup inicial (uma vez por clone)

Com o projeto aberto no editor, rode os dois menus na ordem:

1. **`MAR Tools → Setup Project`** — cria por código as 3 cenas (`Boot`, `Main`, `Game`) em
   `Assets\_Project\Scenes\`, com o objeto `[Services]` e os managers já wireados.
2. **`MAR Tools → Create MVP Content`** — gera por código todos os assets de conteúdo do MVP em
   `Assets\_Project\ScriptableObjects\`: 8 `GateConfigSO` (CANON §10), 5 `UnitConfigSO`, 5 `BossConfigSO`,
   `ElementChartSO` default, 4 `RarityConfigSO`, 4 `UpgradeConfigSO`, 3 `WorldConfigSO` e
   20 `LevelConfigSO` com seeds determinísticas.

Depois disso, abra `Assets\_Project\Scenes\Boot.unity` e dê Play. A janela utilitária do menu
**`MAR Tools`** (limpar save/PlayerPrefs, abrir `persistentDataPath`, overrides locais de
Remote Config) ajuda no dia a dia.

---

## 4. Rodando os testes

### 4.1 Testes .NET do Domain (NÃO precisam de Unity)

No PowerShell, a partir da **raiz do repositório** (a pasta acima de `MutantArmyRun\`):

```powershell
dotnet test "tests\Domain.Gameplay.Tests" -v minimal
dotnet test "tests\Domain.Flow.Tests" -v minimal
dotnet test "tests\Domain.Persistence.Tests" -v minimal
```

Os 3 projetos compilam os MESMOS arquivos `.cs` de
`MutantArmyRun\Assets\_Project\Scripts\Domain\` (via `<Compile Include>` com lista explícita),
com `LangVersion 9.0` — baseline conservadora que compila tanto no Unity quanto no .NET 8.

### 4.2 Testes EditMode (precisam do Unity aberto)

1. Rode antes o **`MAR Tools → Create MVP Content`** (os `GateConfigTests` validam os 8 assets de portal gerados).
2. **Window → General → Test Runner → aba EditMode → Run All**.
3. Suítes: `JsonRoundTripTests` (round-trip JsonUtility do `SaveData` completo + checksum/backup) e
   `GateConfigTests` (os 8 portais do CANON §10 aplicam `GateMath` corretamente).

---

## 5. Status — verificado hoje vs pendente de Unity instalado

Estado em **2026-06-11** (atualizado após a migração para Unity 6000.4.8f1 e validação em batchmode):

| Item | Status | Como foi/será verificado |
|---|---|---|
| Domain (`MutantArmy.Domain`) compila em .NET 8 com C# 9 | ✅ Verificado hoje | `dotnet build` implícito no `dotnet test` |
| `tests\Domain.Gameplay.Tests` — GateMath, SupplyLedger, ElementChart, CombatMath, FormationMath, RiskGate | ✅ Verificado hoje — **80 testes verdes** | `dotnet test` nesta máquina |
| `tests\Domain.Flow.Tests` — GameStateStack, Countdown, RunWallet, InterstitialPolicy | ✅ Verificado hoje — **49 testes verdes** | `dotnet test` nesta máquina |
| `tests\Domain.Persistence.Tests` — SaveData, SaveMigration, SaveChecksum, EconomyMath | ✅ Verificado hoje — **94 testes verdes** | `dotnet test` nesta máquina |
| Compilação dos asmdefs da camada Unity (Core/Gameplay/Meta/Services/UI/Editor) | ✅ Verificado hoje — **0 erros CS, 8 assemblies** | Batchmode no 6000.4.8f1 |
| Testes EditMode (`JsonRoundTripTests`, `GateConfigTests`) | ✅ Verificado hoje — **18/18 verdes** | `-runTests -testPlatform EditMode` em batchmode |
| `MAR Tools → Setup Project` / `Create MVP Content` | ✅ Executado hoje — 3 cenas + **53 assets** gerados | `-executeMethod` em batchmode; inspecionar no editor |
| 60 fps com exército cheio (Supply 60) em celular mediano | ⏳ Pendente de Unity + device | Profiler em build Android de desenvolvimento |
| Boot ≤ 2,5 s até o primeiro input | ⏳ Pendente de Unity + device | Cronômetro/Profiler em device |
| SDKs (MAX, Firebase, RevenueCat) + Mediation Debugger/DebugView | ⏳ Pendente (pós-Unity, semanas S1–S4) | Plano de integração do GDD doc 15 §6.2 |
| Build Android (.aab) | ⏳ Pendente de Unity + módulo Android | Build Settings → Android |

> Os totais de testes acima são o snapshot de hoje; novas suítes podem elevar os números — o critério
> permanente é `dotnet test` 100% verde nos 3 projetos.

---

## 6. Estrutura de pastas (resumo)

```text
jogo test\                          ← raiz do repositório
├── GDD\                            ← spec (CANON.md é a fonte da verdade)
├── docs\superpowers\plans\         ← plano de implementação
├── tests\                          ← 3 projetos xUnit (.NET 8) compilando o Domain
│   ├── Domain.Gameplay.Tests\
│   ├── Domain.Flow.Tests\
│   └── Domain.Persistence.Tests\
├── _research\                      ← clones de ESTUDO (sem licença) — NUNCA commitado, NUNCA copiado
└── MutantArmyRun\                  ← projeto Unity (abra ESTA pasta no Hub)
    ├── README.md                   ← este arquivo
    ├── THIRD-PARTY-NOTICES.md      ← política de licenças + notices MIT
    ├── licencas-de-assets.csv      ← planilha de assets (nome, url, licença, autor, uso)
    ├── Packages\manifest.json      ← URP 14, TMP, uGUI, Test Framework (sem Input System — input legacy)
    ├── ProjectSettings\
    └── Assets\_Project\
        ├── Scripts\
        │   ├── Domain\             ← C# puro (noEngineReferences) — lógica determinística testada
        │   ├── Core\               ← GameEvents, GameManager, bootstrap, 10 ScriptableObjects
        │   ├── Gameplay\           ← CrowdManager (SoA), GateManager, LevelManager, Boss, câmera
        │   ├── Meta\               ← EconomySystem, UpgradeSystem, RewardSystem, SaveSystem
        │   ├── Services\           ← Ads/Analytics/RemoteConfig atrás de providers Null
        │   ├── UI\                 ← UIManager (2 pilhas), HUD, BossScout, Result
        │   ├── Editor\             ← MAR Tools, validadores, cheats (SÓ código de editor)
        │   └── Tests\EditMode\     ← testes que rodam no Test Runner do Unity
        ├── Scenes\                 ← geradas pelo MAR Tools/Setup Project
        ├── ScriptableObjects\      ← gerados pelo MAR Tools/Create MVP Content
        └── Prefabs\ · Art\ · Audio\ · VFX\ · Settings\ · Resources\
```

---

## 7. Regras do repositório

- **`_research\` é apenas-estudo.** Os repositórios clonados ali não têm licença (todos os direitos
  reservados) — é PROIBIDO copiar qualquer linha. Padrões aprendidos são reimplementados do zero.
- **Licenças:** política completa e notices em `THIRD-PARTY-NOTICES.md`; todo asset novo entra com
  linha em `licencas-de-assets.csv` com a licença confirmada na página de origem.
- **Domain não conhece o Unity.** Nenhum `using UnityEngine` em `Scripts\Domain\` — a camada Unity
  delega ao Domain, nunca duplica lógica.
- **Código de editor só em `Scripts\Editor\`** (o `EditorGuards` escaneia violações).
- **Contratos de nomes:** tipos e métodos seguem o GDD doc 12 — não renomear sem atualizar a spec.
