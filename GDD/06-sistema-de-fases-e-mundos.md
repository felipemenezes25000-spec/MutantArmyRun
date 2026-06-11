# 06 — Sistema de Fases & Mundos · Mutant Army Run

> **Entregável 12 do pacote de design.** Define a estrutura de 100 fases / 10 mundos, a anatomia interna de cada fase, obstáculos e armadilhas por mundo, a curva de dificuldade, as regras das fases-chave e o pipeline de construção de níveis.
> Fontes normativas: `CANON.md` (prevalece em qualquer conflito) e `BRIEF.md`. Docs irmãos citados: doc 03 (unidades), doc 04 (portais), doc de bosses (entregável 10), `11-analytics.md`, `13-roadmap-e-backlog.md`.
> Versão 1.0 — 2026-06-11.

---

## 1. Visão geral e princípios do sistema

A fase é a unidade de consumo do jogo: **corrida de 45–75 s + boss de 10–20 s ≈ 60–90 s por fase** (CANON §1). O sistema de fases existe para transformar essa unidade em uma esteira de "mais uma fase" durante semanas, sem nunca quebrar os 4 pilares (CANON §2).

Princípios derivados dos pilares, aplicados a TODA fase:

| # | Princípio | Consequência prática |
|---|---|---|
| P1 | Legível em 3 s | Todo obstáculo tem telegraph visual ≥ 0,8 s; portais visíveis a ≥ 25 m (≥ 5 s de antecedência a 5 m/s). |
| P2 | Escolha inteligente | Toda fase é gerada **a partir do boss** (Boss Scout, CANON §3.1): existe sempre ≥ 1 rota ótima e ≥ 1 rota "armadilha aparentemente boa". |
| P3 | Espetáculo constante | Nenhum trecho de pista fica > 8 s sem evento (portal, obstáculo, inimigo, chuva de moedas). |
| P4 | Progressão em 3 camadas | A fase entrega progresso interno (portais), externo (recompensa/baú) e de longo prazo (desbloqueio de mundo/tropa). |
| P5 | Honestidade | Nunca existe portal mentiroso ou armadilha sem aviso (CANON §3.4). A rota armadilha engana pela **matemática** (ex.: x3 que estoura Supply), nunca pela informação. |

Números-âncora do sistema:

| Âncora | Valor | Fonte |
|---|---|---|
| Total de fases / mundos (release) | 100 / 10 | BRIEF, CANON §7 |
| Fases no MVP | 20 (M1 1–7 · M2 8–14 · M3 15–20) | CANON §7/§15 |
| Pares de portais por fase | 3 a 5 | Este doc, §2 |
| Velocidade base do exército | 5 m/s (baseline Soldado, CANON §5) | CANON §5 |
| Boss por fase | Sempre (fases x1–x9 = variantes regionais; x10 = boss único gigante) | CANON §6 |
| Supply | 60 no MVP; até 300 com meta | CANON §3.2 |
| Taxa de vitória alvo | 95% (1–3) · 85% (4–10) · ~70% (meio de mundo) · ~55% (fase 10 de mundo) | CANON §12 |

---

## 2. Anatomia de uma fase

### 2.1 Esqueleto

Toda fase segue o mesmo esqueleto, parametrizado por comprimento e densidade:

```
[LARGADA] → [PAR DE PORTAIS 1] → [ZONA 1] → [PAR 2] → [ZONA 2] → [PAR 3] → [ZONA 3]
         → [PAR 4 (opcional)] → [ZONA 4] → [PAR 5 (opcional)] → [ZONA 5]
         → [RETA FINAL] → [ARENA DO BOSS]
```

**Template Padrão (4 pares de portais)** — usado em ~60% das fases:

| Segmento | Comprimento | Tempo @ 5 m/s | Função de design |
|---|---|---|---|
| S0 — Largada | 20 m | 4 s | Zero perigo. Exército acelera, 1º par de portais já visível ao fundo (P1). |
| G1 — Par de portais 1 | 5 m | 1 s | Escolha de abertura, sempre positiva nos 2 lados. |
| Z1 — Zona de obstáculos 1 | 40 m | 8 s | 1–2 obstáculos do mundo, intensidade baixa. |
| G2 — Par 2 | 5 m | 1 s | Primeira escolha com trade-off real. |
| Z2 — Zona de obstáculos 2 | 45 m | 9 s | Obstáculos + 1ª leva de inimigos de pista. |
| G3 — Par 3 | 5 m | 1 s | **Par decisivo**: aqui mora a bifurcação ótima/armadilha. |
| Z3 — Zona de inimigos | 50 m | 10 s | Combate em movimento; testa a composição escolhida. |
| G4 — Par 4 | 5 m | 1 s | Última correção de rota (elemento/mutação para o boss). |
| Z4 — Zona mista | 50 m | 10 s | Obstáculos + inimigos, pico de intensidade da corrida. |
| RF — Reta final | 35 m | 7 s | Chuva de moedas, fanfarra, contagem do exército em destaque (cena de anúncio). |
| **Total corrida** | **260 m** | **52 s + ~4 s de perdas** (lentidão, knockback) ≈ **56 s** | Dentro da banda 45–75 s. |
| AB — Arena do boss | ~12 × 10 m | 12–16 s de combate | Ver §2.4. |
| **Fase completa** | — | **≈ 68–72 s** | Dentro da banda 60–90 s (CANON §1). |

### 2.2 Variantes de comprimento

| Variante | Pares de portais | Comprimento da pista | Corrida | Boss | Fase total | Uso típico |
|---|---|---|---|---|---|---|
| Curta | 3 | ≈ 230 m | 46–48 s | 10–12 s | ≈ 60 s | Fases x1 (respiro pós-boss), fases de introdução de mecânica. |
| Padrão | 4 | ≈ 260 m | 52–56 s | 12–16 s | 68–72 s | Corpo do jogo (~60% das fases). |
| Longa | 5 | ≈ 310 m | 62–67 s | 16–20 s | 80–88 s | Fases x8–x10, "gauntlets" de fim de mundo. |
| Onboarding | 2 (contíguos) | ≈ 160 m | ~32 s | ~6 s | ~40 s | **Exclusiva da fase 1.** Única fase autorizada a furar a banda do CANON §1, por força do §16 ("vitória em <60 s da abertura do app"). Regra específica prevalece. |

### 2.3 Geometria e regras de pista

- **Pista:** 6 m de largura, 3 corredores virtuais de 2 m. O exército segue o dedo (controle horizontal contínuo).
- **Portais:** cada portal do par ocupa **2,6 m** (o par ocupa 5,2 m, com vão central de 0,4 m e frestas laterais de 0,2 m por borda — medidas do doc 04 §2, fonte da verdade de portais); o portal atravessado é o que o **centro de massa** do exército cruza, e atravessar um desativa o outro. `gate_missed` dispara ao evitar ambos pelas frestas laterais (doc 04 §9.2; Analytics, `11-analytics.md`).
- **Distância de leitura:** o conteúdo do par (número/ícone/porcentagem) é legível a 25 m — ~5 s de decisão (P1).
- **Mecânica de pista assinatura:** cada mundo introduz **1 regra de pista própria** (tabela §3.1) — é o que faz M6 "parecer outro jogo" sem mudar o core.
- **Barra de progresso:** topo do HUD, com ícone do boss no fim; tocar no ícone reabre o lembrete do Boss Scout por 1 s sem pausar (CANON §3.1).

### 2.4 Arena do boss

- Dimensões **~12 m de largura × 10 m de profundidade** em todos os mundos (doc 05 §3.1, fonte do kit de bosses; o doc 03 assume a mesma arena de 12 m no tuning de velocidade). O exército trava na **linha de combate** a ~8 m do boss; unidades de alcance ficam atrás, corpo a corpo na frente (formação automática, doc 03).
- Entrada do boss ≤ 2 s (CANON §6), câmera fecha 30%, barra de vida gigante no topo.
- O boss tem **1 ataque especial telegrafado** (área marcada no chão por 1,0–1,5 s); o jogador esquiva movendo o exército lateralmente — o combate é curto mas ativo.
- Golpe final: slow motion 0,3× por 0,8 s (valor canônico do pacote — doc 12, VFXManager) + explosão de recompensa (BRIEF, tela 3).

### 2.5 Garantia do Boss Scout na montagem

Regra de geração obrigatória (CANON §3.1): conhecido o boss da fase (elemento + fraqueza), o conjunto de pares deve conter:

1. **Rota ótima** — sequência de escolhas que explora a fraqueza do boss e respeita o Supply (EV máximo real);
2. **Rota armadilha** — sequência com aparência de EV maior (números grandes) mas resultado pior: estoura Supply (excedente vira moedas, CANON §3.2), pega elemento igual ao do boss (−50% de dano, CANON §4) ou troca qualidade por quantidade na fase errada;
3. **Rota mediana** — o que um jogador casual faz; é contra ela que o HP do boss é calibrado (§5.2).

---

## 3. Os 10 mundos

### 3.1 Tabela master

Paletas em hex (4 cores: base / secundária / acento / perigo). Perigo é **sempre** a cor mais quente e saturada da paleta — vocabulário visual consistente entre mundos (P1).

| Mundo | Fases | Tema | Paleta | Mecânica de pista assinatura | Obstáculos típicos | Boss único (fase x10) — fraqueza |
|---|---|---|---|---|---|---|
| 1 · Campo Inicial | 1–10 | Campos, fazendas, colinas; tutorial vivo | `#7BC950 / #9BE1FF / #FFD23F / #E84B3C` | Nenhuma — pista limpa para ensinar o core | Cercas de feno, troncos rolantes, poços de lama | **Gigante de Madeira** — fraco: Fogo |
| 2 · Cidade Zumbi | 11–20 | Ruas destruídas, carros capotados, neon falhando | `#3B4250 / #8FBF3F / #C7F464 / #FF8C42` | Inimigos de pista que **perseguem** (hordas) | Carros explosivos, hordas rastejantes, vazamentos tóxicos | **Zumbi Titã** — fraco: Fogo e Luz; imune: Veneno |
| 3 · Deserto Robótico | 21–30 | Areia, ferro-velho, máquinas enterradas | `#E8C170 / #B5532A / #3E7C8F / #FF5A1F` | Torretas estáticas — primeira pressão de **alcance** | Serras de sucata, torretas enferrujadas, areia movediça | **Robô Escorpião** — fraco: Raio; imune: Veneno |
| 4 · Floresta Mutante | 31–40 | Plantas gigantes, esporos, bioluminescência | `#2E6F40 / #8E44AD / #FF6EC7 / #D7263D` | Vegetação que **reage** ao exército (proximidade) | Vinhas dentadas, esporos-mina, flores aspiradoras | **Planta Carnívora Gigante** — fraco: Fogo e Veneno |
| 5 · Vulcão dos Gigantes | 41–50 | Lava, basalto, pedras caindo | `#2B2118 / #6E6A6F / #FFA62B / #FF4E1F` | Ciclos de erupção (perigo em **ritmo**, não em posição) | Gêiseres de lava, rochas rolantes, pontes quebradiças | **Dragão de Lava** — fraco: Gelo; resiste: Fogo |
| 6 · Reino Congelado | 51–60 | Geleiras, nevasca, auroras | `#BFE8FF / #2D6CB5 / #9D7BD8 / #F4FBFF` | **Piso escorregadio** — deriva lateral do controle | Estalactites, sopros de nevasca, gelo polido | **Rei de Gelo** — fraco: Fogo; resiste: Gelo |
| 7 · Arena Medieval | 61–70 | Castelos, estandartes, multidão de arquibancada | `#8A8D93 / #7A5230 / #D4AF37 / #C0392B` | Artilharia de **área marcada** (catapultas) | Aríetes pendulares, saraivadas de catapulta, grades levadiças | **Cavaleiro Colosso** — fraco: Raio (armadura conduz) |
| 8 · Laboratório Alienígena | 71–80 | Tubos, plasma, experimentos soltos | `#2A1B4A / #E8F4F2 / #41E8E0 / #5BFF8E` | Padrões de laser **memorizáveis** (sequência fixa) | Grades de laser, tanques de mutação instável, sentinelas de plasma | **Alien Supremo** — fraqueza rotativa a cada 25% de HP (sempre no HUD) |
| 9 · Planeta Mecânico | 81–90 | Fábricas infinitas, engrenagens, fumaça | `#4A4E57 / #2E9BFF / #F7D154 / #F2780C` | **Esteiras** a favor/contra — gestão de velocidade | Prensas industriais, esteiras reversas, enxames de drones | **Mecha Supremo** — fraco: Raio; imune: Veneno |
| 10 · Dimensão Final | 91–100 | Realidade quebrada, ilhas flutuantes, caos | `#14101F / #6C2BD9 / #3FE0E0 / #E03FD8` | **Gravidade instável** — arrasto do controle inverte (telegrafado) | Estilhaços de realidade, espelhos fraturados, fendas gravitacionais | **Entidade Dimensional** — alterna elementos; usa os portais do jogador contra ele |

Justificativa da ordem: alterna mundos de **pressão de dano** (2, 5, 7, 9) com mundos de **pressão de controle** (3, 6, 8, 10) para variar o músculo exigido; os 4 elementos do MVP são "estrelados" cedo (Fogo em M1–M2, Raio em M3) e os pós-MVP entram com seus mundos temáticos (Luz/Sombra em M8, Metal em M9, Alien em M8/M10).

### 3.2 Arquétipos de boss regionais (fases x1–x9)

CANON §6: cada mundo tem **3 arquétipos** que escalam tamanho/vida/cor. **O catálogo oficial dos 30 arquétipos — nomes, fraquezas e mecânicas únicas — é o do doc 05 §6 (Sistema de Bosses, entregável 10), fonte única; este documento não o duplica.** Ficam aqui apenas as regras de agendamento dentro do mundo, que são responsabilidade do sistema de fases:

- **Rotação por tier (doc 05 §6):** arquétipo A nas fases x1–x3 (tier T1, escala ×1,0), B nas x4–x6 (T2, ×1,2), C nas x7–x9 (T3, ×1,4). O tier muda silhueta, cor e moveset; o HP sai **sempre** da fórmula por fase (doc 05 §4.1), nunca de curva de HP paralela.
- O arquétipo C de cada mundo prenuncia tematicamente o boss único da fase x10 (ex.: o Guardião do Limiar do M10 cria um portal ÷2 que persegue o exército — prévia da Entidade Dimensional, doc 05 §6).
- Variantes de escala usam sufixo P/M/G no nome de exibição (mesmo prefab, escala + recolor — correspondem aos tiers T1/T2/T3 do doc 05).

**No MVP** (corte do CANON §15): 1 arquétipo por mundo em vez de 3 — Golem de Pedra (fases 1–6), Brutamontes Zumbi (8–13), Robô Escorpião em versões P/M/G (15–19) — mais os bosses únicos Gigante de Madeira (7), Zumbi Titã (14) e Robô Escorpião G (20). Total: 5 assets de boss, conforme CANON §6/§15 (fichas completas no doc 05 §7).

---

## 4. Obstáculos e armadilhas

### 4.1 Regras globais de interação por classe

Referência de dano: Soldado nível 1 = HP 10 (CANON §5). Dano de obstáculo é aplicado por unidade atingida.

| Tropa | Regra de interação com obstáculos/armadilhas |
|---|---|
| **Ninja** | 75% de chance de esquivar de qualquer obstáculo/armadilha (dash com i-frames, CANON §5). |
| **Corredor** | Atravessa zonas de dano contínuo 40% mais rápido → dano acumulado reduzido ~40%; +20% de chance de não ser atingido por varreduras. |
| **Gigante / Titã** | "Tanka": imune a knockback e atropelamento; dano por acerto limitado a 30% do HP atual (nunca morre de um golpe); **para** objetos rolantes (vira escudo móvel do grupo). |
| **Escudeiro** | Intercepta projéteis de pista, protegendo até 2 unidades pequenas atrás de si (CANON §5). |
| **Arqueiro / Mago** | Únicos capazes de destruir obstáculos destrutíveis e inimigos **antes** do contato (pressão de alcance). |
| **Robô / Mecha** | Imunes a Veneno → ignoram zonas tóxicas (CANON §4/§5). |
| **Médico** | Cura 1% do HP do grupo/s enquanto fora de zonas de dano — transforma "respiros" em recuperação real. |
| **Dragão** | Voo: ignora obstáculos de chão (CANON §5) — zonas, rolantes e quedas. Ainda é atingido por projéteis. |

### 4.2 Catálogo por mundo (3 por mundo, originais e temáticos)

Tipos: **B** barricada destrutível · **M** obstáculo móvel · **Z** zona de efeito · **I** inimigo de pista. "Interações" cita só as mais relevantes (as globais de §4.1 sempre valem).

| Mundo | Obstáculo | Tipo | Efeito / dano | Interações de destaque |
|---|---|---|---|---|
| 1 | Cerca de Feno | B | Bloqueia 1 corredor; HP 30; dano 0 (só atrasa) | Arqueiro derruba à distância; Gigante atravessa sem parar |
| 1 | Tronco Rolante | M | Cruza a pista a cada 3 s; 10 de dano (mata 1 Soldado) | Ninja esquiva; Gigante para o tronco |
| 1 | Poço de Lama | Z | Lentidão 25% por 2 s; dano 0 | Corredor quase não sente; Dragão sobrevoa |
| 2 | Carro Capotado | B | Bloqueia 1,5 corredor; HP 60; explode ao ser destruído: 12 em raio 2 m | Destruir à distância = grátis; destruir em cima = punição honesta (ícone de chama avisa) |
| 2 | Horda Rastejante | I | 6–10 zumbis (HP 8, DPS 1) que agarram e desaceleram | Veneno inútil (mortos-vivos); Fogo limpa rápido; Escudeiro segura a linha |
| 2 | Vazamento Tóxico | Z | 4 HP/s por unidade dentro da poça | Robô imune; Corredor cruza com ~metade do dano |
| 3 | Serra de Sucata | M | Lâmina em trilho transversal; 12 por toque; ciclo telegrafado 1,2 s | Ninja esquiva; padrão aprende-se em 1 tentativa |
| 3 | Torreta Enferrujada | I | 1 projétil/1,5 s, 6 de dano; HP 40 | Escudeiro bloqueia; Arqueiro/Mago destroem antes do alcance |
| 3 | Areia Movediça | Z | Lentidão 35%; se o exército **parar** na zona, perde 1 unidade pequena a cada 2 s | Pune hesitação, não travessia; Dragão sobrevoa |
| 4 | Vinha Dentada | M | Varre 1 corredor a cada 2,5 s; 10 de dano + arremesso | Corredor passa entre varreduras; Gigante ignora o arremesso |
| 4 | Esporo-Mina | B/Z | Explode a 1 m: 8 em área 1,5 m + Veneno 3% HP/s por 4 s (CANON §4) | Estourar à distância = seguro; Robô ignora o veneno |
| 4 | Flor Aspiradora | I | Suga 1 unidade pequena/s num raio de 3 m; HP 50 | Contra-jogada clara: matar à distância ou contornar; Gigante não é sugado |
| 5 | Gêiser de Lava | Z/M | Jato vertical em ciclo 2 s ligado / 2 s desligado; 14 de dano | Ritmo > posição: atravessar no intervalo; Tropa Glacial "tampa" o gêiser por 3 s |
| 5 | Rocha Incandescente | M | Rola da rampa contra o exército; 12 por atropelo | Gigante para a rocha; Ninja esquiva |
| 5 | Ponte Quebradiça | Z | Seção desaba 1 s após o 1º pisão; unidades que caem são removidas | Decisão de rota sob pressão; Dragão e voadoras ignoram |
| 6 | Gelo Polido | Z | Deriva lateral (controle "escorrega"); dano 0 | Corredor estabiliza mais rápido; mundo inteiro testa precisão |
| 6 | Estalactite | M | Cai sob o exército; sombra telegrafa 1 s; 12 em área 1 m | Ninja esquiva; Escudeiro não bloqueia (vem de cima) |
| 6 | Sopro de Nevasca | Z | Vento lateral empurra 1 corredor por 3 s; 4 de dano se imprensado na parede | Gigante não é empurrado; combinação com Gelo Polido é o pico do M6 |
| 7 | Aríete Pendular | M | Pêndulo varre o corredor central; 14 + knockback | Gigante ignora knockback; janelas de passagem nas laterais |
| 7 | Saraivada de Catapulta | M | Projéteis com marcador no chão (telegraph 2 s); 10 em área 1,5 m | Mover o exército para fora das marcas; Médico cura entre saraivadas |
| 7 | Grade Levadiça | M/B | Sobe/desce em ciclo de 4 s; bloqueia; esmaga 12 se fechar em cima | Timing puro; Corredor cruza com folga |
| 8 | Grade de Laser | M | Barreiras intermitentes em sequência fixa; 12 por toque | Padrão memorizável (assinatura do M8); Ninja perdoa erros |
| 8 | Tanque de Mutação Instável | B | Opcional: quebrar dá mutação aleatória (60%) ou 10 em área (40%) — porcentagens exibidas | Risco honesto estilo portal de risco (CANON §3.4) |
| 8 | Sentinela de Plasma | I | Voadora; 7 por tiro a cada 2 s; HP 35 | Só unidades de alcance alcançam — pune exército 100% corpo a corpo |
| 9 | Prensa Industrial | M | Pistão esmaga o corredor em ciclo 1,8 s; 16 de dano | Maior dano do jogo; ritmo rápido exige leitura antecipada |
| 9 | Esteira Reversa | Z | Empurra para trás a 2,5 m/s (velocidade efetiva cai ~50%) | Corredor compensa; estende o tempo exposto às prensas |
| 9 | Enxame de Drones | I | 8 drones (HP 10) atacando de cima, 1 DPS cada | Raio encadeia (50% do dano a até 2 próximos, CANON §4) — momento de brilho do elemento |
| 10 | Estilhaço de Realidade | M | "Meteoros" com marcador; 14 em área; padrão denso | Versão final da catapulta do M7 — vocabulário já aprendido |
| 10 | Espelho Fraturado | I | Cria clones hostis de 3 unidades do exército (50% dos stats) | Tema do mundo: o jogo usa suas armas contra você — sempre telegrafado |
| 10 | Fenda Gravitacional | Z | Inverte o arrasto do controle por 2 s + puxa ao centro; dano 0; distorção visual avisa 1 s antes | Teste de controle puro; Gigante reduz a atração |

### 4.3 Regras de fair play (invariantes)

1. **Telegraph mínimo** por trecho (tabela §5.2) — nada atinge o jogador sem aviso visual + sonoro.
2. **Teto de dano direto por acerto** por trecho (tabela §5.2) — obstáculo nunca apaga um exército saudável; quem mata é acúmulo de erro.
3. **Remoções especiais** (Flor Aspiradora, Ponte Quebradiça, Areia Movediça parada) sempre têm contra-jogada explícita e ritmo lento (≥ 1 unidade/s).
4. Sem sangue: unidades atingidas "desmontam" em peças/partículas (CANON §1).
5. Densidade nunca cria becos sem saída: sempre existe ≥ 1 corredor transitável por janela de tempo.

---

## 5. Curva de dificuldade

### 5.1 Alvos de taxa de vitória (CANON §12)

Perfil **dente de serra**: sobe dentro do mundo, alivia na entrada do mundo seguinte. Alvos por posição na década (release completo):

| Posição no mundo | x1 | x2 | x3 | x4 | x5 | x6 | x7 | x8 | x9 | x10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Win rate alvo (M2+) | 88% | 84% | 80% | 75% | 70% | 76% ◂respiro | 68% | 63% | 59% | **55%** |
| Mundo 1 (override §12) | 99% | 96% | 95% | 88% | 87% | 86% | 85% | 83% | 82% | **60%** |

Resolução de conflito do CANON §12: a banda "85% (fases 4–10)" descreve a **média do trecho**; a regra específica "~55% na fase 10 de cada mundo" prevalece na fase de boss — amortecida para 60% no M1 (o "~" autoriza), porque um paredão na primeira hora mataria o D1. Reviver via rewarded (1×/fase, CANON §11) é o amortecedor pago pelo jogador nas fases x10.

### 5.2 Os três botões de escala

A dificuldade NUNCA escala por "inimigo esponja" genérico. Só existem três botões, sempre nesta ordem de prioridade:

**Botão 1 — HP do boss (pressão de eficiência).** A fonte única de tuning de boss é o **doc 05 (Sistema de Bosses, entregável 10)** — este documento não mantém fórmula nem tabela paralela. Fórmula canônica (doc 05 §4.1):

```
HP_boss(fase) = DPS_p50(fase) × TTK_alvo(fase) × M(win_rate_alvo)
DPS_p50(fase) = Supply_usado_p50 × 2,0 × Mult_upgrades(fase) × Mult_elemental_esperado
```

- `Supply_usado_p50` = Supply usado pela **rota mediana** (medido pelo bot, §7.3, e por telemetria após o launch);
- `2,0` = DPS por ponto de Supply (baseline Soldado, CANON §5);
- `Mult_upgrades(fase)` = multiplicador esperado dos upgrades (+5%/nível, CANON §9);
- `Mult_elemental_esperado` = 1 + 0,5 × adoção da rota de fraqueza (estimativa pré-launch: 60% nas fases com Boss Scout óbvio — doc 05 §4.1);
- `M` = fator derivado do win rate alvo (doc 05 §4.1; no MVP, gradiente comprimido do doc 05 §4.5);
- `TTK_alvo` cresce de ~4 s (fase 1) a 16 s (fases x10), nunca projetando acima de 20 s para o p50 (teto do CANON §6).

Âncoras do MVP (valores do doc 05; Remote Config recalibra, CANON §8/§13):

| Fase MVP | Boss | HP (doc 05) | Fonte |
|---|---|---|---|
| 1–6 | Golem de Pedra T1–T3 | 100 / 220 / 400 / 550 / 700 / 900 | doc 05 §7.1 |
| 7 | **Gigante de Madeira** | **1.600** (cálculo passo a passo no doc 05 §4.3) | doc 05 §4.3/§7.2 |
| 8–13 | Brutamontes Zumbi T1–T3 | 1.000 / 1.150 / 1.400 / 1.600 / 1.850 / 2.100 | doc 05 §7.3 |
| 14 | **Zumbi Titã** | **2.800** | doc 05 §7.4 |
| 15–19 | Robô Escorpião P→G | fórmula §4.1 do doc 05 com win rate 70→65% (M 0,95–1,00) | doc 05 §4.5 |
| 20 | **Robô Escorpião (boss de mundo)** | **3.000** (DPS neutro — sem portal de Raio no MVP) | doc 05 §7.5 |

**Botão 2 — Agressividade dos obstáculos (pressão de atrito).**

| Trecho | Densidade (obst./100 m) | Telegraph mínimo | Teto de dano por acerto | Inimigos de pista por zona |
|---|---|---|---|---|
| M1 | 2–3 | 1,5 s | 10 (1 Soldado) | 0–4 |
| M2–M3 | 3–4 | 1,2 s | 12 | 4–8 |
| M4–M6 | 4–5 | 1,0 s | 14 | 6–10 |
| M7–M9 | 5–6 | 0,9 s | 16 | 8–12 |
| M10 | 6–7 | 0,8 s | 16 + padrões combinados | 10–14 |

**Botão 3 — Qualidade média dos portais (pressão de decisão).** `EV de rota` = multiplicador total esperado de poder do exército ao fim da corrida:

| Trecho | EV rota ótima | EV rota mediana | EV rota armadilha | Leitura de design |
|---|---|---|---|---|
| M1 | 12× | 9× | 6× — **ainda vence** | Erro é perdoado; jogador aprende sem punição |
| M2–M3 | 14× | 9× | 5× — vence raspando | Erro custa tensão (cena de anúncio "quase-derrota") |
| M4–M6 | 16× | 9× | 4× — perde | Planejar com o Boss Scout vira obrigatório |
| M7–M9 | 18× | 9× | 3,5× — perde claro | Maestria de Supply + elemento |
| M10 | 20× | 9× | 3× | Spread máximo: skill ceiling do jogo |

Insight central: **a rota mediana rende ~9× o jogo inteiro** — quem escala é o boss e o spread entre ótima e armadilha. Dificuldade = exigir decisões melhores, não dedos mais rápidos (pilar 2).

### 5.3 Respiros (onde o jogo solta a mão)

| Respiro | Onde | Mecânica |
|---|---|---|
| Pós-boss | Fase x1 de cada mundo | Win rate +8–10 pp vs fase anterior; fase Curta; novo bioma "se vendendo" (espetáculo > desafio). |
| Meio de mundo | Fase x6 | +6 pp; normalmente carrega um portal de risco generoso (ex.: Zona de Perigo x10 com zona curta — catálogo do doc 04 §3.5). |
| Marcos de recompensa | Fase 10 (MVP) e fases x10 vencidas | Explosão de recompensa (baú épico + gemas) — respiro emocional. |
| Amortecedor de frustração | Após 2 derrotas seguidas na mesma fase | HP do boss −10% por derrota (máx. −20%), invisível, via Remote Config; coerente com CANON §11 (nunca interstitial após 2 derrotas). |

### 5.4 Calibração viva

Win rate real por fase é monitorado via `level_complete`/`level_fail` (`11-analytics.md`). Desvio > ±5 pp do alvo por 3 dias → ajuste de `HP_boss` ou `obstacleDensity` por Remote Config, sem update de binário (CANON §13). O "boss mais difícil" e o "portal mais escolhido" (BRIEF, métricas) alimentam o rebalanceamento semanal.

---

## 6. Fases-chave — regras do CANON §16, fase a fase

Numeração do MVP. Mapeamento para o release completo em §6.1.

| Fase | Regra canônica | Implementação detalhada | Critério de aceite |
|---|---|---|---|
| **1** | Impossível perder; primeiro x2; primeiro boss morre fácil; vitória < 60 s da abertura do app | Variante Onboarding (160 m): par 1 = **+10 vs +10** (espelhado — ensina o gesto sem risco); par 2 = **x2 vs +10** (com 11 unidades, x2≈+11: qualquer lado vence). Pares **contíguos, sem vão** — impossível não atravessar. Boss: Golem de Pedra P, HP 100 (doc 05 §7.1), ataque especial de 5 de dano (não mata o exército mínimo de 21 unidades nem em 2 min). Sem obstáculos que causem dano. | Bot "pior caso" (sempre o lado pior, sem desviar) vence 100% das 500 simulações; tempo boot→vitória ≤ 55 s no dispositivo de referência (e "Jogar" em ≤ 5 s após abrir, BRIEF). |
| **2** | Primeira escolha estratégica real (quantidade vs qualidade) | Introduz **+25** e **Virar Arqueiro** (cronograma do doc 04 §9.3); o par-armadilha **x2 vs +25** com exército de ~15 (x2=30 < 40, doc 02 §4.3) ensina que símbolo maior ≠ resultado maior. Par decisivo: **x2 (quantidade) vs Virar Arqueiro (qualidade)**. Arqueiros vencem ~15% mais rápido (atacam antes da linha de combate e fora do alcance do especial) — mas ambos os lados vencem (alvo 96%). O portal de classe aqui é o *teaser* da tropa que será desbloqueada permanentemente na fase 5. | Win rate 96% ± 2 pp; ≥ 30% dos jogadores escolhem cada lado (se um lado < 30%, a escolha não é real — rebalancear). |
| **3** | Primeiro boss "uau": Golem de Pedra com entrada cinematográfica | Golem de Pedra G: salta para a arena com tremor de tela, partículas de pedra e câmera orbit — entrada de 2 s (teto do CANON §6). Primeiro Boss Scout "com peso": card de ~2 s antes da fase ("GOLEM DE PEDRA — dica: saia da zona vermelha do Soco Sísmico"). Introduz **x3** e **÷2** (doc 04 §9.3): o primeiro portal punitivo entra num par óbvio (+25 vs ÷2) — ensina a LER antes de tocar, com perda recuperável no par seguinte. | 95% ± 2 pp; ≥ 90% dos jogadores assistem o card sem pular (medir `tutorial_step`); `gate_selected` em ÷2 < 10% na 1ª tentativa; fase 3 é a cena âncora do vídeo de anúncio "Desafio". |
| **4** (apoio) | — | Introduz **Elemento Fogo** (fase mín. 4, doc 04 §9.3) num par de perfil "bom vs bom"; o Golem de Pedra M é fraco a Fogo (doc 05 §7.1), e o ÷2 da fase 3 reaparece posicionado na rota cômoda (atenção espacial, doc 02 §4.3). | ≥ 30% dos jogadores escolhem Elemento Fogo quando ofertado (leitura de elemento antes do payoff dramatizado nas fases 6–7). |
| **5** | Desbloqueia Upgrades + primeira tropa nova (Arqueiro permanente) | Vitória da fase 5 concede o XP que fecha o nível 2 de jogador (nv2 = Upgrades, CANON §8 — curva de XP calibrada no doc de economia para coincidir). Arqueiro entra no roster permanente com fanfarra. Fluxo pós-vitória: tela de upgrade com o primeiro upgrade custando 100 moedas = exatamente a recompensa da fase 1 acumulada (CANON §8). | ≥ 80% dos jogadores compram ≥ 1 upgrade antes da fase 6 (`unit_upgraded`/upgrade track). |
| **6** (apoio) | Interstitials só a partir daqui (CANON §11/§16) | Introduz **Zona de Perigo x10**, o portal de risco canônico do MVP ("x10 se sobreviver à zona de perigo" — CANON §10; doc 04 §3.5, fase mín. 6), junto do primeiro estouro de Supply com fanfarra (doc 02 §4.3): risco por habilidade, sem RNG. Boss = "Golem Musgoso" (recolor G coberto de vinhas, fraco a Fogo) — o jogador vê o +50% funcionando ANTES do boss de mundo. Primeiro interstitial elegível **ao fim da fase 6** (satisfaz "a partir da fase 6" §11 e "após a fase 6" §16); máx. 1 a cada 3 fases; nunca após 2 derrotas seguidas. | Jogador que pegou Fogo mata o boss ~33% mais rápido (validar no bot); `interstitial_shown` = 0 nas fases 1–5. |
| **7** | Boss de mundo M1: Gigante de Madeira + baú grande | Fase Longa (5 pares). Boss Scout: "GIGANTE DE MADEIRA — fraco contra FOGO 🔥". A fase oferece a rota Fogo (ótima) e uma armadilha sedutora (x3 tardio que estoura o Supply 60 → excedente vira moedas com fanfarra, CANON §3.2). Especial "Pisão de Raiz": onda telegrafada 1,2 s, 12 de dano em meia-arena, esquivável. Recompensa: **Baú de Mundo** (garante 1 carta Rara+) + **10 gemas** (CANON §8). | 75% ± 2 pp (doc 05 §4.5); ≥ 60% dos vencedores passaram pelo portal Fogo; `boss_defeated` com `weakness_used=true` ≥ 60%. |
| **10** | Recompensa grande: baú épico + 50 gemas | Fase de celebração no meio do M2: win rate 76% (doc 05 §4.5 — o tom de celebração vem da recompensa, não de dificuldade reduzida), EV de portais generoso, par extra de risco honesto com a **Zona de Perigo x10** em versão generosa (zona de perigo curta — risco canônico do CANON §10, doc 04 §3.5). Tela de vitória com sequência especial de abertura do baú épico. | Retenção: ≥ 70% dos jogadores que vencem a fase 10 iniciam a 11 na mesma sessão (`level_start` encadeado). |

### 6.1 Mapeamento MVP → release completo

Quando M1 expandir para 10 fases (CANON §7), as regras migram com as fases, não com os números:

| Regra | MVP | Release completo |
|---|---|---|
| Boss de mundo M1 (Gigante de Madeira) + baú grande | Fase 7 | Fase 10 do M1 — funde-se com a regra "recompensa grande" (baú épico + 50 gemas + 10 gemas de boss de mundo) |
| Introdução do portal de Risco (Zona de Perigo x10) | Fase 6 | Fase 6 do M1 (mesma posição — docs 02 §4.3 e 04 §9.3) |
| Numeração global | 1–20 | MVP 1–7 → 1–7 · MVP 8–14 → 11–17 · MVP 15–20 → 21–26 |

`SaveSystem` migra o progresso com tabela de equivalência versionada (`levelIndexMap_v1`) — ninguém perde fases concluídas.

---

## 7. Pipeline de construção de fases

### 7.1 Decisão: híbrido template + variação (recomendado)

| Critério | 100% manual | 100% procedural em runtime | **Híbrido (recomendado)** |
|---|---|---|---|
| Custo para 100 fases | ~50 dias de level design | ~0 após o gerador | ~12 dias (30 manuais) + gerador |
| Pacing das fases-chave | Ótimo | Não confiável | Ótimo (manuais) |
| Garantia Boss Scout (rota ótima + armadilha) | Trivial | Exige solver robusto | Solver simples + bake validado |
| QA / determinismo | Simples | Difícil (estado infinito) | Simples (assets bakeados, seed fixa) |
| LiveOps / A-B | Update de binário | Arriscado | Remote Config sobrescreve escalares (HP, densidade, recompensa) |
| Variedade percebida | Limitada ao orçamento | Alta mas "sem assinatura" | Alta com assinatura nos marcos |

**Justificativa:** as fases que carregam retenção e vídeo de anúncio (chaves do §16, todas as x1 e x10) precisam de autoria — pacing emocional não emerge de gerador. As ~70 fases de "corpo" são variações estruturais de um mesmo esqueleto (§2.1) e ficam melhores parametrizadas: o gerador garante matematicamente as invariantes (EV de rotas, win rate alvo) que um humano só garante testando à mão.

**Divisão (release completo):** Tier A manual ≈ 30 fases — as 10 fases x1, as 10 fases x10, as chaves do M1 (§16) e 1 fase de introdução de mecânica por mundo. Tier B template ≈ 70 fases. **No MVP:** 9 manuais (1, 2, 3, 5, 6, 7, 10, 14, 20) + 11 por template.

### 7.2 Estrutura de dados

Tudo via ScriptableObjects do CANON §13 (`LevelConfigSO`, `WorldConfigSO`, `GateConfigSO`, `BossConfigSO`):

```csharp
[CreateAssetMenu(menuName = "MAR/Level")]
public class LevelConfigSO : ScriptableObject {
    public int worldId;                 // 1..10
    public int levelIndex;              // 1..100 (global)
    public LevelAuthoring authoring;    // Manual | Template
    public LevelTemplateSO template;    // se Template (Curta/Padrao/Longa)
    public int seed;                    // fixo por fase: mesmo layout p/ todos (QA, social)
    public BossConfigSO boss;           // variante regional ou boss único
    public ElementType bossElement;
    public ElementType bossWeakness;    // alimenta o card do Boss Scout
    public string scoutHintKey;         // dica tática p/ bosses sem fraqueza elemental
    public List<SegmentDef> segments;   // preenchido à mão (Manual) ou pelo baker (Template)
    public DifficultyProfile difficulty;
    public RewardConfigSO rewards;
}

[Serializable] public class SegmentDef {
    public SegmentType type;            // Start|GatePair|ObstacleZone|EnemyZone|FinalStretch|BossArena
    public float lengthMeters;
    public GatePairDef gatePair;        // se GatePair: GateConfigSO esquerda/direita + rótulos honestos
    public ObstacleSetSO obstacleSet;   // se zona: catálogo do mundo (§4.2)
    public float density;               // 0..1 sobre a densidade-teto do trecho (§5.2)
}

[Serializable] public class DifficultyProfile {
    public float targetWinRate;         // ex.: 0.70
    public float medianRouteEV;         // ~9x (constante de design)
    public float optimalRouteEV;        // 12x..20x conforme trecho
    public float trapRouteEV;           // 6x..3x conforme trecho
    public float telegraphSeconds;      // teto do trecho (§5.2)
    public int   bossTTKSeconds;        // alvo p/ rota mediana
}

[CreateAssetMenu(menuName = "MAR/World")]
public class WorldConfigSO : ScriptableObject {
    public int worldId;
    public string displayNameKey;
    public Color[] palette;             // base, secundária, acento, perigo (§3.1)
    public ObstacleSetSO[] obstacleSets;
    public BossConfigSO[] archetypes;   // A, B, C (§3.2)
    public BossConfigSO uniqueBoss;     // fase x10
    public TrackRuleType signatureRule; // mecânica de pista assinatura (§3.1)
    public AudioClip musicTheme;
}
```

`LevelManager` consome só `LevelConfigSO` — não sabe se a fase nasceu manual ou de template. `GateManager` lê `GatePairDef`; `BossManager` lê `boss` + `bossWeakness` para montar o card do Scout.

### 7.3 Fluxo de geração e validação (Tier B)

Geração é **ferramenta de editor** (bake) — nada procedural no device (determinismo, QA, tamanho de build):

1. **Layout:** sorteia variante (Curta/Padrão/Longa) pela banda de duração-alvo da fase e preenche o esqueleto §2.1 com `obstacleSets` do `WorldConfigSO` na densidade do trecho.
2. **Solver de portais:** dado o boss (elemento/fraqueza) e o `DifficultyProfile`, preenche os pares garantindo as 3 rotas de §2.5 dentro dos EVs-alvo (§5.2). A armadilha é construída por um dos 3 padrões: estouro de Supply, elemento igual ao do boss, quantidade na fase de qualidade.
3. **Validação por bot:** 500 simulações headless com política "gananciosa-míope 70%" (escolhe a melhor opção visível com 70% de acerto; desvia de obstáculos com taxa por classe). Win rate fora de ±5 pp do alvo → auto-ajuste do `HP_boss` por bisseção (máx. 6 iterações); se não converge, flag para revisão humana.
4. **Bake:** serializa o resultado como asset `LevelConfigSO` versionado no repositório. Seed fixa = todos os jogadores veem a mesma fase (comparável, "qual portal você escolheria?" funciona como UGC).
5. **LiveOps:** Remote Config sobrescreve escalares (`HP_boss`, `density`, recompensas) por `levelIndex` — nunca a estrutura (CANON §13, BRIEF Remote Config).

Checklist de aprovação humana por fase (mesmo as de template): leitura em 3 s em device de 5", rota armadilha "sedutora" de verdade, nenhum beco sem saída, duração dentro da banda, cena de reta final filmável.

---

## 8. MVP — as 20 fases (M1 1–7 · M2 8–14 · M3 15–20)

Bosses limitados aos 5 assets do CANON §15; variantes P/M/G = escala/recolor do mesmo prefab. Os 8 portais canônicos (CANON §10) são todos introduzidos até a fase 6, seguindo o cronograma único de introdução do doc 04 §9.3. Tuning de bosses (HP, TTK, M e win rate): fonte única no doc 05 §4–§7 — a coluna de win rate abaixo é cópia de referência do doc 05 §4.5.

| Fase | Mundo | Variante | Boss (fraqueza acionável) | Portais novos | Objetivo de aprendizado | Win rate alvo (doc 05 §4.5) | Duração alvo |
|---|---|---|---|---|---|---|---|
| 1 | M1 | Onboarding | Golem de Pedra P (—) | **x2, +10** | Portal multiplica → exército cresce → boss cai. Vitória < 60 s do boot. | 99%+ | 40–45 s |
| 2 | M1 | Curta | Golem de Pedra P+ (—) | **+25, Virar Arqueiro** | Quantidade vs qualidade (1ª escolha real); símbolo maior ≠ resultado maior (x2 vs +25). | 96% | 60 s |
| 3 | M1 | Padrão | **Golem de Pedra G — cinematográfico** (—) | **x3, ÷2** | Boss é um evento; ler o Boss Scout — e ler antes de tocar (÷2). | 95% | 65 s |
| 4 | M1 | Padrão | Golem de Pedra M (Fogo) | **Elemento Fogo** | Elementos entram no pool; payoff do +50% dramatizado nas fases 6–7. | 88% | 68 s |
| 5 | M1 | Padrão | Golem de Pedra M+ (Fogo) | — | Meta-progressão: Upgrades + Arqueiro permanente. | 87% | 70 s |
| 6 | M1 | Padrão | Golem Musgoso G (Fogo) | **Zona de Perigo x10** | Elemento certo = +50% de dano; risco por habilidade (sobreviva à zona → x10) + 1º estouro de Supply. Interstitials liberados ao fim da fase. | 86% | 70 s |
| 7 | M1 | Longa | **Gigante de Madeira** (Fogo) | — (consolidação) | Plano completo: Scout → rota Fogo → boss de mundo. Baú de Mundo + 10 gemas. | 75% | 85 s |
| 8 | M2 | Curta | Brutamontes Zumbi P (Fogo) | — | Respiro + bioma novo: inimigos de pista (Horda Rastejante). | 80% | 60 s |
| 9 | M2 | Padrão | Brutamontes Zumbi M (Fogo) | — | Portais bons em posições difíceis: posicionar o exército é parte do puzzle (doc 02 §4.3). | 78% | 70 s |
| 10 | M2 | Padrão | Brutamontes Zumbi M+ (Fogo) | — | Marcos recompensam: **baú épico + 50 gemas**. | 76% | 70 s |
| 11 | M2 | Padrão | Brutamontes Zumbi G (Fogo) | — | Supply: estourar 60 vira moedas — x3 nem sempre é o melhor. | ~70% (derrota desenhada — doc 02 §4.3) | 72 s |
| 12 | M2 | Longa | Brutamontes Zumbi G (Fogo) | — | Gerenciar 5 pares + zonas tóxicas (Veneno é inútil aqui: mortos-vivos). | 75% | 80 s |
| 13 | M2 | Longa | Brutamontes Zumbi G+ "élite" (Fogo) | — | Rota armadilha sutil (x3 tardio vs Fogo cedo). | 70% | 80 s |
| 14 | M2 | Longa | **Zumbi Titã** (Fogo; imune Veneno) | — | Boss de mundo 2: aplicar tudo. Baú de Mundo + 10 gemas. | 65% | 85 s |
| 15 | M3 | Curta | Robô Escorpião P (—) | — | Respiro + bioma novo: pressão de alcance (Torreta → valor do Escudeiro/Arqueiro). | 70% | 62 s |
| 16 | M3 | Padrão | Robô Escorpião P+ (—) | — | Elementos são situacionais: Scout mostra "fraco: RAIO · FOGO: neutro" — Fogo deixa de ser resposta automática. | 69% | 70 s |
| 17 | M3 | Padrão | Robô Escorpião M (—) | — | Obstáculos compostos (Serra + Areia Movediça): não parar nunca. | 67% | 72 s |
| 18 | M3 | Longa | Robô Escorpião M+ (—) | — | Armadilha dupla (2 pares enganosos na mesma fase). | 66% | 80 s |
| 19 | M3 | Longa | Robô Escorpião G (—) | — | Gauntlet: densidade máxima do MVP; ensaio geral do boss. | 65% | 85 s |
| 20 | M3 | Longa | **Robô Escorpião G — boss de mundo** (imune Veneno) | — | Final do MVP: vitória por composição/Supply, sem muleta elemental. Baú épico + 10 gemas + teaser "novos mundos em breve". | 55% | 90 s |

Notas de design do MVP:

- **Fases 15–20 sem portal de Raio** (os 8 portais do CANON §10 não o incluem): o Boss Scout do M3 exibe a fraqueza Raio honestamente e marca "FOGO: neutro" — a profundidade do trecho vem de matemática de Supply e composição. É também o gancho do primeiro update ("portais de Raio chegaram!", ver `13-roadmap-e-backlog.md`).
- **Interstitials:** elegíveis somente do fim da fase 6 em diante, máx. 1 a cada 3 fases, nunca após 2 derrotas seguidas (CANON §11).
- **Objetivo do MVP** (BRIEF): a tabela cobre os 10 pontos de validação — em especial "usuário encadeia fases" (respiros nas 8 e 15) e "gera bons vídeos" (fases 3, 6, 7, 14 e 20 são as cenas de captura).

---

## 9. Telemetria da fase

Eventos emitidos pelo `LevelManager`/`GateManager`/`BossManager` (contrato completo em `11-analytics.md`):

| Momento | Evento (BRIEF) | Parâmetros mínimos |
|---|---|---|
| Início da fase | `level_start` | `world_id`, `level_index`, `authoring`, `seed` |
| Cada par de portais | `gate_selected` / `gate_missed` | `gate_pair_index`, `side`, `gate_type`, `army_size_before/after` |
| Entrada na arena | `boss_start` | `boss_id`, `army_supply_used`, `mutations_active` |
| Fim do boss | `boss_defeated` / `boss_failed` | `ttk_seconds`, `weakness_used` |
| Fim da fase | `level_complete` / `level_fail` | `duration`, `fail_reason` (`army_wiped_track` \| `army_wiped_boss`), `revive_used` |

`fail_reason` alimenta a tela de derrota (BRIEF, tela 5: "motivo") e o rebalanceamento de §5.4.

---

## 10. Rastreabilidade (requisitos do brief cobertos por este doc)

| Requisito do BRIEF | Seção |
|---|---|
| 100 fases / 10 mundos com tema e boss final | §3.1 |
| Corrida 30–90 s, arena final, boss gigante (core loop passos 4–9) | §2 |
| Obstáculos/armadilhas/inimigos (passo 7) e interações por classe | §4 |
| "Só 1% passa dessa fase" / dificuldade e quase-derrota (viralização) | §5 (spread de EV, fases x10) |
| Regras de produto: fases 1, 2, 3, 5, 10 + tutorial curto | §6 |
| ScriptableObjects de fases e mundos; Remote Config de dificuldade | §7 |
| MVP: 20 fases, 3 mundos, 5 bosses | §8 |
| Analytics de fase (level/gate/boss) | §9 |

**Fora do escopo deste doc:** stats finais de tropas (doc 03), catálogo completo de portais (doc 04), kit completo de bosses (entregável 10), economia de recompensas por fase (entregável 8), wireframe da tela de Mapa (entregável 7).
