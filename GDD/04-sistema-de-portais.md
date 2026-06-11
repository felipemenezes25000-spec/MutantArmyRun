# 04 — Sistema de Portais (Gates) · Mutant Army Run

> **Entregável 11 do pacote de design.** Fonte da verdade para tudo que envolve portais: taxonomia, números, geração de fase, telegraphing, regras anti-frustração e telemetria.
> Obedece integralmente a `CANON.md` (especialmente §3.1 Boss Scout, §3.2 Supply, §3.3 Mutações, §3.4 Portais pareados, §4 Elementos, §10 Portais do MVP) e cobre 100% da seção "Portais (variedade exigida)" de `BRIEF.md`.
> Docs relacionados: Sistema de Unidades (entregável 9), Sistema de Bosses (entregável 10), Sistema de Fases (entregável 12), Economia (entregável 8).

---

## 1. Papel dos portais no jogo

O portal é a **unidade mínima de decisão** do core loop: a cada 9–13 segundos de corrida o jogador faz uma escolha binária que muda o exército. Os quatro pilares do CANON §2 aplicados a portais:

1. **Legível em 3 s** — todo portal comunica seu efeito com número + ícone + cor antes de qualquer leitura de texto.
2. **Escolha inteligente, não "maior número"** — o valor de um portal depende do boss da fase (Boss Scout), do Supply disponível e da composição atual. O x5 pode ser a pior escolha da fase.
3. **Espetáculo constante** — atravessar um portal é o momento de maior feedback do jogo: flash, partículas, fanfarra numérica, transformação visível nos modelos.
4. **Progressão em 3 camadas** — dentro da fase, os portais SÃO a progressão.

**Regra-mãe (CANON §3.4):** portais aparecem **sempre em pares** (esquerda/direita), com **informação honesta** — número exato, ícone de classe/elemento ou porcentagem clara. A tensão vem da escolha, nunca da trapaça.

---

## 2. Anatomia de um portal e do par

| Propriedade | Valor | Justificativa |
|---|---|---|
| Largura da pista | 6,0 m | Padrão de runner vertical; 3 "colunas mentais" de desvio |
| Largura de cada portal | 2,6 m | O par ocupa 5,2 m; gap central de 0,4 m + 0,2 m por borda |
| Altura do arco | 3,5 m | Visível por cima do exército mesmo com 200+ unidades |
| Distância de leitura | 25 m (≈ 5 s a 5 m/s) | Número legível a 18 m; cor/categoria legível a 25 m |
| Exclusividade | Atravessar um portal **desativa o outro** (o vidro do irmão estilhaça) | Escolha binária limpa; sem "pegar os dois" |
| Evitar ambos | Possível pelas frestas laterais de 0,2 m — desvio quase perfeito | Skill avançada; recompensa leitura de pares ruins; dispara `gate_missed` |
| Zona limpa | 8 m antes e depois do par sem obstáculos | A decisão nunca compete com o desvio (exceto portais de risco, cuja zona de perigo vem DEPOIS do arco) |

O efeito aplica em **toda unidade que cruza o plano do arco**, com onda de VFX percorrendo a multidão de frente para trás em 0,4 s (CrowdManager propaga; GateManager resolve o efeito de uma vez para evitar custo por unidade).

---

## 3. Taxonomia — os 5 grupos

Convenções das tabelas:

- **VE (valor esperado)** é calculado sobre o **Exército de Referência (ER)**: 30 Soldados nível 1 (HP 300, DPS 60, Supply 30 de 60). "Poder" ≈ produto unidades × stats. O VE real varia com a composição — **isso é proposital** (pilar 2).
- **Peso** = peso de spawn no sorteio do gerador (relativo dentro do pool da fase): 100 muito comum · 70 comum · 40 incomum · 20 raro · 8 muito raro.
- **Fase mín.** = primeira fase do release completo (1–100) em que o portal entra no pool. No MVP só existem os 8 portais marcados **✅ MVP** (CANON §10), todos com fase mín. dentro de 1–20.
- Excedente de Supply converte em moedas: **2 moedas por ponto de Supply excedente × Mb (multiplicador de mundo, doc 07 §2.1)** — default canônico da chave `supply_overflow_coin_rate` do Remote Config (doc 07 §9). Os exemplos das tabelas assumem Mb = 1,0 (Mundo 1). Ver §7.2.

### 3.1 Portais matemáticos (8)

Alteram a **quantidade** de unidades. Multiplicadores replicam a composição atual proporcionalmente (x2 num exército de 10 Soldados + 2 Magos dá 20 Soldados + 4 Magos).

| Portal | Efeito exato | Custo/risco | Peso | Fase mín. | VE sobre o ER | MVP |
|---|---|---|---|---|---|---|
| **+10** | +10 unidades do tipo mais comum do exército | Nenhum | 100 | 1 | ×1,33 | ✅ |
| **+25** | +25 unidades do tipo mais comum | Nenhum | 70 | 2 | ×1,83 | ✅ |
| **+50** | +50 unidades do tipo mais comum | Pode estourar Supply | 40 | 8 | ×2,0 + 40 moedas (estoura cap em 20 Supply) | — |
| **x2** | Duplica cada tipo de unidade | Pode estourar Supply | 70 | 1 | ×2,0 (60/60, exato no cap) | ✅ |
| **x3** | Triplica cada tipo | Estouro quase certo no early | 40 | 3 | ×2,0 + 60 moedas | ✅ |
| **x5** | Quintuplica cada tipo | Estouro massivo sem upgrades de Supply | 20 | 12 | ×2,0 + 180 moedas | — |
| **−10** | Remove 10 unidades, **das mais baratas primeiro** | Perda seca | 40 | 5 | ×0,67 | — |
| **÷2** | Remove metade (sobreviventes = ⌈n/2⌉, arredonda a favor do jogador), das mais baratas primeiro | Perda seca | 40 | 3 | ×0,5 | ✅ |

**Leitura de design:** com Supply 60, x3 e x5 entregam o MESMO poder que x2 — a diferença vira moedas. É a demonstração viva do anti-"maior número" (CANON §3.2): o jogador aprende que multiplicador grande + Supply cheio = banco, não exército. Com Supply 300 (meta avançada), x5 volta a ser rei. **−10 e ÷2 nunca reduzem abaixo de 1 unidade** (piso absoluto, §7.1).

### 3.2 Portais de classe (8)

Transformam unidades na tropa-alvo. Regra de conversão canônica:

> **Conversão por valor de Supply, nunca por contagem.** Some o Supply das unidades convertíveis e divida pelo custo da tropa-alvo (arredonda para baixo, piso 1). O resto da divisão permanece nas unidades originais. Só converte unidades de **raridade inferior** à tropa-alvo — o portal nunca rebaixa ninguém (anti-frustração).

Exemplo com o ER (30 Soldados = 30 Supply): "Virar Mago" (Supply 4) → 7 Magos + 2 Soldados restantes. Poder bruto ≈ igual (+10–20% de prêmio de raridade, CANON §5), mas o **perfil tático** muda: área, alcance, esquiva. Tropas exclusivas de corrida (run-only) existem só dentro da fase, seguem o baseline de stats por Supply do doc de Unidades e não entram no meta de cartas.

| Portal | Tropa-alvo (Supply) | Efeito tático | Custo/risco | Peso | Fase mín. | VE sobre o ER | MVP |
|---|---|---|---|---|---|---|---|
| **Virar Arqueiro** | Arqueiro (2) | 15 Arqueiros: dano à distância, atacam o boss antes do contato | Frágeis a ataques em área | 70 | 2 | ×1,1 + alcance | ✅ |
| **Virar Mago** | Mago (4) | 7 Magos + 2 Soldados: dano em área | Poucos corpos = vulnerável a armadilhas | 40 | 6 | ×1,15 + área | — |
| **Virar Zumbi** | Zumbi Errante, run-only (2) | 15 Zumbis: revivem 1× com 50% HP ao morrer | Morto-vivo: Veneno não os afeta, mas Luz inimiga causa +50% neles | 40 | 11 | ×1,1 + segunda vida | — |
| **Virar Ninja** | Ninja (3) | 10 Ninjas: esquivam de obstáculos/armadilhas automaticamente | DPS modesto | 40 | 13 | ×1,1 + imunidade a armadilhas | — |
| **Virar Gigante** | Gigante (12) | 2 Gigantes + 6 Soldados: muralha de HP e dano | Lentos; poucos alvos = crítico se um cai | 20 | 17 | ×1,2 concentrado | — |
| **Virar Robô** | Robô (8) | 3 Robôs + 6 Soldados: dano + resistência, imunes a Veneno | Conduzem Raio (+50% recebido, elemento Metal de chassi) | 20 | 21 | ×1,2 | — |
| **Virar Dragonete** | Dragonete, run-only (6) | 5 Dragonetes: voam (ignoram obstáculos de chão) + área pequena de Fogo | Recebem +50% de Gelo | 8 | 41 | ×1,2 + voo | — |
| **Virar Cavaleiro** | Cavaleiro de Aço, run-only (5) | 6 Cavaleiros: elemento Metal nativo (+30% defesa, −50% físico recebido) | +50% de Raio recebido (armadura conduz) | 20 | 61 | ×1,15 + tanque | — |

**Justificativa:** conversão por Supply mantém o portal de classe sempre "justo" em poder bruto e desloca a decisão para o contexto — Arqueiros brilham contra boss com aura de contato, Ninjas pagam-se em fases cheias de serras, Gigantes são armadilha contra boss de área. É o portal que mais conversa com o Boss Scout.

### 3.3 Portais de elemento (8)

Aplicam o elemento ao exército inteiro pela fase (tropas com elemento nativo — Lança-Chamas, Tropa Glacial — mantêm o seu). Pegar outro elemento **substitui** o anterior. Efeitos seguem o chart canônico (CANON §4): ciclo **Fogo > Gelo > Raio > Fogo**, vantagem = +50% de dano, mesmo elemento vs mesmo elemento = −50%.

| Portal | Efeito exato | Custo/risco | Peso | Fase mín. | VE | MVP |
|---|---|---|---|---|---|---|
| **Elemento Fogo** | Ataques de Fogo; +50% vs Gelo/planta/orgânico | −50% vs bosses de Fogo/lava | 70 | 4 | ×1,5 dano vs boss fraco; ×0,5 se mesmo elemento | ✅ |
| **Elemento Gelo** | +50% vs Raio; aplica lentidão de 30% por 2 s (não acumula) | −50% vs bosses de Gelo | 70 | 9 | ×1,5 + controle | — |
| **Elemento Raio** | +50% vs Fogo e vs alvos de Metal (conduz); encadeia 50% do dano para até 2 inimigos próximos | −50% vs bosses elétricos | 70 | 16 | ×1,5 + cadeia | — |
| **Elemento Veneno** | Dano contínuo de 3% HP/s por 4 s; +50% vs orgânicos | **0% vs máquinas e mortos-vivos** — pior portal possível nos mundos 2, 3 e 9 | 40 | 22 | ×1,5 vs orgânico; ×0 vs imunes | — |
| **Elemento Luz** | Ataques curam 2% do HP do exército; +50% vs Sombra e mortos-vivos | Sem dano bônus vs máquinas | 20 | 41 | ×1,5 + sustain | — |
| **Elemento Sombra** | 20% de chance de reviver aliado morto como "sombra" (50% dos stats); +50% vs Luz | Recebe +50% de Luz | 20 | 45 | ×1,3 + ressurreição | — |
| **Elemento Metal** | +30% defesa; −50% dano físico recebido | +50% de Raio recebido | 20 | 61 | sobrevivência ×1,4 | — |
| **Energia Alienígena** | 25% de chance por ataque de efeito aleatório (queimar, congelar, encadear, envenenar) | Imprevisível; nenhum bônus garantido | 8 | 71 | ×1,25 médio, variância alta | — |

**Justificativa:** o portal de elemento é o coração do Boss Scout — é barato de entender ("FOGO contra GELO") e cria a armadilha mais elegante do jogo: o portal do MESMO elemento do boss, lindo e brilhante, que corta seu dano pela metade.

### 3.4 Portais de mutação (12)

Mutações aplicam-se ao exército inteiro, ficam **visíveis nos modelos** e duram a fase inteira (CANON §3.3). Sistema de slots no §4. Todos pós-MVP.

| Portal | Efeito numérico exato | Custo/risco | Peso | Fase mín. | VE | Mudança visual |
|---|---|---|---|---|---|---|
| **Velocidade** | +25% velocidade de movimento (mais DPS uptime no boss, desvio mais ágil) | Nenhum | 70 | 5 | ×1,1–1,2 indireto | Rastro de vento nos pés |
| **Armadura** | −30% dano físico recebido | Nenhum | 70 | 7 | sobrevivência ×1,4 | Placas metálicas no torso |
| **Braços Extras** | +30% velocidade de ataque (DPS ×1,3) | Nenhum | 70 | 10 | ×1,3 | 2 braços a mais animados |
| **Escudo** | Bolha que absorve 30% do HP máx; quebra e só recarrega ao entrar na arena do boss | Não regenera na corrida | 40 | 12 | HP efetivo ×1,3 | Bolha hexagonal translúcida |
| **Tamanho** | +40% HP, +20% dano, hitbox +25% | Hitbox maior = mais hits de obstáculos | 40 | 14 | ×1,5 com risco | Unidades 25% maiores |
| **Regeneração** | Cura 2% HP máx/s fora de combate, 1%/s em combate | Nenhum | 40 | 18 | sustain alto em boss longo | Aura verde pulsante |
| **Asas** | Voo: ignora buracos e obstáculos de chão; +10% esquiva vs ataques de área do boss | Não evita projéteis | 40 | 21 | neutraliza ~70% dos obstáculos | Asas batendo, exército a 1 m do chão |
| **Ataque em Área** | Cada golpe causa 50% do dano em raio de 2 m | Nenhum | 20 | 24 | ×1,4 vs grupos | Onda de choque por golpe |
| **Cabeças Extras** | 25% de chance de golpe duplo (DPS efetivo ×1,25); cada unidade mira até 2 alvos | Nenhum | 20 | 28 | ×1,25 | Segunda cabeça funcional |
| **Laser** | Ataque extra à distância: +25% do DPS como feixe ocular, alcance 8 m (dispara antes do contato) | Nenhum | 20 | 31 | ×1,25 + range | Olhos vermelhos com feixe |
| **Tentáculos** | 20% de chance de prender inimigo comum por 1,5 s; +15% dano vs boss | Nenhum | 20 | 34 | ×1,15 + controle | Tentáculos no lugar de 1 braço |
| **Clonagem** | Instantâneo: +25% de unidades-clone (50% dos stats, 50% do Supply, piso 1 clone), visual translúcido | Clones não recebem cura | 8 | 38 | ×1,12 + corpos | Cópias translúcidas azuladas |

**Versão Lendária:** toda mutação tem variante dourada com **efeito ×2** (Armadura Lendária = −60%; Braços Lendários = DPS ×1,6...), obtida pelo portal de risco "Mutação Lendária Instável" (§3.5) ou ao **repetir uma mutação já ativa** — pegar Armadura com Armadura equipada promove-a a Lendária sem gastar slot. Decisão de design: transforma o portal "repetido" (que seria desperdício) em jackpot e em cena de anúncio.

### 3.5 Portais de risco (7)

Regra inviolável (CANON §3.4): **toda probabilidade é exibida no arco em texto grande e é honesta** — o RNG usa as odds mostradas, com seed logada para auditoria. Riscos determinísticos exibem o tradeoff completo ("DANO x2 / VIDA −35%"). Nunca há texto oculto, asterisco ou arredondamento enganoso.

| Portal | Texto no arco (odds/payoff exibidos) | Resolução exata | Peso | Fase mín. | VE | MVP |
|---|---|---|---|---|---|---|
| **Zona de Perigo x10** | "SOBREVIVA → x10" + 3 caveiras | Após o arco, 30 m com 3 fileiras de serras/lasers; sobreviventes são multiplicados por 10 ao final da zona | 20 | 6 | Jogador médio perde ~40% → ×6 poder; habilidoso chega a ×9 | ✅ |
| **Baú do Perigo** | "BAÚ RARO → ROTA PERIGOSA" | Desvia para corredor lateral com armadilhas (perda média de 25% do exército); ao final, baú com 1 carta rara garantida + 150–300 moedas + 10% de chance de carta épica | 20 | 15 | Poder ×0,75 + valor econômico alto | — |
| **Mutação Lendária Instável** | "65% MUTAÇÃO LENDÁRIA / 35% NADA" | 65%: mutação aleatória em versão Lendária (efeito ×2). 35%: o portal solta fumaça e nada acontece (momento cômico, sem perda) | 20 | 25 | 0,65 × (mutação ×2) — VE positivo, variância alta | — |
| **Pacto de Dano** | "DANO x2 / VIDA −35%" | DPS ×2 e HP máximo −35% até o fim da fase (determinístico) | 40 | 9 | Ótimo perto da arena (pouca exposição); ruim no 1º par | — |
| **Horda Lenta** | "QUANTIDADE x2 / VELOCIDADE −30%" | Duplica unidades; velocidade de movimento −30% até o fim da corrida (não afeta a arena do boss) | 40 | 12 | ×2 poder, desvio fica difícil; sinergia com Asas/Ninja | — |
| **Roleta de Transformação** | Roleta com 4 fatias: "40% COMUM / 35% RARA / 20% ÉPICA / 5% LENDÁRIA" | Converte o exército INTEIRO por valor de Supply na tropa sorteada da raridade obtida (regra do §3.2) | 8 | 30 | ≈ ×1,15 médio; variância máxima do jogo | — |
| **Sacrifício do Titã** | "METADE DO EXÉRCITO → 1 TITÃ" | Consome 50% das unidades (as mais baratas primeiro, piso: sobra ≥1 além do Titã); entrega 1 Titã (Supply 25). Se estourar o cap, converte outras tropas em moedas até caber — o Titã nunca é convertido | 8 | 35 | Sobre o ER: ≈ ×1,4 poder + qualidade concentrada | — |

**Justificativa por portal:** Zona de Perigo converte risco em **skill**, não em moeda de RNG — é o risco do MVP porque ensina o gênero do jogo. Pacto de Dano e Horda Lenta são tradeoffs determinísticos cuja resposta certa depende da **posição na pista** (cedo vs tarde), criando profundidade sem aleatoriedade. Roleta e Sacrifício são os geradores de clipe viral (§8).

---

## 4. Sistema de 3 slots de mutação (CANON §3.3)

- **3 slots**, exibidos como medalhões no topo do HUD, na ordem em que foram obtidos.
- **Pegar a 4ª mutação substitui a mais antiga (FIFO):** o medalhão mais antigo pisca 0,5 s, estoura em partículas, e a parte do corpo correspondente se transforma na nova **em câmera visível** — é deliberadamente um momento de vídeo.
- A decisão de pegar a 4ª é sempre informada: ao se aproximar de um portal de mutação com 3 slots cheios, o medalhão que será perdido **pisca em vermelho** desde os 25 m de leitura.
- Mutação repetida não gasta slot: promove a existente a Lendária (efeito ×2, §3.4).
- Mutações diferentes **acumulam multiplicativamente** (Braços ×1,3 e Cabeças ×1,25 → DPS ×1,625).
- Ao fim da fase, todas as mutações expiram (persistência é por fase, não entre fases — a progressão entre fases vive nos upgrades e cartas).

**Justificativa:** o limite de 3 transforma mutações de "buff genérico empilhável" em **gestão de portfólio**: o jogador que vê "boss de área" no Boss Scout guarda um slot para Escudo; quem vai de Zona de Perigo x10 quer Asas antes. E o FIFO punindo a mutação mais ANTIGA (não a pior) gera dilemas legítimos sem exigir menu de escolha no meio da corrida.

---

## 5. Geração de fase

### 5.1 Quantidade e espaçamento

Corrida de 45–75 s a ~5 m/s = pista de 225–375 m (CANON §1).

| Fases | Pares por fase | Espaçamento entre pares |
|---|---|---|
| 1–3 | 3 | 60–70 m (12–14 s) |
| 4–10 | 3–4 | 55–70 m |
| 11+ | 4–5 | 45–60 m |
| Fase 10 de cada mundo (boss único) | 5 | 45–55 m |

Regras de posicionamento: primeiro par a ≥40 m da largada (8 s para o jogador "sentir" o exército); último par a ≥25 m da arena (5 s para a expectativa do boss montar); espaçamento mínimo absoluto de 45 m (9 s — nunca duas decisões em sequência sem respiro); zona limpa de obstáculos de 8 m em volta de cada par (§2).

### 5.2 Arquétipos de par

Cada par é classificado pelo gerador num arquétipo, com cotas por fase:

| Arquétipo | Composição | Frequência alvo | Disponível | Exemplo |
|---|---|---|---|---|
| **A — Bom vs Bom** | Dois VE positivos com perfis diferentes | 45% | Fase 1+ | x2 vs Virar Arqueiro (quantidade vs alcance) |
| **B — Bom vs Ruim** | Teste de leitura; o ruim é óbvio para quem olha 1 s | 25% | Fase 2+ | +25 vs ÷2 |
| **C — Seguro vs Risco** | VE parecidos, variâncias opostas | 20% | Fase 6+ | +25 vs Zona de Perigo x10 |
| **D — Ruim vs Menos Ruim** | "Escolha o menos pior" — máx. 1 por fase, **nunca consecutivo a outro D, nunca no último par** | 10% | Fase 15+ | −10 vs ÷2 (com fresta lateral para os experts evitarem ambos) |

### 5.3 Integração com o Boss Scout (CANON §3.1)

O gerador lê o `BossConfigSO` da fase **antes** de sortear portais e garante, por construção:

1. **≥1 rota "ótima"** — pelo menos um portal cujo efeito explora a fraqueza exibida no cartão do Boss Scout (elemento forte contra o boss, classe counter, mutação que neutraliza o ataque especial). Posicionado preferencialmente num dos 2 últimos pares, para que a recompensa da leitura chegue perto do clímax.
2. **≥1 "armadilha aparentemente boa"** — um portal de número alto ou visual chamativo que é anti-sinérgico com o boss: o elemento do PRÓPRIO boss (−50% de dano), um multiplicador que estoura o Supply sem ganho de poder, ou Gigantes lentos contra boss de ataque em área. A armadilha nunca mente — ela só não combina, e o cartão do Boss Scout deu a informação para perceber isso.
3. **O último par é sempre "o par do boss":** um lado conversa com a fraqueza, o outro é a armadilha ou um genérico forte. É a prova final da leitura do Scout.

Exemplo jogável (fase 9, boss variante de Gelo, fraco contra Fogo): par 1 `+10 vs x2` (A) · par 2 `Virar Arqueiro vs +25` (A) · par 3 `+25 vs Zona de Perigo x10` (C) · par 4 (par do boss) `Elemento Fogo vs Elemento Gelo` — o Gelo brilha lindo e azul, e corta seu dano pela metade contra o boss de Gelo. Quem tocou no ícone do boss na barra de progresso (lembrete de 1 s, CANON §3.1) não cai.

### 5.4 Validação automática (pós-geração)

Antes de servir a fase, o `LevelManager` roda dois bots de simulação:

- **Bot guloso** (sempre escolhe o maior número/efeito mais chamativo): precisa terminar com poder suficiente para a taxa de vitória alvo da fase (CANON §12: 95% fases 1–3, 85% fases 4–10, ~70% meio de mundo, ~55% fase 10).
- **Bot ótimo** (joga a rota ótima): não pode trivializar o boss além de 3× o alvo (vitória boa, não tédio).
- Checagens estruturais: ≥1 rota ótima, ≥1 armadilha, cotas de arquétipo respeitadas, nenhum D consecutivo, fase mín. e pesos respeitados. Reprovou → regenera com novo seed (custo < 2 ms, roda no load).

**Seed:** fixo por fase nas fases 1–10 (replays idênticos durante o aprendizado — o jogador derrotado refina o plano, não reroda a sorte); aleatório por tentativa da fase 11 em diante (rejogabilidade). Pesos, cotas e odds são expostos no Remote Config (`RemoteConfigManager`) para tuning sem build.

---

## 6. Telegraphing visual e sonoro por categoria

A categoria precisa ser identificável a 25 m **antes** do número ser legível, e sem depender só de cor (acessibilidade para daltônicos: cada categoria tem cor + forma de borda + ícone distintos).

| Categoria | Cor do arco | Ícone central | Borda | SFX de aproximação |
|---|---|---|---|---|
| Matemático positivo | Azul elétrico `#2E8BFF` | Número gigante com "+/x" | Sólida branca, limpa | "Ping" ascendente |
| Matemático negativo | Vermelho `#FF3B30` | Número com "−/÷" | Rachada/quebrada | Zumbido grave |
| Classe | Verde `#2ECC71` | Silhueta da tropa + seta de transformação | Dupla (anel interno girando) | "Whoosh" de transformação |
| Elemento | Cor do elemento (Fogo laranja, Gelo ciano, Raio amarelo, Veneno verde-musgo, Luz branco-dourado, Sombra roxo-escuro, Metal cinza-aço, Alien magenta) | Glifo do elemento | Partículas do próprio elemento orbitando | Som do elemento (crepitar, cristais, estalo...) |
| Mutação | Roxo `#9B59B6` | Ícone da parte do corpo | Orgânica, pulsante (batimento 1,2 Hz) | Batida de coração |
| Risco | Dourado `#FFC93C` | Porcentagens/tradeoff em texto grande + dado | Listras preto-amarelo animadas (hazard) | Tique-taque de tensão |

Regras adicionais: números formatados com K/M acima de 9.999; porcentagens de risco em fonte ≥ 60% da altura do número principal; o portal NÃO exibe raridade de spawn (peso é dado interno, não informação de jogo); em pares com 3 slots de mutação cheios, o medalhão a ser perdido pisca em vermelho no HUD (§4).

---

## 7. Regras anti-frustração

Princípio: **o jogador pode perder poder por escolha, nunca por trapaça ou por matemática cruel.**

### 7.1 Piso de 1 unidade

`÷2` e `−10` (e qualquer efeito negativo) **nunca zeram o exército** — piso absoluto de 1 unidade. ÷2 arredonda sobreviventes para cima (⌈n/2⌉). Efeitos negativos removem as unidades **mais baratas primeiro** (o jogador nunca perde o Gigante para um −10). Ficar com 1 unidade dispara o feedback "ÚLTIMO SOBREVIVENTE!" — que é também uma das cenas virais desejadas pelo BRIEF (último soldado vencendo o boss).

### 7.2 Estouro de Supply = fanfarra, nunca punição (CANON §3.2)

Excedente convertido automaticamente em **2 moedas por ponto de Supply excedente × Mb (multiplicador de mundo, doc 07 §2.1)** — taxa controlada pela chave `supply_overflow_coin_rate` do Remote Config (default 2, faixa segura 1–4, doc 07 §9). Apresentação: jato de moedas voando para o contador + texto "MEGA ARMY!" + som de caixa registradora. Exemplo (Mundo 1, Mb = 1,0): ER atravessa x5 → 150 unidades viram 60 efetivas + 90 Supply excedente = **180 moedas** com chuva dourada. O jogador sorri em vez de sentir que "perdeu" 90 soldados.

### 7.3 Proteção estrutural de pares

- **Nunca dois pares "ruim vs ruim" (arquétipo D) seguidos** — regra dura do gerador, mantida mesmo se o Remote Config aumentar pesos de portais negativos. Reforço: máx. 1 par D por fase, nunca no último par, e inexistente antes da fase 15.
- Fases 1–3 não contêm portais negativos fora do arquétipo B (ruim óbvio pareado com bom óbvio — é tutorial de leitura, não pegadinha).
- **Pity de derrota:** após 2 derrotas consecutivas na mesma fase, o peso de portais de VE positivo sobe +25% na próxima tentativa (parâmetro `gate_pity_boost` no Remote Config). Combina com a regra canônica de nunca exibir interstitial após duas derrotas seguidas (CANON §11).

### 7.4 Honestidade auditável

Odds exibidas = odds executadas, com seed do RNG logado em `gate_selected` para auditoria interna. Nenhum portal tem efeito oculto. Se um efeito não couber no arco em ≤4 palavras + 1 número, o portal não entra no jogo (filtro de design).

---

## 8. Design para anúncio — pares "qual você escolheria?"

Os criativos usam **cenários reais do gerador** (números autênticos, capturados in-game) — anúncio honesto envelhece melhor e o CPI alvo (CANON §12) depende de retenção pós-instalação, não só de clique. Pares com melhor performance esperada por formato do BRIEF:

| Par (esquerda vs direita) | Formato de anúncio | Por que funciona |
|---|---|---|
| **x5 (multidão formigando) vs Virar Gigante (2 colossos)** | 1 — Escolha / 7 — Comparação | Quantidade vs qualidade é o debate perfeito de comentários; ambos parecem certos e a resposta depende do boss — engajamento sem resposta "burra" |
| **+25 seguro vs Zona de Perigo x10** | 4 — Desafio / 6 — Quase-derrota | Serras girando + multiplicação ×10 dos sobreviventes = tensão e payoff visual no mesmo plano de 5 s |
| **Elemento Fogo (modesto) vs Elemento Gelo (deslumbrante) antes de boss de Gelo** | 8 — Curiosidade ("parece ruim, mas é o melhor") | Inverte a intuição do espectador; o cartão do Boss Scout aparece no início do vídeo e recompensa quem prestou atenção |
| **÷2 escolhido de propósito** (para ativar fresta lateral ou setup de Sacrifício do Titã) | 2 — Erro ("não cometa esse erro") | O exército derretendo pela metade é a cena de "dor" mais legível do gênero; o plot twist da jogada inteligente segura até o fim |
| **+50 vs Sacrifício do Titã** | 1 — Escolha / 3 — Evolução | "Metade do exército por ISSO?" — o Titã entrando em câmera lenta é o money shot; números grandes dos dois lados |
| **x2 garantido vs Mutação Lendária Instável (65%/35%)** | 5 — Satisfação / 4 — Desafio | Gambling visível e honesto; os 35% de fumaça geram tanto clipe quanto o acerto |
| **4ª mutação substituindo a mais antiga** (Asas estourando para entrar Laser) | 3 — Evolução | Transformação corporal visível quadro a quadro — o "humano→fogo→asas→laser" do BRIEF em um único portal |

Diretriz de captura: câmera nos 25 m de aproximação (o par inteiro em quadro), corte no flash do arco, payoff em ≤2 s. Todos os pares acima existem no gerador com as cotas do §5.2 — o time de UA nunca precisa "encenar" uma situação falsa.

---

## 9. Dados, telemetria e implementação

### 9.1 `GateConfigSO` (CANON §13)

Campos: `gateId`, `category` (Math/Class/Element/Mutation/Risk), `displayLabel` (número/odds exibidos), `effectType` + `effectParams` (ex.: `multiplier=2`, `targetUnitId=archer`, `odds=[0.65,0.35]`), `minLevel`, `spawnWeight`, `allowedPairArchetypes`, `vfxColor`, `iconRef`, `sfxRef`, `isMvp`. O `GateManager` resolve efeitos; o `CrowdManager` aplica em lote; `LevelManager` roda geração + validação do §5.

### 9.2 Eventos de analytics (BRIEF)

- `gate_selected` — params: `gate_id`, `gate_category`, `pair_id`, `pair_archetype`, `level`, `army_count_before/after`, `supply_before/after`, `mutation_slots`, `rng_seed`.
- `gate_missed` — disparado ao evitar ambos os portais do par (fresta lateral); params: `pair_id`, `level`.

Esses dois eventos alimentam a métrica "portal mais escolhido" do BRIEF e o tuning de pesos via Remote Config: portal com taxa de escolha >75% em par A está forte demais (nerf de peso ou de número); par A com escolha 50/50 é o ideal do arquétipo.

### 9.3 Resumo do MVP (CANON §10 e §15)

| # | Portal | Grupo | Fase mín. (MVP 1–20) |
|---|---|---|---|
| 1 | +10 | Matemático | 1 |
| 2 | x2 | Matemático | 1 |
| 3 | +25 | Matemático | 2 |
| 4 | Virar Arqueiro | Classe | 2 |
| 5 | x3 | Matemático | 3 |
| 6 | ÷2 | Matemático | 3 |
| 7 | Elemento Fogo | Elemento | 4 |
| 8 | Zona de Perigo x10 | Risco | 6 |

O MVP valida os 5 sabores de decisão com 1 representante de cada eixo: crescer (+/x), perder (÷2), transformar (classe), planejar contra o boss (elemento) e arriscar (zona de perigo). Mutações e os demais 25 portais entram no pós-MVP nas fases mínimas das tabelas do §3, sem mudança de arquitetura — tudo é `GateConfigSO` + pesos no Remote Config.
