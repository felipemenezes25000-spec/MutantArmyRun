# 03 — Sistema de Unidades · Mutant Army Run

> Entregável 9 do pacote de design. Fonte da verdade: `CANON.md` (§4 Elementos, §5 Tropas, §8 Economia). Requisitos: `BRIEF.md` (seção "Unidades").
> Interfaces com: doc 04 (Sistema de Portais — gates de classe), doc 05 (Sistema de Bosses — janelas de DPS), doc 07 (Economia & Upgrades — fragmentos/baús e trilhas de meta).
> Todos os identificadores de código em inglês (glossário CANON §14): Unit, Supply, Shard, Gate, Chest.

---

## 1. Princípios do sistema

1. **Supply é o cérebro do jogo (Pilar 2).** Cada unidade custa Supply (CANON §3.2 e §5). O orçamento de stats é proporcional ao Supply — logo, nenhuma tropa é "objetivamente melhor", apenas melhor *para aquele boss, aquele mundo, aquela composição*.
2. **Stats derivam de uma única âncora.** Soldado nível 1 = HP 10 · DPS 2 · 5 m/s (CANON §5). Tudo escala a partir daí por uma fórmula auditável (§2 abaixo). Nenhum número da tabela mestra é "no olho".
3. **Raridade é prêmio pequeno, não poder bruto.** Comum→Lendário ganha no máximo +20% de eficiência por ponto de Supply (CANON §5). Lendárias vencem por *habilidade* e *espetáculo*, não por quebrar a conta — isso protege o anti pay-to-win (CANON §11).
4. **Toda unidade é legível em 3 s (Pilar 1).** Silhueta, cor de raridade (CANON §8) e papel precisam ser óbvios a 2 m de distância de câmera. A evolução visual (§4) muda silhueta nos marcos, nunca a leitura do papel.
5. **Habilidades têm número, não adjetivo.** "Cura aliados" vira "cura 8 HP/s no aliado mais ferido em 5 m". Tudo que está na tabela é implementável direto no `UnitConfigSO`.

---

## 2. A conta do balanceamento — Orçamento de Combate (OC)

### 2.1 Fórmula

Cada tropa nível 1 tem um **Orçamento de Combate** que ela deve gastar integralmente entre HP, dano efetivo e utilidade:

```
OC_alvo = 12 × Supply × R

onde 12 = OC do Soldado (HP 10 + DPS 2) por 1 de Supply  (âncora CANON §5)
R = prêmio de raridade:  Comum 1,00 · Raro 1,10 · Épico 1,15 · Lendário 1,20
```

O gasto real é:

```
OC_real = HP + DPSe + U
DPSe (DPS efetivo) = DPS nominal × FP (Fator de Papel)
U = valor em pontos de OC das habilidades não ofensivas (cura, revive, esquiva, voo…)
```

**Regra de aceitação:** `|OC_real − OC_alvo| ≤ 2%` para toda tropa (a tabela mestra em §3.1 prova isso linha a linha). O prêmio por raridade fica dentro dos 10–20% exigidos pelo CANON: 12,0 → 13,2 → 13,8 → 14,4 pontos de OC por Supply.

### 2.2 Catálogo de Fator de Papel (FP)

O FP converte DPS "de ficha" em valor real de combate. Valores de partida (calibrados por playtest, ver §9):

| Fator | Valor | Racional |
|---|---|---|
| Alvo único, corpo a corpo (1–2 m) | 1,00 | referência (Soldado) |
| Alcance médio (4–6 m) | +0,15 | ataca antes, morre depois |
| Alcance longo (8–10 m) | +0,25 | fica fora de pisões e áreas de boss |
| Área pequena (cone/arco, ~1,5 alvos médios) | ×1,2–1,5 | valor real medido nas hordas da corrida |
| Área média (~2 alvos médios) | ×1,6–1,75 | Mago |
| Área grande (≥4 m, ~2,5 alvos) | ×2,0 | Dragão |
| Crítico | ×(1 + chance×(mult−1)) | Ninja: 1 + 0,20×2 = 1,40 |
| Efeito caótico (Alien, CANON §4) | ×1,3 | valor esperado dos 4 efeitos |

**Importante (honestidade da conta):** contra um boss sozinho, área não vale nada — o que conta é o DPS nominal. É exatamente isso que cria escolhas reais: a tropa de área "paga" pelo valor na corrida e "devolve" menos no boss. A prova do Pilar 2 (§8) usa só DPS nominal contra o boss.

### 2.3 Utilidade (U)

| Habilidade | Conversão para OC |
|---|---|
| Cura | 1 HP/s curado ≈ **3 OC** (cura estende o uptime de DPS de todo o exército — validado na simulação do build "Coração da Montanha", §7) |
| Revive (Necromante) | OC médio devolvido por luta ≈ 3 ciclos × 22 OC = 66 |
| Torreta (Engenheiro) | OC da torreta como se fosse unidade: HP 30 + DPSe 32,5 ≈ 62 |
| Esquiva de armadilha | 8 OC para 75% (Ninja); 2 OC para 30% + velocidade (Corredor) |
| Voo (ignora obstáculos de chão) | 18 OC (Dragão — elimina ~todas as perdas por obstáculo) |
| Imunidades/armadura passiva | 8 OC (Robô: imune a Veneno + −20% físico) |
| Fúria do Demônio (+5%/abate, máx +50%) | ≈ +20% de DPSe médio ≈ 32 OC |

### 2.4 Exemplo passo a passo — Mago

```
Supply 4 (CANON §5), Raro → OC_alvo = 12 × 4 × 1,10 = 52,8
FP = alcance médio (+0,15) combinado com área média → 1,75
Alocação: HP 25 · DPS nominal 16 → DPSe = 16 × 1,75 = 28 · U 0
OC_real = 25 + 28 + 0 = 53,0 → desvio +0,4% ✓
```

---

## 3. Tabela mestra — 19 tropas (nível 1)

★ = tropa do MVP (CANON §5: Soldado, Arqueiro, Escudeiro, Mago, Gigante).

### 3.1 Stats de combate e prova do orçamento

| Tropa (`unit_id`) | Raridade | Supply | HP | DPS | FP | DPSe | U | OC real | OC alvo | Δ |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| ★ Soldado (`unit_soldier`) | Comum | 1 | 10 | 2 | 1,00 | 2,0 | 0 | 12,0 | 12,0 | 0,0% |
| ★ Arqueiro (`unit_archer`) | Comum | 2 | 14 | 8 | 1,25 | 10,0 | 0 | 24,0 | 24,0 | 0,0% |
| ★ Escudeiro (`unit_shieldbearer`) | Comum | 3 | 30 | 4 | 1,00 | 4,0 | 2 | 36,0 | 36,0 | 0,0% |
| Corredor (`unit_runner`) | Comum | 1 | 7 | 3 | 1,00 | 3,0 | 2 | 12,0 | 12,0 | 0,0% |
| ★ Mago (`unit_mage`) | Raro | 4 | 25 | 16 | 1,75 | 28,0 | 0 | 53,0 | 52,8 | +0,4% |
| Ninja (`unit_ninja`) | Raro | 3 | 18 | 10 | 1,40 | 14,0 | 8 | 40,0 | 39,6 | +1,0% |
| Lança-Chamas (`unit_flamethrower`) | Raro | 4 | 26 | 18 | 1,50 | 27,0 | 0 | 53,0 | 52,8 | +0,4% |
| Tropa Glacial (`unit_glacial`) | Raro | 4 | 28 | 14 | 1,15 | 16,1 | 8 | 52,1 | 52,8 | −1,3% |
| Médico (`unit_medic`) | Raro | 4 | 24 | 4 | 1,15 | 4,6 | 24 | 52,6 | 52,8 | −0,4% |
| Robô (`unit_robot`) | Épico | 8 | 66 | 30 | 1,15 | 34,5 | 8 | 108,5 | 110,4 | −1,7% |
| ★ Gigante (`unit_giant`) | Épico | 12 | 120 | 40 | 1,20 | 48,0 | 0 | 168,0 | 165,6 | +1,4% |
| Necromante (`unit_necromancer`) | Épico | 8 | 32 | 10 | 1,25 | 12,5 | 66 | 110,5 | 110,4 | +0,1% |
| Engenheiro (`unit_engineer`) | Épico | 8 | 36 | 10 | 1,15 | 11,5 | 62 | 109,5 | 110,4 | −0,8% |
| Alien (`unit_alien`) | Épico | 8 | 60 | 38 | 1,30 | 49,4 | 0 | 109,4 | 110,4 | −0,9% |
| Dragão (`unit_dragon`) | Lendário | 20 | 130 | 70 | 2,00 | 140,0 | 18 | 288,0 | 288,0 | 0,0% |
| Titã (`unit_titan`) | Lendário | 25 | 260 | 80 | 1,25 | 100,0 | 0 | 360,0 | 360,0 | 0,0% |
| Anjo de Guerra (`unit_warangel`) | Lendário | 18 | 120 | 75 | 1,40 | 105,0 | 36 | 261,0 | 259,2 | +0,7% |
| Demônio Mutante (`unit_mutantdemon`) | Lendário | 20 | 100 | 130 | 1,20 | 156,0 | 32 | 288,0 | 288,0 | 0,0% |
| Mecha Supremo (`unit_supreme_mecha`) | Lendário | 25 | 200 | 115 | 1,40 | 161,5 | 0 | 361,5 | 360,0 | +0,4% |

Notas de desvio: Gigante fecha em +1,4% como **compensação pela velocidade 3,5 m/s** (chega ~1,5 s atrasado na arena); Robô fecha em −1,7% porque a imunidade a Veneno vale mais nos mundos 4 e 8 do que os 8 OC tabelados. Demônio: HP 100 + DPSe 156 + Fúria 32 = 288 — glass cannon assumido.

### 3.2 Atributos táticos

| Tropa | Vel. (m/s) | Alcance (m) | Fraqueza | Vantagem | Habilidade especial (números) |
|---|---:|---:|---|---|---|
| ★ Soldado | 5,0 | 1,5 | morre em bloco para dano em área | custo 1 — escala perfeito com gates ×N | **Coesão:** +2% DPS por cada 10 Soldados vivos (máx +20%) |
| ★ Arqueiro | 5,0 | 8,0 | 7 HP/Supply — morre para projéteis em área | ataca antes de todo mundo; fica fora dos pisões | **Tiro Certeiro:** a cada 5º tiro, dano ×2 |
| ★ Escudeiro | 4,5 | 1,0 | DPS 1,3/Supply — quase não fere boss | mantém frágeis vivos | **Muralha:** intercepta 50% do dano de até 3 aliados de Supply ≤2 num raio de 3 m |
| Corredor | 6,5 | 1,5 | HP 7 — qualquer área o apaga | mais rápido do jogo; Supply 1 | **Sprint:** esquiva passiva de 30% contra armadilhas e projéteis |
| ★ Mago | 4,5 | 6,0 | frágil e lento em combate | derrete hordas na corrida | **Nova Arcana:** a cada 8 s, pulso em área (3 m) com 3× o DPS (48 de dano) |
| Ninja | 6,0 | 1,5 | corpo a corpo — toma área melee do boss | rei dos mundos de armadilha (M3, M7) | **Golpe Sombrio:** 20% de crítico ×3; esquiva 75% de armadilhas |
| Lança-Chamas | 5,0 | 3,0 | −50% vs bosses de Fogo (M5); alcance curto | +50% vs Gelo (M6) — CANON §4 | **Cone Ígneo:** atinge todos num cone de 3 m; aplica queimadura (Fogo) |
| Tropa Glacial | 4,5 | 5,0 | −50% vs bosses de Gelo (M6 resiste) | +50% vs Raio (M9); controla ritmo | **Rajada Gélida:** lentidão de 30% por 2 s, não acumula (CANON §4) |
| Médico | 5,0 | 5,0 | DPS 4 — inútil sozinho | multiplica lutas longas (todo boss) | **Triagem:** cura 8 HP/s no aliado mais ferido em 5 m (prioriza maior HP máx.) |
| Robô | 4,5 | 5,0 | conduz Raio: recebe +50% de dano elétrico | imune a Veneno (CANON §5) — M4 e M8 | **Blindagem:** imune a Veneno; −20% de dano físico recebido |
| ★ Gigante | 3,5 | 2,0 | lento; ímã de ataques telegrafados | aguenta 4 pisões de 25 (HP 120) | **Pancada Sísmica:** golpe em área de 2 m (FP 1,2) |
| Necromante | 4,0 | 8,0 | HP 32; inútil sem cadáveres baratos | devolve exército durante a luta | **Levantar:** a cada 6 s revive até 3 tropas caídas (Supply total ≤6) com 60% HP / 100% DPS |
| Engenheiro | 5,0 | 4,0 | valor ~zero durante a corrida | DPS "grátis" e permanente na arena | **Torreta Mk-1:** constrói em 1,5 s na arena do boss: HP 30 · DPS 25 · alcance 8 m |
| Alien | 5,5 | 4,0 | variância (Veneno = 0 vs máquinas/mortos-vivos, CANON §4) | quebra qualquer resistência na média | **Caos:** 25% de chance por ataque de efeito aleatório (queimar/congelar/encadear/envenenar) |
| Dragão | 6,0 (voo) | 6,0 | 1/3 do Supply inicial (60) em 1 corpo | voo ignora obstáculos de chão (CANON §5) | **Sopro Devastador:** área grande (FP 2,0) |
| Titã | 3,0 | 2,5 | mais lento do jogo; chega por último | âncora de 260 HP | **Inabalável:** imune a empurrão/lentidão; provoca os ataques telegrafados do boss (taunt) |
| Anjo de Guerra | 5,5 | 5,0 | recebe +50% de Sombra (CANON §4) | +50% vs Sombra e mortos-vivos — M2 inteiro | **Aura Solar:** cura 12 HP/s em 5 m; ataques de Luz curam 2% do HP do exército (CANON §4) |
| Demônio Mutante | 5,5 | 2,0 | recebe +50% de Luz; HP baixo p/ lendário | maior DPS nominal do jogo (130) | **Fúria Crescente:** +5% de dano por abate, máx +50% |
| Mecha Supremo | 3,5 | 10,0 | lento; conduz Raio (+50% recebido) | maior alcance do jogo; mata antes de apanhar | **Arsenal Total:** laser contínuo (80 DPS, 10 m) + salva de mísseis a cada 4 s (35 DPS médio, área 2 m) |

### 3.3 Regra da velocidade — guia vs combate

Para o runner nunca "quebrar" com tropas lentas:

- **Na corrida**, o exército inteiro avança na **velocidade do guia** — constante da fase definida no `LevelConfigSO` (5 m/s no M1, até 6,5 m/s no M10). O atributo de velocidade da unidade **não** afeta o avanço da pista.
- O atributo de velocidade vale para: **reposicionamento na arena do boss** (quem chega primeiro ao alvo), **resposta ao swipe lateral** (tropas lentas "derrapam" 0,1–0,3 s ao trocar de lane — risco real contra obstáculos) e **janela de esquiva** (Corredor/Ninja se realinham mais rápido).
- Implementação: `CrowdManager` move o aglomerado; `UnitManager` aplica `unitSpeed` apenas em `BossArenaState` e no offset de formação.

**Justificativa:** mantém o Pilar 1 (a multidão é um corpo só, legível) sem apagar o trade-off "forte e lento" exigido pelo BRIEF (Gigante/Titã chegam ~1,5–2,5 s atrasados numa arena de 12 m — em lutas de 10–20 s, isso é 10–20% do DPS perdido, já compensado no OC).

---

## 4. Evolução visual por marco de nível

Marcos canônicos deste doc: **nv 3** (acessório), **nv 5** (troca de arma/material), **nv 7** (efeito de partícula permanente), **nv 10** (forma "Apex": silhueta alterada + VFX + título no card). Skins (10 recolors+acessório do Soldado no MVP, CANON §15) ocupam **slot separado** — skin troca paleta/adereço, marco troca silhueta; os dois compõem sem conflito. Sem violência gráfica em nenhum estágio (CANON §1).

| Tropa | nv 3 | nv 5 | nv 7 | nv 10 — forma Apex |
|---|---|---|---|---|
| Soldado | capacete de combate | armadura azul + espada maior | capa + lâmina brilhante | **Veterano Mutante:** braço direito mutado gigante |
| Arqueiro | aljava dupla | arco composto verde | flechas de energia | **Olho da Tempestade:** arco vivo com olho mutante |
| Escudeiro | escudo com cravos | escudo-torre | barreira de energia no escudo | **Bastião:** escudo duplo, anda como fortaleza |
| Corredor | faixas aerodinâmicas | botas a jato | rastro de luz | **Estampido:** pernas biônicas + sonic boom visual |
| Mago | cristal no cajado | manto estrelado | 3 orbes orbitando | **Arquimago:** terceira mão espectral conjurando |
| Ninja | lâmina dupla | traje preto-carmesim | pós-imagem ao correr | **Mestre da Névoa:** vira fumaça entre golpes |
| Lança-Chamas | tanque de combustível maior | bocal duplo | chamas azuis | **Pira Viva:** armadura ígnea semiderretida |
| Tropa Glacial | cristais nos ombros | lança de gelo | aura de nevasca | **Geleira:** coração de gelo visível no peito |
| Médico | bolsa médica reforçada | drone de cura | aura verde pulsante | **Cirurgião-Exo:** exotraje com 4 braços cirúrgicos |
| Robô | placas extras | canhão de ombro | olhos-laser | **Modo Tanque:** transforma o torso em blindado |
| Gigante | manoplas de pedra | armadura de placas | runas brilhantes no corpo | **Dorso Vulcânico:** fissuras de magma nas costas |
| Necromante | grimório flutuante | coroa de ossos | espíritos orbitando | **Senhor da Maré:** trono flutuante de ossos |
| Engenheiro | chave inglesa gigante | mochila-oficina | 2 drones ajudantes | **Gambiarra Prime:** mecha-armadura improvisada |
| Alien | antenas bioluminescentes | pele iridescente | terceiro olho energético | **Forma Instável:** corpo muda de cor a cada ataque |
| Dragão | chifres curvos | envergadura +30% | escamas incandescentes | **Bicéfalo:** segunda cabeça (dois sopros) |
| Titã | correntes partidas nos pulsos | armadura ancestral | olhos de magma | **Coroa da Montanha:** 4 braços + coroa rochosa |
| Anjo de Guerra | auréola dupla | armadura dourada | 4 asas | **Aurora:** 6 asas + lança solar |
| Demônio Mutante | chifres em espiral | garras flamejantes | asas rasgadas | **Abissal:** segunda boca estilizada no torso (sem gore) |
| Mecha Supremo | antena de radar | ombreiras-lançadores | laser duplo | **Cidade Andante:** 2 torretas extras nas costas |

**Por que marcos e não troca contínua:** 4 trocas de mesh/material por tropa cabem no orçamento de arte (19×4 = 76 variações, maioria via acessório + material swap em URP) e cada marco vira **momento de anúncio** ("evolução visível" — BRIEF, formatos 3 e 5).

---

## 5. Scaling por nível (1–10)

### 5.1 Fórmulas

```
HP(n)  = HP(1)  × 1,15^(n−1)        (arredondado para inteiro em runtime)
DPS(n) = DPS(1) × 1,15^(n−1)
Velocidade, alcance e SUPPLY não escalam com nível.   ← Supply fixo é o que mantém o puzzle estável
Fragmentos para subir de n → n+1 = 10 × 2^(n−1)        (CANON §8; máx nível 10)
Moedas para subir de n → n+1 = 100 × 2^(n−1) × M       M: Comum 1 · Raro 2 · Épico 4 · Lendário 8   (doc 07 §2.1/§6)
```

- Nível 10 = ×3,52 sobre o nível 1 (1,15⁹). Crescimento composto de 15% é sentido a cada nível, mas nunca dobra o poder de um nível para o outro — protege a curva de dificuldade do doc 06.
- Total de fragmentos do nv 1 ao 10: **5.110 por tropa** (10+20+40+80+160+320+640+1.280+2.560).
- Custo de moedas do nv 9→10 de uma Comum = 25.600 — compatível com a recompensa por fase de meio de jogo (100 × 1,10^(fase−1), CANON §8). Lendária 9→10 = 204.800 — alvo de fim de jogo (fase ~95+, já considerando a recalibração de renda do doc 07 §5).

### 5.2 Tabela exemplo — 1 tropa por raridade

Multiplicador, stats resultantes (HP/DPS) e custo pago **para alcançar** o nível da linha:

| Nv | Mult. | Soldado (C) HP/DPS | Mago (R) HP/DPS | Gigante (É) HP/DPS | Dragão (L) HP/DPS | Frags | Moedas C/R/É/L |
|---:|---:|---|---|---|---|---:|---|
| 1 | 1,00 | 10 / 2,0 | 25 / 16,0 | 120 / 40,0 | 130 / 70,0 | — | — |
| 2 | 1,15 | 12 / 2,3 | 29 / 18,4 | 138 / 46,0 | 150 / 80,5 | 10 | 100 / 200 / 400 / 800 |
| 3 | 1,32 | 13 / 2,6 | 33 / 21,2 | 159 / 52,9 | 172 / 92,6 | 20 | 200 / 400 / 800 / 1.600 |
| 4 | 1,52 | 15 / 3,0 | 38 / 24,3 | 183 / 60,8 | 198 / 106,4 | 40 | 400 / 800 / 1.600 / 3.200 |
| 5 | 1,75 | 17 / 3,5 | 44 / 28,0 | 210 / 70,0 | 227 / 122,5 | 80 | 800 / 1.600 / 3.200 / 6.400 |
| 6 | 2,01 | 20 / 4,0 | 50 / 32,2 | 241 / 80,5 | 261 / 140,8 | 160 | 1.600 / 3.200 / 6.400 / 12.800 |
| 7 | 2,31 | 23 / 4,6 | 58 / 37,0 | 278 / 92,6 | 301 / 161,9 | 320 | 3.200 / 6.400 / 12.800 / 25.600 |
| 8 | 2,66 | 27 / 5,3 | 67 / 42,6 | 319 / 106,4 | 346 / 186,2 | 640 | 6.400 / 12.800 / 25.600 / 51.200 |
| 9 | 3,06 | 31 / 6,1 | 76 / 48,9 | 367 / 122,3 | 398 / 214,1 | 1.280 | 12.800 / 25.600 / 51.200 / 102.400 |
| 10 | 3,52 | 35 / 7,0 | 88 / 56,3 | 422 / 140,9 | 458 / 246,4 | 2.560 | 25.600 / 51.200 / 102.400 / 204.800 |

Habilidades que escalam junto: valores de cura, dano de torreta e dano de pulso usam o mesmo ×1,15^(n−1). Percentuais fixos **não** escalam (esquiva 75%, crítico 20%, lentidão 30%, proc Alien 25% — os dois últimos são CANON §4).

---

## 6. Aquisição de tropas

### 6.1 Tropa-na-fase vs tropa-na-conta (diferença fundamental)

| | **Tropa-na-conta** (carta) | **Tropa-na-fase** (instância) |
|---|---|---|
| O que é | Desbloqueio **permanente** via carta de baú/recompensa | Unidade criada por um **Gate de classe** durante a corrida |
| Duração | Para sempre; tem nível e fragmentos | Só até o fim da fase (vitória, derrota ou saída) |
| Stats | Definidos pelo **nível da carta** na conta | Herda o nível da carta da conta no momento do spawn |
| Custo | Fragmentos + moedas para evoluir (§5) | Zero — só o Supply dentro da corrida |
| Efeito sistêmico | Habilita gates daquela classe no gerador de fases e o slot de favorita | Nenhum — não persiste, não consome carta |

Regras de geração (alinhadas ao doc 04 e ao Boss Scout, CANON §3.1):

1. **Gates de classe só oferecem tropas desbloqueadas na conta** — exceções: (a) fases-prévia scriptadas (28, 48 e 88) emprestam a próxima tropa marcante 2 fases antes do desbloqueio real, gerando desejo; (b) rewarded "testar tropa lendária por 1 fase" (CANON §11) injeta gates da lendária sorteada naquela corrida.
2. **Loadout de favoritas:** 3 slots na tela de Tropas; tropas favoritas recebem +40% de peso na geração de gates de classe. É assim que o jogador "leva" sua coleção para a corrida sem virar deck-builder pesado.
3. **Boss Scout garante counter:** se o jogador possui uma tropa/elemento que acerta a fraqueza do boss, o gerador garante ≥1 gate dela na fase (a "rota ótima" do CANON §3.1).
4. Toda corrida começa com Soldados (BRIEF: "fase começa com 1 unidade"); a composição nasce dos gates.

**Fragmentos** (evolução) vêm de: cartas repetidas em baús, drop de boss (CANON §6), missões diárias e loja (gemas). Detalhe de taxas no doc 07. Pity de coleção: tropa nova do pool atual é garantida em no máximo 8 baús abertos (+12,5% de chance acumulada por baú sem drop novo) — sustenta o anti pay-to-win do CANON §11 ("baús grátis dropam lendárias").

### 6.2 Ordem de desbloqueio — release completo (100 fases)

| Fase | Tropa | Raridade | Fonte | Racional |
|---:|---|---|---|---|
| 1 | Soldado | Comum | inicial | âncora do tutorial |
| 5 | Arqueiro | Comum | recompensa garantida | CANON §16 — "fase 5 desbloqueia algo novo" |
| 9 | Escudeiro | Comum | recompensa garantida | chega junto com 1º pico de dificuldade pré-boss M1 |
| 12 | Corredor | Comum | recompensa garantida | M2 introduz hordas — Supply 1 barato ganha valor |
| 15 | Médico | Raro | baú garantido (M2) | bosses zumbis longos pedem sustain |
| 18 | Mago | Raro | recompensa garantida | preparação para o boss M2 (fase 20) |
| 22 | Ninja | Raro | baú do boss M2 / pool | M3 estreia armadilhas de serra — counter na mão |
| 26 | Lança-Chamas | Raro | pool de baús | 1º elemento ofensivo permanente |
| 30 | Robô | Épico | baú garantido do boss M3 | tema: Robô Escorpião "dropa" tecnologia |
| 34 | Tropa Glacial | Raro | pool de baús | fecha o trio elemental do MVP+ |
| 38 | Necromante | Épico | pool de baús (M4) | floresta mutante = hordas baratas para reviver |
| 46 | Engenheiro | Épico | pool de baús (M5) | bosses de M5+ têm mais HP — torreta entra na conta |
| 50 | Dragão | Lendário | baú épico do boss M5 (pool lendário abre) | derrotar o Dragão de Lava "liberta" o seu |
| 58 | Alien | Épico | pool de baús (M6) | ponte para o tema do M8 |
| 65 | Anjo de Guerra | Lendário | pool lendário | counter de Sombra antes do M8 |
| 75 | Demônio Mutante | Lendário | pool lendário (M8) | pico de poder do late game |
| 85 | Titã | Lendário | pool lendário (M9) | âncora para os bosses finais |
| 90 | Mecha Supremo | Lendário | baú do boss M9 (chance alta + pity) | espelho do boss Mecha Supremo (CANON §6) |

"Pool" = a carta entra no sorteio de baús a partir dessa fase (com pity de 8 baús). "Garantida" = entregue na primeira vitória da fase.

### 6.3 Mapeamento MVP (20 fases — CANON §15)

| Fase MVP | Tropa | Observação |
|---:|---|---|
| 1 | Soldado | inicial |
| 5 | Arqueiro | mantém o marco canônico (CANON §16) |
| 8 | Escudeiro | início do M2 (fases 8–14) |
| 10 | Mago | carta garantida no baú épico da fase 10 (doc 02 §4.2) — chega junto da recompensa grande do CANON §16, na hora em que as hordas zumbis pedem dano em área |
| 14 | Gigante | troféu da vitória sobre o Zumbi Titã (doc 02 §5.2) — âncora do beat do D2 e preparação para o clímax do MVP (boss M3, fase 20) |

No MVP as 5 cartas vêm de recompensa garantida na primeira vitória da fase ("cartas simples", CANON §15) — o Mago dentro do baú épico da fase 10 e o Gigante como recompensa direta do boss da fase 14; baús com fragmentos existem só para essas 5 tropas. Este cronograma (Mago f10, Gigante f14) é o único válido para o MVP e espelha a tabela §6 do doc 02.

---

## 7. Sinergias — 4 builds de exemplo (com as contas)

Todos os números saem das tabelas §3.1/§3.2. Lutas de boss assumem 10–20 s (CANON §6).

### Build 1 — "Falange" (Escudeiro + Arqueiro)
**Composição (Supply 36):** 4 Escudeiros (12) + 12 Arqueiros (24). DPS bruto: 12×8 + 4×4 = **112**.
**A conta:** cada Escudeiro cobre 3 aliados de Supply ≤2 → 4 Escudeiros cobrem exatamente 12 Arqueiros. O Brutamontes Zumbi (arquétipo M2) arremessa entulho de 18 de dano em área: sem Muralha, Arqueiro (HP 14) morre em 1 hit — perde ~8 Arqueiros no primeiro arremesso (−64 DPS). Com Muralha, o Arqueiro recebe 9 (sobrevive com 5) e o Escudeiro absorve 9 (HP 30 aguenta 3 entulhos). Em 15 s de luta: sem = 96 DPS por 4 s + 32 DPS depois ≈ **736 de dano**; com = 96×15 ≈ **1.440**. Quase 2× de dano pela mesma composição de ataque.

### Build 2 — "Coração da Montanha" (Médico + Gigante)
**Composição (Supply 44):** 3 Gigantes (36) + 2 Médicos (8).
**A conta:** boss de meio de jogo inflige ~50 HP/s agregados na linha de frente. Pool dos Gigantes = 360 HP → caem em 7,2 s → dano causado = 120 DPS × 7,2 ≈ **864**. Com 2 Médicos (16 HP/s de cura, fora da área a 5 m): dano líquido 34 HP/s → 10,6 s de uptime → (120+8) × 10,6 ≈ **1.357** (+57%) gastando só 18% do Supply em cura. É esta conta que justifica "1 HP/s de cura ≈ 3 OC" (§2.3).

### Build 3 — "Maré Sem Fim" (Necromante + horda barata)
**Composição (Supply 44):** 2 Necromantes (16) + 28 Soldados (28) — contra a alternativa de gastar tudo em 44 Soldados.
**A conta:** boss com pisão a cada 5 s que mata 70% dos corpo-a-corpo. 44 Soldados: 44→13→4→1 ≈ **450 de dano** em 18 s. Com Necromantes (cada um revive até 3 caídos de Supply ≤6 a cada 6 s, 60% HP/100% DPS): a linha se mantém em ~10–14 Soldados de regime permanente ≈ 498 de dano da horda + 2×10 DPS×18 s dos Necromantes a 8 m = **858** (+90%). A sinergia é direcional: revive **precisa** de cadáveres baratos e numerosos — com 2 Gigantes mortos o Levantar não tem alvo válido (Supply ≤6).

### Build 4 — "Passo Fantasma" (Ninja + Corredor, mundos de armadilha M3/M7)
**A conta:** trecho com 5 armadilhas, cada uma atinge 20% do exército que cruza. Sobrevivência por unidade: Soldado (1−0,20)⁵ = 33%; Corredor (1−0,20×0,70)⁵ = 47%; Ninja (1−0,20×0,25)⁵ = 77%.
Em 36 de Supply: 36 Soldados entregam 11,9 vivos × 12 OC = **143 OC na arena**; 12 Ninjas entregam 9,3 vivos × 40 OC = **372 OC na arena** — 2,6× mais poder atravessa o mesmo trecho. Variante econômica: 6 Ninjas (18) + 18 Corredores (18) = 186 + 102 = 288 OC, trocando teto por resiliência de quantidade. É por isso que Boss Scout + tema do mundo mudam a resposta certa do mesmo gate.

---

## 8. Prova numérica do Pilar 2 — "x10 Soldados perde para +2 Magos"

**Cenário concreto (fase 9, release):** o jogador chega com **6 Soldados** ao último par de gates antes da arena: esquerda **[×10]**, direita **[+2 Magos]**. Boss: **Golem de Pedra** (variante regional do M1, CANON §6) — HP **650**, ataque especial telegrafado **Pisão** a cada 5 s (primeiro em t=3 s): 25 de dano em área de 4 m, atingindo ~70% das unidades corpo a corpo. Magos atacam a 6 m — fora da área do Pisão. Stats reais da tabela §3.1: Soldado HP 10/DPS 2 · Mago HP 25/DPS 16 (contra boss único vale o DPS **nominal** — área não ajuda, §2.2).

**Rota A — ×10 → 60 Soldados (Supply 60 = cap inicial cheio, CANON §3.2):**

| t (s) | Evento | Soldados vivos | DPS | Dano acumulado |
|---:|---|---:|---:|---:|
| 0–3 | abertura | 60 | 120 | 360 |
| 3 | Pisão 1 (−70%) | 18 | 36 | 360 |
| 3–8 | | 18 | 36 | 540 |
| 8 | Pisão 2 | 5 | 10 | 540 |
| 8–13 | | 5 | 10 | 590 |
| 13 | Pisão 3 | 2 | 4 | 590 |
| 13–18 | | 2 | 4 | 610 |
| 18 | Pisão 4 | 0 | 0 | **610 < 650 → DERROTA** |

Exército eliminado com o boss a 40 HP — derrota "por um fio", combustível perfeito de retry e do anúncio "parece fácil, mas não é" (BRIEF).

**Rota B — +2 Magos → 6 Soldados + 2 Magos (Supply 14):**

| t (s) | Evento | Linha de frente | DPS total | Dano acumulado |
|---:|---|---|---:|---:|
| 0–3 | abertura | 6 Soldados | 12+32 = 44 | 132 |
| 3 | Pisão 1 | 2 Soldados | 4+32 = 36 | 132 |
| 3–8 | | 2 Soldados | 36 | 312 |
| 8 | Pisão 2 | 0 Soldados | 32 | 312 |
| 8–18,6 | Magos intactos a 6 m | — | 32 | **650 → VITÓRIA em ~18,6 s** |

**Leitura do resultado:** a Rota A usa **60 de Supply** e perde; a Rota B usa **14** e vence — sobrando 46 de Supply que, na fase real, teriam sido preenchidos por outros gates no caminho. Bônus do sistema: se o jogador tivesse 10+ Soldados antes do ×10, o excedente acima do cap 60 viraria moedas com fanfarra (CANON §3.2) — o jogo nunca pune, mas o número bruto satura. A vitória em 18,6 s respeita a janela canônica de boss de 10–20 s (CANON §6). *(Conservador: a conta ignora Nova Arcana e Tiro Certeiro — só reforçariam a Rota B.)*

---

## 9. Balanceamento contínuo e Remote Config

### 9.1 Regras de ouro

1. **Nunca tunar Supply por Remote Config.** O custo de Supply é vocabulário aprendido pelo jogador e premissa da geração de fases (doc 04/06). Mudança de Supply = mudança de design, via update com patch notes.
2. **Ordem de intervenção:** primeiro `hp_mult`/`dps_mult` da tropa (±15% máx. por ciclo), depois parâmetros de habilidade, por último os prêmios de raridade (globais — mexem em tudo).
3. **Percentuais do CANON §4 (lentidão 30%/2 s, proc Alien 25%, encadeamento de Raio 50%) têm válvula, mas o default é canônico** — alterar só com aprovação de design e atualização do CANON.
4. Toda mudança roda como **A/B no soft launch** (Firebase Remote Config + Analytics, CANON §13) com `gate_selected`, `level_fail` e `boss_failed` como métricas de leitura (eventos do BRIEF).

### 9.2 Válvulas de Remote Config

**Globais (defaults / faixa segura):**

| Chave | Default | Faixa | Efeito |
|---|---:|---|---|
| `rc_units_global_hp_mult` | 1,00 | 0,80–1,30 | HP de todas as tropas |
| `rc_units_global_dps_mult` | 1,00 | 0,80–1,30 | DPS de todas as tropas |
| `rc_units_level_growth` | 1,15 | 1,10–1,18 | crescimento por nível (§5.1) |
| `rc_units_rarity_premium_rare/epic/leg` | 1,10/1,15/1,20 | 1,08–1,22 | prêmio de raridade (§2.1) |
| `rc_units_shard_growth` | 2,0 | 1,8–2,2 | base da curva 10×2^(n−1) (CANON §8 — mexer só em crise de economia) |
| `rc_supply_overflow_coins` | 2 | 1–5 | moedas por ponto de Supply excedente (alinhar com a chave `supply_overflow_coin_rate` do doc 07) |

**Por tropa** — padrão `rc_unit_{id}_hp_mult` e `rc_unit_{id}_dps_mult` (faixa 0,85–1,20) para as 19, mais válvulas específicas de habilidade:

| Tropa | Chave específica | Default | Faixa |
|---|---|---:|---|
| Soldado | `rc_soldier_cohesion_cap` | 0,20 | 0,10–0,30 |
| Arqueiro | `rc_archer_powershot_every` | 5 | 4–7 |
| Escudeiro | `rc_shield_redirect_pct` / `_targets` | 0,50 / 3 | 0,35–0,65 / 2–4 |
| Corredor | `rc_runner_dodge_pct` | 0,30 | 0,20–0,40 |
| Mago | `rc_mage_nova_cooldown_s` | 8 | 6–12 |
| Ninja | `rc_ninja_dodge_pct` / `_crit_chance` | 0,75 / 0,20 | 0,60–0,85 / 0,15–0,25 |
| Lança-Chamas | `rc_flame_cone_width_m` | 3,0 | 2,5–4,0 |
| Tropa Glacial | `rc_glacial_slow_pct` / `_duration_s` | 0,30 / 2,0 | canônico §4 — ver regra 3 |
| Médico | `rc_medic_heal_per_s` | 8 | 5–12 |
| Robô | `rc_robot_phys_reduction` | 0,20 | 0,10–0,30 |
| Gigante | `rc_giant_aoe_radius_m` | 2,0 | 1,5–2,5 |
| Necromante | `rc_necro_interval_s` / `_count` / `_supply_cap` | 6 / 3 / 6 | 4–10 / 2–4 / 4–8 |
| Engenheiro | `rc_turret_dps` / `_hp` | 25 / 30 | 18–35 / 20–45 |
| Alien | `rc_alien_proc_chance` | 0,25 | canônico §4 — ver regra 3 |
| Dragão | `rc_dragon_breath_radius_m` | 4,0 | 3,0–5,0 |
| Titã | `rc_titan_taunt_radius_m` | 6,0 | 4,0–8,0 |
| Anjo de Guerra | `rc_angel_aura_heal_per_s` | 12 | 8–16 |
| Demônio Mutante | `rc_demon_fury_per_kill` / `_cap` | 0,05 / 0,50 | 0,03–0,08 / 0,30–0,60 |
| Mecha Supremo | `rc_mecha_missile_cooldown_s` | 4 | 3–6 |

### 9.3 Red flags de balanceamento (gatilhos de ação no dashboard)

| Sinal (Analytics) | Limiar de alerta | Ação primeira-linha |
|---|---|---|
| Pick rate de um gate de classe (`gate_selected`) | <8% ou >25% sustentado | ±10% no `dps_mult` da tropa |
| Tropa presente em composições vencedoras de boss de mundo | >35% (dominância) | revisar válvula de habilidade antes do nerf de stat |
| Winrate do par Médico+Gigante em bosses M3+ | >90% | `rc_medic_heal_per_s` 8→7 |
| Demônio Mutante em M8+ | >35% das vitórias | `rc_demon_fury_cap` 0,50→0,40 |
| `level_fail` em fases de armadilha (M3/M7) sem Ninja na conta | >45% | adiantar Ninja na ordem de desbloqueio (§6.2) ou +1 fase-prévia |
| Conversão do rewarded "testar lendária" | <20% dos elegíveis | trocar a lendária ofertada pela counter do boss atual (Boss Scout) |

**Riscos conhecidos e assumidos:** Necromante e Engenheiro são as tropas mais sensíveis a parâmetro (U alto, stat baixo) — por isso têm 3 válvulas cada; Demônio é glass cannon por design (espetáculo, Pilar 3) e será o primeiro candidato a nerf de Fúria, nunca de DPS base (o número grande no card vende o baú).

---

*Fim do doc 03. Próximas leituras: doc 04 (como os gates entregam essas tropas na pista) e doc 05 (as janelas de DPS e ataques telegrafados que estas contas assumem).*
