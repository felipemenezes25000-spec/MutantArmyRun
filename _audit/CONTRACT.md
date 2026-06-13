# CONTRATO DE IMPLEMENTAÇÃO — Missão Nota 10 (MutantArmyRun)

> LEITURA OBRIGATÓRIA para todo agente de implementação. Violação de qualquer regra = retrabalho.
> Mapas de subsistema (leitura obrigatória conforme sua tarefa): `C:\Users\Felipe\Downloads\jogo test\_audit\map-*.md`

## 0. Caminhos

- Projeto Unity: `C:\Users\Felipe\Downloads\jogo test\MutantArmyRun`
- Scripts: `MutantArmyRun\Assets\_Project\Scripts\{Domain,Core,Gameplay,Meta,Services,UI,Editor,Tests}`
- Unity: `D:\6000.4.8f1\Editor\Unity.exe` (batchmode disponível)
- Testes Domain: `tests\Domain.{Flow,Gameplay,Persistence}.Tests` (xunit net8.0, `dotnet test`)
- GDD canônico: `GDD\CANON.md` (fonte da verdade de números/nomes)

## 1. Regras invioláveis de arquitetura

1. **Domain é puro**: zero `using UnityEngine`, C# 9, APIs netstandard2.1 (SEM records C#10, SEM Convert.ToHexString). RNG sempre `System.Random` injetado por parâmetro. Estilo: `static class` p/ math, `sealed class` p/ estado, `struct` p/ dados de fronteira.
2. **Fronteiras de asmdef** (direção das referências): Domain ← Core ← {Meta, Services} ← UI ← Gameplay. Gameplay PODE referenciar UI; UI NUNCA referencia Gameplay; Gameplay NUNCA referencia Meta/Services. Comunicação entre camadas-irmãs = `GameEvents`/`JuiceEvents` (Core) ou hooks no GameManager (`RunStartBonusProvider`, `ResultBuilder`, `RunCommitter`).
3. **Enums append-only** em `Domain/Enums.cs` — NUNCA reordenar; ordinais são contrato com SOs serializados. NÃO adicionar valores em `GameState` (SanityTests asserta 8 valores exatos).
4. **Eventos**: bus estático com payload struct, padrão `event Action<T>` + `RaiseX(T)`. Listeners: `-=` antes de `+=` em Init; limpar em OnDisable/OnDestroy.
5. **Pooling**: NUNCA `Instantiate/Destroy` em gameplay por frame — `ObjectPool`, `Release` nunca `Destroy`. Todo sistema novo PRECISA de caminho de drain/reset (soft reset na mesma cena: `ResetRun→BeginRun`).
6. **Determinismo**: ordem de consumo do RNG da fase é CONTRATO (gates → obstáculos → segmentos). Sistemas novos usam RNG DERIVADO: `new System.Random(level.seed * primo + constante)` (padrão RiskResolver: `seed*486187739+1`). Inimigos usarão `seed * 92821 + 7`.
7. **Conteúdo nasce na FACTORY** (`Editor/MvpContentFactory.cs`), nunca em .asset YAML manual — a factory sobrescreve campos gerenciados no próximo `CreateAll`. Campos que sobrevivem: `prefab`/`viewPrefab` (load-or-keep), `arenaWaves` quando != null.
8. **Sem `UnityEditor` fora de `Editor/`** (EditorGuards quebra build). Novos SOs FORA de `Resources/` (só GameSettings.asset é permitido lá). Novo SO = foreach de validação em `Editor/SoValidator.cs`.
9. **Zero `Debug.LogError` em caminhos normais** — PlayMode tests com `LogAssert.ignoreFailingMessages=false` derrubam tudo. Warning ok com parcimônia.
10. **Slow motion**: usar `VFXManager.SlowMotion(scale, seconds)` (multiplicativo/restaurável). Nunca setar `Time.timeScale` direto (testes rodam a 8-10x).
11. **Comentários em PT-BR**, estilo existente: explicam CONTRATO/decisão, citam doc/CANON quando relevante. Sem comentários "o que a linha faz".
12. **Null-safety greybox-friendly**: todo acesso a `X.Instance`/campo serializado com guarda de null — o jogo degrada, nunca quebra.
13. **Save**: campo novo no `SaveData` = aditivo + bump `SaveMigration.CurrentVersion` + gate de migração normalizando null + testes round-trip.
14. **Decisão registrada**: limiares de fase de boss permanecem 0.5/0.25 (canônicos, wired no HUD/Domain/testes) — "66%/33%" da missão tratado como aproximação. 3 fases continuam existindo.

## 2. CONTRATO DE TIPOS NOVOS (Onda 1 — exato, não alterar assinaturas)

### Domain/Enums.cs (append no fim do arquivo)
```csharp
public enum ElementRelation { Neutral, Weakness, Resisted, Immune }
public enum ComboKind { PerfectGate, WeaknessHit, BossBreaker, Clutch, NoLoss, Overkill }
public enum FailReason { None, ArmyTooSmall, WrongElement, TooManyLossesOnTrack, BossResistedDamage,
                         NoTankUnits, NoAreaDamage, IgnoredHealers, HitByLava, HitByLaser, LowUpgradePower }
public enum TrackEnemyKind { WeakHorde, Tank, Ranged, Healer }
```

### Domain novos arquivos (com testes xunit; adicionar `<Compile Include>` no csproj certo)
- `Domain/WeaknessJudge.cs` → `public static class WeaknessJudge { public static ElementRelation Classify(float multiplier); }` (>1.05 Weakness; <=0 Immune; <0.95 Resisted; senão Neutral). Teste em Domain.Gameplay.Tests.
- `Domain/ComboMath.cs` → `public static class ComboMath` com:
  - `public struct RunComboStats { public int bestGateChoices; public int totalGateChoices; public int weaknessHits; public int unitsLostOnTrack; public int survivors; public int armyPeak; public float bossFightSeconds; public float overkillDamage; public float bossMaxHp; }`
  - `public static int Evaluate(RunComboStats s, bool won, ComboKind[] buffer)` — preenche buffer, retorna contagem. Regras: PerfectGate = bestGateChoices==totalGateChoices && totalGateChoices>0; WeaknessHit = weaknessHits>0; BossBreaker = won && bossFightSeconds>0 && bossFightSeconds<=8; Clutch = won && survivors>0 && armyPeak>0 && survivors<=Math.Max(1,(int)(armyPeak*0.1f)); NoLoss = won && unitsLostOnTrack==0; Overkill = won && bossMaxHp>0 && overkillDamage>=bossMaxHp*0.25f.
  - `public static int BonusCoins(ComboKind kind)` — PerfectGate 25, WeaknessHit 15, BossBreaker 40, Clutch 50, NoLoss 30, Overkill 20.
  Teste em Domain.Flow.Tests.
- `Domain/FailReasonResolver.cs` → `public static class FailReasonResolver` com
  `public struct DefeatContext { public int armySizeAtBossStart; public int unitsLostOnTrack; public int armyPeak; public float damageDealtToBoss; public float bossMaxHp; public bool bossUsedLaser; public bool armyHadResistedElement; public bool diedOnTrack; public bool diedToHazard; }`
  `public static FailReason Resolve(DefeatContext c)` — prioridade: WrongElement (armyHadResistedElement) > HitByLaser (bossUsedLaser && !diedOnTrack) > HitByLava (diedToHazard) > TooManyLossesOnTrack (unitsLostOnTrack >= armyPeak/2 && armyPeak>=10) > ArmyTooSmall (armySizeAtBossStart < 10) > BossResistedDamage (damageDealtToBoss < bossMaxHp*0.5f) > LowUpgradePower. Teste em Domain.Flow.Tests.
- `Domain/BossCollectionMath.cs` → `public static class BossCollectionMath` com
  `[Serializable] public class BossRecord { public string bossId; public int kills; public float bestTimeSeconds; public int bestSurvivors; public bool weaknessDiscovered; public int rareKills; }` (classe Serializable p/ JsonUtility)
  `public static BossRecord FindOrAdd(List<BossRecord> list, string bossId)`; `public static bool RegisterKill(BossRecord r, float timeSeconds, int survivors, bool usedWeakness, bool wasRare)` (retorna true se algum recorde melhorou); `public static int TotalKills(List<BossRecord> list)`. Teste em Domain.Persistence.Tests.
- `Domain/RareBossMath.cs` → `public static class RareBossMath { public static bool Roll(System.Random rng, float chance); public static float HpMultiplier => 1.5f; public static float RewardMultiplier => 3f; }` (chance clamp 0..0.25). Teste em Domain.Flow.Tests.

### Domain/SaveData.cs (aditivo) + SaveMigration v5
```csharp
public List<BossCollectionMath.BossRecord> bossCollection = new List<BossCollectionMath.BossRecord>();
public int tutorialStepMask;   // bitmask de passos do TutorialDirector já vistos
```
`SaveMigration.CurrentVersion` 4→5; gate `if (d.schemaVersion < 5)` normaliza null. Atualizar BuildFullSave dos testes JsonRoundTrip.

### Core/EventStructs.cs (structs novos)
```csharp
public struct BossElementalHit { public ElementType element; public ElementRelation relation; public float damage; public Vector3 position; }  // + ctor
public struct ComboEarned { public ComboKind kind; public int bonusCoins; }                                    // + ctor
public struct BossSpecialTelegraph { public float seconds; public Vector3 position; public string bossId; }    // + ctor
public struct BossDied { public string bossId; public Vector3 position; public bool wasRare; public float fightSeconds; }  // + ctor
public struct TrackEnemyKilled { public TrackEnemyKind kind; public Vector3 position; public int coins; }      // + ctor
public struct EnemyWaveCleared { public int enemiesKilled; public Vector3 position; }                          // + ctor
```
### Core/EventStructs.cs — LevelResult ganha campos NOVOS no fim (ctors antigos preservados, default 0/None):
```csharp
public int comboCount;        // combos conquistados na fase
public int comboBonusCoins;   // bônus total em moedas dos combos
public FailReason failReason; // motivo de derrota rico (None em vitória)
```

### Core/GameEvents.cs (eventos novos, mesmo padrão)
```csharp
public static event Action<BossElementalHit> OnBossElementalHit;     // rate-limited na origem (>=0.5s entre raises)
public static event Action<ComboEarned> OnComboEarned;
public static event Action<BossSpecialTelegraph> OnBossSpecialWarning;
public static event Action<BossDied> OnBossDied;
public static event Action<TrackEnemyKilled> OnTrackEnemyKilled;
public static event Action<EnemyWaveCleared> OnEnemyWaveCleared;
// + RaiseX correspondentes
```

### Core/SO/EnemyConfigSO.cs (NOVO, em Core/SO — Core não enxerga Gameplay)
```csharp
[CreateAssetMenu(menuName = "MutantArmy/Enemy")] public class EnemyConfigSO : ScriptableObject {
  public string enemyId; public string displayName; public TrackEnemyKind kind;
  public float maxHp = 10f; public float dps = 1f; public float moveSpeed = 0f;
  public ElementType element = ElementType.None; public BodyType bodyType = BodyType.Organic;
  public int rewardCoins = 1; public int worldIndex = 1;     // mundo temático (1..10)
  public float attackRange = 2f;    // Ranged usa >6
  public float healPerSecond = 0f;  // Healer
  public GameObject prefab;          // null → cápsula/cubo fallback tintado por kind
}
```

### Core/SO/LevelConfigSO.cs (aditivo)
```csharp
[Serializable] public class EnemySlot { public float trackPosition; public EnemyConfigSO enemy; public int count = 3; }
public EnemySlot[] enemies = new EnemySlot[0];
```

## 3. PROPRIEDADE DE ARQUIVOS POR ONDA (só edite o que é seu; LEITURA é livre)

### Onda 1 (1 agente): contratos
EDITA: Domain/Enums.cs, Domain/SaveData.cs, Domain/SaveMigration.cs, Core/EventStructs.cs, Core/GameEvents.cs, Core/JuiceEvents.cs, Core/SO/LevelConfigSO.cs; CRIA: Domain/{WeaknessJudge,ComboMath,FailReasonResolver,BossCollectionMath,RareBossMath}.cs, Core/SO/EnemyConfigSO.cs, testes xunit + linhas Compile Include nos csproj; Tests/EditMode/JsonRoundTripTests.cs (BuildFullSave).

### Onda 2 (4 agentes paralelos): gameplay
- **W2-A Boss**: EDITA Gameplay/{BossManager,BossRuntime,CrowdManager,CombatSystem}.cs, Core/SO/BossConfigSO.cs; CRIA Gameplay/Bosses/*.cs (BossBehavior, BossContext, behaviors).
- **W2-B Inimigos**: EDITA Gameplay/LevelManager.cs, Core/SO/EndlessLevelGenerator.cs; CRIA Gameplay/Enemies/*.cs (TrackEnemyManager + behaviors). NÃO edita CrowdManager/CombatSystem (só CHAMA APIs existentes).
- **W2-C Combos/Gates**: EDITA Gameplay/{GateManager,RiskResolver}.cs; CRIA Gameplay/ComboSystem.cs.
- **W2-D Juice**: EDITA Gameplay/{JuiceController,VFXManager,CameraRig}.cs.

### Onda 3 (4 agentes paralelos): UI/Meta/Services
- **W3-A UI in-game**: EDITA UI/{FeedbackTextController,TutorialController}.cs; CRIA UI/BossHudController.cs.
- **W3-B Resultado**: EDITA UI/{ResultScreen,GameUIController}.cs.
- **W3-C Meta**: EDITA Core/GameManager.cs, Meta/{EconomySystem,MissionSystem,RewardSystem}.cs; CRIA Meta/BossCollectionSystem.cs.
- **W3-D Services**: EDITA Services/{AnalyticsManager,AudioManager,AudioCatalogSO,NullRemoteConfigProvider,AdsManager,IAPManager}.cs, Core/RcKeys.cs; CRIA Services/{MockAdsProvider,MockIapProvider}.cs (se fizer sentido).

### Onda 4 (3 agentes paralelos): conteúdo/editor/Android
- **W4-A Conteúdo**: EDITA Editor/{MvpContentFactory,SoValidator}.cs.
- **W4-B Cenas/wiring**: EDITA Editor/{ProjectSetup,GreyboxFactory,JuiceFactory}.cs.
- **W4-C Android/Perf**: EDITA Editor/BuildTools.cs, ProjectSettings/ProjectSettings.asset; CRIA Gameplay/DevPerfOverlay.cs.

### Onda 5: testes + pipeline + correções (sequencial, coordenado por mim)

## 5. ADENDO ONDA 2 — APIs cruzadas FIXADAS (assinaturas obrigatórias)

Eventos adicionados pelo orquestrador (já existem, USE-os):
- `GameEvents.OnFailReasonResolved(FailReason)` / `RaiseFailReasonResolved` — Gameplay publica ANTES da transição p/ Defeat.
- `GameEvents.OnRareBossAnnounced(RareBossAnnounce)` / `RaiseRareBossAnnounced` — struct {bossId, hpMultiplier, rewardMultiplier}.
- `JuiceEvents.OnGoodGateChoice(Vector3)` / `OnBadGateChoice(Vector3)` / `OnRiskResolved(bool, Vector3)`.

**W2-A (Boss) DEVE expor** (W2-C e Onda 3 dependem disso):
- `CrowdManager.RunUnitsLost { get; }` (int — unidades perdidas na CORRIDA+arena, reset no início da corrida)
- `CrowdManager.RunArmyPeak { get; }` (int — pico de exército na corrida, reset idem)
- `BossManager.LastFight { get; }` — `public struct BossFightSummary { public string bossId; public float maxHp; public float fightSeconds; public bool wasRare; public int weaknessHits; public int resistedHits; public bool victory; }` (preenchido na morte/derrota; sobrevive ao fim da luta)
- `BossManager.NextFightRare { get; }` (bool — rolado no StateEntered(BossScout) com RNG derivado `seed*48611+3`, chance default serializada 0.06f; consome no BeginFight: HP ×RareBossMath.HpMultiplier)
- Raises: `OnBossElementalHit` (rate-limited ≥0,5 s, no tick do CombatSystem), `OnBossSpecialWarning` (início do telegraph), `OnBossDied` (na morte, ANTES de ChangeState(Victory)), `OnRareBossAnnounced` (no BossScout se raro), `OnFailReasonResolved` (no caminho de wipe do exército, via FailReasonResolver com DefeatContext completo)
- Morte cinematográfica: sequência de ~1,2 s (Countdown) entre Die() e ChangeState(Victory) — slow motion existente + behavior.OnDeath(); estado do GameManager permanece BossFight durante a sequência.

**W2-C (Combos) DEVE expor/consumir**:
- `GateManager.WasBestChoice(GateConfigSO chosen, GateConfigSO rejected)` (bool público, usa o Classify privado existente vs boss da fase)
- ComboSystem (novo, `Gameplay/ComboSystem.cs`, IInitializable + singleton padrão dos managers): assina OnGateConsumed (avalia via WasBestChoice → JuiceEvents.RaiseGood/BadGateChoice), OnBossElementalHit (conta weakness hits), OnBossDied (vitória: monta ComboMath.RunComboStats com CrowdManager.RunUnitsLost/RunArmyPeak/Count, BossManager.LastFight, CombatSystem.TotalDamageDealt → Evaluate → RaiseComboEarned POR combo, na ordem). Reset por GameManager.LevelStarted.
- RiskResolver publica `JuiceEvents.RaiseRiskResolved(success, pos)` no veredito.

**Onda 3 consome**: OnComboEarned (GameManager soma p/ LevelResult.comboCount/comboBonusCoins; EconomySystem credita bônus), OnFailReasonResolved (GameManager → LevelResult.failReason), OnBossDied/OnRareBossAnnounced (BossCollectionSystem, UI, Audio), OnBossElementalHit (FeedbackTextController FRAQUEZA!/RESISTIU!, AudioManager), JuiceEvents.OnGood/BadGateChoice (FeedbackTextController BOA ESCOLHA!), OnBossSpecialWarning (BossHud + Audio), OnTrackEnemyKilled/OnEnemyWaveCleared (Economy credita coins, Audio, Analytics).

## 4. Verificação (todo agente DEVE rodar antes de declarar pronto)

- Domain: `dotnet test "C:\Users\Felipe\Downloads\jogo test\tests\<Projeto>" --nologo -v q` (os 3 projetos se tocou Domain).
- Compilação Unity (só quando instruído — 1 instância por vez, NUNCA paralelo):
  `& "D:\6000.4.8f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath "C:\Users\Felipe\Downloads\jogo test\MutantArmyRun" -logFile "<log>"` e grep `error CS` no log.
  REGRA DE OURO DAS ONDAS PARALELAS: agentes de onda NÃO rodam Unity (lock de projeto) — o orquestrador compila no fim da onda.
- C# sintático rápido: revisar o diff você mesmo; na dúvida, conferir convenções num arquivo vizinho.
