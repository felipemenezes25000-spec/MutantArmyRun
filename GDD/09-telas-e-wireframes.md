# 09 — Telas & Wireframes (Entregáveis 6 e 7)

> **Pacote de design — Mutant Army Run** · Versão 1.0 · 2026-06-11
> Fontes da verdade: `CANON.md` (decisões fixas) e `BRIEF.md` (requisitos). Docs relacionados: `02-core-loop-e-progressao.md`, `04-sistema-de-portais.md`, `07-economia-e-upgrades.md`, `08-monetizacao.md`, `12-arquitetura-unity.md`.
> Escopo: lista completa de telas (entregável 6) e wireframe textual de cada tela (entregável 7), incluindo overlays, HUD detalhado, navegação e princípios de UX.

---

## 1. Princípios de UX (aplicam-se a TODAS as telas)

| # | Princípio | Regra concreta | Justificativa |
|---|---|---|---|
| P1 | **Legível em 3 s** | Cada tela tem 1 ação primária óbvia (maior botão, cor de destaque, pulso sutil). Máx. 7 elementos interativos por tela. | Pilar 1 do CANON. Jogador casual decide em segundos. |
| P2 | **One-hand play** | 100% das ações críticas na metade inferior da tela (thumb zone). Topo = só informação. Gameplay = 1 dedo, arrastar em qualquer ponto da tela. | Público mobile casual joga em pé, no ônibus, com uma mão. |
| P3 | **Jogar em <5 s** | Boot → Inicial → fase em **no máximo 2 toques** (na prática, 1: JOGAR). Orçamento de tempo na §3.2. | Regra de produto do BRIEF. |
| P4 | **Thumb zone** | Em 1080×1920: zona quente = y 1280–1820 (botões primários); zona morna = y 960–1280 (botões secundários); zona fria = y 0–960 (informação, header). | Alcance médio do polegar em telas 6–6,7". |
| P5 | **Toque mínimo** | Alvo de toque ≥ **120×120 px** @1080p (~9 mm), espaçamento ≥ 24 px entre alvos. Botão primário: 880×160 px. | Evita misclick — principal causa de frustração em runner. |
| P6 | **Tipografia e contraste** | Fonte display bold arredondada (números do HUD 90–120 px; títulos 64 px; corpo ≥ 36 px). Contraste ≥ 4,5:1; todo texto sobre gameplay tem outline escuro 4 px + sombra. Nada de texto sobre fundo móvel sem placa/outline. | Legibilidade em 9:16 sob sol, em vídeo vertical comprimido (TikTok/Reels). |
| P7 | **Acessibilidade (daltônicos)** | **Nenhuma informação só por cor.** Elementos têm ícone fixo (Fogo=chama, Gelo=floco, Raio=relâmpago, Veneno=gota); portais sempre exibem o sinal matemático (+ × ÷ −) como sinal primário; raridade tem moldura com silhueta distinta (Comum lisa, Raro cantos chanfrados, Épico moldura facetada, Lendário moldura ornada) além da cor canônica; positivo/negativo usa ↑/↓ e ✓/✗, nunca só verde/vermelho. | ~8% dos homens têm daltonismo; portais ilegíveis = churn. |
| P8 | **Feedback imediato** | Todo toque responde em ≤ 50 ms: squash & stretch do botão (escala 0,92 → 1,0 em 120 ms), som de clique, vibração leve opcional. Botões desabilitados explicam o porquê em 1 linha ao toque. | Game feel é prioridade máxima do BRIEF. |
| P9 | **Honestidade** | Probabilidades sempre visíveis ("70% ×10 / 30% perde metade"); preços de ad/IAP nunca escondidos; X de fechar sempre presente em ofertas. | CANON §3.4 — a tensão vem da escolha, não da trapaça. |
| P10 | **Safe area** | Layout ancorado em safe area (notch/punch-hole). Header a ≥ 64 px do topo físico; tab bar a ≥ 48 px da borda inferior. | Android primeiro = fragmentação extrema de telas. |

---

## 2. Lista de telas (Entregável 6)

### 2.1 Telas principais (as 10 do brief)

| ID | Tela | Conteúdo-chave (BRIEF) | MVP? | Desbloqueio |
|---|---|---|---|---|
| SCR-01 | **Inicial (Home)** | Logo, JOGAR, moedas, gemas, progresso, acesso a Loja/Tropas/Upgrades/Passe | ✅ | Sempre |
| SCR-02 | **Gameplay (corrida)** | Exército correndo, portais, obstáculos, contador de unidades, barra de progresso, feedback de dano, números grandes | ✅ | Sempre |
| SCR-03 | **Boss (arena)** | Arena, boss gigante, barra de vida, exército atacando, impacto, slow motion, explosão de recompensa | ✅ | Toda fase |
| SCR-04 | **Vitória** | Moedas, XP, sobreviventes, dano causado, baú, "dobrar com anúncio", próxima fase | ✅ | — |
| SCR-05 | **Derrota** | Motivo da derrota, reviver com anúncio, melhorar tropa, tentar de novo | ✅ | — |
| SCR-06 | **Tropas** | Cartas, raridade, nível, fragmentos, evoluir, comparação de atributos | ✅ | Sempre (evolução nv. jogador 2+) |
| SCR-07 | **Upgrades** | 8 trilhas de upgrade (MVP: 4) com custo e nível | ✅ | Nível de jogador 2 / fase 5 |
| SCR-08 | **Loja** | Moedas, gemas, baús, skins, sem-anúncios, passe | ✅ (reduzida) | Completa no nível de jogador 4 |
| SCR-09 | **Mapa** | Mundos, fases, bosses, recompensas por mundo, bloqueio/desbloqueio | ❌ pós-MVP* | Sempre (via Home) |
| SCR-10 | **Eventos** | Diário, semanal, ranking, recompensas especiais | ❌ pós-MVP | Nível de jogador 6 |

\* No MVP, o progresso de mundo aparece como widget na SCR-01 (faixa "Mundo 1 · Fase 4/7"); a SCR-09 completa entra no release.

### 2.2 Overlays e estados (não são telas cheias — modais sobre a tela atual)

| ID | Overlay | Aparece sobre | Duração / fechamento | MVP? |
|---|---|---|---|---|
| OVL-01 | **Boss Scout card** | Carregamento da fase (e mini-versão sobre SCR-02) | ~2 s auto; toque pula | ✅ |
| OVL-02 | **Pausa** | SCR-02 / SCR-03 | Até o jogador fechar | ✅ |
| OVL-03 | **Level-up de conta** | SCR-04 (após contagem de XP) | Toque para coletar | ✅ |
| OVL-04 | **Recompensa de baú** | SCR-04, SCR-08, SCR-09, SCR-10 | Toques para revelar cartas | ✅ |
| OVL-05 | **Oferta de reviver** | SCR-03 (no momento da derrota, antes de SCR-05) | 5 s de decisão | ✅ |
| OVL-06 | **Configurações** | OVL-02 e SCR-01 | Toque no X | ✅ |
| OVL-07 | **Oferta inicial (US$ 2,99)** | SCR-01 (1× nas primeiras 48 h, nunca antes da fase 3) | X sempre visível | ❌ pós-MVP |

Estados transversais: **interstitial** (só a partir da fase 6, máx. 1 a cada 3 fases, nunca após 2 derrotas seguidas — controlado por Remote Config, ver `08-monetizacao.md`) e **loading** (barra fina + dica de gameplay de 1 linha, ex.: "Gelo deixa inimigos 30% mais lentos").

---

## 3. Mapa de navegação

### 3.1 Diagrama de fluxo (textual)

```
[BOOT ~2,5 s: splash + load async]
        │ (automático)
        ▼
┌──────────────────────────────────────────────────────────────┐
│ SCR-01 INICIAL                                               │
│   ├─ toque JOGAR ──► [load fase + OVL-01 Boss Scout ~2 s]    │
│   │                        │ (auto / toque pula)             │
│   │                        ▼                                 │
│   │                  SCR-02 GAMEPLAY ──(fim da pista)──►     │
│   │                        │                SCR-03 BOSS      │
│   │                        │ exército zerado │        │      │
│   │                        │ (sem reviver)   │        │      │
│   │                        │           (boss 0 HP) (derrota) │
│   │                        │                 │        ▼      │
│   │                        │                 │     OVL-05    │
│   │                        │                 │   ad│  │recusa│
│   │                        │                 │     ▼  │      │
│   │                        │                 │  SCR-03│      │
│   │                        ▼                 │ (volta)│      │
│   │                     SCR-05 ◄─────────────┼────────┘      │
│   │                 DERROTA: │               ▼               │
│   │      tentar de novo ─► OVL-01      SCR-04 VITÓRIA        │
│   │      melhorar tropa ─► SCR-06            │ próxima fase  │
│   │      voltar ─► SCR-01                    ▼ (1 toque)     │
│   │                              [interstitial? §2.2]        │
│   │                                    │                     │
│   │                                    ▼                     │
│   │                              OVL-01 ─► SCR-02 (loop)     │
│   ├─ tab LOJA ────► SCR-08                                   │
│   ├─ tab TROPAS ──► SCR-06 ─ evoluir/detalhe (in-place)      │
│   ├─ tab UPGRADES ► SCR-07                                   │
│   ├─ tab EVENTOS ─► SCR-10 (nv6; cadeado antes)              │
│   ├─ widget progresso ─► SCR-09 MAPA ─ fase ─► OVL-01 ─► jogo│
│   ├─ banner PASSE ─► SCR-08 (seção Passe)                    │
│   └─ engrenagem ──► OVL-06 CONFIGURAÇÕES                     │
└──────────────────────────────────────────────────────────────┘
Regras globais: tab bar persistente em SCR-01/06/07/08/10 (não em
gameplay/resultado); botão Android "voltar" = voltar à SCR-01
(em gameplay abre OVL-02 Pausa); OVL-03/04 sempre por cima de tudo.
```

### 3.2 Orçamento "jogar em <5 s" (regra de produto do BRIEF)

| Etapa | Tempo acumulado | Toques | Notas |
|---|---|---|---|
| Abrir app → splash | 0,0–2,0 s | 0 | Load assíncrono (Addressables do Mundo atual pré-carregados; ver `12-arquitetura-unity.md`) |
| SCR-01 interativa | ≤ 3,0 s | 0 | JOGAR já habilitado mesmo se sync Firestore não terminou (save local-first) |
| Toque em JOGAR | ~3,2 s | **1** | Fase carrega em paralelo com OVL-01 |
| Boss Scout card (2 s, pulável) | 3,2–5,0 s | 0–1 | Toque na tela pula o restante do card |
| **Exército correndo** | **≤ 5,0 s** | **total: 1–2** | Cumpre "fase em no máx. 2 toques" com folga |

Decisão de design: o Boss Scout NÃO é uma tela com botão "continuar" — é um card de loading com auto-dismiss. Informação estratégica entregue sem custo de fricção.

---

## 4. Wireframes textuais (Entregável 7)

Convenções: caixas ASCII em proporção 9:16; elementos numerados `(n)`; após cada wireframe, tabela **Toques** (todo toque possível e seu resultado) e **Estados**. Moeda = ícone `(o)`, gema = `(◆)`.

### 4.1 SCR-01 — Tela Inicial

```
┌───────────────────────────────┐
│ (1)⚙  (2)(o)1.250+ (3)(◆)45+ │  topo: header (zona fria)
│                               │
│ (4)      MUTANT ARMY RUN      │  logo 3D animado
│                               │
│ (5) ┌───────────────────────┐ │
│     │ MUNDO 1 · CAMPO       │ │  widget progresso
│     │ Fase 4/7  ▓▓▓▓░░░ 🗿  │ │  (ícone do boss no fim)
│     └───────────────────────┘ │
│ (6) [🎁 Baú grátis em 02:14]  │
│ (7) [🏆 PASSE DE TEMPORADA]   │  banner (nv5+)
│                               │
│ (8)   ╔═══════════════════╗  │
│       ║      JOGAR ▶      ║  │  zona quente do polegar
│       ║      Fase 4       ║  │  880×200 px, pulso suave
│       ╚═══════════════════╝  │
│                               │
│(9)┌────┬─────┬──────┬──────┐ │
│   │LOJA│TROPA│UPGRADE│EVENTO│ │  tab bar 4 abas + badge
│   └────┴─────┴──────┴──────┘ │
└───────────────────────────────┘
```

**Elementos:** (1) engrenagem → OVL-06 · (2) saldo de moedas, botão `+` abre SCR-08 na seção moedas · (3) saldo de gemas, idem seção gemas · (4) logo com animação idle (mutação sutil a cada 8 s) · (5) widget de progresso do mundo: nome do mundo, fase atual/total, barra e ícone do boss do mundo no fim · (6) chip do baú grátis diário com timer (nv3+; pronto = brilha + badge) · (7) banner do Passe (nv5+) · (8) **JOGAR** — único botão grande da tela, mostra a próxima fase · (9) tab bar persistente; abas bloqueadas exibem cadeado + "Nv X".

**Hierarquia visual:** JOGAR (1º) ≫ widget de progresso (2º) > baú diário (3º) > resto. Nada compete em cor com o JOGAR (dourado/verde saturado sobre fundo do mundo atual desfocado).

| Toque | Resultado |
|---|---|
| (8) JOGAR | Load da fase + OVL-01 Boss Scout → SCR-02 |
| (5) widget | Abre SCR-09 Mapa (MVP: expande lista das fases do mundo) |
| (6) baú pronto | OVL-04 Recompensa de baú |
| (6) baú em timer | Tooltip "Pronto em 02:14 — abrir agora? (◆)20" |
| (2)/(3) `+` | SCR-08 na seção correspondente |
| (7) banner | SCR-08, seção Passe |
| (9) aba | Troca de tela com slide 200 ms; aba bloqueada: balança + "Desbloqueia no nível 6" |
| (1) | OVL-06 Configurações |

**Estados:** primeira sessão = só (4)(8) visíveis + mão-guia apontando JOGAR (tutorial sem texto); abas aparecem conforme nível de conta (nv2 Upgrades, nv3 Baús, nv4 Loja completa, nv5 Passe, nv6 Eventos); badge vermelho numérico em abas com ação pendente (upgrade comprável, baú pronto).

---

### 4.2 SCR-02 — Gameplay (corrida) + HUD em detalhe

```
┌───────────────────────────────┐
│(1)▓▓▓▓▓▓░░░░░░░░░░░░░🗿  (2)⏸│  barra de progresso + pausa
│(3)        ⚔ 47                │  contador de unidades
│(4)   SUPPLY ▓▓▓▓▓▓▓░░ 52/60  │
│(5)              🪽 🔥 [ ]     │  slots de mutação (3)
│                               │
│   (6)┌─────────┐┌─────────┐  │
│      │   ×2    ││  +25    │  │  par de portais
│      │  azul ↑ ││ azul ↑  │  │  (sinal sempre visível)
│      └─────────┘└─────────┘  │
│        (7) ▲▲▲ espinhos       │  obstáculo
│                               │
│ (8)      ░░█████░░            │
│          ░███████░            │  exército (multidão)
│           "NICE!" (9)         │  feedback textual
│        +25 ✨  (10)           │  números flutuantes
│                               │
│ (11)◄═ arrastar p/ mover ═►   │  input: 1 dedo, tela toda
│ (12)                    [☢]  │  slot reservado (pós-MVP)
└───────────────────────────────┘
```

**Elementos e regras do HUD:**

1. **Barra de progresso da fase** (topo, largura total): preenche da esquerda; marcas finas onde há pares de portais; no fim, **ícone do boss da fase** (retrato circular com ícone do elemento dele). **Boss Scout retocável:** tocar no ícone reabre o lembrete por 1 s **sem pausar** — balão "🗿 GOLEM DE PEDRA · fraco contra 🔥 FOGO" (CANON §3.1; fraqueza canônica do Golem de Pedra no doc 05 §7.1).
2. **Pausa** (44 px da borda, alvo 120×120 px) → OVL-02.
3. **Contador de unidades** — o número mais importante da tela: 96 px bold, branco com outline; **escala com tween elástico a cada mudança** (+ verde-água com ↑, − laranja com ↓ — cor E seta, P7). Acima de 999 exibe "1,2K" com tooltip do valor exato no fim da fase.
4. **Barra de Supply** (52/60): fica âmbar ≥ 80% e pulsa a 100%. Ao estourar, dispara fanfarra "MEGA ARMY!" + chuva de moedas (conversão do excedente — CANON §3.2; nunca parece punição).
5. **Slots de mutação (3):** ícones das mutações ativas; pegar a 4ª → a mais antiga pisca 0,5 s e é substituída com partícula de "troca".
6. **Portais pareados:** painel translúcido com número/efeito em 110 px + sinal matemático primário + ícone de classe/elemento + seta ↑/↓. Portal de risco: dois painéis empilhados "70% ×10 / 30% −½" com ícones de dado. Cores: positivos azul/ciano, negativos laranja/vermelho — mas o **sinal é sempre o sinal gráfico**, não a cor (P7).
7. **Obstáculos/armadilhas:** silhueta de alto contraste + brilho de borda vermelho pulsante 0,5 s antes do alcance.
8. **Exército:** ocupa o terço central-inferior; mutações visíveis nos modelos (asas, armadura...).
9. **Feedbacks textuais** (régua canônica do BRIEF) — aparecem no centro, 120 px, pop elástico 0,6 s, nunca 2 ao mesmo tempo (fila com prioridade crescente):

| Texto | Gatilho | Extra |
|---|---|---|
| NICE | portal positivo simples (+10/+25) | som curto |
| GREAT | multiplicador ×2/×3 atravessado | shake leve de câmera |
| INSANE | 3 portais positivos seguidos sem perder unidade | partículas |
| GODLIKE | exército ≥ 200 unidades OU portal de risco vencido | flash dourado |
| PERFECT | chegar à arena sem perder nenhuma unidade | selo na SCR-04 |
| MUTATION | pegar portal de mutação | ícone da mutação junto |
| MEGA ARMY | estourar o Supply (conversão em moedas) | chuva de moedas |
| BOSS BREAKER | golpe final no boss (ver SCR-03) | slow motion |

10. **Números flutuantes:** `+25` (ganho de unidades, verde-água ↑), `−12` (perda, laranja ↓), `+38 (o)` (conversão de Supply), dano no boss em branco; **crítico = 1,5× maior, amarelo, com "!"**. Máx. 8 simultâneos (pool de objetos; agrupa excedentes em "+97").
11. **Input:** arrastar horizontalmente em **qualquer ponto da tela** move o exército (sem joystick virtual); resposta ≤ 50 ms; soltar = exército mantém a posição.
12. **Slot de poder especial:** o BRIEF pede "se existir" — decisão: **não existe poder ativo no MVP** (preserva P1 e o input de 1 dedo). Slot inferior-direito reservado para o pós-MVP ("Fúria Mutante", ver `13-roadmap-e-backlog.md`).

| Toque/gesto | Resultado |
|---|---|
| Arrastar (qualquer lugar) | Move o exército lateralmente |
| Toque no ícone do boss (1) | Lembrete Boss Scout 1 s, sem pausa |
| Toque em (2) | OVL-02 Pausa (jogo congela) |
| Botão Android "voltar" | OVL-02 Pausa |

**Estados:** últimos 3 s antes da arena = câmera afasta 10% + vinheta "BOSS À FRENTE"; exército zerado na corrida → **direto para SCR-05**, sem oferta de reviver — o rewarded de reviver é exclusivo da derrota na arena do boss (CANON §11; placement restrito no doc 08 §2 e evento `revive_offered` só em boss no doc 11 §5).

---

### 4.3 SCR-03 — Boss (arena)

```
┌───────────────────────────────┐
│(1) GOLEM DE PEDRA  fraco:🔥    │  nome + fraqueza SEMPRE visível
│(2) ▓▓▓▓▓▓▓▓▓▓▓░░░░  68%      │  barra de vida gigante
│                               │
│(3)        ▄▄█████▄▄           │
│          ██  ◣◢  ██           │  BOSS (1/3 da tela)
│           ██████████          │
│            ▀██████▀           │
│      (4)⚠ zona do golpe ⚠    │  telegrafia do ataque
│                               │
│(5)  ⚔38  ░████████░          │  exército atacando
│         ░██████████░          │
│ (6)  -142!   -89   -204!      │  números de dano
│(7)▓▓░ SUPPLY     (8)💊 1/1   │  HUD reduzido + reviver
└───────────────────────────────┘
```

**Elementos:** (1) nome do boss + ícone de elemento + fraqueza com ícone (para o Alien Supremo/M8, a fraqueza rotativa é atualizada aqui a cada 25% de HP — CANON §6) · (2) barra de vida gigante segmentada a cada 25% (marcos de fases do boss), com % numérico · (3) boss ocupa ~1/3 vertical; entrada cinematográfica ≤ 2 s (câmera sobe do chão ao rosto) · (4) ataque especial telegrafado: área vermelha pulsante no chão 1 s antes — arrastar tira o exército da área · (5) exército ataca automaticamente; contador segue visível · (6) números de dano flutuantes (críticos maiores/amarelos; com vantagem elemental, números saem com o ícone do elemento, ex. "🔥-204" contra o Golem de Pedra) · (7) Supply reduzido (canto) · (8) indicador do reviver disponível (1×/fase).

**Coreografia de impacto (BRIEF "game feel"):** hit impact com micro-shake (2–4 px) a cada golpe forte; ao chegar a 0 HP → **slow motion 0,3× por 0,8 s** (valor canônico do pacote — `12-arquitetura-unity.md`, VFXManager) no golpe final + "BOSS BREAKER!" + câmera aproxima → boss "desmonta" em peças/partículas (sem sangue — CANON §1) → **explosão de recompensa**: moedas voam em arco para o contador → transição automática para SCR-04 (sem toque).

| Toque/gesto | Resultado |
|---|---|
| Arrastar | Reposiciona o exército (esquiva do golpe telegrafado) |
| Toque em (1)/(2) | Reabre lembrete de fraqueza 1 s (mesmo comportamento do Boss Scout) |
| Botão "voltar"/pausa | OVL-02 |

**Estados:** exército zerado → OVL-05 Reviver (painel inferior: "REVIVER COM 50% DO EXÉRCITO? ▶📺 anúncio · 1×/fase", timer circular de 5 s; recusou/expirou → SCR-05). Vida do boss < 15% = barra pisca + música acelera (tensão de quase-derrota, cena viral nº 4 do BRIEF).

---

### 4.4 SCR-04 — Vitória

```
┌───────────────────────────────┐
│(1)      ★ VITÓRIA! ★         │
│(2)   FASE 4 COMPLETA  [PERFECT]│
│ (3) (o) +100 moedas  (conta)  │
│ (4) XP ▓▓▓▓▓▓░░░ +20  nv 2    │
│ (5) ⚔ Sobreviventes: 31      │
│ (6) 💥 Dano causado: 14.380   │
│ (7)      ┌─────────┐          │
│          │ 🎁 BAÚ  │ (se fase │
│          └─────────┘  de baú) │
│                               │
│ (8) ╔═══════════════════════╗ │
│     ║ 📺 DOBRAR ×2 (o)+100  ║ │  zona quente
│     ╚═══════════════════════╝ │
│ (9) ╔═══════════════════════╗ │
│     ║   PRÓXIMA FASE ▶      ║ │
│     ╚═══════════════════════╝ │
│ (10)      voltar ao início    │  link discreto
└───────────────────────────────┘
```

**Elementos:** (1) título com confete + fanfarra · (2) selo PERFECT se aplicável · (3) moedas com **contagem animada** (rolo de 0 → total em 0,8 s, moedas voando da pilha ao header; fase 1 = 100 moedas; cresce ≈ ×1,10/fase — CANON §8) · (4) barra de XP do jogador anima; se encheu → OVL-03 entra em sequência · (5) sobreviventes (nº + miniaturas das mutações ativas) · (6) dano causado total (número grande — satisfação) · (7) baú quando a fase premia (fase 7: baú grande; fase 10: baú épico + 50 gemas; toque → OVL-04) · (8) **DOBRAR ×2 com anúncio** — botão mais saliente da tela, mostra exatamente quanto será ganho (P9); rewarded sempre opcional · (9) PRÓXIMA FASE — caminho de 1 toque para encadear fases (meta ≥ 6 fases/sessão) · (10) link textual para SCR-01.

| Toque | Resultado |
|---|---|
| (8) DOBRAR | Rewarded ad → ao completar, +100% das moedas da fase com 2ª chuva de moedas; botão vira "✓ DOBRADO"; ad falhou → toast "Sem vídeo agora" e botão some |
| (9) PRÓXIMA FASE | [checagem de interstitial §2.2] → OVL-01 da próxima fase → SCR-02 |
| (7) baú | OVL-04 (abre antes de poder sair da tela) |
| (10) | SCR-01 |

**Estados:** ordem fixa de animação (3)→(4)→(5)/(6)→(7) em ~2 s total, **pulável com 1 toque em qualquer lugar** (mostra tudo pronto); fase 7/10 etc. com recompensas de marco exibe faixa "BOSS DE MUNDO DERROTADO! +10 (◆)".

---

### 4.5 SCR-05 — Derrota

```
┌───────────────────────────────┐
│(1)      DERROTA...            │  sem tom punitivo
│(2)┌─────────────────────────┐ │
│   │ MOTIVO: seu ataque de   │ │
│   │ GELO ❄ causou −50% de   │ │
│   │ dano no REI DE GELO ❄   │ │
│   │ 💡 Dica: leve FOGO 🔥   │ │
│   └─────────────────────────┘ │
│(3)  Boss restante: ▓░░░ 12%   │  "quase!"
│                               │
│(4) ╔═══════════════════════╗  │
│    ║ 📺 REVIVER (50%) 1/1  ║  │  (se ainda disponível)
│    ╚═══════════════════════╝  │
│(5) ╔═══════════════════════╗  │
│    ║   TENTAR DE NOVO ↻    ║  │
│    ╚═══════════════════════╝  │
│(6) [ ⬆ MELHORAR TROPA +15% ]  │
│(7)        voltar ao início    │
└───────────────────────────────┘
```

**Elementos:** (1) título neutro (nunca "VOCÊ PERDEU" agressivo; cor azul-escura, não vermelho) · (2) **motivo da derrota** gerado por telemetria da fase + dica acionável de 1 linha. Catálogo de motivos: `elemento_fraco` ("ataque de X causou −50% no boss de X"), `exercito_pequeno` ("você chegou ao boss com só N unidades — busque portais ×"), `portal_ruim` ("o portal ÷2 cortou seu exército no meio"), `obstaculo` ("os espinhos eliminaram N unidades"), `golpe_especial` ("o golpe de área atingiu o exército inteiro — arraste para fora da zona vermelha") · (3) vida restante do boss — reforça o "quase consegui" (re-tentativa) · (4) REVIVER com anúncio, disponível apenas quando a derrota ocorreu na arena do boss (CANON §11 — rewarded de "reviver no boss") e se OVL-05 não foi usado nesta fase (1×/fase); volta direto ao ponto da derrota com 50% do pico do exército · (5) TENTAR DE NOVO — reinicia a fase na hora (sem energia, sem custo — CANON §8) · (6) MELHORAR TROPA — deep link para SCR-06 já na tropa mais relevante, com preview do ganho ("+15% dano") quando há evolução comprável · (7) link para SCR-01.

| Toque | Resultado |
|---|---|
| (4) | Rewarded → volta à SCR-03 no ponto da queda (só existe para derrota no boss) |
| (5) | OVL-01 → SCR-02 (recomeço imediato; **nunca** precedido de interstitial — regra "nunca após 2 derrotas seguidas" do CANON é controlada aqui) |
| (6) | SCR-06 com a tropa sugerida em destaque |
| (7) | SCR-01 |

**Estados:** sem reviver disponível (já usado na fase, sem fill de ad ou derrota fora da arena do boss) → (4) oculto e (5) sobe para a posição quente; 2ª derrota seguida na mesma fase → (6) ganha destaque e badge "RECOMENDADO".

---

### 4.6 SCR-06 — Tropas

```
┌───────────────────────────────┐
│(1)◄ TROPAS   (o)1.250 (◆)45  │
│(2)[Todas][Comum][Rara][Épica][Lend.]│
│(3)┌──────┐┌──────┐┌──────┐   │
│   │SOLDADO││ARQUEIRO││ESCUDEIRO│ │
│   │ nv3  ││ nv2  ││ nv1  │   │
│   │▓▓░12/20││▓░ 6/10││░ 2/10│   │  fragmentos
│   └──────┘└──────┘└──────┘   │
│   ┌──────┐┌──────┐            │
│   │ MAGO ││GIGANTE│  cards     │
│   │ nv1  ││ 🔒   │  3 colunas │
│   └──────┘└──────┘            │
│(4)┌─────────────────────────┐ │
│   │ SOLDADO nv3 → nv4       │ │  painel de detalhe
│   │ HP 14→16  DPS 2,8→3,2   │ │  (comparação antes/depois)
│   │ Supply 1 · veloc 5 m/s  │ │
│   │ ╔═════════════════════╗ │ │
│   │ ║EVOLUIR 40 frag+(o)80║ │ │
│   │ ╚═════════════════════╝ │ │
│   └─────────────────────────┘ │
│(5)[LOJA][TROPAS][UPGRADE][EVENTO]│
└───────────────────────────────┘
```

**Elementos:** (1) header com voltar e saldos · (2) filtro por raridade (chips; cores canônicas + molduras distintas — P7) · (3) grade de cartas 3 colunas: retrato, nome, nível atual, barra de fragmentos `12/20`; bloqueada = silhueta escura + 🔒 + "Baús do Mundo 2" (onde obter) · (4) painel de detalhe (abre ao tocar numa carta, desliza de baixo — fica na thumb zone): **comparação de atributos antes → depois** da evolução (HP, DPS, alcance, velocidade, Supply, habilidade especial; fraqueza/vantagem elemental com ícones), custo (fragmentos da própria tropa + moedas; nv n→n+1 = 10×2^(n−1) fragmentos — CANON §8; nível máx. 10) e botão EVOLUIR · (5) tab bar.

**Estados do botão EVOLUIR:** ✅ habilitado (verde, pulso) quando fragmentos E moedas suficientes; ⛔ desabilitado (cinza) mostra o que falta em vermelho-com-ícone ("✗ 28/40 fragmentos") e, ao toque, balança + tooltip "Consiga fragmentos em baús 🎁"; 🏁 MAX no nível 10 (moldura dourada).

| Toque | Resultado |
|---|---|
| Carta | Abre/troca painel (4) com tween 150 ms |
| EVOLUIR habilitado | Explosão de partículas da cor da raridade, carta sobe de nível, stats animam de → para, som "power up", `unit_upgraded` (analytics) |
| EVOLUIR desabilitado | Shake + tooltip do requisito faltante |
| Carta bloqueada | Tooltip com a fonte de desbloqueio |
| (2) chip | Filtra grade (fade 120 ms) |

---

### 4.7 SCR-07 — Upgrades (meta-progressão)

```
┌───────────────────────────────┐
│(1)◄ UPGRADES    (o)1.250      │
│(2)┌─────────────────────────┐ │
│   │⚔ DANO INICIAL      nv3 │ │
│   │ +15% → +20%   ▓▓▓░░░░  │ │
│   │        [ (o)182 ⬆ ]    │ │
│   ├─────────────────────────┤ │
│   │❤ VIDA INICIAL      nv2 │ │
│   │ +10% → +15%  [ (o)135 ]│ │
│   ├─────────────────────────┤ │
│   │👥 EXÉRCITO INICIAL  nv4 │ │
│   │ +2 → +2 un.* [ (o)246 ]│ │
│   ├─────────────────────────┤ │
│   │💰 RECOMPENSA       nv1 │ │
│   │ +5% → +10%   [ (o)100 ]│ │
│   ├─ ─ ─ pós-MVP ─ ─ ─ ─ ─ ┤ │
│   │🏃 VELOCIDADE · 🎯 CRÍTICO│ │
│   │🗡 DANO VS BOSS · 🛡 RESIST│ │
│   └─────────────────────────┘ │
│(3)[LOJA][TROPAS][UPGRADE][EVENTO]│
└───────────────────────────────┘
```

**Elementos:** (2) lista vertical de trilhas; cada linha = ícone + nome + nível, efeito **atual → próximo** (+5%/nível; Exército inicial +1 unidade a cada 2 níveis — \*por isso a linha mostra quando o próximo nível não adiciona unidade), barra de nível e botão de compra com custo. Custo do nível n = 100 × 1,35^(n−1): nv1=100, nv2=135, nv3=182, nv4=246, nv5=332 (CANON §9). MVP exibe 4 trilhas; as 8 completas no release (Dano inicial · Vida inicial · Velocidade · Multiplicador de recompensa · Exército inicial · Chance crítica · Dano contra boss · Resistência a obstáculos).

**Estados do botão de compra:** habilitado (verde, custo branco) · desabilitado (cinza, custo em vermelho + ícone ✗; toque mostra "Faltam (o)57") · MAX (selo dourado). Linha comprável tem brilho sutil na borda; a aba mostra badge com o nº de upgrades compráveis (motivo para "mais uma fase").

| Toque | Resultado |
|---|---|
| Botão habilitado | Débito animado no header, barra preenche, efeito numérico rola (+15%→+20%), partículas, som; custo recalcula |
| Botão desabilitado | Shake + "Faltam (o)57 — jogue a fase 5 (+~146)" (estimativa da recompensa atual) |
| Linha (fora do botão) | Expande descrição de 1 linha ("Seu exército começa cada fase com +20% de dano") |

---

### 4.8 SCR-08 — Loja

```
┌───────────────────────────────┐
│(1)◄ LOJA       (o)1.250 (◆)45│
│(2)┌─────────────────────────┐ │
│   │🚫 SEM ANÚNCIOS US$ 4,99 │ │  destaque fixo no topo
│   │ + (◆)200 de bônus       │ │
│   └─────────────────────────┘ │
│(3)[🏆 PASSE DE TEMPORADA      │
│    US$ 6,99/mês — ver itens ] │
│(4) BAÚS ──────────────────    │
│   [🎁 Grátis 02:14][📺+1/dia] │
│   [Baú Raro (◆)300][Épico...] │
│(5) GEMAS ─────────────────    │
│   [(◆)80 US$0,99][(◆)500 ...] │
│(6) MOEDAS ────────────────    │
│   [(o)1.000 (◆)50][(o)6.000..]│
│(7) SKINS ─────────────────    │
│   [Soldado Neon (◆)150] ...   │
│(8)[LOJA][TROPAS][UPGRADE][EVENTO]│
└───────────────────────────────┘
```

**Elementos:** (2) Remover Anúncios US$ 4,99 com bônus de 200 gemas — âncora de IAP sempre no topo (CANON §11) · (3) Passe de Temporada US$ 6,99/mês — toque expande os 7 benefícios (nova tropa, novo boss, skin, recompensas diárias, baús premium, ranking, evento especial) · (4) baús: grátis diário com timer, **baú extra diário por rewarded**, Baú Raro 300 gemas, Baú Épico (preço definido em `07-economia-e-upgrades.md` coerente com a âncora de 300) · (5)/(6) pacotes de gemas (IAP) e moedas (compradas com gemas — gema é a única ponte real-money → recurso) · (7) skins (10 no MVP: recolor + acessório do Soldado), preview 3D girando ao toque · preços reais por região via RevenueCat.

| Toque | Resultado |
|---|---|
| Item IAP | Sheet de confirmação nativa (RevenueCat) → sucesso = OVL-04 (se baú) ou aplicação imediata + `purchase_completed` |
| Baú grátis pronto | OVL-04 |
| 📺 baú extra | Rewarded → OVL-04; 1×/dia, depois mostra timer |
| Baú de gemas | Confirmação simples ("Abrir por (◆)300?") → OVL-04 |
| Skin | Preview + [COMPRAR]/[EQUIPAR]; equipada = selo ✓ |

**Estados:** antes do nível de conta 4, a Loja abre em versão reduzida (apenas (2), (5) e baú grátis) com faixa "Loja completa no nível 4"; seção de baús só a partir do nv3; "Sem Anúncios" comprado → card vira "✓ ATIVO" e interstitials somem (rewarded continuam, por escolha do jogador).

---

### 4.9 SCR-09 — Mapa (mundos e fases)

```
┌───────────────────────────────┐
│(1)◄ MAPA      MUNDO 1/10      │
│(2)      ☁ MUNDO 2 🔒         │
│        CIDADE ZUMBI           │
│        boss: ZUMBI TITÃ       │
│   ────────────────────────    │
│(3)        (10)🗿💰            │  fase de boss de mundo
│          (9)●                 │
│         (8)●                  │
│        (7)●🎁                 │  fase com baú
│       (6)●                    │
│      (5)●⭐                   │  marco (desbloqueio)
│     (4)◉ ← você               │  fase atual (pulsa)
│    (3)✓                      │
│   (2)✓     caminho em S      │
│  (1)✓                        │
│(4)┌─────────────────────────┐ │
│   │ RECOMPENSAS DO MUNDO 1: │ │
│   │ 🎁 baú grande (f7)      │ │
│   │ (◆)10 boss · ⚔ Arqueiro │ │
│   └─────────────────────────┘ │
│(5)        ╔══════════╗        │
│           ║ JOGAR F4 ║        │
└───────────╚══════════╝────────┘
```

**Elementos:** (2) topo do scroll mostra o próximo mundo bloqueado com silhueta do boss + cadeado + requisito ("Complete a fase 10") — antecipação · (3) caminho em S com nós: ✓ concluída (toque = rejogar p/ farm), ◉ atual (pulsa, anel dourado), ● bloqueada (cinza), ícones de recompensa nos marcos (🎁 baú f7, ⭐ desbloqueio f5 = Arqueiro permanente + Upgrades, 🗿💰 boss de mundo f10 com baú épico + 50 gemas — pacing CANON §16) · (4) painel de recompensas do mundo · (5) JOGAR contextual fixo no rodapé (thumb zone). Scroll vertical percorre os 10 mundos; cada mundo tem fundo do seu tema.

| Toque | Resultado |
|---|---|
| Nó atual / (5) | OVL-01 → SCR-02 |
| Nó concluído | Popup "Rejogar fase 3? Recompensa: (o)~36 (reduzida)" → jogar |
| Nó bloqueado | Shake + "Complete a fase 4 primeiro" |
| Mundo bloqueado | Card com tema, lista de bosses do mundo e requisito |
| Ícone de recompensa | Tooltip com o conteúdo exato |

---

### 4.10 SCR-10 — Eventos

```
┌───────────────────────────────┐
│(1)◄ EVENTOS        ⏰ 11:42:08│
│(2)┌─────────────────────────┐ │
│   │ MISSÕES DIÁRIAS  3/5    │ │
│   │ ✓ Vença 3 fases   (◆)5  │ │
│   │ ✓ Use 1 portal ×3 (◆)5  │ │
│   │ ░ Derrote 2 bosses(◆)10 │ │
│   │ [RESGATAR TUDO]         │ │
│   └─────────────────────────┘ │
│(3)┌─────────────────────────┐ │
│   │ DESAFIO SEMANAL         │ │
│   │ "Exército Mínimo": vença│ │
│   │ com Supply ≤ 30         │ │
│   │ 🎁 baú épico + (◆)40    │ │
│   └─────────────────────────┘ │
│(4)┌─────────────────────────┐ │
│   │ 🏆 RANKING DA SEMANA    │ │
│   │ #142 · Liga Bronze      │ │
│   │ top: maior dano em boss │ │
│   └─────────────────────────┘ │
│(5)[LOJA][TROPAS][UPGRADE][EVENTO]│
└───────────────────────────────┘
```

**Elementos:** (1) timer de reset (diário 24 h / semanal no topo do card) · (2) missões diárias com recompensas em gemas (soma alvo: 20–40 gemas/dia para jogador ativo — CANON §8); barra 3/5 com baú bônus ao completar 5 · (3) desafio semanal com modificador de regra (usa sistemas existentes, sem conteúdo novo — barato de operar) · (4) ranking semanal por liga, critério claro e anti-frustração (percentil, não top global) · recompensas especiais de evento sazonal aparecem como card extra quando ativos via Remote Config.

| Toque | Resultado |
|---|---|
| Missão completa | Coleta com voo de gemas ao header |
| RESGATAR TUDO | Coleta em cascata (0,15 s entre itens) |
| Missão incompleta | Deep link para a ação ("Derrote 2 bosses" → JOGAR) |
| Card semanal | Detalhe + [JOGAR DESAFIO] (carrega fase com modificador) |
| Ranking | Lista da liga + recompensas por faixa |

**Estados:** aba bloqueada até o nível de conta 6 (cadeado + "Nv 6"); evento sazonal ativo adiciona card no topo com arte própria.

---

## 5. Overlays — wireframes

### 5.1 OVL-01 — Boss Scout card (inovação central — CANON §3.1)

```
┌───────────────────────────────┐
│  (fundo: fase carregando)     │
│   ┌───────────────────────┐   │
│   │ (1)  🗿 SILHUETA →     │   │
│   │     RETRATO DO BOSS    │   │
│   │ (2) GOLEM DE PEDRA     │   │
│   │ (3) elemento: sem      │   │
│   │     elemento (neutro)  │   │
│   │ (4) FRACO CONTRA 🔥FOGO│   │
│   │     ▼▼▼ (seta p/ baixo)│   │
│   │ (5) "Priorize portais  │   │
│   │      de FOGO!"         │   │
│   └───────────────────────┘   │
│  (6) toque para começar ▸     │
└───────────────────────────────┘
```

Sequência de ~2 s: silhueta (0–0,5 s) → revela retrato com stinger sonoro (0,5–1,0 s) → fraqueza com ícone do elemento em destaque pulsante (1,0–2,0 s) → fade para a corrida. **Qualquer toque pula.** A fraqueza usa ícone + nome do elemento, nunca só cor (P7). O mesmo conteúdo reaparece como balão de 1 s ao tocar no ícone do boss na barra de progresso (sem pausa). Consequência exibida em jogo: portais do elemento forte contra o boss ganham um brilho sutil — sempre há ≥ 1 rota ótima e 1 armadilha aparente (CANON §3.1).

### 5.2 OVL-02 — Pausa

Painel central sobre gameplay escurecido (jogo congelado): **(1) CONTINUAR ▶** (primário, thumb zone) · (2) Recomeçar fase ↻ (confirma: "Perder o progresso da corrida?") · (3) lembrete do Boss Scout (boss + fraqueza — útil para quem pausou para pensar) · (4) atalhos de som/música/vibração (toggles diretos) · (5) Sair para o início (confirma). Toques fora do painel = continuar. Sem anúncios na pausa, nunca.

### 5.3 OVL-03 — Level-up de conta

Sobre a SCR-04, após a barra de XP encher: explosão radial + **"NÍVEL 3!"** (140 px) → card do desbloqueio com ícone grande e frase de valor ("🎁 BAÚS DESBLOQUEADOS — ganhe cartas e fragmentos") + recompensa imediata ((o) e/ou (◆)) → botão COLETAR. Desbloqueios canônicos: nv2 Upgrades · nv3 Baús · nv4 Loja completa · nv5 Passe · nv6 Eventos (CANON §8). Se o desbloqueio é uma aba, ao fechar o overlay a aba nova brilha na tab bar (orienta sem tutorial).

### 5.4 OVL-04 — Recompensa de baú

Fundo escurecido com raios de luz da cor da raridade do baú. Sequência: baú treme (0,5 s) → **toque 1** abre (tampa voa, explosão de partículas) → itens saem 1 a 1 em cartas viradas; **cada toque revela a próxima** (ou "REVELAR TUDO" após a 1ª) → ordem fixa: moedas → fragmentos → carta mais rara por último (antecipação) → carta Épica+ ganha pausa dramática com vinheta + som exclusivo (momento de vídeo). Tela final: resumo do loot + COLETAR (voo dos itens para os contadores). Drop de Lendária dispara "GODLIKE!". Evento `chest_opened` com conteúdo no payload.

### 5.5 OVL-05 — Oferta de reviver (no momento da derrota)

Painel inferior (thumb zone) sobre a cena congelada em dessaturação: "SEU EXÉRCITO CAIU!" + **▶📺 REVIVER com 50% do exército (1×/fase)** com anel de countdown de 5 s + link discreto "Desistir". Expira ou recusa → SCR-05. Aceita → rewarded → volta exatamente ao ponto da queda com fanfarra de retorno. Indisponível (já usada ou sem fill de ad) → vai direto à SCR-05. **Aparece apenas sobre a SCR-03** (derrota na arena do boss — CANON §11; doc 08 §2); exército zerado durante a corrida vai direto à SCR-05, sem oferta.

### 5.6 OVL-06 — Configurações

Som, música, vibração (toggles) · idioma · suporte/restaurar compras (RevenueCat) · política de privacidade/termos · ID do jogador (Auth anônimo, para suporte) · créditos. Sem login social no MVP.

---

## 6. Especificações transversais de implementação

| Tema | Especificação |
|---|---|
| Resolução de referência | 1080×1920 (9:16); Canvas Scaler `Scale With Screen Size`, match 0,5; testar 19,5:9 e 4:3 (tablets: pillarbox do gameplay, UI estica) |
| Stack de UI | `UIManager` (CANON §13) com stack de telas/overlays; telas = prefabs com Addressables; transições padrão: slide 200 ms (tabs), fade 150 ms (overlays), sem transição > 300 ms |
| Pooling | Números flutuantes, feedbacks textuais e partículas de moeda usam object pool (alvo: 0 alloc em gameplay — celulares medianos, BRIEF "estilo visual") |
| Analytics por tela | `level_start`/`level_complete`/`level_fail` (SCR-02/04/05), `boss_start`/`boss_defeated`/`boss_failed` (SCR-03), `gate_selected`/`gate_missed` (SCR-02), `unit_upgraded` (SCR-06), `chest_opened` (OVL-04), `rewarded_ad_shown`/`rewarded_ad_completed` (SCR-04/05, OVL-05, SCR-08), `interstitial_shown` (transição pós-vitória), `purchase_*` (SCR-08), `season_pass_opened` (SCR-08) — lista completa em `11-analytics.md`/BRIEF |
| Remote Config na UI | Frequência de interstitial, valores de recompensa exibidos, ofertas ativas, eventos ativos e textos de feedback são data-driven — nenhum número de economia hardcoded em prefab |
| Localização | Strings em tabela (PT-BR base; EN/ES no soft launch); feedbacks NICE→BOSS BREAKER ficam em inglês em todas as línguas (são "linguagem de jogo", curtos e virais) |

---

## 7. Checklist de cobertura do BRIEF (escopo deste doc)

| Requisito do BRIEF | Onde |
|---|---|
| Lista das 10 telas (entregável 6) | §2.1 |
| Estados/overlays (Boss Scout, pausa, level-up, baú) | §2.2, §5 |
| Wireframe textual de cada tela (entregável 7) | §4.1–4.10 |
| Jogar em <5 s / ≤2 toques | §3.2 |
| HUD: contador, barra de progresso c/ boss, feedbacks NICE→BOSS BREAKER, números flutuantes | §4.2 |
| Botão de poder especial "se existir" | §4.2 (12) — decisão: slot reservado, sem poder no MVP |
| Tela de boss: arena, barra gigante, impacto, slow motion, explosão de recompensa | §4.3 |
| Vitória: moedas, XP, sobreviventes, dano, baú, dobrar com anúncio, próxima fase | §4.4 |
| Derrota: motivo, reviver com anúncio, melhorar tropa, tentar de novo | §4.5 |
| Tropas: cartas, raridade, nível, fragmentos, evoluir, comparação de atributos | §4.6 |
| Upgrades: as 8 trilhas com custos do CANON | §4.7 |
| Loja: moedas, gemas, baús, skins, sem-anúncios, passe | §4.8 |
| Mapa: mundos, fases, bosses, recompensas, bloqueio | §4.9 |
| Eventos: diário, semanal, ranking, recompensas especiais | §4.10 |
| One-hand play, thumb zone, legibilidade 9:16, daltônicos | §1 (P2, P4, P6, P7) |
| Game feel de UI (resposta ao toque, vibração, explosões) | §1 P8, §4.2–4.4, §5.4 |
| Sem menu complexo / sem tutorial longo | §1 P1, §4.1 (tutorial = mão-guia, zero texto) |

*Fim do documento 09. No pacote, `05-sistema-de-bosses.md` e `08-monetizacao.md` detalham regras que esta UI expõe.*
