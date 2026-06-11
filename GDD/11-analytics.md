# Analytics & Experimentação — Mutant Army Run

> Documento 11 do pacote de design. Fontes da verdade: `CANON.md` (decisões fixas) e `BRIEF.md` (requisitos). Este doc define a taxonomia completa de eventos, os funis, as métricas com metas do CANON §12, o programa de experimentação A/B via Remote Config e os dashboards. Implementação concentrada no `AnalyticsManager` (CANON §13); chaves de experimento no `RemoteConfigManager`.

---

## 1. Objetivos e princípios de instrumentação

1. **Cada pilar de design tem um sensor.** O Boss Scout, o Supply e as Mutações são os diferenciais do jogo (CANON §3) — se não medirmos seu uso, não saberemos se o "inteligente por dentro" está funcionando. Por isso existem eventos dedicados a eles (§5).
2. **Evento só entra se alimenta uma decisão.** Todo evento desta taxonomia responde a pelo menos uma pergunta de produto ("o jogador entende o portal de risco?", "a fase 7 está dura demais?"). Nada de telemetria "por via das dúvidas".
3. **Tudo que é tunável é mensurável.** Cada chave de Remote Config (dificuldade, moedas, frequência de ads — BRIEF §Tecnologia) tem uma métrica de sucesso e um guardrail definidos aqui antes de qualquer experimento rodar.
4. **Custo de rede mínimo.** Eventos de gameplay de alta frequência (`gate_selected`) são enviados em batch pelo SDK do Firebase; nunca bloqueiam o frame. Sem PII em nenhum parâmetro (LGPD/GDPR, §10).

## 2. Stack e arquitetura de dados

| Camada | Ferramenta | Papel |
|---|---|---|
| Coleta | Firebase Analytics (SDK Unity) | Eventos + user properties; DebugView para QA |
| Identidade | Firebase Auth anônimo | `user_id` estável entre reinstalações com sync Firestore |
| Armazenamento | BigQuery (export diário + streaming) | Tabelas `events_*`; base de todos os funis e dashboards |
| Experimentação | Firebase Remote Config + A/B Testing | Variantes, alocação, significância |
| Receita ads | AppLovin MAX (impression-level revenue) | `ad_impression` com `value`/`currency` por impressão, encaminhado ao Firebase |
| Receita IAP | RevenueCat (webhooks → BigQuery) | Receita validada, refunds, assinatura do passe |
| Atribuição | MMP (AppsFlyer ou Adjust, decidir no doc 19 — Roadmap) | CPI, IPM, ROAS por criativo; postbacks SKAN no iOS |
| Crash | Crashlytics | Crash-free users cruzado com retenção |

**Fluxo:** `AnalyticsManager.LogEvent(name, params)` → fila local (flush a cada 30 s ou em `OnApplicationPause`) → Firebase → BigQuery. Dashboards em Looker Studio sobre views materializadas do BigQuery (§9).

### User properties (definidas uma vez, atualizadas quando mudam)

| Property | Tipo | Valores / exemplo |
|---|---|---|
| `player_level` | int | nível de XP do jogador (CANON §8) |
| `highest_level` | int | maior fase concluída (1–20 no MVP) |
| `world_current` | int | 1–3 no MVP |
| `payer_type` | string | `none` / `minnow` / `dolphin` / `whale` (por receita IAP acumulada: 0 / <10 / 10–50 / >50 US$) |
| `ads_removed` | int (0/1) | comprou Remover Anúncios |
| `season_pass_active` | int (0/1) | passe vigente |
| `supply_cap` | int | 60 fixo no MVP (CANON §15) |
| `ab_groups` | string | concat dos experimentos ativos, ex. `exp03_B;exp07_A` |
| `install_campaign` | string | campanha de origem (via MMP) |

## 3. Convenções

- **Nomes:** `snake_case`, ≤ 40 caracteres, verbo no passado ou substantivo de estado (`level_complete`, não `completeLevel`). Parâmetros ≤ 24 por evento (limite Firebase: 25).
- **Tipos:** `int`, `float`, `string`, `bool` (booleans logados como int 0/1 — limitação do SDK).
- **Parâmetros globais** anexados automaticamente pelo `AnalyticsManager` a TODO evento: `session_id` (string), `level` (int, fase atual ou última), `world` (int), `app_version` (string), `seconds_in_session` (int). Não são repetidos nas tabelas abaixo.
- **Enums em string** sempre em inglês e minúsculas, espelhando os ScriptableObjects: `gate_type` ∈ {`add`, `multiply`, `divide`, `subtract`, `class`, `element`, `mutation`, `risk`}; `element` ∈ {`fire`, `ice`, `lightning`, `poison`, `light`, `shadow`, `metal`, `alien`, `none`}; `unit_id` ∈ {`soldier`, `archer`, `shieldbearer`, `runner`, `mage`, `ninja`, `flamethrower`, `glacial`, `medic`, `robot`, `giant`, `necromancer`, `engineer`, `alien_unit`, `dragon`, `titan`, `war_angel`, `mutant_demon`, `supreme_mecha`} (glossário CANON §14 e roster §5).

## 4. Eventos obrigatórios (BRIEF §Analytics — cobertura 100%)

### 4.1 FTUE e fases

| Evento | Gatilho exato | Parâmetros (tipo) |
|---|---|---|
| `tutorial_start` | Primeiro frame jogável da fase 1 na primeira sessão (overlay de tutorial ativo). Dispara 1× por usuário | `time_to_start_sec` (float — tempo desde `first_open`; meta < 5 s, BRIEF §Regras de produto) |
| `tutorial_complete` | Morte do primeiro boss na fase 1 (vitória garantida — CANON §16). 1× por usuário | `duration_sec` (float), `gates_taken` (int) |
| `level_start` | `LevelManager` instancia a fase e o input é liberado | `attempt` (int — tentativa nesta fase, começa em 1), `army_size_start` (int), `supply_cap` (int), `upgrade_power_score` (int — soma dos níveis das 4 trilhas), `source` (string: `next_button` / `retry` / `map`) |
| `level_complete` | Barra de HP do boss chega a 0 e o slow motion final dispara | `attempt` (int), `duration_sec` (float), `run_duration_sec` (float — só a corrida), `boss_duration_sec` (float — alvo 10–20 s, CANON §1), `army_size_at_boss` (int), `units_survived` (int), `coins_earned` (int), `gates_taken` (int), `gates_missed` (int), `mutations_active` (string — csv, ex. `wings,laser`), `supply_overflow_total` (int) |
| `level_fail` | Exército zerado (na corrida ou no boss) e tela de derrota exibida — após recusa/indisponibilidade de revive | `attempt` (int), `fail_reason` (string: `boss` / `obstacle` / `bad_gate` / `trap_zone`), `fail_progress_pct` (float 0–1 — posição na pista; 1.0 = arena), `army_size_max` (int — pico na corrida), `boss_hp_pct_remaining` (float 0–1; −1 se não chegou ao boss) |

### 4.2 Boss

| Evento | Gatilho exato | Parâmetros (tipo) |
|---|---|---|
| `boss_start` | Fim da animação de entrada do boss (≤ 2 s, CANON §6); combate liberado | `boss_id` (string, ex. `stone_golem`), `boss_element` (string), `boss_weakness` (string), `army_size` (int), `supply_used` (int), `army_main_element` (string — elemento dominante do exército), `has_weakness_element` (bool — exército carrega o elemento da fraqueza), `mutations_active` (string) |
| `boss_defeated` | HP do boss = 0 | `boss_id` (string), `fight_duration_sec` (float), `units_lost` (int), `used_revive` (bool), `weakness_exploited` (bool), `overkill_pct` (float — DPS excedente; sensor de fase fácil demais) |
| `boss_failed` | Exército zerado dentro da arena | `boss_id` (string), `fight_duration_sec` (float), `boss_hp_pct_remaining` (float 0–1), `used_revive` (bool), `weakness_exploited` (bool) |

### 4.3 Portais (coração do jogo)

| Evento | Gatilho exato | Parâmetros (tipo) |
|---|---|---|
| `gate_selected` | Centro de massa do exército cruza o plano de um portal do par | `gate_pair_index` (int — ordem do par na fase, 1..n), `gate_type` (string), `gate_value` (string — ex. `x2`, `+25`, `fire`, `archer`, `risk_x10`), `gate_side` (string: `left`/`right`), `alternative_type` (string), `alternative_value` (string), `army_size_before` (int), `army_size_after` (int), `supply_before` (int), `supply_after` (int), `was_optimal` (bool — coincide com a rota "ótima" gerada pelo `GateManager`, CANON §3.1), `risk_outcome` (string: `win`/`lose`/`na`) |
| `gate_missed` | Exército passa pela linha do par sem ativar nenhum portal (desviou de ambos) | `gate_pair_index` (int), `left_type` (string), `left_value` (string), `right_type` (string), `right_value` (string), `army_size` (int) |

> **Por que `was_optimal`:** é a métrica direta do pilar "escolha inteligente, não maior número" (CANON §2.2). Se ≥ 80% dos jogadores pegam a rota ótima já na 1ª tentativa, os portais estão óbvios demais; se < 40%, estão ilegíveis. Faixa saudável-alvo: 55–70%.

### 4.4 Coleção e economia

| Evento | Gatilho exato | Parâmetros (tipo) |
|---|---|---|
| `unit_unlocked` | Tropa nova adicionada à coleção (1× por tropa) | `unit_id` (string), `rarity` (string: `common`/`rare`/`epic`/`legendary`), `source` (string: `level_reward`/`chest`/`shop`/`season_pass`/`event`) |
| `unit_upgraded` | Confirmação do botão "Evoluir" na tela Tropas | `unit_id` (string), `rarity` (string), `from_level` (int), `to_level` (int), `shards_spent` (int — custo 10 × 2^(n−1), CANON §8), `coins_spent` (int), `shards_remaining` (int) |
| `chest_opened` | Animação de abertura do baú concluída | `chest_type` (string: `free_daily`/`level`/`epic`/`rare_shop`/`pass`), `source` (string: `level_reward`/`shop`/`rewarded_ad`/`season_pass`), `gems_spent` (int — 300 p/ baú raro, CANON §8), `coins_granted` (int), `gems_granted` (int), `shards_granted` (int), `best_rarity` (string) |

### 4.5 Ads

| Evento | Gatilho exato | Parâmetros (tipo) |
|---|---|---|
| `rewarded_ad_shown` | Callback `OnAdDisplayed` do MAX após toque no botão de rewarded | `placement` (string: `double_reward`/`revive_boss`/`extra_chest`/`legendary_trial`/`upgrade_boost` — os 5 do CANON §11), `ad_network` (string), `ecpm_usd` (float) |
| `rewarded_ad_completed` | Callback `OnRewardEarned` (vídeo assistido até o fim) | `placement` (string), `reward_granted` (string — ex. `coins_x2:240`), `ad_network` (string), `ecpm_usd` (float) |
| `interstitial_shown` | Callback `OnAdDisplayed` do interstitial (regras CANON §11: só a partir da fase 6, máx. 1 a cada 3 fases, nunca após 2 derrotas seguidas) | `levels_since_last` (int), `session_interstitial_count` (int), `ad_network` (string), `ecpm_usd` (float) |

### 4.6 IAP e passe

| Evento | Gatilho exato | Parâmetros (tipo) |
|---|---|---|
| `purchase_started` | Toque em "Comprar" antes de abrir o billing nativo | `product_id` (string: `remove_ads_499`/`starter_offer_299`/`season_pass_699`/`gems_pack_s|m|l`/`coins_pack_s|m|l`/`skin_*`), `price_usd` (float), `source_screen` (string: `shop`/`offer_popup`/`defeat_screen`/`pass_screen`) |
| `purchase_completed` | Validação do recibo pelo RevenueCat | `product_id` (string), `price_usd` (float), `currency_local` (string), `is_first_purchase` (bool), `hours_since_install` (float) |
| `season_pass_opened` | Tela do passe aberta (desbloqueia no nível 5 de jogador, CANON §8) | `season_id` (string, ex. `s01`), `pass_tier_current` (int), `is_purchased` (bool) |
| `season_pass_purchased` | Recibo do passe validado | `season_id` (string), `price_usd` (float), `days_into_season` (int), `pass_tier_current` (int) |

### 4.7 Retenção (eventos explícitos)

| Evento | Gatilho exato | Parâmetros (tipo) |
|---|---|---|
| `day_1_retention` | Primeiro `app_open` no dia-calendário D+1 após `first_open` (1× por usuário) | `levels_completed_d0` (int), `payer_type` (string) |
| `day_3_retention` | Primeiro `app_open` no dia-calendário D+3 | `highest_level` (int) |
| `day_7_retention` | Primeiro `app_open` no dia-calendário D+7 | `highest_level` (int), `total_rewarded_completed` (int) |

> **Justificativa:** o Firebase calcula coortes automaticamente, mas os eventos explícitos permitem (a) usar retenção como **condição de audiência no Remote Config** (ex.: oferta para quem voltou no D3 sem comprar) e (b) funis que cruzam retenção com comportamento (ex.: D7 segmentado por uso de Boss Scout). O `SaveSystem` guarda `first_open_date` local para o cálculo do dia-calendário no fuso do dispositivo.

## 5. Eventos extras (recomendação do nosso design)

| Evento | Gatilho exato | Parâmetros (tipo) | Justificativa de design |
|---|---|---|---|
| `boss_scout_viewed` | Cartão de Boss Scout exibido antes da fase (auto) ou lembrete reaberto na corrida (toque no ícone — CANON §3.1) | `boss_id` (string), `boss_weakness` (string), `view_type` (string: `pre_level`/`reminder`), `reminder_count_level` (int) | Mede o uso do **diferencial central do jogo**. Cruzar `reminder_count_level` com `weakness_exploited` e taxa de vitória valida (ou refuta) a tese de que o Scout transforma reflexo em plano. Se ninguém reabre o lembrete, o ícone precisa de redesign — antes de cortarmos a feature errada. |
| `supply_overflow` | Conversão automática de excedente em moedas ao estourar o Supply (CANON §3.2) | `units_converted` (int), `coins_granted` (int), `supply_cap` (int), `trigger_gate_type` (string) | Sensor do anti-"maior número". Overflow em > 60% das fases = portais de quantidade desbalanceados (gerador de fases precisa de ajuste); overflow ≈ 0% = o sistema é invisível e não cria a tensão desejada. Também valida que a fanfarra é lida como prêmio: comparar churn de sessão após overflow vs sem. |
| `mutation_applied` | Mutação entra num slot vazio (1º–3º slot, CANON §3.3) | `mutation_id` (string, ex. `wings`/`laser`/`armor`/`size`), `slot_index` (int 1–3), `mutations_active` (string) | Ranking de popularidade e poder real das mutações (cruzar com `level_complete`). Alimenta o doc 04 (portais) na escolha de quais mutações aparecem em quais mundos e os criativos de UA (mutações mais "filmáveis"). |
| `mutation_replaced` | Jogador pega a 4ª mutação e a mais antiga é substituída | `new_mutation_id` (string), `replaced_mutation_id` (string), `was_voluntary` (bool — passou pelo portal podendo desviar) | A substituição é o "momento de vídeo" estratégico do CANON §3.3. Se `was_voluntary` for quase sempre falso, os jogadores não entenderam a regra dos 3 slots → reforçar UI. Pares novo/substituído revelam a hierarquia percebida entre mutações. |
| `revive_offered` | Tela de derrota no boss exibe o botão "Reviver com anúncio" (1×/fase, CANON §11) | `boss_id` (string), `boss_hp_pct_remaining` (float), `attempt` (int) | Denominador do funil de revive (§6.2). Sem ele, só veríamos aceitação, nunca a taxa de oferta → impossível otimizar o placement mais valioso do jogo. |
| `revive_accepted` | Toque em "Reviver" e callback de reward concedido | `boss_id` (string), `boss_hp_pct_remaining` (float), `won_after_revive` (bool — preenchido no fim do combate) | A taxa `revive_accepted / revive_offered` deve subir quanto menor o HP restante do boss. `won_after_revive` calibra o buff de retorno: revive que ainda perde é o pior outcome de UX do jogo (gastou 30 s de vídeo e perdeu de novo). Alvo: ≥ 85% de vitória pós-revive. |
| `near_win` | `level_fail` com `boss_hp_pct_remaining` < 0.10 | `boss_id` (string), `boss_hp_pct_remaining` (float), `revive_was_offered` (bool), `revive_was_accepted` (bool) | A "quase-vitória" é o momento de maior intenção de retry e de revive — e o formato de anúncio nº 6 do BRIEF. Medir sua frequência por fase permite **desenhar** a quase-derrota (tuning de HP do boss) em vez de torcer por ela. Meta: 10–15% das derrotas em boss são `near_win`; acima de 25% indica HP frustrante por design. |
| `tutorial_step` | Conclusão (ou pulo) de cada passo do overlay de FTUE da fase 1 e dos cards de ensino posteriores (ex.: primeiro Boss Scout "com peso" da fase 3 — doc 06 §6) | `step_id` (string, ex. `tap_to_move`/`first_gate`/`first_boss`/`boss_scout_card_l3`), `step_index` (int), `duration_sec` (float), `was_skipped` (bool) | Granularidade que `tutorial_start`/`tutorial_complete` não dão: localiza o passo exato em que o FTUE perde gente. É o evento do critério de aceite da fase 3 no doc 06 §6 (≥ 90% assistem o card do Boss Scout sem pular → `was_skipped = 0`). |
| `screen_view` | Tela principal de UI ativada pelo `UIManager` (1× por abertura da tela, nunca por frame) | `screen_name` (string: `home`/`shop`/`units`/`upgrades`/`pass`/`settings`), `source` (string — origem da navegação, ex. `home_button`/`victory_screen`/`level_up_popup`) | Denominador do passo 2 do funil de IAP (§6.3): sem saber quem **abriu** a Loja, a conversão de compra fica cega para problemas de descoberta (badge fraco, tela enterrada). Também revela telas mortas na navegação geral. |
| `offer_popup` | Popup de oferta exibido automaticamente ao jogador (Oferta Inicial nas primeiras 48 h — CANON §11; ou gatilho alternativo do Exp. 5) | `offer_id` (string: `starter_offer_299`), `trigger` (string: `first_48h`/`first_defeat`), `hours_since_install` (float) | Segundo denominador do passo 2 do funil §6.3 e denominador da métrica primária do Exp. 5 (conversão da oferta = compras ÷ exibições). Sem ele, o experimento de timing da Oferta Inicial não tem como ser lido. |

Evento auxiliar de funil: `rewarded_offer_shown` — botão de rewarded renderizado na tela (mesmos `placement` de §4.5). É o denominador real da conversão de rewarded; sem ele o funil começa no clique e fica cego para problemas de visibilidade do botão.

## 6. Funis

Todos os funis são *closed funnels* no BigQuery (janela de 7 dias desde `first_open`, salvo indicação). Taxas esperadas calibradas para as metas do CANON §12; valores entre parênteses = limiar de alerta (aciona investigação no daily, §9.1).

### 6.1 Funil FTUE (por coorte de instalação, janela D0–D1)

| # | Passo | Evento | % da etapa anterior | % do install | Alerta se < |
|---|---|---|---|---|---|
| 1 | Install | `first_open` | — | 100% | — |
| 2 | Começou o tutorial | `tutorial_start` | 97% | 97% | 94% (problema de loading/crash na abertura) |
| 3 | Completou o tutorial | `tutorial_complete` | 96% | 93% | 90% (fase 1 deve ser impossível de perder — CANON §16) |
| 4 | Venceu a fase 3 (1º boss "uau") | `level_complete{level:3}` | 86% | 80% | 72% (win rate alvo 95% nas fases 1–3) |
| 5 | Venceu a fase 7 (boss de mundo M1) | `level_complete{level:7}` | 69% | 55% | 45% (fase 7 é o 1º teste real; win rate alvo 75% por tentativa — doc 05 §4.5, fonte única de tuning —; com retries a passagem acumulada deve ficar ≥ 55%, e o restante da queda é churn natural D0–D1) |

**Leitura:** a queda 4→5 concentra o churn de dificuldade; é onde rodam os experimentos 2 e 4 (§8).

### 6.2 Funil de rewarded (por DAU, diário)

| # | Passo | Evento | % da etapa anterior | % do DAU |
|---|---|---|---|---|
| 1 | Viu uma oferta de rewarded | `rewarded_offer_shown` | — | 92% |
| 2 | Tocou e o vídeo abriu | `rewarded_ad_shown` | 46% | 42% |
| 3 | Assistiu até o fim | `rewarded_ad_completed` | 93% | 39% |
| 4 | DAU com ≥ 1 rewarded completo no dia | (distinct users) | — | **≥ 35% (meta CANON §12)** |

Quebras esperadas por placement (share de `rewarded_ad_completed`): `double_reward` 55% · `revive_boss` 25% · `extra_chest` 12% · `legendary_trial` 5% · `upgrade_boost` 3%. Passo 2→3 abaixo de 88% = problema de fill/crash de rede de ads (abrir ticket com a mediação, não com design).

### 6.3 Funil de IAP (janela D0–D7 da coorte)

| # | Passo | Evento | % da etapa anterior | % do install |
|---|---|---|---|---|
| 1 | Install | `first_open` | — | 100% |
| 2 | Abriu a Loja (nv4) ou viu a Oferta Inicial (48 h) | `screen_view{screen_name: shop}` ∪ `offer_popup` (definidos em §5) | 55% | 55% |
| 3 | Iniciou compra | `purchase_started` | 3,3% | 1,8% |
| 4 | Completou compra | `purchase_completed` | 83% | **1,5%** |

Conversão pagante D7 alvo: 1,5% (faixa saudável hybrid-casual: 1–2,5%). Passo 3→4 abaixo de 75% = atrito de billing (preço local, falha de cartão) — verificar por `currency_local` antes de mexer em design. Primeiro produto esperado: `starter_offer_299` (≈ 45% das primeiras compras), depois `remove_ads_499` (≈ 35%).

### 6.4 Funil do Passe de Temporada (por usuários nível ≥ 5, janela da temporada)

| # | Passo | Evento | % da etapa anterior | % dos elegíveis |
|---|---|---|---|---|
| 1 | Desbloqueou o passe (nv5 de jogador) | `player_level ≥ 5` | — | 100% |
| 2 | Abriu a tela do passe | `season_pass_opened` | 60% | 60% |
| 3 | Reabriu ≥ 3× (considera a trilha de recompensas) | `season_pass_opened` ×3 | 45% | 27% |
| 4 | Iniciou compra do passe | `purchase_started{season_pass_699}` | 7% | 1,9% |
| 5 | Comprou o passe | `season_pass_purchased` | 80% | **1,5%** |

Se a etapa 2 ficar < 45%, o ponto de entrada do passe na tela inicial está fraco (badge/animação). Se 3→4 ficar < 5%, o problema é proposta de valor da trilha, não o preço — revisar recompensas com o doc de monetização antes de testar preço.

## 7. Métricas, definições exatas e plano de ação

| Métrica | Definição exata (BigQuery) | Meta (CANON §12) | Se vier abaixo → ação |
|---|---|---|---|
| **D1** | usuários ativos no dia-calendário D+1 ÷ novos usuários do dia D (fuso do projeto) | ≥ 40% | Auditar funil FTUE (§6.1): se a queda é nos passos 2–3 → bug/performance (Crashlytics); se nos passos 4–5 → rodar Exp. 2 e 4; verificar `time_to_start_sec` > 5 s |
| **D3** | ativos no dia D+3 ÷ novos do dia D | ≥ 22% | Olhar `highest_level` médio no D1: se < 7, o problema é pacing do M1; se ≥ 7, falta gancho de meta → adiantar desbloqueio de Baús (nv3) e revisar missões diárias |
| **D7** | ativos no dia D+7 ÷ novos do dia D | ≥ 12% | Cruzar com `total_rewarded_completed` e progresso M2: se churn concentra na transição M1→M2 (fase 8), suavizar curva de HP via RC; ativar push FCM D5 com baú grátis |
| **Sessão média** | soma de `engagement_time_msec` ÷ nº de sessões | ≥ 8 min | Se fases/sessão estiver OK mas o tempo baixo, fases estão curtas demais (< 60 s) → revisar comprimento de pista; se ambos baixos, ver interstitials (Exp. 1) |
| **Fases/sessão** | count(`level_start`) ÷ count(distinct `session_id`) | ≥ 6 | Medir tempo entre `level_complete` e próximo `level_start`: > 15 s = atrito de UI no pós-fase (tela de vitória longa, ads); testar auto-avanço "próxima fase em 3 s" |
| **Taxa de vitória por fase** | `level_complete` ÷ (`level_complete` + `level_fail`) por `level` e `attempt` | 95% (f. 1–3) · 85% (f. 4–10) · ~70% (meio de mundo) · ~55% (boss de mundo) | Heatmap diário por fase (§9.1); desvio > 8 p.p. → ajustar `difficulty_boss_hp_mult_<level>` via RC no mesmo dia (sem release) |
| **Conversão rewarded** | distinct users com `rewarded_ad_completed` ÷ DAU | ≥ 35% | Funil §6.2: se passo 1 < 85%, faltam ofertas visíveis por sessão; se 1→2 < 40%, valor percebido baixo → Exp. 3 (x2→x3) e Exp. 9 |
| **ARPDAU** | (receita ads por impressão MAX + receita IAP líquida RevenueCat) ÷ DAU, por dia | ≥ US$ 0,08 | Decompor: ads/DAU vs IAP/DAU. Ads baixo → Exp. 1 (frequência) e revisão de eCPM/redes na mediação; IAP baixo → funil §6.3 e Exp. 5 |
| **LTV (D90 projetado)** | Σ ARPU acumulado D0–D14 + extrapolação da curva de retenção (ajuste potência `r(t)=a·t^(−b)` sobre D1/D3/D7/D14) × ARPDAU médio | ≥ 3× CPI (≥ US$ 1,20 BR/LatAm; ≥ US$ 4,50 US) | LTV < 3× CPI → pausar escala de UA, atacar D7 e ARPDAU primeiro; recalcular semanalmente por país e por criativo |
| **CPI** | gasto de UA ÷ installs atribuídos (MMP), por país/campanha/criativo | ≤ US$ 0,40 BR/LatAm · ≤ US$ 1,50 US | Rotacionar criativos com IPM < 1% (dashboard §9.3); priorizar os 8 formatos do BRIEF; testar hooks de "quase-derrota" e "portal armadilha" |
| **Conversão de compra** | distinct users com `purchase_completed` ÷ installs da coorte (D7) | ≥ 1,5% | Funil §6.3; Exp. 5 (timing da oferta inicial); checar preço local vs poder de compra (tiers Google Play) |
| **Portal mais escolhido** | ranking de `gate_selected` por `gate_type`+`gate_value`, com taxa de escolha quando oferecido | distribuição saudável: nenhum tipo > 45% de pick rate quando oferecido contra alternativa | Tipo dominante → seu valor está alto (nerf via RC) ou a alternativa é ilegível (rever ícone/cor no doc de UI) |
| **Boss mais difícil** | ranking de `boss_failed` ÷ `boss_start` por `boss_id` | nenhum boss não-final com fail rate > 45% | Ajustar `difficulty_boss_hp_mult` ou garantir portais do elemento da fraqueza na geração da fase (CANON §3.1: sempre existe rota ótima) |
| **Crash-free users** | Crashlytics, diário | ≥ 99,5% | Bloqueio de release; crash em fase específica → desativar a fase via RC e hotfix |

## 8. Programa de experimentação A/B (Remote Config)

**Processo:** 1 hipótese por experimento · alocação 50/50 (ou 33/33/33) só para **novos usuários** (exceto Exp. 1 e 3, que aceitam usuários existentes) · mínimo 1.000 usuários/braço ou 14 dias, o que vier depois · significância 95% na métrica primária · **guardrail universal: D1 não pode cair mais de 2 p.p. em nenhum experimento** (auto-stop). Resultados arquivados em planilha de decisões com link no dashboard §9.1.

| # | Experimento | Hipótese | Variável (chave RC) | Braços | Métrica primária de sucesso | Risco e mitigação |
|---|---|---|---|---|---|---|
| 1 | Frequência de interstitial | Espaçar interstitials de 3 para 4 fases perde pouca receita e melhora retenção | `ads_interstitial_level_interval` | A=3 (controle, CANON §11), B=4 | ARPDAU total (ads+IAP) com D1/D3 como guardrail | Perda de receita de ads; mitigar medindo LTV D14, não só receita do dia |
| 2 | Dificuldade da fase 7 | HP do Gigante de Madeira −15% eleva a passagem da fase 7 sem matar a tensão (boss de mundo deve reter o "uau") | `difficulty_boss_hp_mult_l7` | A=1.0, B=0.85 | % da coorte que completa a fase 7 em D0–D1 (alvo ≥ 55% do install) + D1 | Boss fácil demais reduz `near_win` e intenção de revive → monitorar conversão de `revive_boss` como guardrail |
| 3 | Valor do x2 de rewarded | Oferecer x3 (em vez de x2 — CANON §8) na tela de vitória aumenta conversão de rewarded acima da inflação de moedas que causa | `rewarded_victory_multiplier` | A=2, B=3 | Conversão rewarded (% DAU) com guardrail no índice de inflação (§9.2): moedas ganhas/gastas ≤ 1,3 | Inflação encurta a vida da economia → se inflação estourar, compensar custo de upgrade via Exp. 7 antes de adotar |
| 4 | Duração do Boss Scout | Cartão de 3 s (vs 2 s) aumenta `weakness_exploited` e a vitória nas fases 4+ sem irritar | `boss_scout_duration_sec` | A=2.0 (CANON §3.1), B=3.0, C=2.0 + botão "pular" | `weakness_exploited` em `boss_defeated` (alvo +5 p.p.) | Atrito pré-fase reduz fases/sessão → guardrail fases/sessão ≥ 6 |
| 5 | Timing da Oferta Inicial | Disparar a oferta de US$ 2,99 após a 1ª derrota (momento de necessidade) converte mais que o gatilho fixo de 48 h | `starter_offer_trigger` | A=`first_48h` (CANON §11), B=`first_defeat` | Conversão da oferta (compras ÷ exibições) e conversão pagante D7 | Oferta na derrota pode parecer predatória → guardrail: rating do popup (1-tap survey) e D1 |
| 6 | Curva de moedas por fase | Crescimento 1,12 (vs 1,10 — CANON §8) acelera a sensação de progresso e o D3 sem quebrar os sinks | `economy_coin_growth_rate` | A=1.10, B=1.12 | D3 da coorte, com guardrail de inflação (§9.2) | Moeda sobrando esvazia o rewarded de x2 → monitorar conversão `double_reward` |
| 7 | Curva de custo de upgrade | Custo 100×1,30^n (vs 1,35^n — CANON §8) gera mais eventos `unit_upgraded` no D0–D3 e melhora D3 | `economy_upgrade_cost_growth` | A=1.35, B=1.30 | nº médio de upgrades por usuário até D3 (alvo +20%) e D3 | Progressão rápida demais achata a curva de poder vs dificuldade → revisar win rates por fase no heatmap |
| 8 | Conversão do Supply overflow | Pagar mais moedas por unidade excedente (1,5×) faz o overflow ser lido como prêmio e aumenta picks de portais de quantidade arriscados | `supply_overflow_coin_rate` | A=1.0, B=1.5 | % de fases com `supply_overflow` seguido de retry imediato (proxy de satisfação) + pick rate de portais `multiply` | Distorce o equilíbrio quantidade vs qualidade (pilar §2.2) → guardrail `was_optimal` na faixa 55–70% |
| 9 | Segundo revive pago | Oferecer um 2º revive por 30 gemas (após o revive grátis por ad, CANON §11 mantém 1×/fase via ad) cria sink de gemas sem aumentar frustração | `revive_second_gems_price` | A=desligado, B=30 gemas | Gasto de gemas/DAU e taxa de vitória pós-revive ≥ 85% | Percepção pay-to-win (CANON §11 anti-P2W) → só em `near_win` (HP < 10%) e nunca em boss de mundo na 1ª tentativa |
| 10 | Densidade da Zona de Perigo | Reduzir de 3 para 2 fileiras de armadilhas na "Zona de Perigo x10" — o portal de risco do MVP (CANON §10), resolvido por habilidade, sem odds (doc 04 §3.5: "SOBREVIVA → x10") — aumenta o pick rate do portal de risco e os momentos virais sem elevar `level_fail` | `gate_risk_zone_trap_rows` | A=3 (controle, doc 04 §3.5), B=2 | Pick rate do portal `risk` quando oferecido (alvo 35–50%) | Zona frouxa vira "no-brainer" e mata a tensão → guardrail: perda média de unidades dentro da zona ≥ 25% (baseline ~40% — doc 04 §3.5) e `risk_outcome=lose` (exército zerado na zona) não pode cair a ~0 |

> **Nota — odds de risco são pós-MVP:** o portal de risco do MVP não tem porcentagem: o CANON §10 o define como "x10 se sobreviver à zona de perigo", e o doc 04 §3.5 confirma a resolução por habilidade. O formato "70% x10 / 30% perde metade" é o **exemplo de exibição honesta do CANON §3.4** (regra de UI dos portais pareados), não uma especificação do portal do MVP. Quando os portais de risco probabilísticos entrarem (pós-MVP — doc 04 §3.5, ex.: "Mutação Lendária Instável", 65% mutação Lendária / 35% nada), rodar a variante deste experimento sobre odds (chave `gate_legendary_mutation_win_chance`, A=0.65, B=0.75), com guardrail equivalente: o desfecho "nada" deve continuar ≥ 25% das ativações para preservar a tensão.

**Prioridade de execução (semanas de soft launch):** S1–S2: Exp. 2 e 4 (retenção FTUE primeiro — sem D1 não há nada para monetizar) · S3–S4: Exp. 1 e 3 (receita) · S5–S6: Exp. 5 e 6 · S7+: 7, 8, 9, 10. Nunca rodar dois experimentos que tocam a mesma métrica primária simultaneamente na mesma população.

## 9. Dashboards (Looker Studio sobre BigQuery; refresh 6 h)

### 9.1 Visão diária de produto (audiência: todo o time, daily de 15 min)

- **Topo:** DAU, novos usuários, D1/D3/D7 por coorte (gráfico de coorte triangular), crash-free users, fases/sessão, sessão média — cada card com a meta do CANON §12 e farol verde/amarelo/vermelho.
- **Funil FTUE** (§6.1) da coorte de ontem vs média de 7 dias.
- **Heatmap de win rate por fase** (1–20) × tentativa, com as bandas-alvo sobrepostas — a ferramenta nº 1 de tuning diário; fase fora da banda por 2 dias seguidos gera tarefa automática.
- **Saúde dos diferenciais:** % de fases com `boss_scout_viewed{reminder}`, `weakness_exploited`, distribuição de `was_optimal`, taxa de `supply_overflow`, picks de mutação.
- **Experimentos ativos:** leitura ao vivo de braços, usuários alocados, métrica primária e guardrails.

### 9.2 Visão de economia (audiência: monetization designer + GD; semanal)

- **Sources vs sinks de moedas:** ganho por origem (`level_complete`, `supply_overflow`, `chest_opened`, rewarded x2) vs gasto (`unit_upgraded`, trilhas de upgrade) — **índice de inflação** = moedas ganhas ÷ gastas por usuário/dia (faixa saudável 1,0–1,3).
- **Fluxo de gemas:** entrada (boss de mundo 10, missões 20–40/dia — CANON §8) vs saída (baú raro 300, Exp. 9); saldo médio por `payer_type`.
- **Progressão de upgrades:** nível médio por trilha × `highest_level` — detecta jogador "rico e parado" (sinal de sink fraco) ou "pobre e travado" (curva de custo dura).
- **Monetização:** ARPDAU decomposto (ads por formato/placement, IAP por produto), eCPM por rede da mediação MAX, conversão rewarded por placement, receita do passe por temporada, % DAU `ads_removed`.
- **Fragmentos:** ritmo de drop por raridade vs custo 10 × 2^(n−1) — tempo projetado até nível 10 por tropa (validar contra o doc 03).

### 9.3 Visão de criativos / UA (audiência: marketing + PM; diária durante soft launch)

- Join BigQuery: dados do MMP (campanha, criativo, custo) × coortes do Firebase (retenção, receita) por `install_campaign`.
- **Por criativo:** IPM, CTR, CPI (metas: ≤ US$ 0,40 BR/LatAm, ≤ US$ 1,50 US), D1 da coorte do criativo, ROAS D7, LTV projetado ÷ CPI (escala quando ≥ 3×).
- **Qualidade do tráfego:** % da coorte que completa o tutorial e a fase 7 por criativo — criativo com CPI baixo mas FTUE ruim (clickbait) é pausado mesmo barato.
- **Validação de formato:** mapear cada criativo a um dos 8 formatos do BRIEF (escolha, erro, evolução, desafio, satisfação, quase-derrota, comparação, curiosidade) e comparar IPM médio por formato — alimenta o backlog de vídeos do doc de ads/viralização.

## 10. QA de tracking e privacidade

- **Validação:** todo evento novo passa por DebugView do Firebase em build interna + teste automatizado no `AnalyticsManager` (mock que valida nome, tipos e limites de parâmetros). Checklist de release inclui replay da fase 1 conferindo a sequência `tutorial_start → gate_selected → boss_start → boss_defeated → tutorial_complete → level_complete`.
- **Versionamento da taxonomia:** este documento é a fonte; mudanças via PR com revisão do PM. Eventos nunca mudam de significado — se o gatilho muda, cria-se evento novo com sufixo `_v2` e o antigo é descontinuado após 2 versões.
- **Privacidade:** sem PII em eventos (nada de e-mail, nome, contatos). Público 13+ (CANON §1): tag de loja adequada, sem mixed audience COPPA, consentimento via Google UMP (GDPR/LGPD) antes de inicializar ads personalizados; iOS com ATT no lançamento iOS (pós-Android). `user_id` = Firebase Auth anônimo; pedido de exclusão de dados atendido via `DeleteUserData` do Firebase + RevenueCat.
- **Orçamento de eventos:** estimativa de ~120 eventos/usuário/dia (dominado por `gate_selected`, ~8 por fase × 6+ fases/sessão) — dentro dos limites do Firebase (500 tipos de evento; volume ilimitado) e do free tier do BigQuery em soft launch.

---

**Resumo executivo:** 23 eventos obrigatórios do BRIEF + 11 eventos proprietários que medem exatamente o que nos diferencia (Scout, Supply, Mutações, quase-vitória) e fecham os funis de FTUE e IAP; 4 funis com limiares de alerta; 14 métricas com definição SQL-precisa, metas do CANON §12 e ação corretiva pré-combinada; 10 experimentos com guardrails que protegem D1 e os pilares de design; 3 dashboards com donos e cadência. Nenhum número deste jogo será ajustado por opinião — sempre por dado + Remote Config.
