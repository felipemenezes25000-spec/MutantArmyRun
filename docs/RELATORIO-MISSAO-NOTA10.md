# RELATÓRIO FINAL — Missão "Nota 10" · Mutant Army Run

> Execução autônoma completa (Ultracode) · 2026-06-12
> Baseline: projeto F6 (último commit `617b9d2`) → Resultado: produto hybrid-casual com bosses modulares, inimigos de pista, combos, coleção de bosses, derrota justa, tutorial contextual e prontidão comercial.

---

## 1. Resumo geral

A missão foi executada em **5 ondas orquestradas** (≈16 agentes de implementação + 13 leitores de auditoria), preservando a arquitetura existente (Domain puro ← Core ← Meta/Services ← UI ← Gameplay) e o pipeline de conteúdo por factories:

- **Onda 1 — Contratos**: enums, structs de evento, eventos do bus, matemática pura (combos, fail reasons, coleção, boss raro), SaveData v5.
- **Onda 2 — Gameplay**: sistema **BossBehavior modular** com 6 bosses + genérico, **inimigos de pista** (4 papéis), **ComboSystem**, juice cinematográfico (flash elemental, chuva de moedas, zoom de morte).
- **Onda 3 — UI/Meta/Services**: FRAQUEZA!/RESISTIU!/BOA ESCOLHA!, HUD de boss, tutorial contextual multi-passo, tela de vitória com combos + próximo boss, derrota que ensina, BossCollection, analytics completo, áudio, mocks de Ads/IAP.
- **Onda 4 — Conteúdo/Cenas/Android**: fases 1–5 redesenhadas na factory, 40 inimigos temáticos (10 mundos), wiring de cenas, Android readiness (IL2CPP/ARM64/portrait/id), build AAB, overlay de performance.
- **Onda 5 — Verificação**: pipeline completa de regeneração (10 etapas), 18 testes Unity novos, correção do bug de atropelo da F4, **todas as suítes verdes**.

**Placar de testes final**: Domain **420/420** (dotnet, 3 projetos) · EditMode **36/36** · PlayMode **8/8** · SoValidator **0 erros** · Build Windows OK.

---

## 2. Arquivos criados (26 de código + 5 de teste)

**Domain (puro, testado em CI):**
- `Domain/WeaknessJudge.cs` — classifica multiplicador elemental em Weakness/Resisted/Immune/Neutral
- `Domain/ComboMath.cs` — avaliação dos 6 combos + bônus de moedas
- `Domain/FailReasonResolver.cs` — derrota justa com prioridade de causas
- `Domain/BossCollectionMath.cs` — álbum de bosses (recordes, kills, fraqueza descoberta)
- `Domain/RareBossMath.cs` — rolagem determinística de variante rara (HP ×1.5, recompensa ×3)

**Core:**
- `Core/SO/EnemyConfigSO.cs` — definição de inimigo de pista (ScriptableObject)

**Gameplay:**
- `Gameplay/Bosses/BossBehavior.cs` — classe abstrata com os 7 hooks da missão (OnFightStart/OnHealthPhaseChanged/OnSpecialAttackWarning/OnSpecialAttackExecute/OnWeaknessHit/OnResistedHit/OnDeath)
- `Gameplay/Bosses/BossContext.cs` — contexto null-safe (runtime, config, view, exército, helpers)
- `Gameplay/Bosses/BossBehaviorRegistry.cs` — bossId → behavior (fallback genérico)
- `Gameplay/Bosses/GenericBossBehavior.cs` + 6 behaviors específicos (WoodGiant, ZombieTitan, ScorpionMech, LavaDragon, IceKing, DimensionalEntity)
- `Gameplay/Enemies/TrackEnemyManager.cs` — manager de inimigos de pista (grupos agregados, pooling, 4 papéis)
- `Gameplay/ComboSystem.cs` — detecção de combos por eventos
- `Gameplay/DevPerfOverlay.cs` — overlay dev (FPS/unidades/stress test 60/120/200) — só em Development Build/Editor

**UI / Meta / Services:**
- `UI/BossHudController.cs` — barra de HP do boss, fraqueza, aviso de especial, anúncio de raro
- `Meta/BossCollectionSystem.cs` — persistência do álbum (SaveData v5)
- `Services/MockAdsProvider.cs` / `Services/MockIapProvider.cs` — simulam ads/IAP em dev (guia de integração AppLovin/RevenueCat em comentário)

**Testes:**
- `tests/Domain.*/...` — ComboMathTests, FailReasonResolverTests, RareBossMathTests, WeaknessJudgeTests, BossCollectionMathTests (+93 testes)
- `Tests/EditMode/MissionContentTests.cs` — 14 testes (SOs, registry, regras de tutorial das fases 1–5)
- `Tests/PlayMode/MissionPlayModeTests.cs` — 4 testes (F3 ensina fraqueza, morte do boss antes da vitória, derrota com fail reason, inimigos da F4 morrem)

## 3. Arquivos alterados (principais)

| Camada | Arquivos |
|---|---|
| Domain | Enums.cs (4 enums novos, append-only), SaveData.cs, SaveMigration.cs (v4→v5) |
| Core | GameEvents.cs (+9 eventos), EventStructs.cs (+7 structs, LevelResult +3 campos), JuiceEvents.cs (+3 eventos), GameManager.cs (combos/failReason no resultado), RcKeys.cs, SO/LevelConfigSO.cs (EnemySlot[]), SO/GameSettingsSO.cs (catálogo de inimigos p/ endless), SO/EndlessLevelGenerator.cs (inimigos procedurais) |
| Gameplay | BossManager.cs (behaviors, morte cinematográfica 1,2s, raro, HP no bus), BossRuntime.cs (multiplicadores de behavior), CrowdManager.cs (veredito elemental, contadores, knockback), CombatSystem.cs (evento FRAQUEZA/RESISTIU rate-limited), LevelManager.cs (spawn de inimigos), GateManager.cs (WasBestChoice + feedback), RiskResolver.cs (veredito publicado), JuiceController.cs, VFXManager.cs (PlayCoinBurst, telegraph colorido, slow-mo endurecido), CameraRig.cs (PunchFocus) |
| UI | FeedbackTextController.cs, TutorialController.cs (diretor contextual), ResultScreen.cs (combos/teaser/raro), GameUIController.cs (fail reasons), HudController.cs, MetaBridge.cs |
| Meta | EconomySystem.cs (moedas de combo/inimigo/raro), MissionSystem.cs (2 missões novas) |
| Services | AnalyticsManager.cs (+12 eventos fiados), AudioManager.cs/AudioCatalogSO.cs (+11 slots), NullRemoteConfigProvider.cs, AdsManager.cs, IAPManager.cs (IIapProvider) |
| Editor | MvpContentFactory.cs (fases 1–5, 40 inimigos, Gate_Add15, waves do Titã, Reward_Level5), ProjectSetup.cs (managers novos + BossHud + wiring da ResultScreen), JuiceFactory.cs (VFX_CoinBurst + banner do tutorial), SoValidator.cs (inimigos + regras de tutorial), AudioFactory.cs, BuildTools.cs (Android AAB/APK) |
| Projeto | ProjectSettings.asset (identidade Android), asmdef do EditMode (ref Gameplay), GameLoopRig.cs |

## 4. Sistemas novos

1. **BossBehavior modular** — comportamento por boss via registry (prefab autorado tem prioridade), com ciclo de vida pooling-safe (Begin/End), multiplicadores por luta no BossRuntime (vulnerabilidade, dano de contato, cooldown) e zero mutação de SO.
2. **Inimigos de pista** — grupos agregados (zero Update por inimigo), 4 papéis (horda fraca com atropelo garantido, tanque que bloqueia lane, atirador a 14m, curador que vira alvo prioritário), pooling + streaming por distância, RNG derivado da seed (determinismo preservado).
3. **ComboSystem** — Perfect Gate, Weakness Hit, Boss Breaker (≤8s), Clutch (≤10% do pico), No Loss, Overkill (≥25% além do HP); bônus em moedas creditado na RunWallet (entra na base do DOBRAR).
4. **Derrota justa** — FailReasonResolver prioriza a causa real (elemento errado > laser > perdas na pista > exército pequeno > dano insuficiente) e a tela de derrota mostra dica curta.
5. **BossCollection** — álbum persistido (kills, melhor tempo, sobreviventes, fraqueza descoberta, kills raros) com eventos para UI futura.
6. **Variantes raras** — rolagem no Boss Scout (6%, determinística por seed), aviso "BOSS RARO!", HP ×1.5, recompensa de vitória ×3.
7. **TutorialDirector contextual** — 5 passos por bitmask persistida, dicas de 3–5 palavras, some quando o jogador age.
8. **HUD de boss** — barra de HP grande por evento, cor por fase, fraqueza visível, aviso de especial piscante.
9. **Ads/IAP/RC/Analytics preparados** — providers null (default) + mocks de dev; IIapProvider novo; 12 eventos de analytics fiados ao bus; chaves RC novas com defaults canônicos.
10. **DevPerfOverlay** — FPS/unidades/inimigos/timeScale + stress test 60/120/200 unidades (F3 ou 4 toques).

## 5. Bosses implementados

| Boss | Fraqueza | Mecânica única | Morte |
|---|---|---|---|
| **Gigante de Madeira** (M1, fase 3 e 10) | Fogo | Acumula fogo visível a cada acerto de fraqueza; 50% → soco no chão (especial mais frequente); 25% → desespero (+25% dano, tinte vermelho) | Tomba em câmera lenta (80°, ease-out) + chuva de moedas |
| **Zumbi Titã** (M2) | Fogo/Luz | Invoca hordas (waves em t=3/8/14s); 50% → perde o braço (grupo rastejante ataca); 25% → grito empurra o exército | Cambaleia, horda evapora, moedas em ondas |
| **Robô Escorpião** (M3) | Raio | Ciclo laser: telegraph → varredura → **núcleo exposto 3s (dano ×2)**; 50% → drones; 25% → laser ×1.5; marca UsedLaser p/ fail reason | Explode em peças + shake forte |
| Dragão de Lava (M5, preparado) | Gelo | Voa (dano recebido ×0.25); gelo durante o voo derruba e abre vulnerabilidade ×2 | Cai congelado em slow motion |
| Rei de Gelo (M6, preparado) | Fogo | Escudo de gelo (dano ×0.4); cada acerto de fogo derrete 20% | Escudo estilhaça |
| Entidade Dimensional (M10, preparado) | Rotativa | Portal negativo converte 10% do exército em moedas; 15% de chance de cair no próprio portal (perde 10% do HP) | Distorção + fenda |
| Demais 13 bosses | conforme CANON | GenericBossBehavior (pulso de fase, tinte em fraqueza) | Slow motion + moedas |

## 6. Inimigos implementados

40 `EnemyConfigSO` (4 por mundo × 10 mundos), nomes temáticos da missão (Espantalho Vivo, Cachorro Zumbi, Torreta, Cipó, Golem Magma, Yeti, Catapulta Viva, Ovo Alien, Aranha Soldadora, Clone do Jogador...), com elemento/corpo temáticos (M2 Undead, M3/M9 Machine, M5 Fire...) e rampa ×1.35 por mundo. Distribuição determinística nas fases 6+ (2–4 grupos, papéis liberados por profundidade), F4 com 2 hordas de tutorial, endless procedural incluído.

## 7. Fases alteradas (1–5 = tutorial jogável)

| Fase | Ensina | Conteúdo |
|---|---|---|
| F1 | Crescimento | +10 vs +25 (2 pares), pista limpa, boss a 20% — quase impossível perder |
| F2 | Multiplicador | +15 vs +10 → **+15 vs x2** (com 16 unidades, 32 vs 31: o x2 vence por pouco e ensina) → x2 vs +10 |
| F3 | **Fraqueza** | Boss trocado para o **Gigante de Madeira** real; portal FOGO vs x5 (a armadilha aparentemente boa); "FRAQUEZA!" + morte cinematográfica |
| F4 | Inimigos | 2 hordas fracas que o exército atropela em cadeia |
| F5 | Decisão/Supply | +50 vs VIRAR MAGO (quantidade × qualidade × supply), tanque na pista, baú comum de recompensa |

## 8. Melhorias de UI/UX
- HUD de boss (HP/fase/fraqueza/aviso de especial) — não existia nenhum.
- Tela de vitória: linhas de combo com stagger, badge de boss raro ×3, teaser do próximo boss com fraqueza ("PRÓXIMO: … — FRACO CONTRA FOGO").
- Tela de derrota: dica curta da causa real + CTA "TENTAR DE NOVO" pulsante.
- Tutorial: banner contextual de 3–5 palavras por passo, persistido por bit.
- Missões novas com descrição na UI.

## 9. Melhorias de game feel
- Flash **na cor do elemento** em acertos de fraqueza; flash cinza "bateu em parede" em resistência (sem shake).
- Morte de boss: slow motion 1,6s + zoom (PunchFocus) + shake 2.5 + 3 bursts de moedas (4 se raro) + haptic pesado.
- Quedas/colapsos por boss (tombo do Gigante em câmera lenta real).
- Atropelo de hordas com morte em cadeia escalonada (0,06s entre views) + bursts tintados.
- Portais: "BOA ESCOLHA!" dourado, portal ruim vermelho discreto, risco com "x10!"/"QUE PENA...".
- Telegraph de especial com cor por boss + aviso no HUD + SFX.
- SlowMotion endurecido (não conflita com pausa nem trava timeScale).

## 10. Melhorias de retenção/vício
- Loop completo: Scout → fraqueza → portais → inimigos → boss → combos → recompensa → próximo boss visível.
- 6 combos com bônus real de moedas (entram na base do DOBRAR — generosidade deliberada).
- Álbum de bosses persistido (fundação para tela de coleção e desafios).
- Boss raro = surpresa positiva (nunca punição: aviso prévio + recompensa ×3).
- Derrota sempre ensina o próximo passo.
- Primeiro baú na fase 5; primeiro upgrade alcançável na fase 1–2 (economia preservada do CANON).

## 11. Monetização / preparação comercial
- Rewarded: DOBRAR (inclui combos) e revive preservados; mocks de dev para testar o fluxo sem SDK.
- Interstitial: política canônica intacta (≥ fase 6, nunca após 2 derrotas) — Domain testado.
- IAP: IIapProvider + MockIapProvider; produtos existentes preservados; entitlements locais.
- Remote Config: chaves novas (rare_boss_chance, combo_bonus_mult, enemy_*_mult) com defaults.
- Analytics: boss_weakness_hit, boss_resisted_hit, combo_earned, enemy_killed, level_fail com fail_reason rico, rare_boss_announced, boss_fight_start, level_complete… fiados ao bus.

## 12–13. Testes adicionados e executados

| Suíte | Antes | Depois | Status |
|---|---|---|---|
| Domain (dotnet, 3 projetos) | 327 | **420** | ✅ 420/420 |
| EditMode | 18 | **36** | ✅ 36/36 |
| PlayMode | 4 | **9** | ✅ 9/9 (inclui regressão de abandono) |
| SoValidator | — | + inimigos + regras de tutorial F1/F3 | ✅ 0 erros |

> Após a revisão adversarial e os 5 fixes, **toda a suíte foi re-executada e confirmada verde** (Domain 420, EditMode 36, PlayMode 9) e o projeto recompila limpo (`error CS` = 0).

Pipeline completa re-executada do zero (Content → Setup → Greybox → UnitVisual → World → SystemScreens → RewardScreens → Juice → Audio → Validate): **10/10 OK**, zero LogError de wiring.

## 14. Erros encontrados e corrigidos

### Detectados por teste / integração
1. **Horda da F4 não morria no contato** (exército pequeno do tutorial não tinha DPS para matar o grupo no tempo de passagem) → implementado **atropelo garantido** para WeakHorde (morte no contato físico, como a missão pede). Detectado pelo teste PlayMode novo.
2. Contador de moedas do HUD dobraria visualmente com as fontes novas (`combo_*`, `enemy_kill`) → classificadas como delta de corrida.
3. Missões novas apareceriam com id cru na UI → descrições adicionadas no MetaBridge.
4. Encoding do script de pipeline (PowerShell 5.1 × UTF-8) → reescrito ASCII.

### Detectados por revisão adversarial (workflow find→verify; 10 brutos → 8 verificados → 5 confirmados, 3 falsos positivos refutados)
Todos no fluxo de **pausa / reiniciar / abandonar durante a luta** — não coberto pelos testes PlayMode originais. Corrigidos e travados por novo teste de regressão (`AbandonoMidLuta_LiberaBossEReseta`):
5. **Causa raiz — `StateExited(BossFight)` nunca disparava no abandono via pausa.** `RestartLevelFromAnyState`/`GoToMainMenuFromAnyState` zeravam a pilha sem `ChangeState`, então o BossManager nunca soltava a view → **boss-fantasma vivo na pista + câmera travada** na corrida seguinte (mesma cena). Fix: `GameManager.ExitStatesBeforeReset()` drena a pilha disparando `StateExited` de cada nível antes de recriá-la.
6. **Tween de queda do Gigante de Madeira sobrevivia ao abandono** (rodava no runner persistente, parado só por `ReleaseView`) → resolvido pela causa raiz (#5): o `ReleaseView` agora dispara e para o tween.
7. **Zona de risco pendente vazava entre fases** (uma zona armada perto do fim da pista resolvia na corrida SEGUINTE, mutando o exército novo). Fix: `RiskResolver.Cancel()` chamado no `LevelManager.DrainAll` (todo BeginRun/ResetRun).
8. **Sequência de morte do boss transicionava para Victory durante a pausa** (tick em tempo unscaled avançava com `timeScale=0`, arrancando o menu de pausa). Fix: `TickDeathSequence` congela quando `Time.timeScale <= 0` (pausa real; o slow-mo 0.3 não congela).
9. **FeedbackTextController vazava estado entre fases no soft reset** (só resetava em `OnLevelFinished`, que o restart da pausa não dispara → feedbacks 1×/PERFECT suprimidos na fase reiniciada). Fix: assina `GameManager.LevelStarted` e reseta ali (cobre todos os caminhos de início de fase).

**Falsos positivos refutados** (não corrigidos, com justificativa verificada no código): acúmulo de componentes BossBehavior na cápsula fallback (inertes por design, `FightActive=false`); feedback 1× marcado como "fired" sem exibir (cenário disable→re-enable não existe nesta arquitetura); shake da câmera anulado pelo CameraRig (a Slerp parte da rotação já-com-shake, o shake domina o frame).

## 15. Pendências reais (honestas)
- **Keystore de loja**: não criada (regra: nunca inventar credencial). AAB sai com debug signing + warning. Para publicar: Player Settings → Publishing Settings → criar keystore própria.
- **Módulo Android no editor**: build AAB requer "Android Build Support" instalado no Unity 6000.4.8f1 (não validado nesta máquina; método falha com erro claro se ausente).
- **Ícones Android + splash + privacy policy + data safety**: pendências de loja documentadas, fora do escopo de código.
- **SDKs reais** (AppLovin MAX, RevenueCat, Firebase): não instalados (por design) — guia de integração em comentário nos mocks; providers null continuam default.
- **Ponte Remote Config → Gameplay** para as chaves novas (rare_boss_chance etc.): Gameplay não enxerga Services; os valores hoje vêm de campos serializados com os MESMOS defaults. Ligar exige um hook no GameManager (padrão RunStartBonusProvider) — ~30 min de trabalho documentado.
- **Visuais finais dos bosses M5/M6/M10**: lógica completa, visual via cápsula/greybox + tints; prefabs reais casam automaticamente pelo bossId no registry.
- **Tela do álbum de bosses (UI)**: sistema + persistência prontos (Records/Find/OnCollectionChanged); a tela visual não foi construída (fundação completa para 1 sessão de trabalho de UI).
- **arenaWaves do Zumbi Titã** usam Soldado como placeholder de zumbi (trocar quando houver unidade temática).

## 16. Como testar no Unity
1. Abrir `MutantArmyRun` no Unity 6000.4.8f1 (cenas e assets JÁ regenerados — não precisa rodar nada).
2. Play na cena `Boot` → JOGAR. Fases 1–5 são o novo tutorial; a F3 tem o Gigante de Madeira (escolha o portal de FOGO e veja FRAQUEZA!).
3. F3 (tecla) em Play = overlay de performance + stress test.
4. Testes: Test Runner (EditMode/PlayMode) ou:
   - `dotnet test tests/Domain.Flow.Tests` (e Gameplay/Persistence)
   - Pipeline completa: `powershell -File _audit/run-pipeline.ps1`
5. Para REGENERAR conteúdo após mudar a factory: menu **MAR Tools → Create MVP Content** e depois as demais factories na ordem (ou `_audit/run-pipeline.ps1`).

## 17. Como gerar build Android
- Menu **MAR Tools → Build Android (AAB)** (ou `-batchmode -executeMethod MutantArmy.Editor.BuildTools.BuildAndroidAab`).
- Requisitos: módulo Android Build Support; saída `Build/Android/MutantArmyRun.aab`.
- APK de teste: **MAR Tools → Build Android (APK Dev)** (Development Build, debugging).
- Identidade já configurada: `com.felipemenezes.mutantarmyrun`, IL2CPP, ARM64, retrato fixo, v0.1.0 (code 1).

## 18. O que ainda falta para a loja
1. Keystore própria + assinatura release.
2. Ícones (adaptive icon) e splash.
3. Política de privacidade hospedada + formulário Data Safety.
4. SDKs reais de ads/analytics (AppLovin/Firebase) plugados nos providers.
5. Teste em device físico (FPS alvo: o overlay dev mede; stress 200 unidades incluído).
6. ASO básico (nome, descrição, screenshots — o DevScreenshotRig já captura cenas de gameplay).

## 19. Riscos técnicos
- Boss raro muda o HP por seed determinística — testes que assumem HP exato em fases específicas devem usar seeds verificadas (os atuais foram validados).
- A morte cinematográfica adiciona ~1,2s reais antes da Victory — qualquer automação futura deve esperar por estado, não por tempo.
- `Setup Project` recria as cenas do zero — sempre re-rodar as factories de augmentação na ordem (use `_audit/run-pipeline.ps1`).
- O `_autoBalancePool` ganha automaticamente qualquer gate novo da pasta Gates — curadoria consciente ao criar gates.
- EditMode tests agora referenciam o assembly Gameplay (asmdef) — mover/renomear assemblies exige atualizar a referência.

## 20. Próximos passos recomendados (ordem de impacto)
1. **Tela do álbum de bosses** (sistema pronto; só UI) + desafios por boss (ex.: "derrote o Escorpião sem tomar laser" — o flag UsedLaserThisFight já existe).
2. **Unidade zumbi temática** para as waves do Titã + prefabs reais dos bosses M5/M6/M10 (registry já casa por id).
3. **Ponte RC→Gameplay** (hook no GameManager) para tunar raro/combo/inimigos por Remote Config.
4. Upgrades mais perceptíveis visualmente (burn de fogo nas tropas, slow de gelo no boss já existem como mecânica — falta VFX dedicado por trilha).
5. Device test Android + tuning de performance com o DevPerfOverlay.
6. Gravar os 10 momentos virais (lista da missão §24) com o DevScreenshotRig para criativos de UA.

---

### Decisões de design documentadas (assumidas com autonomia, conforme a missão)
- **Limiares de fase de boss mantidos em 50%/25%** (canônicos, wired em HUD/Domain/testes) — o "66%/33%" da missão tratado como aproximação; as 3 fases existem e disparam os hooks.
- **F3 com ~192 HP efetivos** (0.12 explícito da missão; o "≈480" do brief era aritmética inconsistente) — luta curta e cinematográfica de tutorial; trocar para 0.30 se quiser ~12s.
- **Combos entram na base do DOBRAR** — generosidade deliberada a favor do jogador.
- **Atropelo garantido de hordas fracas** — prazer visual > simulação de DPS no tutorial.
- **Registry de behaviors por bossId** (em vez de campo no SO) — preserva a fronteira Core/Gameplay sem tocar o BossConfigSO.
- **Nenhum commit foi feito** — o trabalho está na working tree para sua revisão (`git status`); recomendo commitar por área (domain/gameplay/ui/meta/services/editor/content) ou em um commit único `feat(nota10)`.
