# Sistema de Bosses — Mutant Army Run
> Entregável 10 do pacote de design · Versão 1.0 · 2026-06-11
> Fontes da verdade: `CANON.md` (decisões fixas) e `BRIEF.md` (requisitos). Em conflito de detalhe, o CANON prevalece.
> Docs irmãos referenciados: doc 03 (Sistema de Unidades), doc 04 (Sistema de Portais), doc 06 (Sistema de Fases), doc 07 (Economia), doc 08 (Monetização), doc 12 (Arquitetura Unity — classes C# e ScriptableObjects).

## 1. Papel do boss no loop

O boss é o **clímax de toda fase** (CANON §6: toda fase termina em arena com boss) e o ponto onde os 4 pilares se encontram:

| Pilar | Como o boss o serve |
|---|---|
| 1. Legível em 3 s | Boss gigante + barra de vida gigante + ícone de fraqueza no HUD. Qualquer frame da arena comunica "destrua isso". |
| 2. Escolha inteligente | O Boss Scout (CANON §3.1) transforma a corrida inteira em preparação para este momento: o jogador escolheu portais *para este boss*. |
| 3. Espetáculo constante | Entrada cinematográfica, telegraphs vistosos, slow motion na morte, explosão de moedas — o material de anúncio nasce aqui. |
| 4. Progressão em 3 camadas | Drops de carta/fragmento conectam a vitória à meta-progressão; o boss de mundo é o "portão" entre mundos. |

**Orçamento de tempo canônico:** corrida 45–75 s → boss **10–20 s** → fase completa ≈ 60–90 s. Todo design abaixo respeita esse teto: nenhum moveset, fase de vida ou cutscene pode estourar 20 s de combate + 2 s de entrada.

---

## 2. Anatomia canônica de um boss

Todo boss do jogo — do menor mini-boss da fase 1 à Entidade Dimensional — segue exatamente esta espinha dorsal (CANON §6). É um contrato entre design, arte e engenharia: o `BossManager` executa esta máquina de estados para qualquer `BossConfigSO`.

### 2.1 Linha do tempo de referência (combate-alvo de 15 s)

| t (s) | Estado | O que acontece |
|---|---|---|
| 0,0–2,0 | `Entrance` | Câmera corta para a arena; boss entra com animação própria (≤2 s, hard cap); rugido/SFX; banner de fraqueza no HUD ("FRACO CONTRA FOGO 🔥", mesmo card do Boss Scout); exército se posiciona automaticamente. O boss é **invulnerável** e não ataca durante a entrada. |
| 2,0 | `Combat` (fase de vida 100%) | Exército começa a atacar automaticamente. Boss entra no loop de ataque telegrafado (§2.2). |
| ~7 s | Limiar 50% de HP | Transição de fase de vida: stagger de 0,8 s (boss cambaleia, peças caem — feedback de progresso), novo golpe entra no ciclo, telegraphs encurtam. |
| ~11 s | Limiar 25% de HP | Fase "desespero": boss acelera, fica visualmente danificado (rachaduras, faíscas, fumaça), telegraph mínimo, e expõe ponto fraco com mais frequência. |
| ~14 s | HP = 0 → `Death` | O golpe final dispara **slow motion 0,3× por 0,8 s** (valor canônico do pacote — doc 12, VFXManager) com zoom de câmera; boss **desmonta em peças/partículas** (sem sangue — CANON §1); texto "BOSS BREAKER". |
| +0,8 s | `Reward` | Explosão de recompensa: fonte de moedas voando para o contador, baú/carta/fragmento quicando no chão, painel de vitória. |

### 2.2 Loop de ataque telegrafado

Regra de ouro: **o boss nunca causa dano sem avisar antes**. Todo golpe segue `Telegraph → Strike → Recovery`:

| Etapa | Duração padrão | Visual |
|---|---|---|
| Telegraph | 1,2 s (fase 100%) · 1,0 s (50%) · 0,8 s (25%) | Decal vermelho pulsante no chão (círculo, linha ou cone) + pose de "carregar" do boss + SFX de aviso |
| Strike | 0,2–0,4 s | Impacto, screen shake leve, unidades atingidas desmontam em peças |
| Recovery | 1,2–1,8 s | Boss vulnerável; janela em que o exército "ganha de volta" |

Justificativa: em um combate de 10–20 s não há tempo para aprender padrões por tentativa e erro. O telegraph com decal no chão é legível em qualquer frame (pilar 1) e cria a única habilidade de execução do jogador na arena: **reposicionar o exército** (§3.3).

### 2.3 Fases de vida (100% / 50% / 25%)

| Limiar | Nome interno | Mudanças obrigatórias | Mudanças opcionais por boss |
|---|---|---|---|
| 100% | `PhaseNormal` | Moveset básico, telegraph 1,2 s, intervalo entre golpes 3,0–3,5 s | — |
| 50% | `PhaseEnraged` | +1 golpe novo no ciclo, telegraph 1,0 s, intervalo 2,5 s, mudança visual (cor/dano no corpo) | invocar lacaios, mudar arena (lava avança, piso quebra) |
| 25% | `PhaseDesperate` | Telegraph 0,8 s, intervalo 2,0 s, VFX de "quase lá" (rachaduras brilhando) | golpe contínuo, fraqueza extra exposta, auto-dano cênico |

Justificativa: os limiares fixos dão ritmo de história em 15 s (apresentação → virada → clímax), dão ao jogador leitura clara de progresso além da barra de vida e criam o "quase perdi" dos vídeos virais (BRIEF · Viralização: "boss quase vencendo"). O Alien Supremo (M8) usa limiares a cada 25% por regra canônica própria (rotação de fraqueza).

### 2.4 Morte e recompensa

1. Último hit → `timeScale` global 0.3 por 0,8 s, com `fixedDeltaTime` escalado junto (doc 12 §3.1); câmera dá zoom no impacto (BRIEF · Game feel: "slow motion no golpe final").
2. Boss desmonta em 8–20 peças físicas + partículas da paleta do mundo. Nunca gore.
3. Peças somem em 0,8 s viradas em moedas — a "explosão de recompensa" é literal: as moedas saem do corpo do boss.
4. Drops quicam no chão da arena (carta/fragmento/baú) antes do painel de vitória — o jogador *vê* o drop no mundo, não só na UI.
5. Texto de feedback: `BOSS BREAKER` padrão; `GODLIKE` se nenhuma unidade morreu; `PERFECT` se nenhum golpe do boss acertou.

### 2.5 Drops (padrões propostos; valores finais e fonte da verdade no doc 07 — Economia)

| Fonte | Moedas | Gemas | Fragmentos (Shards) | Carta | Baú |
|---|---|---|---|---|---|
| Mini-boss tier 1 (fases 1–3 do mundo) | incluídas na recompensa da fase (CANON §8) | — | 20% → 3 da tropa em destaque | 8% comum | — |
| Mini-boss tier 2 (fases 4–6) | idem | — | 25% → 5 | 12% comum | — |
| Mini-boss tier 3 (fases 7–9) | idem | — | 30% → 8 | 15% comum/rara | — |
| **Boss de mundo (fase 10)** | recompensa da fase ×1,5 | **10 (CANON §8)** | 100% → 10 | 100% rara+, 40% épica | Baú de mundo garantido |

Todas as chances são chaves de Remote Config (`boss_drop_*`). Rewarded ad de "dobrar recompensa" dobra **apenas as moedas** da tela de vitória (+100%, só moedas — nunca gemas nem fragmentos), conforme o doc 07 §2.1.

### 2.6 Contrato de dados — `BossConfigSO` (CANON §13)

Campos mínimos que este documento alimenta: `bossId`, `displayName`, `worldId`, `archetypeTier`, `element`, `weaknesses[]`, `immunities[]`, `resistances[]`, `hpTuning {ttkTarget, difficultyM}`, `moves[] {moveId, shape, telegraphTime, damagePctP50, cooldown, minLifePhase}`, `minionSpawn {prefab, ratePerSec, cap}`, `entranceClip (≤2 s)`, `deathPieces`, `dropTable`, `videoMomentTag`. O `BossManager` consome o SO; nenhum número de boss vive em código.

---

## 3. Como o combate funciona mecanicamente

### 3.1 Arena e ataque automático

- A corrida termina num **funil** que desemboca na arena (retângulo ~12 m de largura × 10 m de profundidade, câmera fixa em ângulo levemente mais baixo para o boss parecer enorme).
- **O exército ataca sozinho.** Cada unidade adquire o boss como alvo e dispara seu ataque no próprio ritmo (dados do doc 03). Não há botão de atacar. Justificativa: o jogo já foi "jogado" na corrida — a arena é a colheita da estratégia (pilar 2) e precisa ser 100% legível como espetáculo (pilar 1).
- Lacaios invocados pelo boss têm prioridade de alvo para unidades corpo a corpo da linha de frente; unidades de longo alcance continuam no boss (regra simples: "quem está perto limpa, quem está longe derruba").

### 3.2 Papel de cada classe na arena (auto-posicionamento)

| Classe | Posição automática | Papel no boss |
|---|---|---|
| Escudeiro, Gigante, Titã, Robô | Linha 1 (frente) | Absorvem os golpes; corpo do boss mira neles por padrão |
| Soldado, Corredor, Ninja | Linha 2 | DPS corpo a corpo; Ninja tem 50% de chance de ignorar dano de telegraph (esquiva com VFX) |
| Arqueiro, Mago, Lança-Chamas, Tropa Glacial, Alien | Linha 3 (fundo) | DPS à distância; aplicam elementos — é aqui que a fraqueza do boss vira número grande |
| Médico, Anjo de Guerra | Linha 3, centro | Curam a linha 1; viram MVP visível em bosses de pressão contínua (Zumbi Titã) |
| Engenheiro | Canto da arena | Constrói torreta (CANON §5) que atira no boss — DPS fixo que ignora reposicionamento |
| Necromante | Linha 3 | Revive caídos durante o Recovery do boss (momento de respiro) |
| Dragão, Mecha Supremo, Demônio Mutante | Voo / Linha 3 | Dano em área nos lacaios + burst no boss; Dragão ignora telegraphs de chão (voa) |

### 3.3 O que o jogador controla — decisão de design

**Decisão: no MVP, o único input na arena é o mesmo da corrida — arrastar o dedo na horizontal para reposicionar a formação.** Os decals de telegraph aparecem no chão; quem não tirar o exército de lá perde unidades. Nada de novos botões, nada de tutorial extra.

Justificativa (em ordem de pilar):
1. **Pilar 1:** zero input novo = zero curva de aprendizado. Quem assiste a um anúncio entende o combate em 1 frame: "boss vai bater ali, exército saiu de lá".
2. **Pilar 2:** a inteligência do jogo está nos portais + Boss Scout. Se a arena exigisse micro complexa, puniria quem planejou bem e premiaria reflexo — invertendo o pilar.
3. **Espetáculo:** com a mão só arrastando, os olhos ficam livres para números de dano, peças caindo e a barra de vida derretendo.

**Rejeitado para o MVP:** tap-to-focus (escolher alvo) — adiciona decisão de baixa legibilidade em um combate de 15 s; a regra automática de prioridade de alvos resolve 95% dos casos. **Pós-MVP (aprovado para teste A/B):** "Ponto Fraco" — a cada transição de fase de vida, um núcleo brilhante fica exposto por 2 s; **um toque** nele dispara golpe crítico do exército inteiro (5% do HP do boss, número gigante na tela). Mantém um único toque, é opcional e gera clipe de anúncio ("toque no ponto fraco!").

### 3.4 Boss Scout fecha o ciclo

O card de ~2 s pré-fase (CANON §3.1) mostra exatamente o banner que reaparece na entrada do boss. Durante a corrida, tocar no ícone do boss na barra de progresso reabre o lembrete por 1 s. Consequência para o design de fases (doc 06): os portais da fase são gerados em função do boss — sempre há ≥1 rota ótima (ex.: portal de Fogo antes do Gigante de Madeira) e 1 armadilha aparentemente boa (ex.: x3 de tropas de elemento igual ao do boss → −50% de dano, CANON §4).

### 3.5 Condições de derrota e válvulas de tensão

- **Derrota = exército zerado** antes do boss morrer. Não existe timer de derrota: se o jogador sobrevive com 1 Soldado, pode vencer ("último soldado vencendo o boss" é cena desejada no BRIEF).
- Reviver com rewarded ad: 1×/fase (CANON §11) — volta com 50% do exército que entrou na arena, boss mantém o HP atual.
- A tela de derrota informa **o motivo** (BRIEF · tela 5): "Seu exército era de GELO contra um boss de GELO (−50% de dano)" ou "O Soco Sísmico atingiu 80% do exército — arraste para desviar".

---

## 4. Fórmula de dificuldade e tuning

> **Fonte única de tuning de bosses do MVP.** Os valores definidos neste documento — HP por fase (§4.3 e §7), TTK, fator M e win rate alvo por fase (§4.5), além da constante de **2,0 de DPS por ponto de Supply** (baseline Soldado do CANON §5) — prevalecem sobre qualquer outra tabela do pacote. O doc 06 (§5.2 e §8), o doc 02 (§4.3) e o doc 11 (§6.1) consomem estes números por referência; todo rebalanceamento começa aqui e propaga para os demais docs.

### 4.1 Fórmula canônica de HP

```
HP_boss(fase) = DPS_p50(fase) × TTK_alvo(fase) × M(win_rate_alvo)
```

- **`DPS_p50(fase)`** — dano por segundo do exército mediano ao chegar à arena. Modelo: `Supply_usado_p50 × 2 (DPS por ponto de Supply, baseline Soldado do CANON §5) × Mult_upgrades(fase) × Mult_elemental_esperado`. `Mult_elemental_esperado = 1 + 0,5 × adoção_da_rota_de_fraqueza` (telemetria `gate_selected`; estimativa pré-launch: 60% nas fases com Boss Scout óbvio).
- **`TTK_alvo`** — tempo-alvo para matar: 8 s (fases 1–3) crescendo até 16 s (fase 10 de mundo). Nunca projeta acima de 20 s para o p50 (teto canônico).
- **`M`** — fator de dificuldade derivado da taxa de vitória alvo do CANON §12:

| Win rate alvo (CANON §12) | Onde se aplica | M |
|---|---|---|
| 95% | fases 1–3 | 0,60 |
| 85% | fases 4–10 (banda geral) | 0,80 |
| ~70% | meio de mundo | 0,95 |
| ~55% | fase 10 de cada mundo | 1,10 |

### 4.2 Fórmula de dano do boss

O HP decide *se o jogador mata a tempo*; o dano do boss decide *se o exército sobrevive até lá*:

```
DPS_pressão = HP_exército_p50 × k(win_rate_alvo)        // dano "ambiente" por segundo
Dano_especial = HP_exército_p50 × burst(win_rate_alvo)   // golpe telegrafado NÃO desviado
```

| Win rate alvo | k (pressão/s) | burst (por especial) |
|---|---|---|
| 95% | 2% | 8% |
| 85% | 4% | 12% |
| 70% | 6% | 18% |
| 55% | 8% | 25% |

`HP_exército_p50 = Supply_usado_p50 × 10 (HP por ponto de Supply) × Mult_upgrades`. Quem desvia dos telegraphs sofre só a pressão ambiente — desviar bem compensa um exército ~20% mais fraco, que é exatamente a margem entre p50 e p30. Resultado: a taxa de vitória cai sobre o percentil desejado de jogadores.

### 4.3 Exemplo calculado — Gigante de Madeira (fase 7 do MVP)

1. `Supply_usado_p50` na fase 7 ≈ 45 (de um teto de 60, CANON §3.2) → DPS base = 45 × 2 = 90.
2. `Mult_upgrades` ≈ 1,15 (jogador mediano com ~3 níveis somados nas 4 trilhas do MVP, +5%/nível — CANON §9).
3. `Mult_elemental_esperado` = 1 + 0,5 × 0,60 = 1,30 (Boss Scout anuncia "FRACO CONTRA FOGO"; 60% pegam o portal de Fogo).
4. `DPS_p50` = 90 × 1,15 × 1,30 ≈ **135**.
5. Boss de mundo no MVP comprimido → win rate alvo 75% (ver §4.5) → M = 0,88; `TTK_alvo` = 14 s.
6. **HP = 135 × 14 × 0,88 ≈ 1.660 → arredonda para 1.600.** Verificação: p50 mata em ~11,9 s ✔ (dentro de 10–20 s); jogador sem rota de Fogo (DPS ≈ 104) mata em ~15,4 s ✔ ainda vence se desviar; exército p25 sem Fogo e sem desviar morre antes — é ele quem compõe os ~25% de derrota.
7. Dano: `HP_exército_p50` = 45 × 10 × 1,15 ≈ 520 → pressão = 4,5%/s ≈ 23/s; Pisão de Raiz (especial) = 18% ≈ 94 por acerto não desviado.

### 4.4 Válvulas de Remote Config (CANON §13 / BRIEF · Tecnologia)

| Chave | Default | Faixa | Efeito |
|---|---|---|---|
| `boss_hp_global_mult` | 1,00 | 0,50–2,00 | Multiplica HP de todos os bosses |
| `boss_dmg_global_mult` | 1,00 | 0,50–2,00 | Multiplica pressão e burst |
| `boss_telegraph_mult` | 1,00 | 0,70–2,00 | Multiplica a janela de telegraph (↑ = mais fácil) |
| `boss_<bossId>_hp_mult` | 1,00 | 0,50–2,00 | Override por boss (ex.: `boss_m3_scorpion_hp_mult`) |
| `boss_<bossId>_dmg_mult` | 1,00 | 0,50–2,00 | Override de dano por boss |
| `boss_minion_rate_mult` | 1,00 | 0,00–2,00 | Taxa de invocação de lacaios (0 desliga) |
| `boss_drop_card_chance_mult` | 1,00 | 0,00–3,00 | Ajuste fino de drops sem rebuild |
| `boss_revive_army_pct` | 0,50 | 0,25–1,00 | % do exército que volta no revive por ad |

Rotina de live-ops: comparar semanalmente a win rate real por `bossId` (eventos §8) com o alvo do CANON §12; desvio >5 p.p. → ajustar `*_hp_mult`/`*_dmg_mult` em passos de ±10%; nunca tocar no telegraph antes do HP (mexer no aviso muda a *sensação*, não só o número).

### 4.5 Alvos de win rate no MVP (mundos comprimidos)

A regra "~55% na fase 10 de cada mundo" vale para o release completo. No MVP (M1 = fases 1–7, M2 = 8–14, M3 = 15–20 — CANON §7), aplicamos gradiente para não criar paredão precoce. **Esta tabela é a fonte única dos alvos de win rate do MVP**: o doc 06 §8, o doc 02 §4.3 e o doc 11 §6.1 a referenciam.

| Fase MVP | Boss | Win rate alvo | M |
|---|---|---|---|
| 1–3 | Golem de Pedra T1 | 95% (fase 1 impossível de perder na prática — CANON §16) | 0,60 |
| 4–6 | Golem de Pedra T2/T3 | 85% | 0,80 |
| 7 | **Gigante de Madeira** | 75% | 0,88 |
| 8–13 | Brutamontes Zumbi T1–T3 | 80% · 78% · 76% · ~70% (derrota desenhada — doc 02 §4.3) · 75% (recuperação) · 70% | 0,85–0,95 |
| 14 | **Zumbi Titã** | 65% | 1,00 |
| 15–19 | Robô Escorpião Protótipo | 70% · 69% · 67% · 66% · 65% | 0,95–1,00 |
| 20 | **Robô Escorpião** | 55% | 1,10 |

---

## 5. Os 10 bosses únicos de mundo (fase 10 de cada mundo)

Fraquezas/imunidades são canônicas (CANON §6) — proibido alterar. "HP relativo" = múltiplo de segundos de `DPS_p50` da fase (já com M = 1,10 e TTK crescente), para que o tuning sobreviva a rebalanceamentos de tropas. Todos: win rate alvo ~55%, 10 gemas + baú de mundo no drop.

| # | Boss (mundo) | HP relativo | Ataque especial (telegrafado) | Fraqueza / Imunidade | Mecânica única | Momento de vídeo |
|---|---|---|---|---|---|---|
| 1 | **Gigante de Madeira** (Campo Inicial) | 14 s × DPS_p50 | Pisão de Raiz: raízes irrompem em linha frontal | Fraco: Fogo | **Queima Cumulativa** — seções do corpo acendem com dano de Fogo; cada seção em chamas = +3% de dano recebido (máx. +12%) e cai como tora ao "morrer" | Boss inteiro em chamas desmontando em toras que viram chuva de moedas |
| 2 | **Zumbi Titã** (Cidade Zumbi) | 15 s × DPS_p50 | Palmada Dupla: duas mãos, dois círculos simultâneos | Fraco: Fogo e Luz · Imune: Veneno | **Braço Rastejante** — aos 50% o braço se solta e ataca o fundo da arena; jogador precisa reposicionar entre duas ameaças | Horda de 200 unidades vs titã + braço perseguindo o exército |
| 3 | **Robô Escorpião** (Deserto Robótico) | 16 s × DPS_p50 | Varredura de Cauda: laser horizontal com 2 zonas seguras | Fraco: Raio · Imune: Veneno | **Núcleo Exposto** — após cada especial, núcleo abre 2 s (+50% dano recebido); Raio no núcleo paralisa o boss por 2 s | Slow motion decepando a cauda no golpe final, chuva de parafusos + moedas |
| 4 | **Planta Carnívora Gigante** (Floresta Mutante) | 15 s × DPS_p50 | Bocada: engole até 10 unidades (círculo na frente da boca) | Fraco: Fogo e Veneno | **Brotos-Escudo** — 3 brotos laterais regeneram 2% HP/s do boss enquanto vivos; o exército os limpa primeiro (prioridade automática) | Unidades engolidas escapam quando o boss explode em folhas e pétalas |
| 5 | **Dragão de Lava** (Vulcão dos Gigantes) | 16 s × DPS_p50 | Chuva de Meteoros: 5 círculos aleatórios | Fraco: Gelo · Resiste: Fogo | **Arena que Encolhe** — a cada fase de vida, lava avança 2 m e reduz o espaço seguro | Dragão congelado em pleno voo estilhaçando em cristais |
| 6 | **Rei de Gelo** (Reino Congelado) | 15 s × DPS_p50 | Nevasca: cone que congela unidades por 1,5 s | Fraco: Fogo · Resiste: Gelo | **Escudo de Gelo** — barreira de 15% do HP que regenera após 6 s; Fogo a derrete 3× mais rápido | Trono derretendo em cascata enquanto o rei desmonta em blocos de gelo |
| 7 | **Cavaleiro Colosso** (Arena Medieval) | 16 s × DPS_p50 | Espadão em Arco: varredura de 180° em metade da arena | Fraco: Raio (armadura conduz) | **Armadura por Peças** — 4 peças (elmo, peitoral, 2 ombreiras); cada 25% de HP derruba uma e acelera o boss em 10% | Raio encadeando (CANON §4) por toda a armadura em um único flash |
| 8 | **Alien Supremo** (Laboratório Alienígena) | 16 s × DPS_p50 | Raio Trator: suga unidades para a boca (linha central) | **Fraqueza rotativa a cada 25% de HP, sempre exibida no HUD** | **Portais na Arena** — a cada rotação de fraqueza, 2 portais de elemento surgem na arena; atravessar converte o exército para contra-atacar | HUD girando a roleta de fraqueza enquanto o exército troca de elemento ao vivo |
| 9 | **Mecha Supremo** (Planeta Mecânico) | 17 s × DPS_p50 | Laser Contínuo: varre a arena de um lado ao outro | Fraco: Raio · Imune: Veneno | **Superaquecimento** — após cada salva de mísseis, exaustores abrem 2,5 s (+50% dano); acertar Raio nos exaustores cancela a próxima salva | Mísseis interceptados por Dragões + explosão final em cogumelo de moedas |
| 10 | **Entidade Dimensional** (Dimensão Final) | 18 s × DPS_p50 | Lança do Vazio: 3 fendas verticais em sequência | **Alterna elementos** (ciclo Fogo→Gelo→Raio a cada fase de vida, exibido no HUD) | **Usa os portais do jogador contra ele** — invoca portais ÷2 que perseguem o exército; aos 25%, o jogador pode atraí-la para um portal ÷2 refletido, cortando o HP dela pela metade | O boss caindo no próprio ÷2 — "faça o boss entrar no portal errado" é o anúncio perfeito |

Notas de coerência elemental (CANON §4): Veneno = 0% contra Robô Escorpião, Mecha Supremo (máquinas) e Zumbi Titã (morto-vivo) — a imunidade canônica é a própria regra do chart, então o HUD a exibe como "IMUNE ☠️" para ensinar o chart. Bosses de Fogo/Gelo punem mesmo-elemento com −50% — a "rota armadilha" do Boss Scout em M5/M6.

---

## 6. Arquétipos regionais — 30 mini-bosses (fases 1–9 de cada mundo)

Cada mundo tem **3 arquétipos** (CANON §6) que cobrem as fases 1–9 em rotação A·A·A → B·B·B → C·C·C, escalando em **tier**:

| Tier | Fases do mundo | Escala visual | Cor | Moveset | HP |
|---|---|---|---|---|---|
| T1 | 1–3 | ×1,0 | paleta base | 2 golpes | fórmula §4.1 da fase |
| T2 | 4–6 | ×1,2 | saturada + detalhe novo (espinhos, faíscas) | +1 golpe | fórmula §4.1 da fase |
| T3 | 7–9 | ×1,4 | variante "alfa" com VFX de aura | +1 golpe e telegraph −0,1 s | fórmula §4.1 da fase |

> O HP **sempre** sai da fórmula por fase — o tier muda silhueta, cor e moveset, nunca cria curva de HP paralela. Um rig por arquétipo, 3 materiais: 30 mini-bosses com custo de produção de 10.

| Mundo | Arquétipo (A/B/C) | Fraqueza | Mecânica única (1 linha) |
|---|---|---|---|
| M1 Campo Inicial | **Golem de Pedra** | Fogo (musgo seco) | Soco Sísmico em círculo; pedras dos ombros caem como feedback de dano |
| M1 | **Espantalho Desperto** | Fogo | Lança corvos em linha reta que atravessam a arena |
| M1 | **Topeira Colossal** | Veneno | Some no chão e emerge sob o exército (decal de terra avisa) |
| M2 Cidade Zumbi | **Brutamontes Zumbi** | Fogo · Imune: Veneno | Arremessa entulho em parábola; perde o braço aos 50% |
| M2 | **Cão Apodrecido** | Fogo · Imune: Veneno | Salto sobre a linha de frente direto na linha 3 (caça os Arqueiros) |
| M2 | **Sargento Caído** | Luz · Imune: Veneno | Apita e invoca 3 zumbis pequenos a cada 4 s |
| M3 Deserto Robótico | **Triturador de Sucata** | Raio · Imune: Veneno | Serra giratória que percorre uma lane; deixa rastro perigoso por 2 s |
| M3 | **Drone-Mãe** | Raio · Imune: Veneno | Flutua fora de alcance corpo a corpo; só DPS à distância a atinge bem |
| M3 | **Tanque Enferrujado** | Raio · Imune: Veneno | Canhão de longo telegraph (1,6 s) com dano altíssimo — ensina a desviar |
| M4 Floresta Mutante | **Cogumelo Ancião** | Fogo | Nuvem de esporos persistente (zona de dano contínuo de Veneno) |
| M4 | **Vinha Estranguladora** | Fogo | Agarra 5 unidades e as suspende; matá-la as liberta |
| M4 | **Sapo Tóxico** | Gelo | Língua puxa a unidade mais forte do exército para perto dele |
| M5 Vulcão dos Gigantes | **Magmoide** | Gelo · Resiste: Fogo | Slam que deixa poça de lava permanente — arena encolhe a cada golpe |
| M5 | **Morcego de Cinzas** | Gelo · Resiste: Fogo | Mergulhos rasantes em linha; entre mergulhos fica vulnerável no chão |
| M5 | **Ciclope Forjador** | Gelo | Martelada com onda de choque em anel (zona segura: perto OU longe) |
| M6 Reino Congelado | **Yeti das Geleiras** | Fogo · Resiste: Gelo | Sopro que congela unidades por 1,5 s (lentidão canônica de Gelo) |
| M6 | **Estilhaço Vivo** | Fogo · Resiste: Gelo | Dispara espinhos de gelo em leque; ao morrer explode em estilhaços |
| M6 | **Lobo Boreal** | Fogo | Uiva e ganha +30% velocidade; circula a arena atacando os flancos |
| M7 Arena Medieval | **Campeão da Arena** | Raio | Escudo frontal bloqueia 80% do dano — reposicionar para flanquear |
| M7 | **Besta de Cerco** | Fogo | Catapulta viva: bombarda 3 círculos no fundo da arena (caça a linha 3) |
| M7 | **Justador Fantasma** | Luz | Investida de lança em lane reta, atravessa e volta |
| M8 Laboratório Alienígena | **Amálgama Instável** | Raio | Aos 50% divide-se em 2 cópias com metade do HP restante |
| M8 | **Olho-Sonda** | Raio | Laser de varredura contínua em arco lento e previsível |
| M8 | **Bio-Tanque** | Fogo | Cuba ambulante que despeja slimes (lacaios) até ser quebrada |
| M9 Planeta Mecânico | **Engrenagem Prima** | Raio · Imune: Veneno | Rola pela arena em trajetória anunciada por trilho luminoso |
| M9 | **Aranha Soldadora** | Raio · Imune: Veneno | Tece barreiras de metal que bloqueiam projéteis até serem destruídas |
| M9 | **Sentinela de Pistões** | Raio · Imune: Veneno | Combo de 3 socos em sequência (esq→dir→centro), ritmo aprendível |
| M10 Dimensão Final | **Eco Distorcido** | alterna (HUD) | Invoca cópias-sombra do próprio exército do jogador (50% dos stats) |
| M10 | **Fragmento do Vazio** | alterna (HUD) | Teleporta para trás do exército após cada golpe |
| M10 | **Guardião do Limiar** | alterna (HUD) | Cria portal ÷2 que persegue o exército lentamente — prévia do boss final |

Justificativa do conjunto: cada trio de mundo ensina, em mini-escala, a mecânica do boss de mundo (ex.: M3 ensina "desvie de linhas de laser" antes do Robô Escorpião; M10 ensina "fuja do ÷2" antes da Entidade Dimensional). Mini-bosses são o campo de treino do clímax.

---

## 7. MVP — os 5 bosses canônicos em detalhe (CANON §6/§15)

No MVP, cada mundo usa **um único arquétipo em tiers** (variantes de cor/escala do §6) para as fases regionais, e seu boss de mundo na última fase. Sem Luz e sem portal de Raio no MVP (CANON §10: único portal elemental é Fogo) — implicações de tuning anotadas por boss.

### 7.1 Golem de Pedra — arquétipo M1 (fases 1–6)

| Ficha | Valor |
|---|---|
| `bossId` | `m1_golem_stone` |
| Elemento / Fraqueza | Sem elemento / **Fogo** (musgo e cipós secos nas costas e juntas — justificativa visual da fraqueza) |
| Entrada (1,8 s) | Pedras do cenário rolam e se montam em golem; olhos acendem |
| Win rate alvo | 95% (fases 1–3) · 85% (4–6) |

**Moveset**

| Golpe | Disponível | Telegraph | Forma | Dano (% HP_exército_p50) | Cooldown |
|---|---|---|---|---|---|
| Soco Sísmico | sempre | 1,2 s | círculo Ø3 m | 8% (T1) → 12% (T3) | 3,5 s |
| Chuva de Pedrisco | T2+ | 1,0 s | 3 círculos Ø1,5 m | 6% cada | 4,0 s |
| Rolamento | T3, HP ≤50% | 1,4 s | faixa vertical (⅓ da arena) | 15% | 6,0 s |

**Fases de vida:** 100% só Soco; 50% pedras dos ombros desabam (stagger 0,8 s) e Pedrisco entra no ciclo; 25% telegraph 0,9 s e +20% velocidade de ataque.

**Tuning por aparição (fórmula §4.1):**

| Fase | 1 | 2 | 3 (entrada cinematográfica — CANON §16) | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| HP | 100 | 220 | 400 | 550 | 700 | 900 |
| TTK p50 | ~4 s | ~5,5 s | ~6,5 s | ~7,5 s | ~8,5 s | ~9,5 s |

Fase 1 é **impossível de perder** (CANON §16): pressão k = 0 (golem só usa Soco com telegraph 1,5 s e dano 4%). Fase 3 recebe a entrada estendida a exatamente 2,0 s, câmera orbitando — o primeiro "uau".

### 7.2 Gigante de Madeira — boss de mundo M1 (fase 7 no MVP; fase 10 no release)

| Ficha | Valor |
|---|---|
| `bossId` | `m1_final_wood_giant` |
| Fraqueza | **Fogo** · sem imunidades |
| HP (MVP, fase 7) | **1.600** (cálculo completo em §4.3) · Release fase 10: 14 s × DPS_p50, M = 1,10 |
| Entrada (2,0 s) | Árvores do funil se curvam; galhos e troncos se montam no gigante; folhas explodem |
| Win rate alvo | 75% (MVP) / ~55% (release) |

**Moveset e timings**

| Golpe | Disponível | Telegraph | Forma | Dano | Cooldown |
|---|---|---|---|---|---|
| Pisão de Raiz | sempre | 1,2 s | linha frontal de raízes (½ da largura) | 18% | 3,5 s |
| Chicote de Galho | sempre | 1,1 s | varredura em metade da arena (esq. ou dir.) | 12% | 3,0 s |
| Sementes Explosivas | HP ≤50% | 1,0 s | 4 círculos Ø1,5 m | 8% cada | 5,0 s |
| Desmoronar | HP ≤25% | 0,8 s/galho | galhos caem a cada 1,5 s até o fim | 10% cada | contínuo |

**Mecânica única — Queima Cumulativa:** o corpo tem 4 seções (2 braços, tronco, cabeça). Dano de Fogo acumula por seção; seção saturada acende (+3% de dano recebido pelo boss por seção em chamas, máx. +12%) e cai como tora ao chegar a 0. Recompensa visivelmente quem seguiu o Boss Scout, e escala o espetáculo com a competência do jogador.

**Roteiro de vídeo (filmável in-game):** exército com mutação de Fogo chega → 4 seções acendem em sequência → aos 25% o boss desmorona sozinho em paralelo aos golpes → slow motion na queda final, toras rolando viram moedas, baú grande da fase 7 (CANON §16) quica na tela. Drop: 10 gemas + baú grande + carta garantida.

### 7.3 Brutamontes Zumbi — arquétipo M2 (fases 8–13)

| Ficha | Valor |
|---|---|
| `bossId` | `m2_zombie_bruiser` |
| Fraqueza / Imunidade | **Fogo** / **Imune a Veneno** (morto-vivo — chart canônico §4; HUD mostra "IMUNE ☠️" como ensino, mesmo sem portal de Veneno no MVP) |
| Entrada (1,6 s) | Soco de dentro de uma van enferrujada; sai rugindo |
| Win rate alvo | 80% → 70% ao longo das fases 8–13 |

**Moveset**

| Golpe | Disponível | Telegraph | Forma | Dano | Cooldown |
|---|---|---|---|---|---|
| Tranco | sempre | 1,0 s | investida curta em linha | 10% | 3,0 s |
| Arremesso de Entulho | sempre | 1,2 s | círculo Ø2 m em parábola | 14% | 4,0 s |
| Grito Pútrido | T2+ | 1,3 s | invoca 3 zumbis pequenos (HP = 3× Soldado) | — | 6,0 s |
| Bote Duplo | T3, HP ≤50% | 0,9 s | dois Trancos encadeados | 8% + 8% | 5,0 s |

**Fases de vida:** 50% perde o braço (desmonta em peças, sem sangue) e ganha Bote Duplo; 25% passa a rastejar rápido com telegraph 0,8 s. **Papel pedagógico:** Grito Pútrido é o primeiro encontro com lacaios — ensina que linha de frente limpa e linha de fundo derruba (§3.1) antes do Zumbi Titã exigir isso.

**HP por aparição:** fases 8/9/10/11/12/13 → **1.000 / 1.150 / 1.400 / 1.600 / 1.850 / 2.100** (fórmula §4.1 com Supply p50 subindo de 46 para 54 e upgrades de 1,16 a 1,28).

### 7.4 Zumbi Titã — boss de mundo M2 (fase 14 no MVP; fase 20 no release)

| Ficha | Valor |
|---|---|
| `bossId` | `m2_final_zombie_titan` |
| Fraqueza / Imunidade | **Fogo e Luz** / **Imune a Veneno**. Nota MVP: Luz é pós-MVP (CANON §4) — a rota explorável no MVP é Fogo; o HUD lista as duas para consistência de canon |
| HP (MVP, fase 14) | **2.800** = DPS_p50 185 (55 Supply × 2 × 1,30 upgrades × 1,30 rota de Fogo) × TTK 15 s × M 1,00 |
| Entrada (2,0 s) | Irrompe do asfalto; carros amassados voam; urro com shake de câmera |
| Win rate alvo | 65% (MVP) / ~55% (release) |

**Moveset**

| Golpe | Disponível | Telegraph | Forma | Dano | Cooldown |
|---|---|---|---|---|---|
| Palmada Dupla | sempre | 1,2 s | 2 círculos Ø2,5 m simultâneos | 14% cada | 3,5 s |
| Varredura de Poste | sempre | 1,1 s | arco de 120° na frente | 16% | 4,0 s |
| Horda | sempre | — | spawna 2 zumbis pequenos a cada 3 s (cap 6 vivos) | — | passivo |
| Braço Rastejante | HP ≤50% | 1,0 s | braço se solta e ataca a linha 3 por trás | 8%/golpe do braço | até ser morto (HP = 10% do boss) |
| Bafo Pútrido | HP ≤25% | 0,9 s | cone verde central | 18% | 4,5 s |

**Mecânica única — duas frentes:** o Braço Rastejante cria a primeira decisão de posicionamento real do jogo: ficar longe do Titã aproxima o exército do braço. Médico (se evoluído via cartas) e Mago brilham aqui — validação do papel das classes na arena (BRIEF · Diferencial). **Roteiro de vídeo:** "Seu exército aguenta DUAS hordas?" — 200 unidades cercadas entre Titã + horda + braço, vitória no último segundo com 3 sobreviventes. Drop: 10 gemas + baú de mundo + carta rara garantida.

### 7.5 Robô Escorpião — boss de mundo M3 (fase 20 no MVP; fase 30 no release)

| Ficha | Valor |
|---|---|
| `bossId` | `m3_final_scorpion_mech` |
| Fraqueza / Imunidade | **Raio** / **Imune a Veneno** (máquina). **Nota crítica de tuning MVP:** não existe portal de Raio no MVP (CANON §10) — o HP abaixo já assume DPS **neutro**, sem multiplicador elemental. No release, recalibrar com adoção de Raio (≈ ×1,30) ou o boss derrete |
| HP (MVP, fase 20) | **3.000** = DPS_p50 168 (60 Supply no teto × 2 × 1,40 upgrades × 1,00 neutro) × TTK 16 s × M 1,10 |
| Entrada (2,0 s) | Duna se abre; a cauda emerge primeiro e "encara" a câmera; corpo se desdobra |
| Win rate alvo | 55% — o paredão final do MVP, gerador do "só 1% passa dessa fase" |

**Moveset**

| Golpe | Disponível | Telegraph | Forma | Dano | Cooldown |
|---|---|---|---|---|---|
| Ferroada Laser | sempre | 1,1 s | linha vertical fina | 16% | 3,0 s |
| Garras Duplas | sempre | 1,3 s | pinças fecham das duas laterais (zona segura: centro) | 20% | 4,5 s |
| Varredura de Cauda | HP ≤50% | 1,2 s | laser horizontal varrendo com 2 zonas seguras | 22% | 6,0 s |
| Mísseis de Sucata | HP ≤50% | 1,0 s | 5 círculos Ø1,5 m | 8% cada | 5,0 s |
| Sobrecarga | HP ≤25% | 0,8 s | Ferroadas contínuas a cada 2,0 s | 14% | contínuo |

**Mecânica única — Núcleo Exposto:** após cada especial (Garras, Varredura, Mísseis), o núcleo do peito abre por 2,0 s e o boss recebe +50% de dano. Sobreviver ao especial é a própria janela de burst — tensão e recompensa no mesmo batimento. (No release, Raio no núcleo também paralisa o boss por 2 s, fazendo a fraqueza canônica ser *sentida*, não só lida.)

**Roteiro de vídeo:** "Você consegue vencer o último boss?" — Garras fecham, exército escapa pelo centro no último frame, núcleo abre, números gigantes, slow motion decepando a cauda. Drop: 10 gemas + baú de mundo + carta épica garantida (fechamento do MVP; doc 08 detalha o pacote de conclusão).

---

## 8. Telemetria e validação (BRIEF · Analytics)

| Evento | Parâmetros mínimos |
|---|---|
| `boss_start` | `boss_id`, `tier`, `level_id`, `army_supply`, `army_dps_estimate`, `element_route`, `mutations[]` |
| `boss_defeated` | + `ttk_seconds`, `units_lost_pct`, `specials_dodged_pct`, `weak_point_taps` (pós-MVP), `revive_used` |
| `boss_failed` | + `boss_hp_remaining_pct`, `fail_cause` (`army_wiped_by:<moveId>`), `revive_offered/accepted` |

Dashboards de live-ops: win rate por `boss_id` vs alvo do §4.5 (válvula: §4.4) · `ttk_seconds` p50 dentro de 10–20 s · % de derrotas por golpe (um golpe causando >40% das derrotas = telegraph curto demais) · adoção da rota de fraqueza por fase (valida o Boss Scout e alimenta `Mult_elemental_esperado` da fórmula) · "boss mais difícil" (métrica pedida no BRIEF) = menor win rate ponderada por tentativas.

**Critérios de aceite do MVP (objetivo "boss gera tensão" do BRIEF):** win rates dentro de ±5 p.p. dos alvos do §4.5; ≥90% dos combates entre 8 e 22 s; conversão do revive por rewarded ≥25% das derrotas em boss; zero combates vencidos sem o boss completar 1 ciclo de ataque (exceto fases 1–2, que são poder-fantasia deliberada).
