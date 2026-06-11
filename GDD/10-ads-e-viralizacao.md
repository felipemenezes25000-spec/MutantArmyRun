# 10 — Ads, UA & Viralização · Mutant Army Run

> Cobre os entregáveis **15** (estratégia de ads/UA), **23** (ideias de anúncios em vídeo), **24** (ideias de thumbnails) e **30** (como deixar mais viral).
> Fonte da verdade: `CANON.md` (especialmente §3, §10, §12, §13, §16). Requisitos: `BRIEF.md` (seções "Viralização", "Formatos de anúncio", "Monetização").
> Docs irmãos referenciados: doc 04 (sistema de portais), doc 12 (sistema de fases), doc 14 (monetização), doc 19 (roadmap), doc 21 (plano do MVP).

---

## 1. Princípios e regra anti-fake-ads

Antes de qualquer criativo, cinco regras inegociáveis:

1. **Tudo que aparece no anúncio existe no jogo.** Toda cena é capturada de uma build real (build de captura com câmera cinematográfica, mas mesma simulação). Sem UI falsa, sem números impossíveis, sem mecânica inventada para o vídeo.
2. **Por quê:** usuário enganado instala, percebe a mentira e desinstala no D0 — destruindo a meta canônica de **D1 ≥ 40%** (CANON §12) e o score de qualidade das redes (CPI sobe). Fake ads são incompatíveis com a estratégia de retenção do produto.
3. **Honestidade dramática, não chatice:** o jogo já é espetacular por design (CANON §2, pilar 3). O trabalho do criativo é **selecionar** o momento certo, não fabricá-lo.
4. **Portais honestos também no anúncio:** se o vídeo mostra um portal de risco, as porcentagens aparecem como no jogo ("70% x10 / 30% perde metade", CANON §3.4).
5. **Checklist de aprovação de criativo** (bloqueante): ☐ cena capturada de build oficial ☐ números atingíveis na fase mostrada ☐ HUD real ou versão limpa sem elementos falsos ☐ disponibilidade do conteúdo confere com o estágio da campanha (MVP vs release) ☐ sem referência a marcas/jogos de terceiros.

**Nota sobre a cena "1 → 10.000" (BRIEF):** essa cena, como pedida, **não é filmável de build real**. O Supply (CANON §3.2) converte o excedente em moedas a cada portal (doc 04 §7.2; `CrowdManager.EnforceSupplyCap`, doc 12), então o pico legível numa run perfeita do MVP é **~600** (Supply 60 × Zona de Perigo x10) e, mesmo no release com Supply 300, o teto fica em ~3.000. A versão honesta está no conceito A01 ("De 1 a 600"): o clímax do criativo é o **estouro do Supply** — o contador pica no frame da conversão, exatamente como o doc 04 §7.2 já descreve ("150 unidades viram 60 efetivas + 270 moedas"), e o excedente explode em gêiser de ouro, um payoff que nenhum clone tem. Números de 4–5 dígitos ficam **proibidos em qualquer criativo** até existir no jogo uma mecânica especificada (docs 04/12) que os produza de verdade.

---

## 2. Entregável 15 — Estratégia de UA

### 2.1 Fases da campanha

| Fase | Janela | Objetivo | Geos | Gate de saída (ir para a próxima fase) |
|---|---|---|---|---|
| **A — Teste criativo** | Semanas 1–4 (paralelo ao fim do MVP) | Validar hooks e medir CPI bruto com a vertical slice | BR, MX, PH | ≥ 3 criativos com CPI ≤ 1,5× alvo do geo e CTR acima do piso (§2.4) |
| **B — Soft launch** | Semanas 5–12 | Validar retenção e monetização (CANON §12): D1 ≥ 40%, D3 ≥ 22%, D7 ≥ 12%, ARPDAU ≥ US$ 0,08, rewarded ≥ 35% DAU | BR + PH (volume barato), depois MX e CA (proxy de US) | Todas as metas do §12 batidas por 2 semanas seguidas **e** LTV_D90 projetado ÷ CPI ≥ 1,2 |
| **C — Escala** | Semana 13+ (release global) | Crescer DAU com payback ≤ 90 dias | BR/LatAm/SEA primeiro, US/EU quando LTV suportar | Operação contínua; revisão mensal de payback |

**Justificativa:** BR e PH como geos de teste porque o público canônico é BR/LatAm/US/SEA (CANON §1) e o CPI baixo permite ~3–5× mais amostra por dólar; CA como proxy de US é prática padrão para validar CPI tier-1 sem queimar orçamento.

### 2.2 CPI alvo por geo

Âncoras do CANON §12 em negrito; demais valores criados coerentemente (escala entre os extremos canônicos):

| Geo | CPI alvo (Android) | Papel |
|---|---|---|
| **BR / LatAm** | **≤ US$ 0,40** | Volume principal + teste criativo |
| SEA (PH, ID, VN) | ≤ US$ 0,30 | Volume barato, teste de retenção |
| MX / CO / AR | ≤ US$ 0,45 | Extensão LatAm |
| EU Ocidental | ≤ US$ 1,20 | Escala fase C, se LTV suportar |
| **US** | **≤ US$ 1,50** | Receita por usuário mais alta; entra só na fase C |
| iOS (todos) | 1,8× o alvo Android do geo | Lançamento posterior (CANON §1: Android primeiro) |

Regra operacional: campanha cujo CPI ficar **> 1,5× o alvo por 7 dias** é pausada e seus criativos voltam para a fila de iteração.

### 2.3 Canais e orçamento relativo

Orçamento total relativo por fase: **A = 10% · B = 25% · C = 65%** do budget de UA do primeiro ano.

| Canal | Fase A | Fase B | Fase C | Função |
|---|---|---|---|---|
| TikTok Ads | 60% | 40% | 35% | Motor de teste criativo (CPM baixo, leitura de hook em horas); formato nativo 9:16 = o jogo é vertical por design |
| Meta (Reels/Stories/FB) | 25% | 30% | 30% | Escala com lookalikes de pagantes e de "completou 10 fases" |
| Google Ads / AdMob (UAC) | 10% | 20% | 25% | Escala automatizada; alimentar com os 5 melhores vídeos + 10 thumbnails do §4 |
| YouTube Shorts (via UAC/reserva) | 5% | 10% | 10% | Formatos "Desafio" e "Curiosidade" performam melhor em audiência de gaming |

Notas: (a) AppLovin MAX é a **mediação de monetização** (CANON §13) — cross-promo via AppDiscovery entra como teste oportunista na fase C, fora da tabela; (b) atribuição via MMP (decisão deste doc: **AppsFlyer**, integrado ao Firebase Analytics do CANON §13); (c) eventos de otimização das campanhas: `level_complete` (fase 6+) na fase A, `rewarded_ad_completed` e `purchase_completed` nas fases B/C (eventos canônicos do BRIEF).

### 2.4 Loop de creative testing (cadência semanal)

| Dia | Atividade |
|---|---|
| Seg | Briefing: escolher 4 iterações de vencedores + 2 conceitos novos do banco (§3); designer de fases marca "momentos anunciáveis" da semana (§6) |
| Ter–Qua | Captura na build de captura (seed fixa para reproduzir a run perfeita) + edição |
| Qui | Sobem 6 criativos em campanha de teste TikTok, US$ 150/criativo, geo BR |
| Sex–Dom | Coleta (mínimo 1.000 impressões e 30 instalações por criativo antes de julgar) |
| Seg seguinte | Decisão kill/iterate/scale + retro de hooks |

**Métricas e gatilhos:**

| Métrica | Definição | Kill se | Escalar se |
|---|---|---|---|
| CTR | cliques ÷ impressões | < 0,8% (TikTok) / < 1,0% (Meta) | ≥ 2,0% |
| IPM | instalações ÷ mil impressões | < 8 | ≥ 25 |
| CPI | custo ÷ instalação | > 2× alvo do geo após US$ 150 | ≤ alvo do geo |
| D1 da coorte do criativo | retenção D1 dos instaladores daquele vídeo | < 30% (indica hook enganoso — revisar honestidade) | ≥ 40% |

Mix de produção: **70% iterações de vencedores** (trocar hook, trocar fase, trocar texto) / **30% conceitos novos**. Expectativa realista: 2–3 vencedores escaláveis por mês. Vencedor "morre" quando o CPI dele sobe 30% sobre a média de 14 dias — criativo fadiga em 4–8 semanas, por isso a cadência é perpétua.

---

## 3. Entregável 23 — 20 conceitos de anúncio em vídeo

Convenções: duração 15–30 s, vertical 9:16, gameplay capturado de build real. **Disp.** = quando o conteúdo existe (MVP = filmável com o escopo do CANON §15; Mx = exige conteúdo do mundo/feature X, pós-MVP). Os 8 formatos do BRIEF estão todos cobertos (tag entre parênteses) e as 14 cenas virais desejadas estão mapeadas (coluna "Cena viral").

| # | Nome | Formato | Cena viral | Disp. |
|---|---|---|---|---|
| A01 | De 1 a 600 | Satisfação | 1 soldado virando 10.000 (versão honesta: pico de 600 no estouro de Supply) | MVP |
| A02 | A Ganância Custa Caro | Erro | portal errado destrói o exército | MVP |
| A03 | x5 ou a Mutação? | Escolha | x100 vs mutação lendária (versão honesta: x5, o maior multiplicador do catálogo) | M4+ |
| A04 | 2% de Vida | Quase-derrota | boss quase vencendo | MVP |
| A05 | O Último Soldado | Quase-derrota | último soldado vence o boss | MVP |
| A06 | 12 Arqueiros > 60 Soldados | Comparação | exército pequeno vence por estratégia | MVP |
| A07 | O Sacrifício | Evolução | fusão cria unidade absurda | M4+ |
| A08 | De Humano a Dragão | Evolução | humano virando dragão | M5+ |
| A09 | Clone Infinito | Satisfação | clone infinito | M4+ |
| A10 | Mutação Caótica | Curiosidade | mutação caótica | M8 |
| A11 | NÃO Escolha Esse Portal | Curiosidade | portal "não escolha esse" / parece ruim mas é o melhor | MVP |
| A12 | Só 3% Conseguiram | Desafio | "só 1% passa dessa fase" (versão honesta) | Release |
| A13 | Você Vence Esse Boss? | Desafio | desafio direto ao espectador | MVP |
| A14 | Parece Fácil. Não É. | Desafio | "parece fácil, mas não é" | MVP |
| A15 | x5 Soldados ou 5 Dragonetes? | Escolha | "qual portal você escolheria?" | M5+ |
| A16 | 100 Fracos vs 1 Titã | Comparação | comparação extrema | M5+ |
| A17 | O Spoiler que Salva | Curiosidade | diferencial Boss Scout | MVP |
| A18 | Números que Sobem | Satisfação | números crescendo, moedas explodindo | MVP |
| A19 | A 4ª Mutação | Erro | troca de slot de mutação na hora errada | M4+ |
| A20 | A Reviravolta do Necromante | Quase-derrota | comeback impossível | M2 release |

### Fichas dos conceitos

**A01 · De 1 a 600** (Satisfação · MVP)
- **Hook (0–2 s):** 1 soldadinho sozinho na pista, contador "1" gigante; corte seco para o contador girando feito caça-níqueis.
- **Roteiro (20 s):** 0–2 hook → 2–6 cadeia +25 e x2: 1 → 26 → 52, exército dobrando de largura a cada portal → 6–12 x3 estoura o Supply pela primeira vez: 156 unidades viram 60 efetivas + jato de moedas, feedbacks NICE → GREAT → MEGA ARMY → 12–16 Zona de Perigo x10 com run perfeita (seed fixa da build de captura): 60 sobreviventes, contador pica em **600** no frame da conversão → 16–19 gêiser de moedas do excedente (540 de Supply × 3 = "+1.620 💰") → 19–20 freeze no exército máximo diante da arena do boss.
- **Texto on-screen:** "1…" → "600?!" → "o que passar do limite vira OURO".
- **Por que retém:** escalada contínua sem platô; o espectador fica para ver "até onde vai o número" e a conversão em moedas é um segundo clímax inesperado.
- **Fonte real:** portais canônicos §10 (+25, x2, x3, Zona de Perigo x10), Supply 60 do MVP (§3.2/§15) e conversão de excedente do doc 04 §7.2 (3 moedas por ponto excedente) — todos os números do roteiro saem dessa conta, sem mecânica extra.

**A02 · A Ganância Custa Caro** (Erro · MVP)
- **Hook (0–2 s):** dedo (overlay de mão real) pairando entre "x2 garantido" e "RISCO: 70% x10 / 30% perde metade"; texto "não faça o que eu fiz".
- **Roteiro (18 s):** 0–2 hook → 2–5 exército saudável de 48 unidades, barra de progresso quase no boss → 5–8 escolhe o risco… animação de dado → 8–11 cai nos 30%: metade do exército desmonta em peças (sem sangue, CANON §1) → 11–15 chega no Golem de Pedra com 24 unidades, derrota em câmera lenta → 15–18 tela de derrota real com "Tentar de novo" e replay da escolha errada.
- **Texto on-screen:** "Eu tinha 48 soldados" → "aí eu fiquei ganancioso" → "NÃO COMETA ESSE ERRO".
- **Por que retém:** estrutura de fábula (erro anunciado no hook); o espectador assiste para confirmar a desgraça e pensa "eu teria escolhido certo" — instala para provar.
- **Fonte real:** portal de risco canônico §10 com odds exibidas (§3.4); boss MVP Golem de Pedra (§6).

**A03 · x5 ou a Mutação?** (Escolha · M4+)
- **Hook (0–2 s):** par de portais lado a lado: "x5" dourado vs portal pulsante "MUTAÇÃO LENDÁRIA ⚡ 80%"; tudo congelado, setas apontando.
- **Roteiro (25 s):** 0–2 hook congelado → 2–4 Boss Scout relembrado: "PLANTA CARNÍVORA — fraca contra FOGO" → 4–9 run A: escolhe x5 (com Supply de meta avançada, a tela vira um mar de soldados neutros) → 9–13 boss devora ondas de soldados, derrota → 13–15 "e se…?" rebobina com efeito VHS → 15–21 run B: mutação lendária Laser de Fogo aplica no exército inteiro (visível nos modelos, §3.3) → 21–25 boss derrete, slow motion no golpe final, BOSS BREAKER.
- **Texto on-screen:** "x5… ou ISSO?" → "errado." → "agora sim. 🔥"
- **Por que retém:** formato pergunta + dupla resolução; rebobinar cria segundo ato que pune quem sair cedo.
- **Fonte real:** portal x5 — o maior multiplicador do catálogo (doc 04), portais de mutação (BRIEF/doc 04), regra de mutação visível §3.3, boss M4 §6, Boss Scout §3.1.

**A04 · 2% de Vida** (Quase-derrota · MVP)
- **Hook (0–2 s):** barra de vida do Gigante de Madeira em 2%, exército reduzido a 3 unidades; tudo em slow motion.
- **Roteiro (15 s):** 0–2 hook → 2–6 boss telegrafa o ataque especial (pisão), 2 unidades morrem, sobra 1 Mago → 6–10 trocas de golpe alternando câmera próxima (game feel canônico) → 10–13 último projétil do Mago atravessa a tela em slow motion → 13–15 barra zera, explosão de recompensa, PERFECT.
- **Texto on-screen:** "2% de vida" → "1 mago" → "ele consegue?".
- **Por que retém:** tensão máxima desde o frame 1; 15 s curtos demais para abandonar antes do desfecho.
- **Fonte real:** boss de mundo M1 (§6), slow motion no golpe final (BRIEF game feel), Mago no MVP (§5).

**A05 · O Último Soldado** (Quase-derrota · MVP)
- **Hook (0–2 s):** contador caindo dentro da arena: 12 → 6 → 3 → 1 a cada pisão do boss; texto "sobrou UM".
- **Roteiro (22 s):** 0–2 hook → 2–7 flashback da run: escolhas ruins e obstáculos comendo unidades; o exército chega à arena do Brutamontes Zumbi com apenas 12 → 7–12 a luta começa bem — a barra do boss desce — até o ataque especial telegrafado varrer blocos do exército: o contador despenca até 1 → 12–17 o último soldado esquiva do segundo telegraph e acerta golpes mínimos; as duas barras quase zeradas, pixel a pixel → 17–20 golpe final em slow motion, soldado de pé sozinho → 20–22 tela de vitória: "Sobreviventes: 1".
- **Texto on-screen:** "todos morreram… menos ele" → "Sobreviventes: 1".
- **Por que retém:** narrativa de azarão; o contador no hook promete um final improvável.
- **Fonte real:** boss spawna com HP cheio e o exército é reduzido a 1 DURANTE o combate (nenhum pré-dano — coerente com docs 05/06); ataque especial telegrafado e combate de 10–20 s (CANON §6); fase de meio de mundo tunada para ~70% de vitória (doc 06, CANON §12); boss MVP Brutamontes Zumbi §6; tela de vitória com "sobreviventes" (BRIEF telas).

**A06 · 12 Arqueiros > 60 Soldados** (Comparação · MVP)
- **Hook (0–2 s):** split screen vertical: em cima "60 SOLDADOS", embaixo "12 ARQUEIROS", mesmo trecho de fase (seed fixa).
- **Roteiro (25 s):** 0–2 hook → 2–6 ambos escolhem portais: run de cima pega x2/x3, run de baixo pega "Virar Arqueiro" e +10 → 6–12 arena: Golem de Pedra usa pisão em área; soldados corpo-a-corpo morrem em blocos, arqueiros atiram de longe ilesos → 12–20 barra do boss: corrida lado a lado, arqueiros ultrapassam → 20–25 vitória embaixo, derrota em cima; "menos pode ser MAIS".
- **Texto on-screen:** "maior exército…" → "…ou exército CERTO?".
- **Por que retém:** corrida paralela cria placar implícito; espectador torce e fica até o resultado.
- **Fonte real:** portal "Virar Arqueiro" (§10), papel do Arqueiro (§5: distância/frágil), seed fixa do sistema de desafios (§5 deste doc).

**A07 · O Sacrifício** (Evolução · M4+)
- **Hook (0–2 s):** portal escuro: "SACRIFICAR METADE DO EXÉRCITO → 1 GIGANTE LENDÁRIO"; exército de 80 unidades para na frente.
- **Roteiro (22 s):** 0–2 hook → 2–6 hesitação (câmera passeia pelo exército), dedo escolhe o sacrifício → 6–10 metade do exército vira luz e converge num vórtice → 10–14 um Titã se ergue ocupando a tela inteira, MUTATION/GODLIKE → 14–19 Titã carrega o resto do exército no ombro e esmaga a arena → 19–22 boss cai num golpe, slow motion.
- **Texto on-screen:** "valeu a pena?" → "SIM."
- **Por que retém:** custo visível antes da recompensa; a transformação é o payoff que o hook promete.
- **Fonte real:** portal de risco "sacrificar metade por 1 gigante lendário" (BRIEF portais de risco, doc 04), Titã §5.

**A08 · De Humano a Dragão** (Evolução · M5+)
- **Hook (0–2 s):** soldadinho comum com seta "ELE" → corte para silhueta de dragão com "?".
- **Roteiro (28 s):** 0–2 hook → 2–7 cadeia de portais de classe: Soldado → Arqueiro → Mago, cada troca com puff elástico → 7–13 portal de elemento Fogo (exército inteiro pega chamas) + mutação Asas (modelos ganham asas, §3.3) → 13–18 portal "dragões pequenos": a multidão alada vira um enxame de dragõezinhos → 18–24 enxame cospe fogo no Dragão de Lava… que resiste (resiste Fogo, §6)! Pânico → 24–28 último par de portais tem Gelo; troca esperta, boss congela e cai. PERFECT.
- **Texto on-screen:** "evolução nível 1 → 100" → "plot twist: boss de LAVA" → "🧊 sempre vence 🔥… aqui".
- **Por que retém:** duas promessas encadeadas (evolução + twist tático); a quebra de expectativa no 18 s renova a atenção.
- **Fonte real:** portais de classe e elemento (BRIEF/doc 04), chart elemental §4 (Fogo vs boss de lava = péssimo, exemplo literal do BRIEF), boss M5 §6.

**A09 · Clone Infinito** (Satisfação · M4+)
- **Hook (0–2 s):** mutação "CLONAGEM" sendo coletada; cada unidade pisca e vira duas.
- **Roteiro (18 s):** 0–2 hook → 2–8 cada portal x2/x3 agora compõe com a clonagem: crescimento exponencial visível, câmera afastando para caber todo mundo → 8–12 contador acelera 60 → 120 → 240, rumo ao teto de Supply 300 (meta avançada), feedbacks INSANE → 12–16 a duplicação seguinte estoura o limite: o excedente vira cascata de moedas (fanfarra) → 16–18 exército máximo + montanha de moedas, "tudo isso numa fase".
- **Texto on-screen:** "1 vira 2. 2 viram 4." → "o jogo NÃO esperava por isso" (nota: o jogo espera — o texto é voz de jogador, não claim de bug).
- **Por que retém:** crescimento exponencial é hipnótico; zoom-out progressivo cria sensação de escala crescente.
- **Fonte real:** mutação "clonagem" (BRIEF mutações, doc 04), Supply até 300 com meta avançada e conversão de excedente §3.2/doc 04 §7.2 — o contador nunca exibe valor acima do que a conversão por portal permite (nada de 4–5 dígitos).

**A10 · Mutação Caótica** (Curiosidade · M8)
- **Hook (0–2 s):** portal roxo "ENERGIA ALIENÍGENA — efeito ALEATÓRIO" tremendo; texto "ninguém sabe o que sai daqui".
- **Roteiro (20 s):** 0–2 hook → 2–6 atravessa: roleta de efeitos na tela (queimar/congelar/encadear/envenenar, §4 Alien) → 6–10 sai "encadear": ataques do exército viram raios saltitantes → 10–14 segundo portal alien: agora o slot de mutação mais antigo é substituído na frente do jogador (§3.3) → 14–18 combinação acidental perfeita contra o boss elétrico-fraco → 18–20 vitória caótica, GODLIKE.
- **Texto on-screen:** "modo cassino 🎰" → "isso NÃO devia ter dado certo".
- **Por que retém:** aleatoriedade honesta = curiosidade genuína; cada segundo pode revelar um efeito novo.
- **Fonte real:** elemento Alien §4 (25% efeito aleatório), regra de 3 slots de mutação §3.3, mundo 8 §7.

**A11 · NÃO Escolha Esse Portal** (Curiosidade · MVP)
- **Hook (0–2 s):** par de portais: "x3" brilhante vs "÷2" vermelho; seta gigante no ÷2 com texto "confia".
- **Roteiro (25 s):** 0–2 hook → 2–6 câmera mostra o que vem DEPOIS de cada portal (leitura honesta da pista): atrás do x3, zona de perigo densa; atrás do ÷2, pista limpa com portal de risco x10 adiante → 6–10 escolhe ÷2: exército cai de 40 para 20, espectador grita → 10–16 pista limpa, x10 com 70% — sucesso: 200 unidades → 16–22 run fantasma comparativa (mesma seed) mostra o caminho do x3 chegando com 35 → 22–25 arena: 200 esmagam o boss; "o pior portal era o melhor".
- **Texto on-screen:** "÷2?! tá maluco?" → "matemática > instinto".
- **Por que retém:** o anúncio contradiz a intuição do espectador no hook — ele fica para ver a justificativa.
- **Fonte real:** regra canônica de level design §3.1 ("sempre 1 rota ótima e 1 armadilha aparentemente boa"), portais ÷2 e risco x10 do MVP §10.

**A12 · Só 3% Conseguiram** (Desafio · Release)
- **Hook (0–2 s):** card real de fim de fase: "Só 3% dos jogadores venceram esta fase com ≤ 5 unidades" sobre o frame do golpe final.
- **Roteiro (20 s):** 0–2 hook → 2–8 replay da run elite: pula multiplicadores, pega só qualidade (2 Magos, elemento certo) → 8–14 boss fight cirúrgico com 4 unidades, cada esquiva no telegraph → 14–18 vitória; card de estatística rara sobe na tela (feature §5.4) → 18–20 "e você? consegue entrar nos 3%?".
- **Texto on-screen:** "97% falham ASSIM" → "os 3% jogam ASSIM".
- **Por que retém:** prova social invertida (elite, não massa); o desafio é mensurável e o card é real, não claim vazio.
- **Fonte real:** sistema de estatística rara do §5.4 deste doc (dados de Analytics agregados); honesto porque a condição "≤ 5 unidades" é exibida — diferente do fake "só 1% passa".

**A13 · Você Vence Esse Boss?** (Desafio · MVP)
- **Hook (0–2 s):** Robô Escorpião entra na arena (animação de entrada ≤ 2 s, §6) esmagando a câmera; texto "ele tem 0 derrotas hoje".
- **Roteiro (22 s):** 0–2 hook → 2–8 três tentativas em cortes rápidos, todas com exércitos enormes de soldados (multiplicadores estourando o Supply), todas terminando em derrota (telas de derrota reais) → 8–12 Boss Scout em destaque, honesto como no jogo: "FRACO CONTRA RAIO · FOGO: NEUTRO" — não há portal de Raio nesta fase, a resposta é outra → 12–18 quarta tentativa montando a COMPOSIÇÃO certa: Virar Arqueiro nos portais, Mago e Gigante preservados, Supply 60 usado até a última vaga → 18–22 corta ANTES do golpe final, congela com a barra em 1%: "termina você".
- **Texto on-screen:** "3 derrotas" → "a resposta não era um exército MAIOR" → "instale e termine".
- **Por que retém:** cliffhanger literal — o desfecho só existe no download. Honesto: nada é mostrado que não exista.
- **Fonte real:** boss MVP Robô Escorpião (CANON §6/§15); no MVP a vitória sobre ele vem de composição/Supply, sem rota elemental — não há portal de Raio (doc 06 §8); Scout honesto "fraco: RAIO · FOGO: neutro" (doc 06); tropas e portais do MVP (CANON §5/§10/§15), Boss Scout §3.1.

**A14 · Parece Fácil. Não É.** (Desafio · MVP)
- **Hook (0–2 s):** fase 1 idílica, exército enorme, texto "jogo de criancinha, né?".
- **Roteiro (25 s):** 0–2 hook → 2–6 vitória trivial da fase 1 (pacing canônico §16: impossível perder) → 6–10 smash cut: fase 14, armadilhas em sequência, par de portais onde AMBOS são ruins de jeitos diferentes (÷2 vs zona de perigo) → 10–16 exército derretendo, escolhas em pânico → 16–21 chega no boss com 9 unidades, quase-derrota → 21–25 derrota por um triz; "fase 14: 30% de derrota hoje".
- **Texto on-screen:** "fácil?" → "fase 14 quer conversar" → "30% perdem aqui".
- **Por que retém:** contraste brutal de tom no segundo 6; estatística final dá credibilidade e desafio.
- **Fonte real:** pacing §16; fase 14 = boss de mundo M2 no MVP (§7) com alvo de 70% de vitória (doc 06 §8, coerente com CANON §12) → 30% de derrota; o número exibido vem de telemetria real agregada do dia (mesma regra do A12/§5.4) — se o dado real divergir do alvo, o criativo exibe o dado real, nunca o contrário.

**A15 · x5 Soldados ou 5 Dragonetes?** (Escolha · M5+)
- **Hook (0–2 s):** congelado no par de portais: "x5 SOLDADOS" vs "VIRAR DRAGONETE 🐉"; barra de Supply visível mostra o custo real (x5 estoura o limite; 5 Dragonetes = 30 de Supply, cabem folgados).
- **Roteiro (15 s):** 0–2 hook → 2–4 enquete: "comenta 🪖 ou 🐉" → 4–9 resolução: os Dragonetes voam POR CIMA dos obstáculos de chão (doc 04: voo ignora obstáculos + área pequena de Fogo) enquanto o fantasma da run x5 vê o excedente virar moedas e ainda perde metade nas armadilhas → 9–13 Dragonetes chegam intactos, área de Fogo no boss → 13–15 vitória, "o Supply sabia a resposta".
- **Texto on-screen:** "qual você escolheria?" → "quantidade tem custo".
- **Por que retém:** formato enquete gera comentário (sinal pago mais barato); resolução rápida recompensa em 15 s.
- **Fonte real:** portais x5 e Virar Dragonete do catálogo do doc 04 (Dragonete: fase mínima 41 = M5, voa + área de Fogo, Supply 6 cada), Supply §3.2, formato 1 do BRIEF.

**A16 · 100 Fracos vs 1 Titã** (Comparação · M5+)
- **Hook (0–2 s):** tela dividida: 100 soldados nível 1 vs 1 Titã; "mesma fase, mesma seed".
- **Roteiro (28 s):** 0–2 hook → 2–10 corrida paralela: a horda atropela obstáculos por massa, o Titã é lento mas imparável → 10–18 arena: horda causa dano por mil cortes; Titã, golpes sísmicos → 18–24 as barras do boss descem quase juntas — final apertado → 24–28 Titã vence por 0,4 s; placar "TITÃ 1 × 0 HORDA"; "discorda? prova".
- **Texto on-screen:** "quantidade vs qualidade, o teste DEFINITIVO".
- **Por que retém:** estrutura de corrida com placar; o resultado apertado gera debate nos comentários (comentário = distribuição orgânica).
- **Fonte real:** Titã §5, seed fixa (§5.3), filosofia anti-"maior número" §2.

**A17 · O Spoiler que Salva** (Curiosidade · MVP)
- **Hook (0–2 s):** o card do Boss Scout em tela cheia: "GIGANTE DE MADEIRA — FRACO CONTRA FOGO 🔥" com som de alarme.
- **Roteiro (22 s):** 0–2 hook → 2–6 "esse jogo te conta o final antes de começar. E mesmo assim…" → 6–12 portais aparecem: x3 neutro vs +Elemento Fogo menor; jogador segue o plano e pega Fogo → 12–16 toque no ícone do boss na barra de progresso reabre o lembrete por 1 s (§3.1) → 16–20 arena: o exército flamejante põe fogo no Gigante de Madeira, que desaba em chamas → 20–22 "saber não é o mesmo que conseguir".
- **Texto on-screen:** "o jogo te dá a resposta" → "97% ainda erram a conta".
- **Por que retém:** vende o diferencial competitivo como mistério ("por que um jogo te daria spoiler?"); educa a mecânica sem parecer tutorial.
- **Fonte real:** Boss Scout completo §3.1, incluindo o lembrete de 1 s; boss de mundo M1 Gigante de Madeira — fraco: Fogo (CANON §6, fase 7 do MVP §16); portal Elemento Fogo do MVP §10.

**A18 · Números que Sobem** (Satisfação · MVP)
- **Hook (0–2 s):** macro no contador de unidades girando + som de multiplicação (game feel canônico); zero contexto, puro ASMR.
- **Roteiro (15 s):** 0–2 hook → 2–6 sequência rítmica de portais no beat da música: x2, +25, x3 → 6–10 cascata de feedbacks NICE → GREAT → INSANE → MEGA ARMY sincronizada → 10–13 golpe final no boss em slow motion + explosão de moedas → 13–15 tela de vitória com contadores rolando.
- **Texto on-screen:** nenhum até 13 s; final: "seu cérebro agradece".
- **Por que retém:** edição musical (cada portal num beat); vídeos sem texto performam em audiências amplas e baratas — é o criativo de volume para UAC.
- **Fonte real:** feedbacks textuais canônicos (BRIEF game feel), portais §10.

**A19 · A 4ª Mutação** (Erro · M4+)
- **Hook (0–2 s):** HUD de mutações com 3 slots cheios (Laser, Armadura, Asas); um 4º portal de mutação "VELOCIDADE" se aproxima; texto "ele não leu a regra".
- **Roteiro (20 s):** 0–2 hook → 2–6 jogador pega a 4ª mutação: o slot mais antigo (LASER, a fonte de dano!) é substituído com animação clara (§3.3) → 6–10 percepção tardia: exército rápido… e sem dano → 10–16 boss fight agônico, DPS insuficiente, derrota no timer do espectador → 16–20 replay da troca de slot em zoom: "a regra estava NA TELA".
- **Texto on-screen:** "3 slots. SEMPRE 3." → "ele trocou dano por… velocidade".
- **Por que retém:** dramatiza uma regra real (o "momento de vídeo" previsto no CANON §3.3); o espectador entende o sistema e se sente mais esperto que o jogador do vídeo.
- **Fonte real:** regra canônica de 3 slots com substituição da mais antiga §3.3.

**A20 · A Reviravolta do Necromante** (Quase-derrota · M2 release)
- **Hook (0–2 s):** campo de batalha coberto de peças de unidades caídas; sobra um Necromante recuando; "acabou… será?".
- **Roteiro (25 s):** 0–2 hook → 2–7 Zumbi Titã avança no Necromante, barra do exército quase zerada → 7–12 Necromante canaliza: unidades caídas se reerguem em onda (habilidade §5) → 12–18 exército ressuscitado + elemento Fogo remanescente (fraqueza do boss §6) vira o jogo → 18–23 barra do boss despenca, slow motion no golpe final → 23–25 "nunca está acabado".
- **Texto on-screen:** "1 unidade viva" → "errado. 1 NECROMANTE vivo".
- **Por que retém:** reversão completa de expectativa; tematicamente perfeito (necromante vs zumbi) — memorável e comentável.
- **Fonte real:** Necromante §5 (revive tropas caídas), Zumbi Titã §6 (fraco: Fogo).

### 3.1 Matriz formato × cobertura

| Formato do BRIEF | Conceitos | Pronto no MVP? |
|---|---|---|
| 1 Escolha | A03, A15 | parcial (A11 cobre escolha no MVP) |
| 2 Erro | A02, A19 | A02 sim |
| 3 Evolução | A07, A08 | não (M4+) |
| 4 Desafio | A12, A13, A14 | A13, A14 sim |
| 5 Satisfação | A01, A09, A18 | A01, A18 sim |
| 6 Quase-derrota | A04, A05, A20 | A04, A05 sim |
| 7 Comparação | A06, A16 | A06 sim |
| 8 Curiosidade | A10, A11, A17 | A11, A17 sim |

**10 dos 20 conceitos são filmáveis com o escopo do MVP** (CANON §15) — suficiente para as fases A e B de UA. Os demais entram com o conteúdo dos mundos 4+ no release (doc 19/22).

---

## 4. Entregável 24 — 10 conceitos de thumbnail (playables, display, ícone)

Diretriz geral: 1 ideia por imagem, legível em 0,3 s a 120 px de altura, paleta saturada do estilo visual canônico (BRIEF "Estilo visual"), texto máximo de 4 palavras, sempre com rosto/olhos quando possível (CTR sobe com rostos).

| # | Nome | Uso | Elemento central | Cor dominante | Texto | Emoção-alvo |
|---|---|---|---|---|---|---|
| T01 | Soldadinho Herói | Ícone da loja | Busto do Soldado com sorriso confiante e um olho mutante brilhando, fundo radial | Azul vibrante + verde mutante | nenhum (ícone) | Carisma + "tem algo estranho aqui" |
| T02 | 1 → 600 | Display/end card | Um soldado à esquerda, multidão massiva no pico do estouro de Supply à direita, seta dourada entre eles | Dourado sobre roxo | "1 → 600" | Espanto |
| T03 | Escolha Impossível | End card de playable | Dois portais frontais (x5 🪖 vs 🐉, o par real do A15), dedo pairando entre eles | Azul vs laranja em contraste duro | "QUAL?" | Indecisão deliciosa |
| T04 | Davi vs Golias | Display | Golem de Pedra ocupando 80% do frame, encarando 1 soldadinho minúsculo de queixo erguido | Cinza-pedra + céu amarelo | "ele tem chance?" | Tensão + ternura |
| T05 | O Portal Proibido | Display/social | Portal ÷2 vermelho com fita "NÃO ESCOLHA" e o exército indo nele mesmo assim | Vermelho alarme | "confia." | Curiosidade rebelde |
| T06 | Mar de Unidades | Display | Vista aérea do exército no pico do estouro de Supply preenchendo todo o frame, contador "600" no topo | Arco-íris de raridades (cinza→dourado, §8) | "MEGA ARMY" | Satisfação/abundância |
| T07 | O Spoiler | Display/social | O card do Boss Scout estilizado: silhueta do boss de gelo + "FRACO CONTRA 🔥" | Azul-gelo + chama laranja | "o jogo te avisa" | Intriga |
| T08 | Antes/Depois | Playable end card | Split vertical: soldado nível 1 vs Mecha Supremo com laser; seta de evolução | Cinza → dourado | "30 segundos" | Aspiração |
| T09 | GODLIKE | Display | Frame congelado do golpe final em slow motion com o feedback "GODLIKE" estourando em 3D | Dourado + partículas brancas | "GODLIKE" (o próprio feedback) | Êxtase |
| T10 | Você Está Aqui | Display de retargeting | Mapa dos 10 mundos com cabeças dos bosses; pino "VOCÊ" no mundo 2; boss final em sombra | Gradiente verde→roxo (M1→M10) | "falta muito?" | Progressão/FOMO |

**Protocolo de teste:** cada thumbnail roda como variação de end card no playable e como display estático; medir CTR e IPM com os mesmos gatilhos do §2.4. Hipóteses registradas por par (ex.: T04 vs T06 testa "tensão vence abundância?"). O ícone T01 é A/B testado na Play Store (Store Listing Experiments) com variação de cor de fundo (azul vs roxo) antes do soft launch.

---

## 5. Entregável 30 — Como deixar o jogo mais viral (priorizado)

Critério de priorização: (impacto esperado em k-factor e UGC) ÷ (custo de implementação), com restrição de não tocar no core loop. KPI guarda-chuva: **taxa de compartilhamento ≥ 2% das vitórias** e **≥ 8% dos cards raros**.

### P0 — entram no primeiro release pós-MVP

**5.1 Replay compartilhável do golpe final (BossBreakerClip)**
- O jogo já faz slow motion no golpe final (BRIEF game feel). Feature: gravar automaticamente os últimos 8–12 s (chegada na arena → golpe final → explosão de recompensa) num clipe 9:16 com contador de unidades, nome da fase e marca d'água discreta do jogo.
- Botão "Compartilhar replay" na tela de vitória (1 toque → share sheet nativo). Sem edição, sem fricção.
- Implementação: re-simulação determinística da seed da run com câmera cinematográfica (mesma tecnologia da build de captura do §6) — mais barato e leve que gravar vídeo da tela em tempo real em aparelhos medianos (restrição de performance do BRIEF).
- KPI: ≥ 2% das vitórias compartilhadas; cada clipe é um anúncio honesto produzido de graça pelo jogador.

**5.2 Card "Qual portal você escolheria?" gerado pelo jogo**
- Ao fim de fases com uma escolha de portal estatisticamente divisiva (split real 40–60% entre jogadores, via Analytics `gate_selected`), o jogo gera uma imagem: o par de portais da fase + "Eu escolhi X. 54% escolheram Y. E você?".
- É o formato de anúncio nº 1 do BRIEF transformado em UGC: o jogador vira o criador do criativo.
- Implementação barata: template de imagem renderizado client-side com os GateConfigSO da fase.
- KPI: ≥ 8% de share quando o card aparece (aparece no máximo 1×/dia para não banalizar).

### P1 — primeiras temporadas

**5.3 Desafio semanal com seed fixa**
- Toda segunda, uma fase especial com seed idêntica para todos (mesmos portais, mesmo boss), 3 modificadores fixos (ex.: "Supply 30", "sem portais x", "boss com 2 fraquezas rotativas") e leaderboard por % de exército sobrevivente.
- Por que viraliza: seed fixa torna runs comparáveis — "como você passou do par de portais do minuto 0:40?" vira conversa em comentário, Discord e duet de TikTok. Também alimenta os conceitos A06/A11/A16 (corridas paralelas justas).
- Recompensa: baú + card exclusivo de resultado (alimenta 5.2/5.4). Sem energia, sem paywall (CANON §8).

**5.4 Estatística rara no fim da fase**
- Após a tela de vitória, se a run cruzou um threshold raro, sobe um card dourado: "Só 3% venceram esta fase com ≤ 5 unidades" · "Você está entre os 1,8% que derrotaram o Robô Escorpião sem perder unidades" · "Primeira pessoa do seu país a vencer com exército 100% Arqueiro hoje".
- Dados reais agregados do Analytics (eventos canônicos `level_complete`, `boss_defeated`) — nunca inventados; se a amostra do dia for < 1.000 runs, o card usa janela de 7 dias.
- É o motor do conceito A12 e o screenshot-bait número 1: estatística rara + botão de share no próprio card.

### P2 — oportunidades contínuas

**5.5 Feedbacks textuais como marca registrada** — os feedbacks canônicos (NICE → GODLIKE → MEGA ARMY → BOSS BREAKER) ganham animações exageradas e únicas por tier; "GODLIKE" é desenhado para ser o frame que as pessoas printam. Custo ~zero (já existem), upside de identidade visual em todo UGC.

**5.6 Skins no replay** — as 10 skins do MVP (CANON §15) aparecem no BossBreakerClip; skin rara visível = status compartilhável = motivação de coleção (sinergia com doc 14).

**5.7 Ghost de amigo no desafio semanal** — correr "contra" o fantasma translúcido da run de um amigo (mesma seed). Convite por link = loop de aquisição direto; medir k-factor do link (alvo ≥ 0,05 no lançamento da feature).

**5.8 Programa de criadores** — kit de captura público (build de captura simplificada + assets de logo) para micro-influencers BR/SEA + desafios oficiais com hashtag por temporada ("vença o desafio da semana com ≤ 10 unidades"). Orçamento: 5% do budget de UA da fase C, medido como canal próprio no MMP.

| Prioridade | Feature | Esforço (dev-semanas) | KPI primário |
|---|---|---|---|
| P0 | 5.1 Replay do golpe final | 3 | share ≥ 2% das vitórias |
| P0 | 5.2 Card de escolha de portal | 1,5 | share ≥ 8% das exibições |
| P1 | 5.3 Desafio semanal seed fixa | 4 | 20% do DAU joga o desafio |
| P1 | 5.4 Estatística rara | 2 | share ≥ 8% dos cards |
| P2 | 5.5–5.8 | 1–3 cada | k-factor agregado ≥ 0,08 |

---

## 6. Boss Scout + portais pareados: cada fase nasce sendo um anúncio

A estrutura canônica da fase coincide, beat a beat, com a anatomia de um criativo vencedor — isso não é coincidência, é diretriz de produção:

| Anatomia do anúncio | Elemento canônico da fase | Por quê funciona |
|---|---|---|
| **Hook (0–2 s)** | Card do Boss Scout (~2 s, §3.1): "BOSS DE GELO — FRACO CONTRA FOGO 🔥" | O jogo já abre cada fase com um hook pronto: vilão + enigma + promessa, na duração exata do slot de hook |
| **Tensão recorrente** | Portais pareados (§3.4): toda escolha é binária, legível e honesta | Pares de portais são thumb-stoppers naturais — o formato "qual você escolheria?" existe a cada 6–10 s de gameplay |
| **Corpo (escalada)** | Multiplicação visual + mutações visíveis + Supply estourando em moedas | Crescimento contínuo sem platô; sempre há um número subindo |
| **Payoff** | Arena, slow motion no golpe final, explosão de recompensa | Clímax garantido em 100% das fases, na janela 15–30 s |

**Regras de produção derivadas:**

1. **Toda fase nova passa por "ad review" no design** (doc 12): o designer marca no LevelConfigSO os campos `adMomentGateIndex` (o par de portais mais divisivo) e `adMomentSeed` (seed que reproduz a run de demonstração). Critério: se uma fase não tem nenhum par de portais que renderia um A11/A15, ela volta para ajuste — porque também será uma fase estrategicamente rasa (os dois problemas são o mesmo problema, pilar 2 do CANON).
2. **A regra "1 rota ótima + 1 armadilha aparentemente boa" (§3.1) é uma fábrica de conceitos "Erro" e "Curiosidade":** cada fase gera automaticamente pelo menos um roteiro A02 (seguir a armadilha) e um A11 (desconfiar dela). 100 fases = 200 roteiros latentes sem trabalho criativo extra.
3. **Build de captura:** flavor da build com câmera cinematográfica (órbita no boss, dolly nos portais, zoom no contador), HUD opcional, seeds reproduzíveis e velocidade de simulação ajustável. É a mesma re-simulação determinística do replay compartilhável (5.1) — um investimento, dois usos.
4. **Boss Scout como série de criativos:** cada boss único de mundo (§6 do CANON: Gigante de Madeira, Zumbi Titã, Robô Escorpião…) gera um episódio do formato A13/A17 com hook novo ("FRAQUEZA ROTATIVA A CADA 25% DE HP" do Alien Supremo é um hook inteiro sozinho). 10 mundos = 10 ondas de criativos sincronizadas com o roadmap de conteúdo (doc 19).
5. **Telemetria fecha o loop:** `gate_selected` identifica os pares de portais mais divisivos do jogo real → esses pares viram os próximos cards 5.2 e os próximos anúncios A03/A15. O jogo live decide o que a UA produz na semana seguinte (cadência do §2.4).

---

## 7. Resumo executivo

- **UA em 3 fases** (teste BR/PH → soft launch com metas do CANON §12 → escala global), TikTok como motor de teste, CPI âncora ≤ US$ 0,40 BR/LatAm e ≤ US$ 1,50 US, cadência perpétua de 6 criativos/semana com gatilhos objetivos de kill/scale.
- **20 conceitos de vídeo prontos para produzir**, todos honestos e rastreáveis a mecânicas canônicas; 10 filmáveis já no MVP; os 8 formatos e as 14 cenas virais do BRIEF cobertos.
- **10 thumbnails** com hipóteses de teste pareadas e protocolo de A/B incluindo o ícone da loja.
- **Viralização como feature**: replay do golpe final e card de escolha de portal no P0, desafio semanal com seed fixa e estatísticas raras no P1 — o jogador vira o produtor dos nossos melhores anúncios.
- **Tese central:** Boss Scout + portais pareados fazem cada fase nascer com hook, tensão e payoff embutidos — o pipeline de criativos é um subproduto do level design, não um custo paralelo.
