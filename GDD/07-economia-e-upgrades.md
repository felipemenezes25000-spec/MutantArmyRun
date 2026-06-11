# 07 — Economia & Upgrades · Mutant Army Run

> **Escopo:** entregáveis 8 (Economia) e 13 (Sistema de Upgrades) do BRIEF.
> **Fontes da verdade:** `CANON.md` (§8 economia, §9 upgrades, §11 monetização, §12 metas, §16 pacing) e `BRIEF.md`. Em caso de conflito de detalhe, o CANON prevalece.
> **Docs relacionados:** doc 02 (core loop e progressão 30 min / 7 dias), doc 03 (tropas), doc 04 (portais), doc 05 (bosses), doc 06 (fases/mundos), doc 08 (monetização), doc 10 (telas/UX).
> Identificadores de código em inglês conforme glossário do CANON §14: Coin, Gem, Shard, Chest, XP, Upgrade Track.

---

## 1. Princípios da economia

1. **Nunca travar quem quer jogar** (CANON §8): sem energia, sem timer que bloqueie fase, baús abrem na hora. O pacing vem de dificuldade + curva de custo (ver §10 deste doc).
2. **Renda cresce ×1,10 por fase; custo cresce ×1,35 por nível.** O gap entre as duas exponenciais é o motor de pacing: no início o jogador compra um upgrade por fase (dopamina constante); a partir do dia 3 ele compra um a cada 2–4 sessões (objetivo de médio prazo).
3. **Toda fonte premium tem versão grátis** (CANON §11): gemas caem de missão/boss/nível, baús grátis dropam lendárias (com pity), rewarded ads são sempre opcionais.
4. **Tudo é válvula de Remote Config** (§9 deste doc): nenhum número desta página é hardcoded; o doc define o *default* e a faixa segura de operação.
5. **Espetáculo na entrega**: toda entrada de moeda tem feedback visual (explosão de moedas, contador rolando). A conversão de Supply excedente em moedas é fanfarra, nunca punição (CANON §3.2).

---

## 2. Os 5 recursos — papéis, fontes e ralos

| Recurso | Código | Papel | Sensação-alvo |
|---|---|---|---|
| Moedas | `Coin` | Soft currency: alimenta as trilhas de upgrade e a evolução de tropas | Abundante, gasta toda sessão |
| Gemas | `Gem` | Hard currency: baús, skins, conveniência | Escassa, gera plano de poupança |
| Fragmentos | `Shard` | Progressão por tropa (evolução nv 1→10) | Coleção, "falta pouco" |
| Baús | `Chest` | Pacote-surpresa: moedas + fragmentos (+ gemas nos altos) | Loot moment, slow-open com raridade |
| XP | `XP` | Nível de jogador: desbloqueia features (trilhas 5–8, baús, loja, passe, eventos) | Progresso garantido, nunca regride |

`Mb` = multiplicador de mundo para valores de moeda fora da recompensa de fase (baús, missões). Regra: `Mb ≈ recompensa média de fase do mundo ÷ 100`. MVP: M1 = 1,0 · M2 = 1,6 · M3 = 2,5. Atualizado via Remote Config a cada mundo novo.

### 2.1 Moedas (Coin)

**Fontes**

| Fonte | Valor | Frequência / limite |
|---|---|---|
| Vitória de fase (1ª vez) | `100 × 1,10^(fase−1)` (CANON §8) | 1× por fase |
| Replay de fase já vencida | 40% da recompensa da fase | Ilimitado (farm com retorno reduzido) |
| Dobrar com rewarded ad | +100% das moedas da tela de vitória (só moedas) | 1× por tela de vitória (CANON §11) |
| Bônus de sobreviventes | +10% da recompensa se ≥50% do exército sobreviver ao boss | Por vitória |
| Conversão de Supply excedente | 2 moedas × custo de Supply da unidade convertida × Mb | Durante a corrida (CANON §3.2) |
| Missões diárias | 100 × Mb por missão (3/dia) | 300 × Mb/dia |
| Baús | Ver tabela do §4 | Conforme fonte do baú |
| Eventos semanais | 500–2.000 (por bracket de poder) | 1×/semana (nível de jogador ≥6) |

**Ralos**

| Ralo | Valor | Observação |
|---|---|---|
| Trilhas de upgrade | `100 × 1,35^(n−1)` para comprar o nível *n* | Ralo principal — ver §5 |
| Evolução de tropa (junto com Shards) | `100 × 2^(n−1) × raridade` (C ×1 · R ×2 · E ×4 · L ×8) | Ver §6 |
| Skin recolor (comum) | 2.500 moedas | Skins superiores custam gemas |
| Oferta diária da loja (10 Shards comuns) | 300 × Mb | 1×/dia — dá utilidade tardia à moeda |

### 2.2 Gemas (Gem)

**Fontes** (alvo F2P ativo: 30–50 gemas/dia, coerente com CANON §8 "20–40 de missões + extras")

| Fonte | Valor | Frequência |
|---|---|---|
| Missões diárias | 10 por missão (3/dia) + 10 de bônus ao completar as 3 | 20–40/dia (CANON §8) |
| Boss de mundo derrotado (1ª vez) | 10 (CANON §8) | 1× por mundo |
| Fase 10 | 50 (CANON §16) | 1× |
| Level-up de jogador | 10 por nível | 19× até o nv 20 |
| Baú Épico / Lendário / de Mundo | 20 / 80 / 15 | Por baú |
| Evento semanal (participação) | 40 | 1×/semana |
| Conquistas (ex.: "100 portais escolhidos") | 5–25 | ~30 conquistas no MVP |
| Passe de Temporada — trilha grátis | 220 por temporada (mês) | Total da trilha grátis dos 30 níveis — doc 08 §5.3 |
| IAP (pacotes, Remover Anúncios +200) | Conforme doc 08 | — |

**Ralos**

| Ralo | Preço | Observação |
|---|---|---|
| Baú Raro / Épico / Lendário na loja | 300 (CANON §8) / 900 / 2.400 | Escada ×3 por tier |
| Skins: Rara / Épica / Lendária | 250 / 600 / 1.500 | Cosmético puro |
| Reviver no boss (alternativa ao rewarded) | 30 | Mesmo limite: 1×/fase (CANON §11) |
| Pular timer de mutação de tropa (§6) | 1 gema / 2 min restantes | Nunca bloqueia jogar |

### 2.3 Fragmentos (Shard)

Fragmentos são **por tropa** (CANON §8). A "carta" da tela de Tropas é o contêiner visual dos fragmentos daquela unidade ("cartas simples" do MVP, CANON §15). Juntar **10 fragmentos de uma tropa nova = desbloqueia a tropa no nv 1**.

**Fontes**

| Fonte | Valor | Frequência |
|---|---|---|
| Pacotes de baú (ver §4) | Comum 10 · Raro 5 · Épico 2 · Lendário 1 fragmento(s) | Por pacote sorteado |
| Drop de boss (qualquer boss) | 30% de chance de 5 fragmentos de tropa do pool do mundo | Por boss morto (CANON §6) |
| Desafio do Boss (nível de jogador 14) | 20–40/dia | Re-luta bosses de mundo |
| Oferta diária da loja | 10 comuns por 300 × Mb moedas | 1×/dia |
| Eventos | 30–100 (tropa em destaque) | Semanal |

**Ralos**

| Ralo | Valor |
|---|---|
| Evolução de tropa nv *n* → *n+1* | `10 × 2^(n−1)` fragmentos da própria tropa + moedas (CANON §8) — tabela no §6 |
| Overflow (tropa já no nv 10 máx.) | Conversão automática: 1 fragmento = 10 moedas (nada é desperdiçado) |

### 2.4 Baús (Chest)

Fontes por tipo na tabela do §4. **Ralo/fricção: nenhum** — baús abrem instantaneamente, sem fila nem timer de abertura. Justificativa: timer de baú é a mecânica nº 1 de frustração em usertests do gênero e contradiz o princípio "nunca travar"; a antecipação vem da animação de slow-open (2 s, pulável) e da cor de raridade.

### 2.5 XP (nível de jogador)

**Fontes**

| Fonte | XP |
|---|---|
| Vitória de fase (1ª vez) | `10 + 5 × fase` (doc 02 §4.4 — fonte única da curva no pacote) |
| Vitória sobre boss de mundo | +30 XP fixos além do XP da fase |
| Replay de fase vencida | 50% do XP da fase |
| Derrota | 5 XP fixos (perder nunca é tempo perdido) |
| Missão diária | 10 |

**Ralo:** nenhum. XP só acumula; nível nunca regride. É a régua de longo prazo e o gate de features (§3.3).

---

## 3. Curvas

Regra geral de arredondamento: **a fórmula é a fonte da verdade**; valores exibidos arredondam para inteiro.

### 3.1 Recompensa por fase — `reward(f) = 100 × 1,10^(f−1)`

| Fase | Moedas | Com rewarded ×2 | | Fase | Moedas | Com rewarded ×2 |
|---|---|---|---|---|---|---|
| 1 | 100 | 200 | | 11 | 259 | 518 |
| 2 | 110 | 220 | | 12 | 285 | 570 |
| 3 | 121 | 242 | | 13 | 314 | 628 |
| 4 | 133 | 266 | | 14 | 345 | 690 |
| 5 | 146 | 292 | | 15 | 380 | 760 |
| 6 | 161 | 322 | | 16 | 418 | 836 |
| 7 | 177 | 354 | | 17 | 459 | 918 |
| 8 | 195 | 390 | | 18 | 505 | 1.010 |
| 9 | 214 | 428 | | 19 | 556 | 1.112 |
| 10 | 236 | 472 | | 20 | 612 | 1.224 |

Soma das primeiras vitórias 1–20 (escopo MVP): **≈ 5.727 moedas**.

**Recalibração planejada (release completo, via Remote Config — CANON §8 "recalibrada"):** ×1,10 puro até a fase 100 daria ~1,25 M de moedas por fase, quebrando a paridade com o custo 1,35 (que é por nível, não por fase). Degraus planejados:

| Faixa de fases | Crescimento | Recompensa no fim da faixa |
|---|---|---|
| 1–30 (M1–M3) | ×1,10 | fase 30 ≈ 1.586 |
| 31–60 (M4–M6) | ×1,07 | fase 60 ≈ 12.073 |
| 61–100 (M7–M10) | ×1,05 | fase 100 ≈ 85.000 |

O MVP (fases 1–20) usa ×1,10 puro, exatamente como o CANON.

### 3.2 Custo de upgrade — `cost(n) = 100 × 1,35^(n−1)`

Convenção: `cost(n)` é o preço para **comprar o nível n** de uma trilha. É a mesma curva do CANON §8 ("custo(n) = 100 × 1,35^n" com n = upgrades já comprados, começando em 0): o 1º upgrade custa 100 × 1,35⁰ = **100 moedas** ✓. Tabela completa nível a nível no §5.2.

Pontos de referência: nv 5 = 332 · nv 10 = 1.489 · nv 15 = 6.678 · nv 20 = 29.946 · nv 30 = 602.115. Custo acumulado de UMA trilha até o nv 10 ≈ 5.459; até o nv 20 ≈ 115.221; até o nv 30 ≈ 2.322.443. Com 8 trilhas, o ralo total é ~18,6 M de moedas — anos de objetivo sem nunca "acabar o jogo".

### 3.3 XP e níveis de jogador 1–20

XP de vitória = `10 + 5 × fase` (fase 1 = 15 XP; fase 20 = 110 XP; boss de mundo +30 XP fixos). **Fórmula e limiares nv 2–6 idênticos ao doc 02 §4.4 — fonte única da curva de XP no pacote.** Com ela, os desbloqueios do CANON §8 caem exatamente nos marcos de pacing do CANON §16: nv 2 fecha na vitória da fase 5 (125 XP acumulados ≥ 120 → Upgrades), nv 3 na fase 7 (240 ≥ 220 → Baús, junto do baú grande), nv 4 na fase 10 (405 ≥ 380 → Loja completa). Os limiares de nv 7–20 estendem a curva no mesmo ritmo, calibrados pela simulação do §7.

| Nível | XP acumulado | Desbloqueio | Momento esperado¹ |
|---|---|---|---|
| 1 | 0 | Jogar, mapa, tela de tropas | — |
| 2 | 120 | **Upgrades** (4 trilhas MVP) — CANON §8 | Fase 5 (D1) — CANON §16 |
| 3 | 220 | **Baús** (slot de abertura + 1 baú grátis/dia) | Fase 7 (D1) — CANON §16 |
| 4 | 380 | **Loja completa** (gemas, skins, ofertas) | Fase 10 (D1) — CANON §16 |
| 5 | 550 | **Passe de Temporada** | Fase 12–13 (fim do D1) |
| 6 | 750 | **Eventos** (diário/semanal/ranking) | D2 |
| 7 | 1.000 | Trilha **Velocidade** | D2–3 |
| 8 | 1.350 | Trilha **Chance Crítica** | D3 |
| 9 | 1.800 | Trilha **Dano contra Boss** | D4 |
| 10 | 2.350 | Trilha **Resistência a Obstáculos** + baú Épico de presente | D5–6 |
| 11 | 3.000 | Missões diárias avançadas (3 → 5/dia) | D7 |
| 12 | 3.800 | Replay com modificadores (+50% recompensa, +25% dificuldade) | D9 |
| 13 | 4.800 | Skins Raras na loja | D12 |
| 14 | 6.000 | **Desafio do Boss** (re-lutar bosses de mundo por Shards) | D15 |
| 15 | 7.400 | Missão semanal épica (baú Raro garantido) | D18 |
| 16 | 9.000 | Skins Épicas na loja | D22 |
| 17 | 10.800 | Contrato de baú (escolhe 1 tropa favorecida no próximo Épico) | D27 |
| 18 | 12.800 | Skins Lendárias na loja | D33 |
| 19 | 15.000 | Missões de elite (+50% gemas nas diárias) | D40 |
| 20 | 17.400 | Título "Comandante Mutante" + baú Lendário de presente | D50 |

¹ Estimativa do jogador F2P ativo da simulação do §7. Recompensa de level-up: **10 gemas** (+ baú Épico no nv 10, baú Lendário no nv 20).

**Supply não é recompensa de nível de jogador.** A elevação 60 → 300 do CANON §3.2 é implementada como **trilha de upgrade de Supply** (§5.5), alinhada ao CANON §15 e ao backlog do doc 13 (épico E1, P1). No MVP o Supply é fixo em 60 (CANON §15).

---

## 4. Baús — tipos, conteúdo e drop tables

Cada baú entrega **moedas + N "pacotes" de fragmentos** (+ gemas nos tiers altos). Um pacote: sorteia a raridade na tabela do baú → sorteia 1 tropa daquela raridade dentro do pool desbloqueável do mundo atual → entrega a quantidade fixa de fragmentos da raridade (**Comum 10 · Raro 5 · Épico 2 · Lendário 1**).

| Tipo | Moedas (× Mb) | Pacotes | Comum | Raro | Épico | Lendário | Garantia | Gemas |
|---|---|---|---|---|---|---|---|---|
| **Comum** | 60–100 | 3 | 85% | 13% | 1,8% | 0,2% | — | — |
| **Raro** | 250–400 | 8 | 65% | 28% | 6% | 1% | ≥1 pacote Raro | — |
| **Épico** | 600–900 | 15 | 45% | 38% | 14,5% | 2,5% | ≥1 pacote Épico | 20 |
| **Lendário** | 1.500–2.500 | 25 | 30% | 40% | 22% | 8% | ≥1 pacote Lendário | 80 |
| **De Mundo** | 5 × recompensa da fase do boss | 10 | 40% | 35% | 20% | 5% | 10 fragmentos da tropa-destaque do próximo mundo (teaser) | 15 |

**Fontes de cada tipo**

| Tipo | Fontes |
|---|---|
| Comum | 30% das vitórias em fase normal · baú grátis diário (1/dia; 2º via rewarded — CANON §11) · 3ª missão diária completada |
| Raro | Streak de 5 vitórias sem derrota · loja (300 gemas — CANON §8) · trilha grátis do Passe · missão semanal épica (nv 15) |
| Épico | Fase 10, 1ª vez (CANON §16) · top 50% do ranking semanal · loja (900 gemas) · level-up nv 10 |
| Lendário | Loja (2.400 gemas) · top 3 do ranking semanal · último tier do Passe premium · level-up nv 20 · pity |
| De Mundo | 1ª vitória na fase-boss de cada mundo — MVP: fases 7, 14 e 20 (CANON §15/§16); release: fase 10 de cada mundo |

**Regras de proteção (anti-frustração e anti-P2W):**

- **Pity de Lendário:** contador global de pacotes; após **50 pacotes** sem Lendário, o próximo pacote Raro+ é promovido a Lendário. O contador conta igualmente em baús grátis e comprados — prova material do "baús grátis dropam lendárias" (CANON §11).
- **MVP:** o roster de 5 tropas (CANON §15) não tem Lendárias; sorteios "Lendário" são rebaixados para Épico (Gigante) e o pity fica suspenso até o release completo.
- **Sem duplicata inútil:** tropa no nv 10 máx. converte fragmentos em moedas (1 = 10) com animação própria.
- Drop tables, contagem de pacotes e moedas são chaves de Remote Config (§9).

---

## 5. Sistema de Upgrades — 8 trilhas (entregável 13)

### 5.1 Regras gerais (CANON §9)

- 8 trilhas: **Dano inicial · Vida inicial · Velocidade · Multiplicador de Recompensa · Exército inicial · Chance Crítica · Dano contra Boss · Resistência a Obstáculos**.
- Efeito: **+5% por nível** (Exército inicial: **+1 unidade a cada 2 níveis**). Custo: `100 × 1,35^(n−1)` para o nível *n* (≡ CANON `100 × 1,35^n` com n contando de 0).
- **MVP usa 4 trilhas:** Dano inicial, Vida inicial, Exército inicial, Multiplicador de Recompensa (CANON §9). As outras 4 desbloqueiam por nível de jogador (7/8/9/10 — §3.3), criando 4 "momentos de novidade" na primeira semana.
- Compra instantânea, sem timer (timers existem só na evolução de tropas, §6). Botão de upgrade mostra sempre: custo, efeito atual → próximo, e Δ ("+5% dano").
- Nível máximo de design: **30 por trilha** (validado nesta tabela); tecnicamente aberto via Remote Config.

### 5.2 Tabela nível → custo → efeito acumulado (níveis 1–30)

"Efeito acumulado" = +5% × nível, válido para as 7 trilhas percentuais (forma de aplicação por trilha no §5.3). Coluna final = unidades extras da trilha Exército inicial (`⌊n/2⌋`).

| Nível | Custo do nível | Custo acumulado | Efeito acumulado | Exército: un. extras |
|---|---|---|---|---|
| 1 | 100 | 100 | +5% | 0 |
| 2 | 135 | 235 | +10% | +1 |
| 3 | 182 | 417 | +15% | +1 |
| 4 | 246 | 663 | +20% | +2 |
| 5 | 332 | 995 | +25% | +2 |
| 6 | 448 | 1.444 | +30% | +3 |
| 7 | 605 | 2.049 | +35% | +3 |
| 8 | 817 | 2.866 | +40% | +4 |
| 9 | 1.103 | 3.970 | +45% | +4 |
| 10 | 1.489 | 5.459 | +50% | +5 |
| 11 | 2.011 | 7.470 | +55% | +5 |
| 12 | 2.714 | 10.184 | +60% | +6 |
| 13 | 3.664 | 13.848 | +65% | +6 |
| 14 | 4.947 | 18.795 | +70% | +7 |
| 15 | 6.678 | 25.474 | +75% | +7 |
| 16 | 9.016 | 34.490 | +80% | +8 |
| 17 | 12.171 | 46.661 | +85% | +8 |
| 18 | 16.431 | 63.092 | +90% | +9 |
| 19 | 22.182 | 85.275 | +95% | +9 |
| 20 | 29.946 | 115.221 | +100% | +10 |
| 21 | 40.427 | 155.648 | +105% | +10 |
| 22 | 54.577 | 210.225 | +110% | +11 |
| 23 | 73.679 | 283.904 | +115% | +11 |
| 24 | 99.466 | 383.371 | +120% | +12 |
| 25 | 134.280 | 517.650 | +125% | +12 |
| 26 | 181.278 | 698.928 | +130% | +13 |
| 27 | 244.725 | 943.653 | +135% | +13 |
| 28 | 330.378 | 1.274.317 | +140% | +14 |
| 29 | 446.011 | 1.720.328 | +145% | +14 |
| 30 | 602.115 | 2.322.443 | +150% | +15 |

### 5.3 Aplicação do efeito por trilha

| Trilha | O que o +5%/nível faz | Cap / regra especial | Justificativa |
|---|---|---|---|
| Dano inicial | +5% DPS base de todas as unidades (aditivo; nv 30 = +150%) | Sem cap | Stat principal; a dificuldade das fases (doc 06) assume esta curva |
| Vida inicial | +5% HP base de todas as unidades | Sem cap | Par do dano; protege da atrição na corrida |
| Velocidade | +5% velocidade de corrida e de ataque | Velocidade de corrida capada em **+50%** (nv 10); do nv 11 em diante o ganho aplica só à velocidade de ataque | Corrida mais rápida que isso quebra a legibilidade dos portais e a duração-alvo de 45–75 s (CANON §1/§2) |
| Mult. de Recompensa | +5% moedas ganhas em qualquer fonte de fase (vitória, replay, sobreviventes) | Sem cap (nv 30 = ×2,5) | Trilha "investimento"; não dá poder direto — segura o jogador racional engajado |
| Exército inicial | +1 unidade inicial a cada 2 níveis (nv 30 = começa com 16) | Limitado pelo Supply | Cada unidade inicial passa por TODOS os multiplicadores da fase — efeito composto |
| Chance Crítica | +5% de chance de golpe crítico (dano ×2) por nível até **50% no nv 10**; do nv 11 em diante, +5% no multiplicador do crítico por nível (×2,05 → ×3,00 no nv 30) | Chance capada em 50% | Mantém "+5% por nível" sempre verdadeiro sem ultrapassar 100% de chance |
| Dano contra Boss | +5% de dano aplicado apenas na arena do boss | Sem cap | Alavanca cirúrgica: bosses de mundo têm win rate alvo ~55% (CANON §12) |
| Resistência a Obstáculos | Perdas por obstáculo multiplicadas por 0,95 por nível (composto: nv 30 ⇒ perde só 21% do normal) | Nunca chega a imunidade | Obstáculo nunca pode virar irrelevante (pilar 2 do CANON); curva composta evita o cap duro |

### 5.4 O jogador racional — qual trilha priorizar em cada momento

Heurística de leitura do jogador (reforçada pela tela de derrota, que mostra o **motivo** — BRIEF, tela 5): morreu **na corrida** → Vida/Resistência; morreu **no boss** → Dano/Dano contra Boss; venceu fácil → Recompensa/Exército.

| Momento | Estado típico | Ordem racional | Por quê (a conta) |
|---|---|---|---|
| Fases 5–7 (D1) | 600–1.000 moedas; só 4 trilhas | **Exército 2 > Dano 2 > Vida 1** | +1 unidade inicial passa por ~2 portais multiplicadores médios (×2,5 cada) ⇒ vale ~6 unidades no boss por 235 moedas. Melhor ROI absoluto do jogo |
| Fases 8–12 (D1) | Win rate alvo 85% | **Dano ↔ Vida alternados (nv 4–6); Recompensa nv 3+ se payback < 3 dias** | Dano e Vida multiplicam entre si na prática (sobreviver = continuar causando dano); alternar mantém o produto (1+D)(1+V) máximo por moeda gasta |
| Fases 13–20 (D2–D4) | Renda ~1.500–3.000/dia | **Recompensa nv 6–8 primeiro; depois Dano contra Boss (nv 9 de jogador) antes das fases 14 e 20** | Payback de Recompensa: `cost(n) ÷ (0,05 × renda diária)`. Ex.: nv 6 = 448 ÷ (0,05 × 2.600) ≈ **3,4 dias** ⇒ compra. Bosses de mundo são o ponto de falha (alvo 55%) ⇒ Dano contra Boss tem o maior Δ de win rate por moeda na véspera |
| Pós-fase 20 / farm (D5+) | Replays + eventos | **Regra do mais barato** entre Dano, Vida, Crítica (paridade Dano↔Crítica: os efeitos multiplicam, então equalizar níveis maximiza o produto) | Com custos iguais por nível, o nível mais barato disponível é sempre o maior ganho marginal % por moeda |
| Mundos com gimmick de pista (M5 pedras, M6 piso) | Derrotas na corrida | **Resistência a Obstáculos 3–5 níveis pontuais** | Curva composta 0,95^n dá os maiores ganhos nos primeiros níveis (nv 5 ⇒ −23% de perdas) |
| Grinder de replay | Farm intencional | **Velocidade + Recompensa** | Velocidade ↑ fases/hora; Recompensa ↑ moedas/fase ⇒ renda/hora cresce ~10%/nível combinado |

**Regra de ouro exibida como dica de loading:** "Perdeu na corrida? Vida. Perdeu no boss? Dano. Ganhou fácil? Recompensa."

### 5.5 Trilha de Supply (pós-MVP) — implementa o CANON §3.2

**Decisão registrada:** a elevação do Supply de **60 → 300** (CANON §3.2, "upgrades de meta elevam até 300") é uma **trilha de upgrade comprada com moedas** — não uma recompensa de nível de jogador. É exatamente a trilha prevista pelo CANON §15 ("sem trilha de upgrade de Supply no MVP" — a trilha existe, apenas fora do escopo do MVP) e pelo doc 13 (épico E1: "Trilha de upgrade de Supply (60 → 300)", P1; entra na F3 junto das trilhas 5–8 — expansão item 2).

- **12 níveis, +20 de Supply por nível:** 60 base → 300 no nv 12.
- **Custo:** `300 × 1,35^(n−1)` — mesmo crescimento canônico ×1,35 das demais trilhas (CANON §8), com base 3× maior porque Supply é o stat de maior impacto sistêmico (é a válvula anti-"maior número" do CANON §3.2).
- Compra instantânea, sem timer, mesma UI do §5.1. **Não conta entre as 8 trilhas do CANON §9** — é trilha à parte, com curva própria.
- No MVP: a trilha não aparece na tela de Upgrades e o limite fica fixo em 60 (CANON §15).

| Nível da trilha | Supply | Custo do nível | Custo acumulado |
|---|---|---|---|
| 0 (base) | 60 | — | — |
| 1 | 80 | 300 | 300 |
| 2 | 100 | 405 | 705 |
| 3 | 120 | 547 | 1.252 |
| 4 | 140 | 738 | 1.990 |
| 5 | 160 | 996 | 2.986 |
| 6 | 180 | 1.345 | 4.331 |
| 7 | 200 | 1.816 | 6.147 |
| 8 | 220 | 2.452 | 8.599 |
| 9 | 240 | 3.310 | 11.909 |
| 10 | 260 | 4.468 | 16.377 |
| 11 | 280 | 6.032 | 22.409 |
| 12 | 300 | 8.143 | 30.552 |

Custo acumulado até o teto ≈ **30.552 moedas**: meta de médio prazo da 2ª–3ª semana, na janela em que o jogador F2P ativo acumulou ~35 mil moedas (§7.1) — o teto de 300 nunca chega "de graça" nem trivializa a conversão de excedente em moedas.

---

## 6. Evolução de tropas (ralo de Fragmentos + Moedas)

Custo nv *n* → *n+1* (CANON §8): `10 × 2^(n−1)` fragmentos da própria tropa + `100 × 2^(n−1) × raridade` moedas (Comum ×1 · Raro ×2 · Épico ×4 · Lendário ×8). Cada nível de tropa: +10% HP e +10% DPS sobre o baseline (tabela de stats no doc 03). Nível máximo 10.

| Evolução | Shards | Moedas (Comum) | Moedas (Raro) | Moedas (Épico) | Moedas (Lendário) | Timer¹ |
|---|---|---|---|---|---|---|
| 1→2 | 10 | 100 | 200 | 400 | 800 | — |
| 2→3 | 20 | 200 | 400 | 800 | 1.600 | — |
| 3→4 | 40 | 400 | 800 | 1.600 | 3.200 | — |
| 4→5 | 80 | 800 | 1.600 | 3.200 | 6.400 | — |
| 5→6 | 160 | 1.600 | 3.200 | 6.400 | 12.800 | 10 min |
| 6→7 | 320 | 3.200 | 6.400 | 12.800 | 25.600 | 20 min |
| 7→8 | 640 | 6.400 | 12.800 | 25.600 | 51.200 | 30 min |
| 8→9 | 1.280 | 12.800 | 25.600 | 51.200 | 102.400 | 45 min |
| 9→10 | 2.560 | 25.600 | 51.200 | 102.400 | 204.800 | 60 min |
| **Total 1→10** | **5.110** | **51.100** | **102.200** | **204.400** | **408.800** | — |

¹ **Timer de mutação** (release completo; MVP sem timers): a tropa continua 100% utilizável no nível atual enquanto "muta" — nunca bloqueia jogar. Pular: rewarded ad (−15 min, é o "acelerar upgrade" do CANON §11) ou 1 gema/2 min. É o gatilho de retorno via notificação FCM ("Seu Mago terminou de mutar!").

---

## 7. Simulação de jogador F2P — dias 1, 3 e 7

**Premissas** (coerentes com o doc 02 e com as metas do CANON §12: sessão ≥ 8 min, ≥ 6 fases/sessão, rewarded ≥ 35% dos DAU): jogador ativo; D1 = 3 sessões (28 min), depois ~2 sessões/dia de 8 min; dobra com rewarded ~45% das vitórias; replay paga 40% de moedas e 50% de XP; completa as 3 missões diárias; valores de renda já incluem o Multiplicador de Recompensa médio do período; arredondados à dezena.

### 7.1 Entradas de moedas (acumulado)

| Linha de receita | Fim do D1 | Fim do D3 | Fim do D7 |
|---|---|---|---|
| 1ª vitória de fases | 2.140 (f1–12) | 4.560 (f1–18) | 5.730 (f1–20) |
| Replays (40%) | 0 | 2.400 (20 replays) | 11.000 (60 replays) |
| Dobras com rewarded | 1.370 (6 dobras) | 3.300 (17) | 7.000 (32) |
| Missões diárias | 300 | 900 | 2.100 |
| Baús (moedas) | 2.400 | 5.200 | 8.200 |
| Eventos | 0 | 0 | 1.500 |
| **Total ganho** | **6.210** | **16.360** | **35.530** |

### 7.2 Saídas de moedas (acumulado)

| Ralo | Fim do D1 | Fim do D3 | Fim do D7 |
|---|---|---|---|
| Trilhas (níveis comprados → custo §5.2) | Dano 6 + Vida 5 + Exército 5 + Recompensa 5 = **4.430** | Dano 9 + Vida 8 + Exército 7 + Recompensa 7 = **10.930** | Dano 12 + Vida 11 + Exército 10 + Recompensa 11 = **30.580** |
| Evolução de tropas (moedas) | 400 | 1.500 | 3.200 |
| **Total gasto** | **4.830** | **12.430** | **33.780** |
| **Saldo de moedas** | **1.380** | **3.930** | **1.750** |

### 7.3 Estado do jogador ao fim de cada dia

| Métrica | Fim do D1 | Fim do D3 | Fim do D7 |
|---|---|---|---|
| Minutos jogados (acum.) | 28 | 60 | 120 |
| Corridas (vitórias + derrotas) | 14 (12+2) | 44 (38+6) | 92 (80+12) |
| Fase máxima vencida | 12 (M2) | 18 (M3) | 20 (MVP completo no D4) |
| XP acumulado → nível² | 580 → **nv 5** (Passe desbloqueado) | ≈1.730 → **nv 8** | ≈3.600 → **nv 11** |
| Gemas ganhas / gastas / saldo | 150 / 0 / **150** | 265 / 0 / **265** | 500 / 300 (baú Raro no D6) / **200** |
| Fragmentos ganhos (todas as tropas) | 380 | 760 | 1.250 |
| Níveis de tropa (S/A/E/M/G) | 4 / 3 / 2 / 2 / 1 | 4 / 4 / 3 / 3 / 2 | 6 / 5 / 4 / 4 / 3 |
| Trilhas (Dano/Vida/Exército/Recomp.) | 6 / 5 / 5 / 5 | 9 / 8 / 7 / 7 | 12 / 11 / 10 / 11 |
| **Índice de Poder¹** | **179** | **233** | **310** |
| Win rate esperada na fronteira | ~85% (CANON: 85% f4–10) | ~75% (CANON: ~70% meio de mundo) | ~60% (CANON: ~55% fase de boss) |

¹ `IP = 100 × (1 + bônus de dano) × (1 + bônus de vida) × (1 + 0,05 × unidades iniciais extras)`. D1: 1,30×1,25×1,10 = 179. D3: 1,45×1,40×1,15 = 233. D7: 1,60×1,55×1,25 = 310.
² Curva do §3.3 (= doc 02 §4.4). D1: 510 (1ªs vitórias f1–12) + 30 (boss f7) + 10 (2 derrotas) + 30 (3 missões) = 580. D3: 1.035 (f1–18) + 60 (bosses f7/f14) + 30 (6 derrotas) + 90 (9 missões) + ~515 (20 replays a 50% do XP) ≈ 1.730. D7: 1.250 (f1–20) + 90 (3 bosses) + 60 (12 derrotas) + 210 (21 missões) + ~1.990 (60 replays) ≈ 3.600.

**Leituras de design:** (a) o jogador compra upgrade praticamente toda sessão até o D3 — dopamina constante; (b) entre D4 e D7 o intervalo entre compras sobe para 2–4 sessões e o saldo encolhe (1.750 no D7) — é aqui que rewarded de dobra e a oferta inicial (doc 08) têm pico de relevância; (c) o primeiro gasto de gemas F2P acontece no D6 (baú Raro de 300) — gemas são poupança com plano, não pó de fada; (d) poder ×3,1 em 7 dias com fases ~×2,4 mais difíceis mantém a sensação de "estou ficando forte" sem trivializar o desafio.

---

## 8. Anti pay-to-win na economia (CANON §11)

| Garantia | Implementação na economia |
|---|---|
| Tudo que dá poder pode ser obtido grátis | Toda tropa (incl. Lendárias) dropa de baús grátis; pity de 50 pacotes conta igualmente em baús grátis e pagos; gemas F2P (30–50/dia) compram baú Raro a cada ~7–9 dias |
| Pagamento acelera e personaliza | IAP vende moedas/gemas/baús/skins/passe — **nunca vende níveis de trilha nem fragmentos infinitos**; skins são 100% cosméticas |
| Teto prático de aceleração | Curva 1,35^n: um pagante agressivo compra ~8–10 níveis à frente do F2P, não 30 — a vantagem é semanas de antecedência, não outra liga |
| Sem exclusivos de poder pagos | Tropa nova de temporada entra no pool de baús grátis na temporada seguinte (regra fixada também no doc 08) |
| Rewarded sempre opcional | Dobra, revive (1×/fase), baú extra, teste de Lendária e aceleração de mutação têm alternativa grátis (esperar) ou em gemas |
| Competição justa | Ranking de eventos usa brackets por Índice de Poder — pagar sobe de bracket, não esmaga iniciantes |
| Sem dark patterns | Preços absolutos sempre visíveis; probabilidades de baú publicadas na própria UI do baú (também exigência de loja Google/Apple) |

---

## 9. Válvulas de Remote Config (CANON §13 + BRIEF)

Todas com default = valor deste doc; faixa segura = limites para LiveOps sem novo build nem aprovação de design.

| Chave | Default | Faixa segura | O que controla |
|---|---|---|---|
| `reward_base` | 100 | 80–150 | Moedas da fase 1 (BRIEF: "moedas por fase") |
| `reward_growth` | 1,10 | 1,06–1,15 | Crescimento por fase (degraus por faixa, §3.1) |
| `reward_ad_multiplier` | 2,0 | 1,5–3,0 | Dobra do rewarded |
| `replay_reward_pct` | 0,40 | 0,25–0,60 | Valor do farm de replay |
| `survivor_bonus_pct` | 0,10 | 0–0,25 | Bônus de sobreviventes |
| `supply_overflow_coin_rate` | 2 | 1–4 | Moedas por ponto de Supply convertido |
| `upgrade_cost_base` / `upgrade_cost_growth` | 100 / 1,35 | 80–150 / 1,25–1,45 | Curva de custo das trilhas (BRIEF: "preço de upgrades") |
| `upgrade_effect_per_level` | 0,05 | 0,03–0,07 | Efeito por nível das trilhas |
| `supply_track_cost_base` / `supply_track_step` | 300 / +20 | 200–500 / +10 a +30 | Trilha de Supply (§5.5, pós-MVP) |
| `shard_evo_base` / `shard_evo_growth` | 10 / 2,0 | 8–15 / 1,8–2,2 | Custo de evolução de tropa |
| `chest_drop_table_{tier}` (JSON) | Tabelas do §4 | Σ = 100% | Chance de drop (BRIEF) |
| `chest_coin_mult_world` (array) | 1,0 / 1,6 / 2,5… | ±30% | Moedas de baú por mundo |
| `chest_pity_legendary` | 50 pacotes | 30–80 | Pity de Lendário |
| `gem_daily_missions_total` | 40 | 20–60 | Gemas/dia de missões (CANON: 20–40 p/ ativo) |
| `gem_world_boss` | 10 | 5–25 | Gemas do boss de mundo |
| `shop_chest_price_{rare,epic,leg}` | 300 / 900 / 2.400 | ±33% | Preços de baú em gemas |
| `boss_hp_mult` / `boss_dmg_mult` | 1,0 / 1,0 | 0,7–1,3 | Dificuldade dos bosses (BRIEF: "dano dos bosses") |
| `unit_hp_mult` / `unit_dps_mult` | 1,0 / 1,0 | 0,8–1,2 | Vida/dano das tropas (BRIEF: "vida das tropas") |
| `difficulty_curve_offset` | 0 | −2 a +2 fases | Dificuldade global (BRIEF: "dificuldade") |
| `interstitial_min_level` / `interstitial_cooldown_levels` / `interstitial_block_after_losses` | 6 / 3 / 2 | só p/ mais brando | Frequência de anúncios — defaults travados pelo CANON §11 |
| `mutation_timer_minutes` (array nv 6–10) | 10/20/30/45/60 | 0–90 | Timers de mutação (0 = desliga, modo MVP) |
| `event_active_{id}` | false | on/off | Eventos ativos (BRIEF) |
| `xp_per_level_curve` (array) | Limiares do §3.3 | ±25% | Ritmo de desbloqueio de features |

**Telemetria que fecha o ciclo** (eventos do BRIEF usados para calibrar estas válvulas): `level_complete`/`level_fail` (win rate vs alvo §12) · `chest_opened` (raridade real vs tabela) · `unit_upgraded` e `gate_selected` (uso de tropas/portais → preço de fragmento) · `rewarded_ad_completed` (conversão de dobra ≥ 35% dos DAU) · `purchase_completed` (elasticidade de preço de baú). Dashboard de economia no doc 08.

---

## 10. Sem energia — por quê, e o que faz o pacing no lugar

**Decisão canônica (CANON §8): não existe sistema de energia.** Justificativa de produto:

1. **Metas de sessão:** queremos ≥ 8 min e ≥ 6 fases/sessão (CANON §12). Energia corta exatamente as melhores sessões — as do jogador mais engajado.
2. **Modelo de receita é ad-first:** cada fase a mais = mais 1 oportunidade de rewarded (dobra) e, após a fase 6, eventuais interstitials. Travar o jogador é travar o ARPDAU (alvo ≥ US$ 0,08).
3. **Retenção:** D1 ≥ 40% depende de o usuário "encadear fases" (objetivo declarado do MVP no BRIEF). Paywall de tempo no D1 é o anti-padrão número 1 do gênero hybrid-casual.

**O que regula o ritmo no lugar da energia:**

| Mecanismo | Como regula |
|---|---|
| Curva de dificuldade | Win rate alvo decrescente (95% → 55%, CANON §12): a parede natural é "preciso ficar mais forte", não "acabou a bateria" |
| Gap renda ×1,10 vs custo ×1,35 | O tempo entre upgrades cresce sozinho (1 por fase no D1 → 1 a cada 2–4 sessões no D7, §7) — pacing econômico, não físico |
| Retorno decrescente do farm | Replay paga 40%; grind continua possível (respeita o princípio), só rende menos que avançar |
| Timers de mutação (nv 6+ de tropa) | Criam motivo de retorno (FCM) sem nunca impedir de jogar — a tropa segue usável durante a mutação |
| Ritmo diário de fontes | Missões diárias, baú grátis e evento semanal concentram "motivos de login", substituindo o papel de retenção que a energia tentaria cumprir |

**Resultado esperado:** o jogador para de jogar porque a próxima vitória pede mais poder (e ele já sabe COMO obtê-lo: upgrade, evolução, baú de amanhã) — nunca porque o jogo o expulsou. "Sempre dar motivo para mais uma fase" (BRIEF, regras de produto).
