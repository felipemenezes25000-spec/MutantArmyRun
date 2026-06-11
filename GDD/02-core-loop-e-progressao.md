# 02 — Core Loop & Progressão · Mutant Army Run

> **Escopo:** entregáveis 2 (core loop detalhado), 3 (progressão dos primeiros 30 minutos) e 4 (progressão dos primeiros 7 dias) do BRIEF.md.
> **Fontes:** CANON.md (decisões fixas) e BRIEF.md (requisitos). Em conflito de detalhe, CANON.md prevalece.
> **Versão:** 1.0 — 2026-06-11 · Time: game design + product + UX + monetização + engenharia Unity.

## 1. Como ler este documento

- A numeração de fases segue a **estrutura do MVP/soft launch** (CANON §15): Mundo 1 = fases 1–7, Mundo 2 = fases 8–14, Mundo 3 = fases 15–20. No release completo (10 fases/mundo, CANON §7), os mesmos *beats* são mantidos em posição relativa equivalente dentro de cada mundo.
- Itens marcados **[Release]** não fazem parte do escopo travado do MVP (CANON §15) e entram na expansão pós-MVP (ver doc 22). No MVP, os níveis de jogador que desbloqueariam essas features concedem pacotes de recompensa equivalentes (detalhado na §7).
- Itens marcados **[F2+]**, **[F3]** ou **[F4]** seguem o roadmap do doc 13 (F2/F3 = soft launch, F4 = lançamento global): também estão **fora da build MVP (F1)**, mas chegam antes do release completo. Mapeamento adotado: baú diário grátis, calendário de login e oferta inicial = **[F2+]** (doc 08 §1.3: soft launch v1.1); missões diárias, baú extra via rewarded e missão de fim de semana = **[F3]** (doc 13 §2.4 e §4 item 2); FCM = **[F4]** (doc 13 §2.4 e §4 item 11). Na build MVP, o "motivo de voltar amanhã" é a própria progressão: próxima fase/boss, barra de upgrade a 80–95%, evolução de tropas por fragmentos e os baús de fase do CANON §16.
- Valores novos criados aqui (curva de XP, conteúdo de baús, recompensas de missão) são coerentes com as âncoras do CANON §8 e ficam expostos em **Remote Config** para calibração no soft launch.
- Glossário PT→EN do CANON §14 vale para todo o documento (Portal=Gate, Baú=Chest, Fase=Level etc.).

---

## 2. Core loop detalhado — loops aninhados (Entregável 2)

O jogo é construído como cinco loops encaixados. Cada loop responde a uma pergunta diferente do jogador e entrega um tipo diferente de recompensa. O segredo do hybrid-casual é que **cada loop termina dentro de um loop maior ainda aberto** — o jogador nunca chega a um "ponto final" natural sem um gancho já armado.

| Loop | Duração | Pergunta do jogador | Recompensa dominante | Gancho para o próximo ciclo |
|---|---|---|---|---|
| Portal | ~5 s de decisão, 1 par a cada 9–14 s | "Esquerda ou direita?" | Multiplicação visual + feedback (NICE!/INSANE!) | Próximo par de portais já visível ao fundo |
| Fase | 60–90 s | "Meu exército derruba esse boss?" | Moedas, XP, slow motion da vitória | Boss Scout da próxima fase + botão "Próxima fase" |
| Sessão | ~10 min | "Até onde eu chego hoje?" | Upgrade comprado, baú aberto, tropa nova | Barra de progresso "faltam X moedas / X XP" |
| Diário | 24 h | "O que tem pra mim hoje?" | Missões (20–40 gemas) **[F3]**, baú diário e calendário **[F2+]** | Missão incompleta + baú que recarrega à meia-noite (no MVP: barra de upgrade a 80–95%) |
| Semanal | 7 dias | "O que estou construindo?" | Evolução de tropas, skins, ranking **[Release]** | Próximo mundo/boss único + reset semanal |

```
┌──────────────────────────── SEMANAL ────────────────────────────┐
│  ┌────────────────────────── DIÁRIO ──────────────────────────┐ │
│  │  ┌──────────────────── SESSÃO ~10 min ─────────────────┐   │ │
│  │  │  ┌──────────── FASE 60–90 s ────────────┐  ×6–8     │   │ │
│  │  │  │  ┌──── PORTAL ~5 s ────┐  ×3–5       │           │   │ │
│  │  │  │  │ ver→ler→decidir→POW │             │           │   │ │
│  │  │  │  └─────────────────────┘             │           │   │ │
│  │  │  └──────────────────────────────────────┘           │   │ │
│  │  └──────────────────────────────────────────────────────┘   │ │
│  └─────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

### 2.1 Loop de 5 segundos — a escolha de portal

**Cadência:** um par de portais (sempre esquerda/direita, CANON §3.4) a cada **9–14 s** de corrida — espaçamento de 45–70 m a 5 m/s, com mínimo absoluto de 45 m (doc 04 §5.1). Corrida de 45–75 s ⇒ **3–5 pares por fase** (doc 06 §1/§2.2: variante Curta = 3, Padrão = 4, Longa = 5; a fase 1 usa a variante Onboarding, com 2 pares contíguos). O orçamento de ~5 s abaixo é a **janela de decisão** dentro de cada ciclo (o par é legível a 25 m, doc 06 §2.3); o restante do ciclo é percurso. Entre pares: obstáculos, inimigos de percurso e moedas soltas, para que a mão nunca pare.

**Anatomia da decisão (orçamento de ~5 s):**

| Momento | Duração | O que acontece | Suporte de UI |
|---|---|---|---|
| Antecipação | ~1,5 s | Par de portais surge no horizonte, brilhando | Cores por tipo (verde=ganho, vermelho=perda, roxo=risco) |
| Leitura | ~1,0 s | Jogador lê número/ícone/porcentagem | Texto gigante, ícone único por portal — pilar "legível em 3 s" |
| Plano | ~1,0 s | Cruza com a fraqueza do boss (Boss Scout) e com o Supply atual | Toque no ícone do boss reabre o lembrete por 1 s sem pausar (CANON §3.1) |
| Execução | ~0,5 s | Desliza o dedo; o exército inteiro segue | Movimento elástico da multidão |
| Recompensa | ~1,0 s | Multiplicação/transformação visível + som + háptica leve | Pop-ups: NICE, GREAT, INSANE, GODLIKE, PERFECT, MUTATION, MEGA ARMY |

**Tipos de decisão (mix por fase, controlado pelo GateManager via LevelConfigSO):**

| Tipo | Exemplo (portais canônicos do MVP, CANON §10) | O que treina | Frequência alvo |
|---|---|---|---|
| Matemática pura | +10 vs +25 | Ler números rápido | 30% |
| Matemática-armadilha | x2 vs +25 com exército de 15 (x2=30 < 40) | "Maior símbolo ≠ maior resultado" | 20% |
| Qualidade vs quantidade | x2 Soldados vs Virar Arqueiro | Supply: x10 fraco pode ser pior que poucos fortes (CANON §3.2) | 20% |
| Elemento vs boss | Elemento Fogo vs +10, com boss fraco a Fogo | Boss Scout como vantagem | 15% |
| Risco | "x10 se sobreviver à zona de perigo" vs +25 seguro | Apetite a risco; momento de vídeo | 10% |
| Negativa posicionada | ÷2 na rota cômoda, ganho na rota com obstáculo | Atenção espacial | 5% |

**Regra de geração (CANON §3.1):** toda fase tem **pelo menos 1 rota "ótima" e 1 rota "armadilha aparentemente boa"**, calculadas a partir do boss da fase. Os portais nunca mentem (informação honesta, CANON §3.4) — a tensão vem da escolha, não da trapaça.

**Justificativa de design:** 5 s é o menor ciclo completo de dopamina que cabe num vídeo de TikTok. Cada par de portais é, sozinho, um anúncio em potencial ("qual você escolheria?", BRIEF §Formatos de anúncio).

### 2.2 Loop de 90 segundos — a fase

| Tempo | Etapa | Sistema | Estado emocional alvo |
|---|---|---|---|
| 0:00–0:02 | **Boss Scout**: cartão com boss, elemento e fraqueza | UIManager + BossConfigSO | Curiosidade → plano ("preciso de Fogo") |
| 0:02–1:00 | Corrida: 3–5 pares de portais (um a cada 9–14 s, doc 04 §5.1), obstáculos, inimigos de percurso | GateManager, CrowdManager | Fluxo crescente; exército inchando na tela |
| ~0:50 | Pico de Supply: estouro converte excedente em moedas com fanfarra (CANON §3.2) | CrowdManager → EconomySystem | "Estou transbordando de poder" — nunca punição |
| 1:00–1:02 | Entrada do boss (animação ≤2 s, CANON §6) | BossManager | Tensão; barra de vida gigante |
| 1:02–1:20 | Combate 10–20 s; 1 ataque especial telegrafado; golpe final em slow motion | CombatSystem, VFXManager | Clímax → alívio espetacular |
| 1:20–1:30 | Explosão de recompensa: moedas → XP → fragmentos → baú (se houver) → oferta "dobrar com anúncio" | RewardSystem, AdsManager | Colheita; números subindo |

**Em caso de derrota:** tela mostra **o motivo** ("Seu exército era forte, mas o boss resistia a Fogo"), oferece **reviver com anúncio (1×/fase, CANON §11)**, botão "Melhorar tropas" com a trilha de upgrade mais relevante destacada, e "Tentar de novo". Derrota deve durar <8 s de tela — frustração curta, ação imediata.

**Justificativa:** a fase é a unidade de "só mais uma". 60–90 s significa que o custo percebido de repetir é quase zero — abaixo do limiar em que o jogador "decide" parar.

### 2.3 Loop de sessão (~10 minutos)

Meta canônica: **sessão média ≥8 min e ≥6 fases/sessão** (CANON §12). Esqueleto da sessão-alvo:

| Bloco | Duração | Conteúdo |
|---|---|---|
| Reentrada | 20–40 s | Tela inicial: badges piscando (no MVP: upgrade disponível/baú de fase; baú diário e missões **[F2+/F3]**) → 1 toque em JOGAR |
| Corridas 1–3 | ~5 min | 3 fases encadeadas pelo botão "Próxima fase" da tela de vitória |
| Toque de meta | ~1 min | Volta à tela principal: compra 1 upgrade ou abre 1 baú (badge de notificação interna guia) |
| Corridas 4–6 | ~4 min | Mais 3 fases; 1 interstitial no máximo (após fase 6 do jogador, 1 a cada 3 fases, CANON §11) |
| Saída com gancho | 20 s | Tela inicial mostra: "faltam 35 moedas para o próximo upgrade" / Boss Scout da próxima fase / "baú diário abre em 2 h" **[F2+]** |

**Regras de desenho da sessão:**
1. **Nunca terminar uma sessão "completa".** Sempre exibir 1 barra a 80–95% (upgrade, XP, missão) na saída.
2. **1–2 ofertas de rewarded por sessão** no caminho natural (dobrar recompensa, reviver) — meta de conversão ≥35% dos DAU (CANON §12). Rewarded nunca interrompe; sempre é botão opcional (CANON §11).
3. **Interstitial nunca após duas derrotas seguidas** (CANON §11) — derrota dupla já é o momento de maior risco de churn.

### 2.4 Loop diário

Checklist diário do jogador ativo (tudo acessível pela tela inicial, sem tela nova). **Escopo:** o loop diário completo é feature de soft launch — nenhum item da tabela está na build MVP/F1 (CANON §15; doc 13 §2.4 e §4 item 2). No MVP, o "o que tem pra mim hoje?" é respondido pela progressão: próxima fase/boss, upgrade a 80–95% e evolução de tropas por fragmentos.

| Item | Recompensa | Reset | Sistema |
|---|---|---|---|
| Baú diário grátis **[F2+]** | Moedas + fragmentos (chance de carta) | 24 h | RewardSystem |
| Baú extra via rewarded **[F3]** (doc 13 §2.4) | 2º baú do dia (CANON §11) | 24 h | AdsManager |
| 3 missões diárias **[F3]** | 20–40 gemas/dia somadas (CANON §8) | Meia-noite local | RewardSystem |
| Calendário de login (7 dias) **[F2+]** | Crescente; dia 7 = carta épica + 60 gemas | Diário | RewardSystem |
| Recompensa diária do passe **[Release]** | Gemas/fragmentos premium | Diário | Season Pass |
| "Corrida do Dia" (fase remixada) **[Release]** | Baú do dia | Diário | EventSystem |

**Pool de missões diárias [F3] (3 sorteadas/dia; soma alvo 25–35 gemas):**

| Missão | Recompensa | O que reforça |
|---|---|---|
| Vença 3 fases | 10 gemas | Volume de fases |
| Atravesse 25 portais | 5 gemas + 100 moedas | Loop de 5 s |
| Derrote 2 bosses | 10 gemas | Clímax da fase |
| Termine uma fase com 40+ unidades | 10 gemas | Maximizar multiplicação |
| Derrote um boss explorando a fraqueza dele | 15 gemas | Boss Scout (diferencial central) |
| Sobreviva a uma zona de perigo (portal de risco) | 10 gemas | Apetite a risco |
| Compre 1 upgrade ou evolua 1 tropa | 10 gemas | Loop de meta |
| Vença uma fase perdendo no máximo 5 unidades | 15 gemas | Jogo "inteligente", não só grande |

**Justificativa:** missões não pedem "assista X anúncios" — rewarded se vende sozinho pelo valor; missão de anúncio cheira a desespero e canibaliza a confiança.

### 2.5 Loop semanal

| Elemento | MVP (soft launch) | Release |
|---|---|---|
| Conteúdo | Progressão pelos 3 mundos + evolução de tropas (fragmentos, CANON §8) | Novo mundo a cada ~2 semanas de jogo médio; 10 mundos |
| Colecionismo | 10 skins do Soldado na loja, rotação semanal de destaque | Skins por tropa + cartas lendárias |
| Competição | Missão especial de fim de semana **[F3]** (ex.: "derrote o Zumbi Titã com ≤30 unidades" — 30 gemas) | Ranking semanal + evento semanal "Caçada ao Boss" (tela Eventos) |
| Monetização | Remover Anúncios (MVP); Oferta inicial (48 h) **[F2+]** | Passe de Temporada mensal com milestones semanais |

O loop semanal responde à pergunta de longo prazo: "o que estou construindo?" A resposta visível é o **exército-coleção**: tela de Tropas com cartas, níveis e fragmentos acumulando — progresso que nenhuma derrota apaga.

---

## 3. O loop de 12 passos do BRIEF — FAZ · SENTE · RECOMPENSA · GANCHO

| # | Passo (BRIEF) | O que o jogador FAZ | O que SENTE | Recompensa recebida | Gancho para o próximo passo |
|---|---|---|---|---|---|
| 1 | Entra no jogo | Toca no ícone; boot ≤5 s | Expectativa | Música + logo animado curto | Tela inicial já carregada com botão pulsando |
| 2 | Tela principal "Jogar" | 1 toque em JOGAR (botão gigante, centro-baixo) | Zero fricção | Badges de upgrade/baú de fase visíveis de relance (baú diário/missões **[F2+/F3]**) | Transição direta para a fase (sem loading perceptível) |
| 3 | Fase começa com 1 unidade | Vê seu único Soldado correndo; Boss Scout de 2 s antes | "Estou pequeno… por enquanto" | Plano grátis: a fraqueza do boss | Primeiro par de portais já no horizonte |
| 4 | Corrida (45–75 s) | Desliza o dedo para mover a multidão | Fluxo, velocidade | Moedas soltas no caminho; música acelera com o tamanho do exército | Cada portal atravessado revela o próximo par |
| 5 | Escolhas entre portais | Compara números/ícones/risco em ~1 s e escolhe | Tensão deliciosa de micro-puzzle | A própria escolha (agência) | Resultado imediato e visível da escolha |
| 6 | Multiplica/transforma/evolui | Atravessa o portal | Êxtase de crescimento ("MEGA ARMY!") | Exército visivelmente maior/mutado; contador salta | "Se x2 foi bom, o que faz aquele x3 ali?" |
| 7 | Evita obstáculos/armadilhas/inimigos | Desvia; protege as unidades | Perigo controlado; perdas doem na medida | Unidades salvas = poder preservado | Zona de perigo anuncia o portal de risco adiante |
| 8 | Arena final | Vê o corredor abrir para a arena | Respiração antes do clímax | Contagem final do exército em destaque | Chão treme: o boss vem aí |
| 9 | Boss gigante | Assiste o choque exército × boss; usa o que plantou | Tensão → poder ("eu PLANEJEI isso") | Barra de vida do boss derretendo; slow motion no golpe final | Explosão de peças/partículas (sem sangue, CANON §1) |
| 10 | Ganha recompensas | Coleta em sequência: moedas → XP → fragmentos → baú | Colheita; números subindo | 100+ moedas, XP, chance de carta (CANON §6, §8) | Botão "DOBRAR com anúncio" + "Próxima fase" |
| 11 | Volta à tela principal | Retorna (ou pula direto via "Próxima fase") | Pausa segura | Badges acesos: upgrade disponível (missão 2/3 **[F3]**) | Toda barra a 80–95% pede "só mais uma" |
| 12 | Melhora tropas / desbloqueia / próxima fase | Gasta moedas em upgrades; evolui cartas com fragmentos | Poder permanente; investimento | +5%/nível nas trilhas (CANON §9); tropa nova | Boss Scout da próxima fase já provoca: "fraco contra FOGO" |

**Notas de design sobre o loop de 12 passos:**

1. **Passo 2 em ≤5 s da abertura do app** (BRIEF §Regras de produto). No primeiro lançamento, a tela inicial aparece em versão mínima (só JOGAR + moedas) — loja/tropas/upgrades surgem conforme desbloqueiam (§7), evitando menu intimidador.
2. **Passo 11 é opcional no caminho feliz.** A tela de vitória tem "Próxima fase" direto; o retorno à tela principal acontece naturalmente a cada 2–3 fases, quando um badge de upgrade/baú/missão cria motivo. Isso preserva o encadeamento de fases (meta ≥6 fases/sessão) sem esconder a meta-progressão.
3. **Sequenciamento do passo 10 é fixo** (moedas → XP → fragmentos → baú → oferta de dobrar): o cérebro precisa de ordem para registrar valor. A oferta de rewarded vem **por último**, quando o jogador já viu o que pode dobrar.
4. **O passo 5 é o produto.** Tudo nos outros 11 passos existe para dar peso à escolha de portal: o Boss Scout dá contexto, o Supply dá nuance, o boss dá consequência.

---

## 4. Progressão dos primeiros 30 minutos (Entregável 3)

### 4.1 Premissas

- Jogador novo, primeira sessão, dispositivo mediano, em sessão contínua (se sair, retoma exatamente no mesmo ponto — save local-first, CANON §13).
- Pacing canônico obrigatório (CANON §16): fase 1 impossível de perder com vitória <60 s da abertura; fase 2 primeira escolha real; fase 3 boss "uau"; fase 5 Upgrades + Arqueiro; fase 7 Gigante de Madeira + baú grande; fase 10 baú épico + 50 gemas; interstitials só após a fase 6.
- Taxas de vitória alvo: bandas do CANON §12 detalhadas fase a fase no doc 05 §4.5 (fonte única de tuning de bosses do MVP) — 95% nas fases 1–3, 85% nas fases 4–6, 75% no boss de mundo (fase 7), 80→70% nas fases 8–13, com ~70% na derrota desenhada da fase 11.

### 4.2 Linha do tempo minuto a minuto

| Tempo | Evento | Detalhe |
|---|---|---|
| 0:00–0:05 | Boot + tela inicial mínima | Assets da fase 1 no pacote base; sem login, sem permissões (Auth anônimo silencioso) |
| 0:05–0:08 | Toque em JOGAR | Botão pulsa; "mão fantasma" toca se o jogador hesitar 3 s |
| 0:08–0:58 | **FASE 1** | Corrida curta (~40 s) + boss fácil (~8 s). **Vitória aos ~0:55 — cumpre "vitória em <60 s"** |
| 0:58–1:20 | Vitória 1 | 100 moedas (CANON §8) + primeira oferta "DOBRAR com anúncio" (opcional, sem pressão) |
| 1:20–2:50 | **FASE 2** + recompensa | Primeira escolha estratégica real |
| 2:50–4:40 | **FASE 3** + recompensa | Boss "uau" com slow motion; teaser: "algo novo na fase 5" |
| 4:40–6:20 | **FASE 4** + recompensa | Obstáculos de verdade + primeiro portal ÷2 |
| 6:20–9:00 | **FASE 5** + tutorial de Upgrades | Nível 2 → tela de Upgrades; compra o 1º upgrade (100 moedas); **Arqueiro permanente** |
| 9:00–10:50 | **FASE 6** + recompensa | Portal de risco + primeiro estouro de Supply; **1º interstitial após a fase** (regra canônica) |
| 10:50–13:20 | **FASE 7** + baú grande | Boss Scout "FRACO CONTRA FOGO 🔥" → **Gigante de Madeira**; nível 3 → Baús (missões diárias chegam em **[F3]**) |
| 13:20–15:00 | **FASE 8** + recompensa | Cinemática de 3 s do Mundo 2 (Cidade Zumbi); **Escudeiro** desbloqueado |
| 15:00–17:00 | **FASE 9** + recompensa | Hordas de percurso maiores; interstitial #2 (3 fases após o anterior) |
| 17:00–20:00 | **FASE 10** + baú épico + loja | **Baú épico + 50 gemas**; nível 4 → Loja completa; **Mago** garantido no baú |
| 20:00–22:30 | **FASE 11** (derrota provável) | Primeira derrota desenhada (~70% vitória); tela de derrota com motivo + 2 saídas |
| 22:30–24:00 | **FASE 11 — revanche** | Vitória após reviver com anúncio OU upgrade de Vida nível 2 |
| 24:00–27:00 | **FASE 12** + recompensa | Consolidação; interstitial #3 |
| 27:00–30:00 | Respiro de meta | Evolui o Soldado para nível 2 (10 fragmentos, CANON §8); revisa as trilhas de upgrade; **teaser do Zumbi Titã (fase 14)** |

### 4.3 Fase a fase (1–12)

| Fase | Mundo | Novidade apresentada | Objetivo de aprendizado | Emoção alvo | Vitória alvo | Recompensa |
|---|---|---|---|---|---|---|
| 1 | M1 Campo Inicial | Deslizar; portais +10 e o **primeiro x2**; boss-filhote | "Portal = crescer; mais = bom" | Deleite imediato | 100% (impossível perder) | 100 moedas + 15 XP |
| 2 | M1 | Par-armadilha x2 vs +25; portal **Virar Arqueiro** (efeito só na fase) | "Pensar 1 s vale mais que reflexo" | Orgulho da 1ª decisão esperta | ≥95% | 110 moedas |
| 3 | M1 | **Golem de Pedra** com entrada cinematográfica; ataque especial telegrafado | "Boss é evento; arena tem regras" | Assombro ("uau") | ≥95% | 121 moedas + drop de fragmentos |
| 4 | M1 | Obstáculos reais (troncos rolantes, espinhos); **÷2 posicionado na rota cômoda** | "Existem portais ruins; atenção espacial" | Perigo controlado | ~85% | 133 moedas |
| 5 | M1 | **Upgrades (4 trilhas MVP)** + **Arqueiro permanente** | "Existe progresso fora da corrida" | Poder crescente; início da coleção | ~85% | 146 moedas + tutorial de upgrade |
| 6 | M1 | **Portal de risco** ("x10 se sobreviver à zona de perigo") + **estouro de Supply** com fanfarra | "Risco é opcional e delicioso; excedente vira moeda" | Adrenalina | ~85% | 161 moedas + bônus de overflow |
| 7 | M1 | **Boss de mundo: Gigante de Madeira (fraco: Fogo)**; portal Elemento Fogo como rota ótima | "Boss Scout + elemento certo = boss derrete" | Clímax + colheita | ~75% (doc 05 §4.5) | 177 moedas + **baú grande** (150 moedas, 12 fragmentos de Soldado) + **10 gemas** (CANON §8) |
| 8 | M2 Cidade Zumbi | Novo bioma; **inimigos de percurso** (hordas que mordem o exército); **Escudeiro** | "Composição defensiva importa" | Novidade + leve ameaça | ~80% | 195 moedas |
| 9 | M2 | Portais bons em posições difíceis; hordas densas | "Posicionar o exército é parte do puzzle" | Maestria emergente | ~78% | 214 moedas |
| 10 | M2 | **Baú épico + 50 gemas** (CANON §16); **Loja completa**; **Mago** (carta garantida no baú) | "Gemas/baús/loja; dano em área muda tudo" | Riqueza; "o jogo é fundo" | ~76% | 236 moedas + baú épico (300 moedas, fragmentos, carta do Mago) + 50 gemas |
| 11 | M2 | **Primeira derrota desenhada** (meio de mundo); boss Brutamontes Zumbi reforçado | "Derrota → upgrade/reviver → vitória" | Frustração produtiva | ~70% | Na revanche: 259 moedas |
| 12 | M2 | Consolidação: mesma dificuldade, jogador mais forte | "Investir em upgrade funciona" | Competência confirmada | ~75% | 285 moedas |

**Detalhamento dos beats críticos:**

- **Fase 1 — o contrato.** Zero texto de tutorial. O jogador aprende deslizando: o corredor afunila suavemente até o primeiro par de portais, **+10 vs +10** (espelhado — qualquer lado ensina o gesto sem risco). O segundo par é **x2 vs +10**: com ~11 unidades, x2 ≈ +11 — qualquer escolha vence. Os dois pares são **contíguos, sem vão** — impossível não atravessar (doc 06 §6). O boss-filhote (variante mínima do Golem de Pedra) morre em ~5 s. Tudo que o jogo promete na loja de apps acontece nos primeiros 60 segundos.
- **Fase 2 — a primeira escolha real (CANON §16).** Par central: exército de ~15 unidades encontra **x2 (=30) vs +25 (=40)**. Quem escolhe "errado" ainda vence (95%), mas o contador mostra a diferença — a lição se ensina sozinha. Segundo par: **x2 Soldados vs Virar Arqueiro** — primeiro contato com qualidade vs quantidade; os Arqueiros atacam o boss à distância e o jogador *vê* o valor.
- **Fase 3 — o boss "uau" (CANON §16).** Golem de Pedra em tamanho real: câmera baixa, entrada em ≤2 s, chão rachando, barra de vida gigante. O ataque especial telegrafado (braçada lenta no chão) esmaga quem ficou agrupado — primeira morte "dramática" de unidades, sem ameaçar a vitória (95%). É a fase desenhada para o jogador gravar/compartilhar.
- **Fase 5 — abre a meta.** Ao vencer, o nível 2 destrava Upgrades (alinhamento XP↔fase na §4.4). O tutorial é uma única ação guiada: comprar "Dano inicial nível 1" por 100 moedas (exatamente o custo do primeiro upgrade, CANON §8 — o jogador sempre tem saldo). Celebração do Arqueiro permanente: agora ele **começa** as fases no exército.
- **Fase 7 — o Boss Scout paga.** O cartão grita "GIGANTE DE MADEIRA — FRACO CONTRA FOGO 🔥". Na corrida, o portal Elemento Fogo está posicionado num par contra um x2 sedutor: quem seguiu o plano vê o boss derreter com +50% de dano (CANON §4); quem ignorou ainda vence, mas sua próxima leitura de Boss Scout será atenta. Baú grande + 10 gemas + teaser do Mundo 2 = motivo de voltar amanhã plantado no minuto 13 (no soft launch, as missões diárias **[F3]** reforçam esse gancho).
- **Fase 10 — a fase-vitrine.** Maior explosão de recompensa dos 30 minutos (baú épico + 50 gemas + Mago + Loja). O Mago chega na hora exata em que as hordas zumbis pedem dano em área — desbloqueio que resolve um problema que o jogador acabou de sentir.
- **Fase 11 — a derrota com roteiro.** O boss reforçado pune exércitos sem upgrade. A tela de derrota mostra o motivo concreto + 2 saídas: **reviver com anúncio** (1×/fase) ou **upgrade de Vida nível 2 (135 moedas — o jogador tem ~1.500)**. Qualquer caminho leva à vitória na revanche. **Nunca** há interstitial aqui (CANON §11).

### 4.4 Orçamento econômico e de XP dos 30 minutos

**XP (valores novos, coerentes com CANON §8; tuning via Remote Config):** vitória = 10 + (5 × nº da fase); boss de mundo = +30; derrota = 5.
**Níveis:** nv2 = 120 XP · nv3 = 220 · nv4 = 380 · nv5 = 550 · nv6 = 750.

| Checkpoint | XP acumulado | Nível | Desbloqueio (CANON §8) |
|---|---|---|---|
| Fim da fase 5 | 125 | **2** | Upgrades |
| Fim da fase 7 | 240 | **3** | Baús (missões diárias **[F3]**) |
| Fim da fase 10 | 405 | **4** | Loja completa |
| Fim da fase 12 | 545 | 4 (faltam 5 XP) | — gancho perfeito de fim de sessão |
| Fase 13 (D2) | 620 | **5** | Passe de Temporada **[Release]** |
| Fase 15 (D2–D3) | 815 | **6** | Eventos **[Release]** |

**Moedas (fase 1 = 100; crescimento ×1,10^(fase−1), CANON §8):**

| Fluxo | Valor aproximado |
|---|---|
| Vitórias fases 1–12 | ~2.140 |
| Baú grande (f7) + baú épico (f10) | ~450 |
| Overflow de Supply + moedas de percurso | ~80 |
| 2 ofertas de dobrar aceitas (estimativa 35–50%) | ~250 |
| **Total ganho** | **~2.900** |
| Upgrades: 4 trilhas nível 1 (4×100) + Dano nv2 (135) + Vida nv2 (135) | −670 |
| Evolução Soldado nível 2 (10 fragmentos + 100 moedas) | −100 |
| **Saldo ao minuto 30** | **~2.100** — jogador termina rico, com próximo upgrade (182) já acessível |

### 4.5 Riscos de churn nos 30 minutos e mitigação

| Fase/momento | Risco de churn | Mitigação | Métrica sentinela (Analytics) |
|---|---|------|---|
| Boot | Load >5 s em aparelho fraco | Pacote base enxuto; fase 1 embutida no APK; URP com perfil "mediano" como padrão | tempo até `level_start` da fase 1 |
| Fase 1 | Não entender o input | Afunilamento físico do corredor + mão fantasma após 3 s parado | `gate_missed` na fase 1 |
| Fase 2 | Escolher "errado" e se sentir burro | Errar ainda vence; contador mostra a diferença sem texto de bronca | taxa de vitória f2 <93% |
| Fase 4 | Primeiro ÷2 percebido como trapaça | ÷2 sempre sinalizado em vermelho com número legível (portais honestos, §3.4 CANON) | quedas de sessão pós-f4 |
| Fase 5 | Tutorial de upgrade chato | 1 ação única guiada, 15 s, pulável | `tutorial_complete` <90% |
| Fase 6 | 1º interstitial irrita | Só após fase 6, com tela-ponte "patrocinado" de 0,5 s; frequência 100% Remote Config | churn na sessão pós-`interstitial_shown` |
| Fase 8 | Hordas de percurso frustram | Escudeiro entregue na mesma fase; hordas matam ≤15% do exército médio | `level_fail` f8 >10% |
| Fase 11 | Derrota dupla → abandono | Reviver + upgrade barato à mão; **bloqueio de interstitial após 2 derrotas**; Remote Config pode amaciar o boss | 2× `level_fail` seguidos no funil |
| Min 25–30 | Cansaço natural | Respiro de meta (colecionismo) + teaser do Zumbi Titã; fim de sessão SEMPRE com barra a 80–95% | duração média da 1ª sessão |

---

## 5. Progressão dos primeiros 7 dias (Entregável 4)

### 5.1 Visão geral

Persona de referência: jogador retido "médio" (1–2 sessões/dia de ~10 min). O jogador "hardcore" corre ~1 dia à frente; o "casual" ~1 dia atrás — os ganchos funcionam nos três ritmos porque são atrelados a **fase e nível**, não a calendário (exceto missões/baús).

**Escopo por build (alinhado ao doc 13):** este plano de 7 dias descreve a experiência-alvo do **soft launch (F2/F3)** — onde D1/D3/D7 são medidos com tráfego real (gates de F2/F3, doc 13 §2.2). Recursos marcados **[F2+]** (baú diário, calendário de login, oferta inicial), **[F3]** (missões diárias, baú extra via rewarded, missão de fim de semana) e **[F4]** (FCM) **não existem na build MVP/F1**: nela, os ganchos de retorno dos 7 dias são a progressão de fases (Zumbi Titã na f14, Robô Escorpião na f20), a evolução de tropas por fragmentos e os baús de fase do CANON §16.

| Dia | Conteúdo típico | Gancho dominante | Momento de risco | Contramedida |
|---|---|---|---|---|
| D1 | Fases 1–12; nv1→4; Arqueiro, Escudeiro, Mago | Curva canônica de pacing (§4) | 1ª derrota (f11); 1º interstitial | Reviver + upgrade barato; interstitial só pós-f6 |
| D2 | Fases 13–16; nv5–6; **Zumbi Titã (f14)**; Gigante | Boss único + oferta inicial expirando (48 h) **[F2+]** | Não voltar nunca (maior queda absoluta) | Zumbi Titã + Gigante como ímã de conteúdo; missões **[F3]**, calendário **[F2+]** e FCM **[F4]** reforçam quando ativos |
| D3 | Fases 17–20; **Robô Escorpião (f20, ~55%)** | Clímax do M3 + missão especial | Paredão de dificuldade + fim do conteúdo de fases (MVP) | Remote Config na dificuldade; pivô para evolução de tropas |
| D4 | Pós-f20: evolução de tropas, replays com missão **[F3]** | Fragmentos: Soldado/Arqueiro nv3 | "Acabou o jogo?" | Missões direcionadas a fases antigas com composições novas **[F3]**; skins |
| D5 | Rotina diária estabelecida | Calendário dia 5 + baú diário **[F2+]** | Monotonia | Missão especial de fim de semana (30 gemas) **[F3]**; rotação de skin na loja |
| D6 | Acúmulo para o prêmio do D7 | "Não quebre a sequência" | Esquecimento | FCM de sequência **[F4]**; badge de calendário **[F2+]** |
| D7 | **Prêmio do calendário [F2+]: carta épica + 60 gemas** | Colheita de 1 semana + ranking **[Release]** | Pós-prêmio: "e agora?" | Prêmio inclui teaser jogável (testar tropa lendária por 1 fase via rewarded **[F3]**, CANON §11) |

### 5.2 Dia a dia

**D1 — Aquisição → hábito.** Tudo da §4. Além disso: ao fechar o app, o estado de saída é fotografado (barra de upgrade, missão incompleta) e vira o conteúdo da 1ª notificação **[F4]** (quando o FCM estiver ativo — doc 13 §2.4). A **oferta inicial (US$ 2,99, 1×, primeiras 48 h — CANON §11) [F2+]** entra no soft launch v1.1 (doc 08 §1.3; doc 13 §3) e aparece apenas APÓS o nível 4 (Loja completa, fase 10): o jogador precisa entender o valor de gemas/baús antes de ver preço. Mostrar oferta antes disso = desperdício da única bala. Na build MVP, a loja expõe apenas Remover Anúncios.

**D2 — O dia que define D3.** Conteúdo: fases 13–16. A fase 14 é o **Zumbi Titã** (fraco: Fogo — alinhado ao CANON §6; no MVP sem elemento Luz). Vitória = **Gigante desbloqueado** (épico, Supply 12) — primeira tropa "pesada", que muda visivelmente a silhueta do exército. Nível 5 (~fase 13): **[Release]** abre o Passe de Temporada com 1 nível grátis de degustação; no MVP, concede pacote de 30 gemas + skin "Recruta de Bronze". A oferta inicial **[F2+]** entra em contagem regressiva visível (últimas horas).

**D3 — A montanha.** Fases 17–20 no Deserto Robótico (M3). A fase 20 é o **Robô Escorpião** (~55% de vitória — CANON §12, fase final de mundo). É a primeira luta que normalmente exige 2–3 tentativas + upgrades — desenhada para ser a história que o jogador conta ("quase, QUASE!"). Boss Scout exibe "FRACO CONTRA RAIO · IMUNE A VENENO" (CANON §6); no MVP, a lição prática é compositiva (Mago + Gigante + upgrades de Dano contra boss), e o portal de Elemento Raio chega no release para pagar essa promessa. Derrotar o Robô Escorpião dá 10 gemas + baú de mundo. Nível 6: **[Release]** abre Eventos (Corrida do Dia + ranking semanal); no MVP, pacote de 30 gemas + 20 fragmentos de Mago.

**D4 — O pivô para colecionismo.** Com as 20 fases do MVP vencidas (jogador médio: parcialmente), o motor vira a **evolução de tropas**: nível n→n+1 custa 10 × 2^(n−1) fragmentos + moedas (CANON §8) — Soldado nv3 (20 fragmentos) e Arqueiro nv2 estão ao alcance. Missões **[F3]** direcionam replays com objetivo ("vença a fase 14 perdendo ≤5 unidades") que mudam a forma de jogar fases conhecidas. **[Release]** D4 cai no meio do evento semanal — milestone intermediário entrega baú raro.

**D5 — Rotina + tempero.** Checklist diário (~6–8 min) + 1–2 fases de replay. Entra a **missão especial de fim de semana [F3]** (válida D5–D7): "Derrote o Zumbi Titã com no máximo 30 unidades — 30 gemas". Recompensa alta + restrição criativa = o tipo de desafio que gera clipes ("exército pequeno vencendo por estratégia", BRIEF §Viralização). Loja rotaciona a skin em destaque.

**D6 — Tensão de véspera.** Badge do calendário **[F2+]** mostra "amanhã: CARTA ÉPICA". Acúmulo de gemas das missões **[F3]** (~120–200 gemas somadas na semana) deixa o **baú raro da loja (300 gemas, CANON §8)** a 1–2 dias de distância — primeira meta de poupança do jogador. **[Release]** último dia do evento semanal: ranking fecha, urgência real.

**D7 — Colheita e recomeço.** Calendário **[F2+]** dia 7: **carta épica garantida + 60 gemas**. A carta épica é de uma tropa que o jogador ainda não maximizou (seleção inteligente server-side) — recompensa que vira plano. Na sequência, oferta de **testar tropa lendária por 1 fase via rewarded [F3]** (CANON §11; doc 13 §2.4): o gostinho do end-game. **[Release]** recompensas de ranking + novo ciclo semanal começam imediatamente — o D8 já tem pauta.

### 5.3 Notificações FCM — política e copies **[F4]**

**Escopo:** o FCM entra apenas em **F4** (doc 12 §7.2; doc 13 §2.4 e §4 item 11 — obrigatório pré-global). No MVP e no soft launch F2–F3 **não há push**; a política e as copies abaixo ficam prontas para essa ativação.

**Política (anti-irritação):**
1. Máximo **1 notificação/dia** (exceção: +1 se uma oferta paga estiver expirando em <6 h).
2. Enviar no **horário modal de jogo do usuário** (aprendido por Analytics); fallback 19h local.
3. **Supressão:** nunca enviar se o jogador jogou nas últimas 8 h; janela de silêncio 22h–9h local.
4. Toda copy é A/B testável via Remote Config + FCM; opt-out granular nas configurações.
5. Toda notificação leva a um estado de jogo específico (deep link), nunca à tela inicial genérica.
6. **Sem culpa (hard rule, doc 14 §3.5):** o padrão "suas tropas sentem sua falta" está **proibido**. Toda copy oferece valor concreto e verificável (baú pronto, missão nova, oferta expirando de verdade) — nunca apela a abandono, saudade ou cobrança emocional.

**Copies de exemplo (PT-BR, originais):**

| Dia/Gatilho | Condição | Copy | Objetivo |
|---|---|---|---|
| D1, ~20 h sem jogar | Não retornou após 1ª sessão | "Seu baú diário está cheio: moedas + chance de carta, grátis. Abra antes de ele transbordar. 🎁" | Ganhar o D1→D2 |
| D2, manhã | Missões resetadas | "3 missões novas = até 35 gemas. Bora começar o dia multiplicando? ⚡" | Hábito diário |
| D2, oferta expirando | Oferta inicial <6 h | "Sua oferta de iniciante desaparece em 6 horas — e ela não volta." | Conversão IAP |
| D2–D3, parado num boss | 2+ derrotas na mesma fase, 12 h fora | "O Zumbi Titã continua parado na fase 14, achando que você desistiu. Prova que não. 🔥" | Reativar no clímax |
| D3, sem login | 24 h ausente | "Baú diário pronto + 3 missões novas valendo até 35 gemas. Tudo grátis, expira à meia-noite. 🪙" | Salvar o D3 |
| D4–D5, fragmentos parados | Pode evoluir tropa e não evoluiu | "Seu Soldado está a 1 toque do nível 3. Ele pediu pra avisar. 💪" | Engajar meta |
| D6, sequência de login ativa | Logou D1–D6 | "Amanhã é dia 7: carta ÉPICA garantida. Não quebra a sequência agora." | Garantir o D7 |
| D7, prêmio não coletado | Logou mas não abriu calendário | "Sua carta épica está na mesa, comandante. É só assinar o recibo. ✍️" | Fechar o ciclo |
| **[Release]** evento, último dia | Top 20% do ranking | "Última chamada: você está a 2 vitórias do baú dourado do ranking. 🏆" | Pico semanal |

### 5.4 Onde D1, D3 e D7 são ganhos ou perdidos

Metas canônicas (CANON §12): **D1 ≥ 40% · D3 ≥ 22% · D7 ≥ 12%**.

| Métrica | Onde se GANHA | Onde se PERDE | Contramedida | Sentinela |
|---|---|---|---|---|
| **D1** | Vitória <60 s; boss "uau" (f3); baú grande na f7; saldo rico ao sair; missões plantadas **[F3]** | Load lento; ÷2 visto como trapaça; interstitial precoce/agressivo; derrota dupla na f11 | Fase 1 embutida no build; portais honestos; interstitial só pós-f6 e 1/3 fases; nunca ad após 2 derrotas; FCM D1 com baú **[F4]** | Funil `level_start`→`level_complete` f1–f5; churn pós-`interstitial_shown` |
| **D3** | Zumbi Titã (f14) como evento; oferta inicial **[F2+]** em urgência honesta; missões **[F3]** + calendário **[F2+]** criando hábito; Robô Escorpião como "montanha" justa | Paredão de dificuldade (f17–20) sem caminho de força; sensação de "acabou o conteúdo"; grind de moedas (custo 1,35^n) sem fontes novas | Dificuldade e recompensas 100% em Remote Config; pivô explícito para evolução de tropas; rewarded de dobrar (MVP) + baú extra **[F3]** como acelerador F2P; missões de replay com objetivo **[F3]** | `level_fail` f17–20 >45%; moedas médias em carteira; DAU que zera missões |
| **D7** | Carta épica do calendário **[F2+]**; meta de poupança (baú raro 300 gemas); missão especial de fds **[F3]**; teaser de lendária via rewarded **[F3]**; ranking **[Release]** | Rotina sem novidade (D4–D6); notificação irritante (uninstall!); upgrades caros demais para o ritmo de renda | Cadência de "tempero" D5; política FCM rígida **[F4]** (1/dia, horário modal, supressão); curva de renda recalibrável; prêmio D7 que vira plano (carta épica direcionada) | Retenção D4–D6 diária; opt-out de push; razão custo-do-próximo-upgrade / renda-diária |

**Regra de ouro do funil:** cada dia precisa terminar com **um plano que só se realiza amanhã** (baú que recarrega, missão nova, carta a 10 fragmentos, oferta expirando). Retenção não é memória — é agenda.

### 5.5 Calendário de login (7 dias) **[F2+]**

Painel simples na tela inicial (sem tela nova). **Escopo:** fora da build MVP — o calendário não consta do escopo travado do CANON §15 nem do backlog P0 do doc 13; esta especificação fica pronta para entrar no soft launch (F2+), junto do baú diário. Desbloqueia no 2º login.

| Dia | Recompensa | Lógica |
|---|---|---|
| 1 | 100 moedas | Valor imediato, sem fricção |
| 2 | 10 gemas | Apresenta a moeda premium cedo |
| 3 | 10 fragmentos de Soldado | Alimenta a 1ª evolução em andamento |
| 4 | Baú comum extra | Reforça o hábito de abrir baús |
| 5 | 20 gemas | Acelera a poupança rumo ao baú raro (300) |
| 6 | 15 fragmentos de Arqueiro | Plano para o D7+ |
| 7 | **Carta épica garantida + 60 gemas** | Pico memorável; a carta é de tropa não maximizada |

---

## 6. Tabela mestra de desbloqueios

| Gatilho | Desbloqueio | Escopo | Justificativa de design |
|---|---|---|---|
| Fase 1 | Core run + Boss Scout (formato mínimo: só o retrato do boss) | MVP | Diferencial central presente desde o frame 1, sem custo cognitivo |
| Fase 2 | Portal "Virar Arqueiro" (transformação válida só na fase) | MVP | Degustação da tropa antes do desbloqueio permanente — desejo antes da posse |
| Fase 3 | Arena cinematográfica + ataque especial telegrafado | MVP | O "uau" canônico (§16); estabelece boss como evento, não como obstáculo |
| Fase 4 | Obstáculos plenos + portal ÷2 | MVP | Risco entra depois da confiança; portais honestos evitam sensação de trapaça |
| Fase 5 / nível 2 | **Tela de Upgrades (4 trilhas MVP)** + **Arqueiro permanente** | MVP | CANON §16; meta-progressão chega quando o jogador já ama o core |
| Fase 6 | Portal de risco + estouro de Supply visível; interstitials habilitados | MVP | Risco e monetização intersticial só após o hábito formado (CANON §11) |
| Fase 7 / nível 3 | **Baús** + boss de mundo M1 (Gigante de Madeira); missões diárias **[F3]** | MVP (missões: F3) | Clímax do M1 entrega o motivo de voltar amanhã (CANON §16); missões chegam no soft launch (doc 13 §4 item 2) |
| Fase 8 | Mundo 2 (Cidade Zumbi) + **Escudeiro** + inimigos de percurso | MVP | Novo problema (hordas) e nova ferramenta (tanque) no mesmo minuto |
| Fase 10 / nível 4 | **Loja completa** + baú épico + 50 gemas + **Mago**; oferta inicial elegível **[F2+]** | MVP (oferta: F2+) | Recompensa grande canônica (§16); loja só depois de entender as moedas |
| Fase 13 / nível 5 | Passe de Temporada **[Release]** / pacote 30 gemas + skin (MVP) | Release | Monetização recorrente só para quem demonstrou retenção (D2) |
| Fase 14 | **Gigante** (vitória sobre o Zumbi Titã) | MVP | Tropa épica como troféu de boss único — poder com narrativa |
| Fase 15 / nível 6 | Eventos: Corrida do Dia + evento semanal + ranking **[Release]** / pacote 30 gemas + 20 fragmentos de Mago (MVP) | Release | Competição entra quando há repertório para competir |
| Fase 15 | Mundo 3 (Deserto Robótico) | MVP | Cadência de bioma a cada ~7 fases mantém novidade visual no D2–D3 |
| Fase 20 | Robô Escorpião + baú de mundo + 10 gemas | MVP | Clímax do MVP (~55% vitória, CANON §12): a "montanha" que gera história |
| 2º login | Calendário de login (7 dias) | **F2+** | Só aparece para quem voltou — não polui o D1; fora do escopo travado do CANON §15 |
| D1 pós-nível 4, 1ª visita à loja | Oferta inicial US$ 2,99 (48 h, 1×) | **F2+** (soft launch v1.1) | CANON §11; valor antes do preço; rollout no doc 08 §1.3 |

---

## 7. Instrumentação e validação deste documento

**Mapeamento loop → eventos de Analytics (BRIEF §Analytics):**

| Loop | Eventos-chave | Pergunta que responde |
|---|---|---|
| Portal (5 s) | `gate_selected`, `gate_missed` | Portal mais escolhido; pares desbalanceados; armadilhas que enganam demais |
| Fase (90 s) | `level_start`, `level_complete`, `level_fail`, `boss_start`, `boss_defeated`, `boss_failed` | Taxas de vitória vs alvos do CANON §12, fase a fase |
| Sessão | `rewarded_ad_shown/completed`, `interstitial_shown`, fases por sessão | ≥6 fases/sessão; conversão rewarded ≥35%; impacto de interstitial no churn |
| Diário | `chest_opened`, missões completadas, `day_1/3/7_retention` | Saúde do checklist; gemas/dia dentro de 20–40 |
| Semanal | `unit_unlocked`, `unit_upgraded`, `purchase_started/completed`, `season_pass_opened` **[Release]** | Profundidade da meta; LTV; conversão de compra |

**Knobs de Remote Config que este documento assume calibráveis (CANON §13, BRIEF §Tecnologia):** HP/dano de cada boss por fase (alvo de vitória), recompensa de moedas por fase, frequência de interstitial; e, quando os respectivos recursos estiverem ativos: valores e mix das missões diárias **[F3]**, recompensas do calendário **[F2+]**, horário/copy de FCM **[F4]**, posição e preço da oferta inicial **[F2+]**.

**Critérios de aceite no soft launch (amarrados ao CANON §12):**
1. ≥85% dos novos usuários vencem a fase 1 em <60 s da abertura do app.
2. Mediana da 1ª sessão ≥10 min e ≥6 fases.
3. Funil fases 1–7 sem degrau de abandono >8% entre fases consecutivas.
4. D1 ≥ 40%, D3 ≥ 22%, D7 ≥ 12%; conversão rewarded ≥35% dos DAU.
5. `gate_selected` na fase 7: ≥60% dos jogadores escolhem o portal Elemento Fogo (prova de que o Boss Scout ensina). Abaixo disso, reposicionar o par via LevelConfigSO.

**Dependências:** doc 01 (visão geral e identidade) · doc 03 (sistema de unidades — stats e evolução) · doc 04 (sistema de portais — tabela completa de gates) · docs de economia e monetização (custos de baús, conteúdo detalhado de cada baú e ofertas) · doc de telas (wireframes da tela inicial, vitória, derrota e painéis de missão/calendário aqui referenciados).
