# Monetização — Mutant Army Run (Entregável 14)

> **Pacote de design · Documento 08** · Versão 1.0 · 2026-06-11
> Fontes de verdade: `CANON.md` (decisões fixas — especialmente §8, §11, §12) e `BRIEF.md` (requisitos).
> Docs relacionados: Economia (entregável 8), Sistema de Upgrades (entregável 13), Estratégia de Ads & Viralização (entregável 15), Progressão dos 7 Dias (entregável 4).

---

## 1. Estratégia geral

### 1.1 Posicionamento de monetização

Mutant Army Run é **hybrid-casual**: a receita vem de um mix de **ads (≈ 65%)** e **IAP (≈ 35%)**, com a camada de meta-progressão (tropas, fragmentos, passe) sustentando o lado IAP. Três princípios regem todas as decisões deste documento:

1. **Ads nunca interrompem a diversão.** Rewarded é sempre opcional e sempre vantajoso; interstitial obedece à política restritiva do CANON §11 e desaparece com uma compra única de US$ 4,99.
2. **IAP acelera e personaliza, nunca destrava poder exclusivo.** Tudo que dá poder pode ser obtido grátis (CANON §11 — baús grátis dropam lendárias). Detalhamento por SKU na §7.
3. **Tudo é tunável por Remote Config.** Frequência de ads, preços em moeda virtual, conteúdo de ofertas e gatilhos contextuais são parâmetros remotos (chaves na §8) — o soft launch calibra sem novo build.

**Justificativa de design:** o público-alvo (13–40, casual, BR/LatAm/US/SEA) tem alta tolerância a rewarded e baixa disposição a pagar cedo. Por isso a espinha dorsal é ad-revenue com IAP de conveniência/cosmético, e a única "assinatura" é o Passe de Temporada — o produto IAP com melhor retenção do gênero.

### 1.2 Projeção de ARPDAU — decomposição da meta ≥ US$ 0,08 (CANON §12)

Premissas de mix geográfico do soft launch: 40% BR/LatAm, 25% US, 35% SEA. eCPMs blended estimados a partir desse mix (mediação AppLovin MAX com AdMob, Meta e Unity Ads — CANON §13):

| eCPM blended | BR/LatAm | US | SEA | Blended |
|---|---|---|---|---|
| Rewarded | US$ 6,00 | US$ 22,00 | US$ 7,00 | **≈ US$ 10,50** |
| Interstitial | US$ 4,00 | US$ 14,00 | US$ 4,50 | **≈ US$ 7,00** |

Decomposição do ARPDAU no **cenário base** (jogador médio: 1,5 sessões/dia, ≥ 6 fases/sessão, sessão ≥ 8 min — metas do CANON §12):

| Fonte | Premissas | Cálculo | ARPDAU |
|---|---|---|---|
| Rewarded | ≥ 35% dos DAU assistem; engajados assistem ~8 vídeos/dia (dobrar a cada vitória + reviver + baú diário) → **2,8 imp/DAU** | 2,8 × 10,50 / 1000 | **US$ 0,029** |
| Interstitial | ~9 fases/dia, 1 a cada 3 fases a partir da fase 6, caps da §3 → **3,0 imp/DAU** | 3,0 × 7,00 / 1000 | **US$ 0,021** |
| IAP (bruto) | 0,40% dos DAU transacionam/dia × ticket médio US$ 8,00 (puxado por Passe e Remover Anúncios) | 0,004 × 8,00 | **US$ 0,032** |
| **Total** | | | **US$ 0,082 ✅** |

Cenários para o soft launch (gatilhos de decisão go/no-go no doc de Roadmap, entregável 19):

| Cenário | Rewarded | Interstitial | IAP | ARPDAU | Leitura |
|---|---|---|---|---|---|
| Conservador | 0,022 | 0,016 | 0,022 | **US$ 0,060** | iterar placements e oferta inicial antes de escalar UA |
| **Base (meta)** | 0,029 | 0,021 | 0,032 | **US$ 0,082** | escalar UA em BR/LatAm (CPI ≤ US$ 0,40) |
| Otimista | 0,038 | 0,026 | 0,048 | **US$ 0,112** | abrir UA nos EUA (CPI ≤ US$ 1,50) |

**Nota sobre taxa de loja:** valores IAP são brutos. Líquido ≈ 70–85% (Google/Apple; 85% via programa de pequenas empresas no primeiro US$ 1M/ano). O modelo de LTV (entregável 15) usa o líquido.

### 1.3 Rollout por fase de produto

| Fase | Monetização ativa | Referência |
|---|---|---|
| MVP (30 dias) | Rewarded **dobrar recompensa** + **reviver no boss** · **interstitials já integrados na build com a política canônica** (§3 — 1º interstitial após a fase 6 no D1, 1 a cada 3 fases, 100% Remote Config) · loja visível com Remover Anúncios | CANON §11/§15/§16; docs 02 §4.2, 06 §8 e 13 (semana 4) |
| Soft launch v1.1 | +3 placements rewarded restantes · Oferta Inicial · pacotes de gemas | este doc §2, §4, §6 |
| Soft launch v1.2 | Pacotes de moedas · baús IAP · skins · ofertas contextuais | §4, §6 |
| Global launch | Passe de Temporada (exige nível de jogador 5 — CANON §8) + eventos com ranking | §5 |

---

## 2. Rewarded Ads — os 5 placements canônicos (CANON §11)

Regras globais: rewarded é **sempre opcional**, o botão sempre mostra o ícone 📺 + a recompensa exata, e falha de fill **nunca** mostra botão quebrado (botão some se não há ad carregado; pré-cache de 2 ads pelo `AdsManager`). Comprar Remover Anúncios **não** remove rewarded — eles são benefício, não interrupção.

| # | Placement (id) | Contexto de exibição | Copy do botão (PT-BR) | Valor entregue | Conversão esperada | Racional psicológico |
|---|---|---|---|---|---|---|
| 1 | `rw_double_reward` | Tela de Vitória, botão primário ao lado de "Continuar" | **"📺 DOBRAR ×2"** (subtexto: "+{moedas} moedas") | Dobra as moedas da fase (CANON §8) | 30–40% das vitórias expostas | Peak-end: o jogador está no pico emocional da vitória; ganho certo e imediato, com o número exato visível ("loss aversion" de deixar moedas na mesa) |
| 2 | `rw_revive_boss` | Tela de Derrota, somente se a derrota ocorreu **na arena do boss**; 1×/fase (CANON §11) | **"📺 REVIVER COM 50% DO EXÉRCITO"** (subtexto: "O boss continua ferido!") | Revive **50% do exército que entrou na arena** (doc 05 §3.5; Remote Config `boss_revive_army_pct = 0,50`; UI em doc 09 OVL-05/SCR-05); o boss **mantém o HP atual** | 45–55% das derrotas em boss | Sunk cost legítimo: 60 s de corrida investidos + boss visivelmente ferido = recompensa quase garantida. O subtexto comunica que o progresso não foi perdido |
| 3 | `rw_daily_chest` | Tela inicial/Loja, slot "Baú do Dia" — 1 baú grátis/dia + este placement libera **1 baú extra** | **"📺 BAÚ EXTRA GRÁTIS"** | 1 Baú Comum extra (§4.4; conteúdo e drop table no doc 07 §4); 1×/dia | 20–25% dos DAU | Hábito diário + curiosidade de loot. Junto do baú grátis, cria o ritual de "abrir a loja todo dia" que alimenta os outros SKUs |
| 4 | `rw_try_legendary` | Tela de início de fase, logo após o **Boss Scout** — aparece quando o jogador ainda não possui nenhuma lendária; máx. 2×/dia | **"📺 CONVOCAR DRAGÃO POR 1 FASE"** (a tropa varia por Remote Config) | 1 unidade Lendária (ex.: Dragão, Supply 20 — CANON §5) no exército inicial **somente nesta fase** | 12–18% das exposições | Test-drive/efeito de posse: sentir o poder de uma lendária por 1 fase cria desejo concreto — é o maior gerador de demanda por baús e Passe. Posição pós-Boss Scout transforma em decisão tática ("Dragão de área contra ESTE boss?") |
| 5 | `rw_speed_upgrade` | Tela de Tropas, no card de uma tropa com **timer de mutação** ativo (evolução nv 6+ — doc 07 §6); máx. 2×/dia | **"📺 ACELERAR MUTAÇÃO (−15 MIN)"** | Reduz **15 minutos** do timer de mutação da tropa — é a implementação única do "acelerar upgrade" do CANON §11 (doc 07 §6); alternativa em gemas: 1 gema/2 min restantes (doc 07 §2.2) | 20–25% das exposições | Acelera a progressão de meta sem vender nada exclusivo: a tropa segue 100% utilizável durante a mutação (nunca bloqueia jogar) e o ad antecipa a notificação FCM "terminou de mutar" — transforma espera em engajamento, não em paywall |

**Meta agregada:** conversão rewarded ≥ 35% dos DAU (CANON §12). O funil é monitorado por `rewarded_ad_shown` / `rewarded_ad_completed` por placement (BRIEF · Analytics); um placement com completion < 85% das exibições iniciadas indica problema de UX ou de fill e dispara investigação.

**Anti-abuso:** recompensas concedidas somente no callback `OnAdRewarded` da mediação; reviver é 1×/fase mesmo reiniciando o app (flag no save da fase em andamento).

---

## 3. Interstitials — política exata e frequency caps

Política canônica (CANON §11 e §16), implementada no `AdsManager` e 100% parametrizada por Remote Config:

| Regra | Valor padrão | Chave Remote Config |
|---|---|---|
| Fase mínima para o 1º interstitial | a partir da **fase 6** (nunca antes) | `ads_inter_min_level = 6` |
| Intervalo entre interstitials | máx. **1 a cada 3 fases** completadas | `ads_inter_level_interval = 3` |
| Bloqueio por frustração | **nunca** exibir após 2 derrotas seguidas | `ads_inter_block_after_losses = 2` |
| Cap por sessão | máx. **3** interstitials/sessão | `ads_inter_session_cap = 3` |
| Cap por dia | máx. **9** interstitials/dia | `ads_inter_daily_cap = 9` |
| Cooldown mínimo entre exibições | **120 s** | `ads_inter_cooldown_s = 120` |
| Carência pós-rewarded | sem interstitial por **60 s** após um rewarded completado | `ads_inter_after_rewarded_s = 60` |
| Momento de exibição | **somente** na transição pós-tela-de-vitória → tela inicial/próxima fase; nunca no meio da corrida, nunca na tela de derrota | hard-coded (regra de produto) |
| Kill switch | liga/desliga global | `ads_inter_enabled = true` |

**Justificativa de design:** com ~9 fases/dia o jogador médio vê ~3 interstitials/dia — o suficiente para US$ 0,021 de ARPDAU sem ameaçar o D1 ≥ 40%. A regra "nunca após 2 derrotas" protege o momento de maior risco de churn; nesse momento o jogo oferece ajuda (dica de fraqueza do boss, sugestão de upgrade ou a oferta contextual da §6.2), não um anúncio.

**Remover Anúncios (US$ 4,99)** zera todos os interstitials para sempre (flag em RevenueCat + save). O slot pós-vitória passa a mostrar apenas a celebração — o que torna o SKU "vendável" pela própria experiência: o jogador sabe exatamente o que está comprando.

---

## 4. Catálogo IAP completo

Preços em US$ (tier Google Play/App Store) e R$ sugerido (tier local — abaixo da conversão cambial para maximizar conversão no BR, prática padrão da categoria). Gerenciado via RevenueCat (CANON §13). Taxa de referência interna de valor: **US$ 1 ≈ 130 gemas** (derivada do melhor pacote, §4.3).

### 4.1 SKUs âncora (CANON §11)

| SKU (id) | Preço US$ | R$ sugerido | Conteúdo exato | Tipo |
|---|---|---|---|---|
| `iap_remove_ads` | **4,99** | R$ 27,90 | Remove **todos** os interstitials para sempre + **200 gemas** de bônus. Rewarded permanecem (são opcionais e benéficos) | Não consumível |
| `iap_starter_offer` | **2,99** | R$ 16,90 | **Oferta Inicial** (1× por conta, primeiras 48 h — detalhes §6.1): 400 gemas + 3.000 moedas + 1 Baú Épico + skin exclusiva "Recruta de Elite" (Soldado) | Consumível 1× |
| `iap_season_pass` | **6,99/mês** | R$ 37,90/mês | Trilha premium do Passe de Temporada vigente (§5) | Assinatura mensal (renovação cancelável; também vendido como compra avulsa da temporada) |

### 4.2 Pacotes de moedas (4 tiers)

| SKU | Nome na loja | Preço US$ | R$ sugerido | Moedas | Bônus vs tier 1 |
|---|---|---|---|---|---|
| `iap_coins_t1` | Punhado de Moedas | 0,99 | R$ 5,90 | 1.200 | — |
| `iap_coins_t2` | Saco de Moedas | 2,99 | R$ 16,90 | 4.000 | +10% |
| `iap_coins_t3` | Carroça de Moedas | 4,99 | R$ 27,90 | 7.500 | +24% |
| `iap_coins_t4` | Cofre de Moedas | 9,99 | R$ 54,90 | 17.000 | +40% |

**Coerência com a economia (CANON §8):** vitória na fase 1 = 100 moedas com crescimento ×1,10^(fase−1); upgrade custa 100 × 1,35ⁿ. O tier 1 equivale a ~6–10 fases no início de jogo; o tier 4 paga um upgrade de nível ~13 — acelera, não pula conteúdo.

### 4.3 Pacotes de gemas (4 tiers)

| SKU | Nome na loja | Preço US$ | R$ sugerido | Gemas | Bônus vs tier 1 |
|---|---|---|---|---|---|
| `iap_gems_t1` | Punhado de Gemas | 0,99 | R$ 5,90 | 100 | — |
| `iap_gems_t2` | Bolsa de Gemas | 4,99 | R$ 27,90 | 560 | +12% |
| `iap_gems_t3` | Caixa de Gemas | 9,99 | R$ 54,90 | 1.200 | +20% |
| `iap_gems_t4` | Cofre de Gemas | 19,99 | R$ 109,90 | 2.600 | +30% |

**Coerência:** missões diárias rendem 20–40 gemas/dia e o boss de mundo dá 10 (CANON §8) — um F2P ativo compra o Baú Raro (300 gemas) a cada ~10 dias; o tier 2 compra ~2 baús raros de uma vez. Gemas nunca expiram.

### 4.4 Baús — preços na loja e bundles IAP (conteúdo: doc 07 §4)

> **Fonte única da verdade de baús: doc 07 (`07-economia-e-upgrades.md`) §4** — conteúdo (moedas × Mb + pacotes de fragmentos), drop tables por raridade, garantias e pity, com as chaves de Remote Config do doc 07 §9 (`chest_drop_table_{tier}`, `chest_coin_mult_world`, `chest_pity_legendary`, `shop_chest_price_{rare,epic,leg}`). Este documento define apenas **onde** cada baú aparece na loja, o preço em gemas e os SKUs IAP que os embrulham — exatamente como o doc 09 §4.8 já assume.

| Baú | Obtenção | Preço | Conteúdo (doc 07 §4) |
|---|---|---|---|
| Baú Comum | Grátis 1×/dia + `rw_daily_chest` | grátis / 📺 | 3 pacotes de fragmentos + 60–100 moedas × Mb |
| Baú Raro | Loja | **300 gemas** (CANON §8) | 8 pacotes, **≥1 pacote Raro garantido** + 250–400 moedas × Mb |
| Baú Épico | Loja | **900 gemas** (doc 07 §2.2/§9) | 15 pacotes, **≥1 pacote Épico garantido** + 600–900 moedas × Mb + 20 gemas |
| Baú Lendário | Loja | **2.400 gemas** (doc 07 §2.2/§9) | 25 pacotes, **≥1 pacote Lendário garantido** + 1.500–2.500 moedas × Mb + 80 gemas |
| `iap_bundle_epic` "Pacote do Caçador" | IAP direta | US$ 4,99 / R$ 27,90 | 1 Baú Épico (conteúdo do doc 07 §4) + 200 gemas |
| `iap_bundle_legendary` "Pacote do Titã" | IAP direta | US$ 14,99 / R$ 79,90 | 1 Baú Lendário (conteúdo do doc 07 §4) + 500 gemas |

**Transparência obrigatória:** probabilidades de drop por raridade (tabelas do doc 07 §4) publicadas na própria UI do baú (conformidade com políticas Google Play/App Store de loot box). **Pity system (doc 07 §4):** contador **global de pacotes** — após **50 pacotes** sem Lendário (`chest_pity_legendary = 50`), o próximo pacote Raro+ é promovido a Lendário; o contador conta igualmente em baús grátis e comprados, e é comunicado na UI como "Sorte Crescente" com barra visível. No MVP o pity fica suspenso (roster sem Lendárias — doc 07 §4). Baús abrem **instantaneamente** — sem timer de abertura (coerente com "SEM sistema de energia / nunca travar o jogador", CANON §8; doc 07 §2.4).

### 4.5 Skins (cosmético puro)

Preços em moeda virtual seguem o **doc 07 §2.1/§2.2** (fonte da economia): recolor comum por moedas; skins Rara/Épica/Lendária por 250/600/1.500 gemas, desbloqueadas na loja pelos níveis de jogador 13/16/18 (doc 07 §3.3).

| Tier de skin | Exemplo | Preço | Observação |
|---|---|---|---|
| Recolor (Comum) | "Soldado Carmesim" | **2.500 moedas** (doc 07 §2.1) | 10 skins do MVP são recolors + acessório do Soldado (CANON §15); dá utilidade tardia à moeda |
| Rara (acessório) | "Arqueiro de Capuz Dourado" | **250 gemas** (doc 07 §2.2) | acessório visível em toda a multidão; loja a partir do nível de jogador 13 |
| Épica (completa) | "Mago de Plasma" (rastro de partículas) | **600 gemas** (doc 07 §2.2) | momento de vídeo — VFX visível em corrida; nível de jogador 16 |
| Lendária (completa c/ VFX intenso) | "Gigante de Magma" (rastro de lava + brasas) | **1.500 gemas** (doc 07 §2.2) | vitrine máxima da loja; nível de jogador 18 |
| `iap_skin_pack_neon` "Pacote Neon" | 3 recolors neon (Soldado/Arqueiro/Mago) | US$ 1,99 / R$ 10,90 | porta de entrada IAP barata |
| Skins de oferta/passe | "Recruta de Elite", "Atirador Cromado" | exclusivas de oferta/passe | exclusividade **estética**, nunca de stats |

**Regra fixa:** skin **nunca** altera stats. É o SKU mais seguro contra P2W e o mais "instagramável" — o exército inteiro veste a skin, então a compra aparece em cada vídeo gravado.

---

## 5. Passe de Temporada — US$ 6,99/mês (CANON §11)

### 5.1 Estrutura

- **Desbloqueio:** nível de jogador 5 (CANON §8). **Duração:** 1 mês-calendário. **30 níveis** de trilha.
- **Progresso:** cada nível custa **100 Pontos de Passe (PP)** → 3.000 PP/temporada. Fontes: missões diárias (3/dia ≈ 60 PP), missão semanal (150 PP), +2 PP por vitória de fase (cap 30 PP/dia). Jogador ativo (meta: 6 fases/sessão) fecha a trilha em ~24–26 dias; casual chega ao nível ~20. **Sem venda de PP por dinheiro** — comprar o passe não compra progresso na trilha (anti-P2W e anti-FOMO).
- **Conteúdo mensal novo (BRIEF):** 1 **tropa sazonal** nova, 1 **boss de evento** novo (evento especial com ranking, aberto a todos), 1 **linha de skins** nova, recompensas diárias e baús premium na trilha.
- A compra a qualquer momento da temporada entrega retroativamente os níveis premium já alcançados.

### 5.2 Exemplo concreto — Temporada 1: "Protocolo Plasma"

- **Tropa sazonal:** *Atirador de Plasma* — Épico, Supply 8, dano de Raio à distância (stats seguem o baseline do CANON §5: DPS+HP por ponto de Supply ≈ constante +10–20% de prêmio por raridade; tabela no doc 03/entregável 9).
- **Boss de evento:** *Behemoth de Cristal* (fraco contra Raio — sinergia didática com a tropa sazonal), disponível no evento semanal para **todos os jogadores**; trilha premium dá +50% de pontos de ranking de evento **não competitivo** (ranking de evento paga cosmético + moedas, ver §7).
- **Skins:** linha "Neon/Plasma" (§4.5) + 3 exclusivas da trilha premium.

### 5.3 Trilha grátis × premium — nível a nível

| Nv | Trilha Grátis | Trilha Premium (US$ 6,99) |
|---|---|---|
| 1 | 300 moedas | **Atirador de Plasma desbloqueado imediatamente** + 100 gemas |
| 2 | 10 gemas | 200 moedas |
| 3 | 1 fragmento (Atirador de Plasma) | 30 gemas |
| 4 | 400 moedas | 1 Baú Raro |
| 5 | 1 Baú Raro | **Skin "Soldado Neon"** (exclusiva da temporada) |
| 6 | 10 gemas | 500 moedas |
| 7 | 1 fragmento (Atirador) | 40 gemas |
| 8 | 500 moedas | 2 fragmentos (Atirador) |
| 9 | 15 gemas | 1 Baú Épico |
| 10 | 1 Baú Raro | 60 gemas + efeito de rastro "Plasma" (cosmético de corrida) |
| 11 | 1 fragmento (Atirador) | 600 moedas |
| 12 | 600 moedas | 40 gemas |
| 13 | 15 gemas | 2 fragmentos (Atirador) |
| 14 | 1 fragmento (Atirador) | 1 Baú Raro |
| 15 | **1 Baú Épico** (marco do meio da trilha) | **Skin "Mago Plasma"** + 80 gemas |
| 16 | 700 moedas | 50 gemas |
| 17 | 20 gemas | 800 moedas |
| 18 | 1 fragmento (Atirador) | 3 fragmentos (Atirador) |
| 19 | 800 moedas | 1 Baú Épico |
| 20 | 1 Baú Raro | 100 gemas |
| 21 | 1 fragmento (Atirador) | 1.000 moedas |
| 22 | 20 gemas | 50 gemas |
| 23 | 1.000 moedas | 3 fragmentos (Atirador) |
| 24 | 1 fragmento (Atirador) | 1 Baú Épico |
| 25 | 30 gemas | 120 gemas |
| 26 | 1 fragmento (Atirador) | 1.200 moedas |
| 27 | 1.200 moedas | 4 fragmentos (Atirador) |
| 28 | 1 fragmento (Atirador) | 2 fragmentos (Atirador) |
| 29 | 40 gemas | 1 Baú Lendário |
| 30 | **1 fragmento (10/10 → Atirador de Plasma DESBLOQUEADO)** + 50 gemas | **Skin lendária "Atirador Cromado"** + 200 gemas + título de perfil "Mutante Supremo" |

**Totais da trilha grátis:** 5.500 moedas · 220 gemas · 10 fragmentos (**desbloqueia a tropa sazonal no nível 30**) · 3 Baús Raros · 1 Baú Épico.
**Totais da trilha premium:** 4.300 moedas · 770 gemas · +19 fragmentos · 2 Baús Raros · 3 Baús Épicos · 1 Baú Lendário · 3 skins exclusivas · 1 efeito de rastro · 1 título · tropa no nível 1 em vez do 30. Valor de referência ≈ 4.500 gemas (~US$ 35, com baús a 300/900/2.400 gemas — doc 07 §2.2) → **≈ 5× o preço** — generosidade honesta e verificável pelo jogador.

### 5.4 Como o F2P também ganha (requisito BRIEF "não pode ser P2W demais")

1. A **tropa sazonal é desbloqueável grátis** ao completar a trilha (10 fragmentos) — o pagante a recebe **antes**, não **em vez de**.
2. Na temporada seguinte, a tropa entra no **pool de baús comum** (inclusive do baú grátis diário).
3. O **boss de evento** e o evento especial são abertos a todos; a trilha premium dá bônus em um ranking que paga cosmético + moedas, nunca poder exclusivo.
4. 220 gemas grátis/temporada ≈ 73% de um Baú Raro só pela trilha — somadas às missões diárias (20–40 gemas/dia, CANON §8).

---

## 6. Oferta inicial e ofertas contextuais — sem dark patterns

**Regras anti-dark-pattern aplicáveis a TODAS as ofertas:** timers sempre reais (expiram de verdade e não "renascem"); botão de fechar visível, do tamanho padrão, no primeiro frame; preço total e conteúdo exato sempre listados; "valor de referência" calculado honestamente a partir dos pacotes da §4.3 (nunca % inventada); nenhuma oferta cobre a tela de gameplay; nenhuma oferta usa moeda de confusão (preço sempre em dinheiro real ou gemas, nunca camadas duplas); frequência limitada por Remote Config (`offer_*`).

### 6.1 Oferta Inicial — `iap_starter_offer` (CANON §11)

| Parâmetro | Valor |
|---|---|
| Gatilho | Primeira abertura da Loja completa (nível de jogador 4 — CANON §8) **ou** 24 h após o install, o que vier primeiro |
| Janela | **48 h reais** a partir do gatilho; 1× por conta, não retorna |
| Preço | **US$ 2,99 / R$ 16,90** |
| Conteúdo | 400 gemas + 3.000 moedas + 1 Baú Épico + skin exclusiva "Recruta de Elite" |
| Valor de referência | ≈ 1.550 gemas (~US$ 12; Baú Épico = 900 gemas — doc 07 §2.2) → mostrado como "vale ≈ 4×" com o cálculo aberto num tooltip |
| Apresentação | Card destacado no topo da Loja + 1 popup único (1 só, na abertura da loja); badge com contagem regressiva real |
| Objetivo | Converter o primeiro pagamento — o maior preditor de LTV do gênero. Preço de entrada baixo + skin exclusiva cria "primeira compra sem arrependimento" |

### 6.2 Oferta pós-derrota dupla — "Kit de Reforço"

| Parâmetro | Valor |
|---|---|
| Gatilho | 2 derrotas seguidas na mesma fase (mesma condição que **bloqueia** interstitial — CANON §11: o jogo oferece ajuda no lugar de anúncio) |
| Frequência | Máx. **1 exibição/24 h**; nunca 2× na mesma fase; some após 3 dispensas consecutivas (Remote Config `offer_rescue_cooldown_h = 24`) |
| Preço | **US$ 1,99 / R$ 10,90** (`iap_rescue_kit`) |
| Conteúdo | 1.500 moedas + 100 gemas + 1 Baú Raro |
| Apresentação | **Card discreto na tela de derrota, abaixo de "Tentar de novo"** — nunca popup bloqueante. Sempre acompanhado de ajuda grátis em igual destaque: dica do Boss Scout ("Este boss é fraco contra FOGO") + atalho para a tela de Upgrades |
| Objetivo | Converter frustração em progresso real (moedas → upgrade imediato), sem explorar o momento: a saída grátis tem o mesmo destaque visual |

### 6.3 Oferta pós-mundo — "Espólio do Mundo"

| Parâmetro | Valor |
|---|---|
| Gatilho | Derrotar o **boss de mundo** (fase 10 de cada mundo — CANON §6), na tela de celebração do mundo |
| Janela | **24 h reais**; 1× por mundo (10 possíveis no jogo completo) |
| Preço | **US$ 4,99 / R$ 27,90** (`iap_world_spoils_m{n}`) |
| Conteúdo (ex. M1) | 1 Baú Épico temático + 300 gemas + skin do mundo ("Recruta do Campo" — tema Campo Inicial; cada mundo tem a sua) |
| Apresentação | Card na tela de vitória do mundo, depois da chuva de recompensas grátis (baú grande + 10 gemas do boss — CANON §8/§16); nunca antes delas |
| Objetivo | Capturar o pico de orgulho/conclusão com um item-troféu colecionável. Coleção de skins de mundo vira meta de longo prazo visível no perfil |

---

## 7. Princípios anti-P2W (CANON §11) e conformidade por SKU

Princípios canônicos: **(A)** tudo que dá poder pode ser obtido grátis (baús grátis dropam lendárias); **(B)** pagamento **acelera** e **personaliza**, nunca cria poder exclusivo; **(C)** sem sistema de energia — pagar nunca compra "permissão para jogar" (CANON §8). Princípio adicional deste doc: **(D)** competição (rankings de evento) paga majoritariamente cosmético + moedas, para que aceleração paga não domine o jogo social.

| SKU | O que vende | Conformidade |
|---|---|---|
| Remover Anúncios | Conforto (zero interstitial) + 200 gemas | A/B ✅ — não toca em poder; gemas são as mesmas obteníveis grátis (20–40/dia) |
| Oferta Inicial / Kit de Reforço / Espólio do Mundo | Moedas, gemas, baús, skin | A/B ✅ — só recursos com fonte grátis equivalente + cosmético exclusivo (estética, não stats) |
| Pacotes de moedas/gemas | Aceleração de upgrades e baús | A/B ✅ — moedas vêm de toda vitória (e dobram com rewarded grátis); gemas vêm de missões e bosses de mundo |
| Baús IAP / em gemas | Aceleração da coleção de fragmentos de tropa (doc 07 §4) | A ✅ — baú grátis diário usa o **mesmo pool de drop**, incluindo Lendárias; odds publicadas; pity system idêntico para todos |
| Skins | Personalização pura | B ✅ — zero stats, por regra fixa |
| Passe de Temporada | Antecipação + cosmético + recursos | A/B/D ✅ — tropa sazonal grátis na trilha free (nível 30) e no pool de baús na temporada seguinte; PP não compráveis; boss de evento aberto a todos |
| Teto de poder | — | O nível máximo de tropa é 10 (CANON §8) e o Supply limita o exército (CANON §3.2): pagar muito **não** cria exército infinito — o teto é o mesmo para todos, pagante só chega antes |

**Teste de sanidade (executado a cada balance pass):** simular jogador F2P ativo (missões diárias + baú grátis + trilha free do passe) vs pagante de US$ 20/mês — o pagante deve estar no máximo **~2 mundos à frente** no mesmo tempo de jogo, e ambos devem atingir o teto de poder. Se a distância passar disso, recalibrar fontes grátis via Remote Config (não nerfar o pagante — aumentar o grátis).

---

## 8. Implementação: Remote Config, eventos e responsabilidades

### 8.1 Chaves de Remote Config (complementa §3)

| Chave | Default | Função |
|---|---|---|
| `ads_inter_*` (8 chaves) | ver §3 | política completa de interstitial |
| `rw_try_legendary_unit` | `"dragon"` | qual lendária o placement 4 empresta |
| `rw_daily_chest_enabled` / `rw_speed_upgrade_daily_cap` | `true` / `2` | liga/limita placements 3 e 5 |
| `offer_starter_window_h` | `48` | janela da Oferta Inicial |
| `offer_rescue_cooldown_h` / `offer_rescue_price_tier` | `24` / `t2` | oferta pós-derrota dupla |
| `shop_chest_price_{rare,epic,leg}` (doc 07 §9) | `300 / 900 / 2.400` | preços dos baús em gemas — CANON §8 fixa o Raro; doc 07 §2.2 define os demais |
| `chest_pity_legendary` (doc 07 §9) | `50` | pity de Lendário: contador global de pacotes (doc 07 §4) |
| `pass_pp_per_level` / `pass_daily_pp` | `100` / `60` | pacing do passe |
| `iap_catalog_version` | `1` | troca de vitrine sem build (via RevenueCat Offerings) |

### 8.2 Eventos de analytics (obrigatórios — BRIEF)

`rewarded_ad_shown` / `rewarded_ad_completed` (param: `placement_id`) · `interstitial_shown` (param: `level`, `session_count`) · `purchase_started` / `purchase_completed` (params: `sku`, `price_usd`, `trigger`) · `season_pass_opened` / `season_pass_purchased` · `chest_opened` (params: `chest_tier`, `source: free|gems|iap|pass`). Dashboards mínimos do soft launch: ARPDAU decomposto por fonte (§1.2), conversão por placement (§2), funil da Oferta Inicial, % DAU pagantes.

### 8.3 Responsabilidades de sistema (CANON §13)

- **`AdsManager`**: pré-cache, caps, callbacks de recompensa, integração AppLovin MAX (AdMob, Meta, Unity Ads como redes).
- **`IAPManager`** + RevenueCat: catálogo, restore purchases, validação de recibo, entitlements (`remove_ads`, `season_pass`).
- **`EconomySystem` / `RewardSystem`**: crédito de moedas/gemas/fragmentos/baús com transação atômica no save local-first (JSON com checksum) + sync Firestore.
- **`RemoteConfigManager`**: todas as chaves da §8.1 com defaults embarcados (o jogo monetiza corretamente offline).

---

## 9. Resumo executivo

| Dimensão | Decisão |
|---|---|
| Mix de receita | ~65% ads (rewarded > interstitial) / ~35% IAP, convergindo para 50/50 com a maturação do Passe |
| ARPDAU base | **US$ 0,082** (rewarded 0,029 + interstitial 0,021 + IAP 0,032) ≥ meta de US$ 0,08 do CANON §12 |
| Rewarded | 5 placements canônicos, sempre opcionais, conversão-alvo ≥ 35% dos DAU |
| Interstitial | **ativo desde o MVP** (docs 02/06/13) · fase ≥ 6 · 1 a cada 3 fases · nunca após 2 derrotas · caps 3/sessão e 9/dia · 100% Remote Config |
| IAP âncora | Remover Anúncios US$ 4,99 (+200 gemas) · Oferta Inicial US$ 2,99/48 h · Passe US$ 6,99/mês |
| Catálogo | 4 tiers de moedas + 4 de gemas + 4 tiers de baús — preços, drop tables e pity de 50 pacotes do doc 07 §2.2/§4 (odds públicas, pity visível) — + skins 100% cosméticas (preços do doc 07 §2.1/§2.2) |
| Passe | 30 níveis, F2P desbloqueia a tropa sazonal na trilha grátis; premium antecipa e adiciona cosmético |
| Ética | sem energia, sem timers de baú, sem dark patterns, anti-P2W auditado por simulação F2P vs pagante |
