# 00 — ÍNDICE DO PACOTE DE GDD · Mutant Army Run

> **"Monte o exército mais absurdo possível em 60 segundos."**
> Runner de multidão hybrid-casual com camada estratégica leve (Boss Scout + Supply + mutações) e meta-progressão.
> Versão do pacote: 1.0 — **2026-06-11**

Este índice é a porta de entrada do pacote. Os requisitos do cliente estão em `BRIEF.md`; as decisões fixas de design (nomes, números e regras finais) estão em `CANON.md`, que **prevalece em caso de conflito de detalhe**.

---

## 1. Como ler este pacote (ordem sugerida por papel)

| Papel | Ordem de leitura | Por quê |
|---|---|---|
| **Game designer** | CANON → 01 → 02 → 04 → 03 → 05 → 06 → 07 → 14 | Primeiro a fonte da verdade e a visão; depois o loop, a tríade de sistemas centrais (portais, unidades, bosses), fases, economia e os guard-rails de profundidade/vício. |
| **Dev (Unity/C#)** | CANON → 12 → 15 → 02 → 04 → 05 → 06 → 11 → 13 | CANON dá nomes e números canônicos; 12 dá pastas, managers, classes e SOs; 15 dá as lições do estudo de código e a política de licenças; depois os sistemas que serão implementados, analytics e o plano de 30 dias. |
| **Artista / áudio** | 01 (§6 direção de arte) → CANON (§1, §5, §6) → 09 → 06 (§3 mundos, §4 obstáculos) → 14 (§5 game feel) → 15 (starter pack de assets) | Estilo, paleta por mundo, silhuetas, telas/wireframes, temas dos 10 mundos, o checklist testável de game feel e os assets CC0 que servem de base do MVP. |
| **UA / marketing** | 01 (§1, §7) → 10 → 08 → 11 → 14 (§6 compliance) | Pitch e nomes; estratégia de UA, 20 conceitos de vídeo e 10 thumbnails; monetização; funis e métricas; política anti-fake-ads. |
| **Investidor / publisher** | 01 → CANON (§12 metas) → 08 (§1 ARPDAU) → 13 (roadmap + equipe) → 14 (§1 riscos) | Visão e diferencial, metas de soft launch (D1/D7, CPI, ARPDAU), projeção de receita, cronograma/custo e matriz de riscos. |

Regra geral: **todo mundo lê CANON.md primeiro** (10 min) — é curto e evita retrabalho.

---

## 2. Tabela de arquivos

| Arquivo | Conteúdo em 1 linha |
|---|---|
| `BRIEF.md` | Requisitos originais do cliente consolidados — a fonte dos REQUISITOS (inclui a lista dos 30 entregáveis). |
| `CANON.md` | Fonte da verdade: identidade, pilares, diferenciais, chart elemental, roster, bosses, economia, monetização, metas, tecnologia e escopo do MVP — prevalece sobre tudo. |
| `01-visao-e-conceito.md` | GDD master: pitch, público, pilares expandidos, diferenciais com exemplo jogável, lista de sistemas, direção de arte e análise/ranking de nomes. |
| `02-core-loop-e-progressao.md` | Loops aninhados (5 s → semanal), os 12 passos do BRIEF, progressão minuto a minuto dos primeiros 30 min e dia a dia dos primeiros 7 dias. |
| `03-sistema-de-unidades.md` | As 19 tropas: fórmula de balanceamento (Orçamento de Combate), tabela mestra de stats, scaling 1–10, desbloqueios, builds de sinergia e prova do "x10 soldados perde para +2 magos". |
| `04-sistema-de-portais.md` | Taxonomia completa de portais (43 em 5 grupos), pares honestos, slots de mutação, geração de fase guiada pelo Boss Scout e regras anti-frustração. |
| `05-sistema-de-bosses.md` | Anatomia do combate de boss (15 s, telegraph, fases de vida), fórmulas de HP/dano, 10 bosses únicos + 30 arquétipos regionais e os 5 bosses do MVP em detalhe. |
| `06-sistema-de-fases-e-mundos.md` | Anatomia da fase, os 10 mundos, catálogo de obstáculos, curva de dificuldade, pipeline template+variação e as 20 fases do MVP. |
| `07-economia-e-upgrades.md` | Os 5 recursos (fontes/ralos), curvas de recompensa e custo, baús e drop tables, as 8 trilhas de upgrade e simulação F2P dos dias 1/3/7. |
| `08-monetizacao.md` | Estratégia de receita: decomposição do ARPDAU, 5 rewarded placements, política de interstitials, catálogo IAP, Passe de Temporada e princípios anti-P2W. |
| `09-telas-e-wireframes.md` | As 10 telas + 6 overlays: princípios de UX, mapa de navegação, orçamento "jogar em <5 s" e wireframes textuais completos. |
| `10-ads-e-viralizacao.md` | UA e viralização: CPI por geo, loop de creative testing, 20 conceitos de anúncio em vídeo, 10 thumbnails e backlog priorizado de features virais. |
| `11-analytics.md` | Instrumentação completa: eventos obrigatórios do BRIEF + extras, funis (FTUE, rewarded, IAP, passe), métricas com definição exata, programa de A/B e dashboards. |
| `12-arquitetura-unity.md` | Estrutura de projeto Unity, 18 managers, event bus, classes C# principais com código, os 10 ScriptableObjects e performance de multidão (200–1000 unidades). |
| `13-roadmap-e-backlog.md` | Roadmap macro, plano do MVP em 30 dias (dia a dia), backlog em 10 épicos priorizados, expansão pós-MVP e equipe/orçamento mínimos. |
| `14-riscos-e-qualidade.md` | Matriz de riscos com contingências, anti-clone, compulsão ética, profundidade sem complexidade, checklist de game feel e compliance (LGPD/lojas/anti-fake-ads). |
| `15-referencias-e-recursos.md` | Estudo de código real de 14 repositórios (com política de licenças por repo), lições por sistema, stack recomendada, starter pack de assets CC0 do MVP e decisão de mediação de ads. Clones para estudo local em `_research/repos/`. |

---

## 3. Mapa dos 30 entregáveis do BRIEF

| # | Entregável | Onde está |
|---|---|---|
| 1 | GDD completo | O pacote inteiro (`01`–`14` + `CANON.md`); visão master em `01-visao-e-conceito.md` (mapa do pacote em §8) |
| 2 | Core loop detalhado | `02-core-loop-e-progressao.md` §2 (loops aninhados) e §3 (os 12 passos do BRIEF) |
| 3 | Progressão dos primeiros 30 min | `02-core-loop-e-progressao.md` §4 (linha do tempo, fase a fase, orçamento econômico, riscos de churn) |
| 4 | Progressão dos primeiros 7 dias | `02-core-loop-e-progressao.md` §5 (dia a dia, FCM, D1/D3/D7, calendário de login) |
| 5 | Lista de sistemas | `01-visao-e-conceito.md` §5 |
| 6 | Lista de telas | `09-telas-e-wireframes.md` §2 (10 telas + overlays) |
| 7 | Wireframe textual de cada tela | `09-telas-e-wireframes.md` §4 (SCR-01 a SCR-10) e §5 (OVL-01 a OVL-06) |
| 8 | Economia | `07-economia-e-upgrades.md` §1–§4 e §7 (simulação F2P); âncoras em `CANON.md` §8 |
| 9 | Sistema de unidades | `03-sistema-de-unidades.md` (documento inteiro); roster canônico em `CANON.md` §5 |
| 10 | Sistema de bosses | `05-sistema-de-bosses.md` (documento inteiro); regras canônicas em `CANON.md` §6 |
| 11 | Sistema de portais | `04-sistema-de-portais.md` (documento inteiro); MVP em `CANON.md` §10 |
| 12 | Sistema de fases | `06-sistema-de-fases-e-mundos.md` (documento inteiro); mundos em `CANON.md` §7 |
| 13 | Sistema de upgrades | `07-economia-e-upgrades.md` §5 (8 trilhas) e §6 (evolução de tropas); regras em `CANON.md` §9 |
| 14 | Monetização | `08-monetizacao.md` (documento inteiro); regras canônicas em `CANON.md` §11 |
| 15 | Estratégia de ads e viralização | `10-ads-e-viralizacao.md` §2 (estratégia de UA) + §1 e §6 |
| 16 | Estrutura de projeto Unity | `12-arquitetura-unity.md` §2 (pastas, cenas, asmdef, URP, build Android) |
| 17 | Classes principais em C# | `12-arquitetura-unity.md` §4 (GameManager, CrowdManager, GateManager, CombatSystem, BossManager etc.) |
| 18 | ScriptableObjects | `12-arquitetura-unity.md` §5 (os 10 SOs canônicos + workflow do designer) |
| 19 | Roadmap | `13-roadmap-e-backlog.md` §1 (macro-fases, soft launch, live ops) |
| 20 | Backlog por prioridade | `13-roadmap-e-backlog.md` §3 (épicos E1–E10) |
| 21 | Plano do MVP em 30 dias | `13-roadmap-e-backlog.md` §2 (dia a dia + DoD); escopo travado em `CANON.md` §15 |
| 22 | Expansão pós-MVP | `13-roadmap-e-backlog.md` §4 |
| 23 | Ideias de anúncios em vídeo | `10-ads-e-viralizacao.md` §3 (20 conceitos com fichas + matriz formato × cobertura) |
| 24 | Ideias de thumbnails | `10-ads-e-viralizacao.md` §4 (10 conceitos: playables, display, ícone) |
| 25 | Nomes melhores | `01-visao-e-conceito.md` §7 (critérios, 11 nomes do brief, 5 novos, ranking final) |
| 26 | Riscos | `14-riscos-e-qualidade.md` §1 (matriz + contingências); riscos de cronograma em `13-roadmap-e-backlog.md` §6 |
| 27 | Como evitar parecer clone barato | `14-riscos-e-qualidade.md` §2 |
| 28 | Como deixar mais viciante | `14-riscos-e-qualidade.md` §3 (loops de compulsão éticos + limites) |
| 29 | Como deixar mais inteligente | `14-riscos-e-qualidade.md` §4 (profundidade sem complexidade + guard-rails) |
| 30 | Como deixar mais viral | `10-ads-e-viralizacao.md` §5 (backlog P0/P1/P2 de features virais) |

---

## 4. Decisões-chave (resumo do CANON em 10 bullets)

- **Identidade:** título de trabalho *Mutant Army Run*; hybrid-casual, runner de multidão com estratégia leve; Android primeiro (Unity 2022 LTS + URP, retrato 9:16); fase completa ≈ 60–90 s; sem violência gráfica.
- **4 pilares em ordem de prioridade** (desempate de qualquer decisão): legível em 3 segundos → escolha inteligente, não "maior número" → espetáculo constante → progressão em 3 camadas.
- **Boss Scout é a inovação central:** cartão de ~2 s antes da fase mostra o boss, seu elemento e sua fraqueza; os portais da fase são gerados levando o boss em conta (sempre há 1 rota ótima e 1 armadilha aparentemente boa).
- **Supply é o anti-"maior número":** cada unidade tem custo de Suprimento (Soldado 1, Mago 4, Gigante 12...); limite inicial 60 (até 300 via meta); excedente vira moedas com fanfarra — x10 soldados nem sempre é melhor que +2 magos.
- **Mutações persistentes e portais honestos:** máximo de 3 mutações simultâneas visíveis nos modelos (a 4ª substitui a mais antiga); portais sempre em pares esquerda/direita com informação honesta (números e porcentagens claras) — a tensão vem da escolha, nunca da trapaça.
- **Elementos:** 8 no total, 4 no MVP (Fogo, Gelo, Raio, Veneno); ciclo principal Fogo > Gelo > Raio > Fogo com vantagem de +50% de dano e penalidade de −50% para mesmo elemento.
- **Conteúdo:** 19 tropas em 4 raridades (5 no MVP); 100 fases em 10 mundos, toda fase termina em boss (10 bosses únicos de mundo + arquétipos regionais; 5 bosses no MVP); MVP com 20 fases em 3 mundos enxutos.
- **Economia sem energia (nunca travar o jogador):** moedas/gemas/fragmentos/baús/XP; vitória na fase 1 = 100 moedas com recompensa ×1,10 por fase; upgrades em 8 trilhas a +5%/nível com custo 100 × 1,35^n; raridades cinza/azul/roxo/dourado.
- **Monetização anti-P2W:** rewarded sempre opcional (dobrar, reviver 1×/fase, baú extra, testar lendária, acelerar); interstitial só a partir da fase 6, máx. 1 a cada 3 fases, nunca após 2 derrotas seguidas; âncoras: Remover Anúncios US$ 4,99, Oferta inicial US$ 2,99, Passe US$ 6,99/mês — tudo que dá poder pode ser obtido grátis.
- **Metas de soft launch e stack:** D1 ≥ 40% · D7 ≥ 12% · sessão ≥ 8 min · ARPDAU ≥ US$ 0,08 · CPI ≤ US$ 0,40 (BR/LatAm); Firebase (Analytics, Remote Config, Crashlytics, FCM), AppLovin MAX como mediação de ads, RevenueCat para IAP, save local-first com sync Firestore.
