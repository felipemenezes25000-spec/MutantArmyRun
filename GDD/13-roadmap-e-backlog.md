# 13 — Roadmap, Backlog & Planos · Mutant Army Run

> Cobre os entregáveis **19 (Roadmap)**, **20 (Backlog por prioridade)**, **21 (Plano do MVP em 30 dias)** e **22 (Expansão pós-MVP)**.
> Fonte da verdade: `CANON.md` (escopo do MVP em §15, metas em §12, pacing em §16). Requisitos: `BRIEF.md`.
> Docs relacionados: `01-visao-e-conceito.md` (visão), `04-sistema-de-portais.md` (portais), `07-economia-e-upgrades.md` (números), `12-arquitetura-unity.md` (managers e ScriptableObjects), `15-referencias-e-recursos.md` (assets CC0/MIT, licenças e ordem de integração de SDKs — base das estimativas de integração deste doc).
> Datas-base: pré-produção inicia em **11/06/2026**; Dia 1 do MVP = **17/06/2026**.

---

## 1. ROADMAP — macro-fases (entregável 19)

### 1.1 Visão geral

| Fase | Período | Duração | Objetivo central |
|---|---|---|---|
| F0 — Pré-produção | 11/06 → 16/06/2026 | 6 dias | Travar design, montar pipeline, comprar assets |
| F1 — MVP | 17/06 → 16/07/2026 | 30 dias | Build jogável que valida o core loop (CANON §15) |
| F2 — Soft launch fechado | 20/07 → 28/08/2026 | 6 semanas | Medir D1/CPI/sessão com tráfego real |
| F3 — Ajustes de soft launch | 31/08 → 25/09/2026 | 4 semanas | Bater todas as metas do CANON §12 por 2 semanas seguidas |
| F4 — Lançamento global | 05/10/2026 | marco | Android global + início do port iOS |
| F5 — Live ops | a partir de 11/2026 | contínuo | Cadência mensal de conteúdo e eventos |

**Justificativa do formato:** hybrid-casual vive ou morre por D1 e CPI. O roadmap coloca o primeiro contato com tráfego pago o mais cedo possível (semana 6 do projeto) e condiciona todo investimento posterior (passe, eventos, mundos 4–10) a gates de métrica — nunca construímos meta-sistemas caros antes de provar retenção.

### 1.2 Critérios de entrada/saída por fase

| Fase | Critério de ENTRADA | Critério de SAÍDA (gate) |
|---|---|---|
| F0 Pré-produção | Brief aprovado pelo cliente | Pacote GDD completo (docs 01–13, 15) revisado · projeto Unity 2022 LTS + URP criado com CI de build Android · asset packs CC0 baixados, com licença confirmada na página de origem e registrados na planilha de assets (KayKit, Quaternius, Kenney — ver 15-referencias-e-recursos.md §5) · projeto Firebase criado |
| F1 MVP | Saída de F0 | 100% do checklist DoD (§2.5) · crash-free ≥ 99% em teste interno (≥ 10 devices) · 60 fps estáveis em device mediano de referência · 20 fases jogáveis do início ao fim sem bug bloqueante |
| F2 Soft launch | Build MVP aprovada · listing na Play Store (ASO v1) · 5 criativos de vídeo prontos (doc de ads) · AppLovin MAX servindo rewarded em produção (gate confirmado: a avaliação formal manteve o MAX do CANON §13; LevelPlay fica documentado como plano B — ver 15-referencias-e-recursos.md §6.1) · funil de analytics validado evento a evento | **D1 ≥ 40%** · **CPI ≤ US$ 0,40 (BR/LatAm)** · sessão média ≥ 8 min · ≥ 6 fases/sessão · conversão rewarded ≥ 35% dos DAU · crash-free ≥ 99,5% (todos do CANON §12) |
| F3 Ajustes | Pelo menos 1 métrica de F2 abaixo do gate (caso todas batam, F3 vira apenas hardening de 2 semanas) | Todas as metas de F2 + **D3 ≥ 22%** · **D7 ≥ 12%** · **ARPDAU ≥ US$ 0,08** (com IAP ligado) mantidas por 2 semanas consecutivas |
| F4 Global | Saída de F3 · FCM integrado · tela Mapa entregue · loja IAP completa via RevenueCat · LiveOps tooling (Remote Config por segmento) | Lançamento executado · UA escalando com CPI ≤ US$ 1,50 (US) · sem regressão de crash-free |
| F5 Live ops | Saída de F4 | Não tem saída: cadência mensal medida por checklist de release (§4.4) |

### 1.3 Soft launch fechado — desenho do teste

- **Países:** Brasil (mercado-alvo primário, CPI ≤ US$ 0,40), México (valida LatAm), Filipinas (CPI baixíssimo para volume de retenção). EUA entra apenas em F3, com orçamento pequeno, para calibrar o CPI ≤ US$ 1,50 antes do global.
- **Orçamento de UA:** US$ 3.000 em F2 (≥ 7.000 instalações esperadas a CPI 0,40) — suficiente para D1/D3 com significância; D7 confirma em F3.
- **Cohorts mínimos por decisão:** nenhuma mudança de balanceamento é aprovada com cohort < 500 usuários; testes A/B via Remote Config (frequência de interstitial, recompensa por fase, curva de custo de upgrade).
- **Cadência de release em F2/F3:** 1 build por semana, sexta-feira é code-freeze, segunda-feira sobe para faixa fechada.
- **Regra de decisão dura:** se após F3 completo D1 < 35% **ou** CPI BR > US$ 0,70, o projeto entra em revisão de conceito (pivô de tema/criativos) antes de qualquer gasto em F4. Matamos rápido ou escalamos rápido.

### 1.4 Live ops — cadência mensal (regime permanente)

| Semana do mês | Entrega |
|---|---|
| 1 | Nova temporada do passe (tropa + boss + skin novos, conforme BRIEF) + reset de ranking |
| 2 | Evento semanal temático + ajuste de economia via Remote Config |
| 3 | Novo conteúdo: 1 mundo novo (até completar os 10) ou pacote de 10 fases/skins |
| 4 | Build de manutenção: correções, otimização, preparação da próxima temporada |

---

## 2. PLANO DO MVP EM 30 DIAS (entregável 21)

### 2.1 Princípios do plano

1. **Core loop completo no dia 10** — corrida → portais → boss → recompensa → upgrade → próxima fase. Tudo depois disso é conteúdo, polish e integração.
2. **Toda entrega é verificável** — cada dia/semana fecha com um critério objetivo que qualquer membro do time consegue testar em device.
3. **Conteúdo usa packs CC0 integrados e recoloridos** (KayKit, Quaternius, Kenney — ver 15-referencias-e-recursos.md §5) — **zero modelagem 3D no MVP**; arte original só onde o jogador olha por mais tempo (portais, silhueta/materiais dos bosses, identidade da UI).
4. **Integrações faseadas S1→S4, cada SDK em branch própria** — ordem fixada (ver 15-referencias-e-recursos.md §6.2): **S1** Firebase + EDM4U único + minSdk 23 → **S2** MAX + rewarded dobrar + Mediation Debugger → **S3** reviver + interstitial + adapters AdMob/Unity Ads → **S4** consent UMP + app-ads.txt + RevenueCat mínimo. Integrar cedo e por estágio dilui o risco clássico de SDK quebrar a build na reta final (risco §6.3).

### 2.2 Dias 1–10 — detalhamento dia a dia

| Dia | Data | Entrega | Critério verificável |
|---|---|---|---|
| 1 | 17/06 | Projeto Unity 2022 LTS + URP portrait, repo Git, CI gerando APK, cena base, esqueleto de `GameManager` e `SaveSystem` (JSON + checksum; base SaveGameFree, MIT — adaptação, ver 15-referencias-e-recursos.md §4) + **Firebase S1** (Auth anônimo, Analytics, Crashlytics, Remote Config com defaults locais) com **EDM4U único + minSdk 23** | APK instala e abre a 60 fps no device de referência (Android mediano, ex.: 4 GB RAM / GPU Adreno 610); evento de teste visível no DebugView |
| 2 | 18/06 | Movimento do player: swipe horizontal contínuo, pista reta de 200 m, câmera follow, `LevelManager` carregando `LevelConfigSO` | Corrida de 60 s controlável de ponta a ponta; input responde em < 50 ms |
| 3 | 19/06 | `CrowdManager` + `UnitManager`: spawn via pooling (`UnityEngine.Pool` nativo — sem infra própria), formação orgânica, contador de unidades no HUD, GPU instancing | 200 unidades simultâneas a 60 fps no device de referência |
| 4 | 20/06 | `GateManager` + portais matemáticos (+10, +25, x2, x3, ÷2) sempre em pares E/D, com colisão e consumo | Matemática auditada por unit tests da função pura `int → int` (x2 → 2n; ÷2 → ⌈n/2⌉, incl. contagens ímpares — regra do doc 04); portal usado não dispara duas vezes |
| 5 | 21/06 | Portais restantes do CANON §10: Virar Arqueiro, Elemento Fogo, Risco "x10 se sobreviver à zona de perigo" + feedbacks textuais (NICE/GREAT/INSANE) | Os 8 tipos canônicos funcionam numa fase de teste única; suíte de unit tests cobrindo os 8 portais fecha até a semana 2 (item do épico E2) |
| 6 | 22/06 | Obstáculos (perda de unidades em peças/partículas, sem sangue) + **Supply fixo 60** com conversão de excedente em moedas com fanfarra | Estourar Supply nunca trava o jogo e exibe a fanfarra de moedas; soldado custa 1, mago 4, gigante 12 |
| 7 | 23/06 | Arena de boss + `CombatSystem` v1 (DPS automático por proximidade) + `BossManager` com Golem de Pedra placeholder, barra de vida gigante, ataque especial telegrafado | Loop completo corrida → boss → vitória/derrota funciona sem reiniciar o app |
| 8 | 24/06 | **Boss Scout**: cartão de ~2 s pré-fase com elemento e fraqueza + lembrete de 1 s tocando o ícone na barra de progresso + chart elemental dos 4 elementos do MVP (Fogo > Gelo > Raio > Fogo; Veneno DoT 3%/s por 4 s) | Atacar a fraqueza aplica +50% de dano, mesmo elemento aplica −50%; lembrete não pausa o jogo |
| 9 | 25/06 | Fases 1–5 configuradas via `LevelConfigSO` seguindo CANON §16 (fase 1 impossível perder; fase 2 = primeira escolha quantidade vs qualidade; fase 3 = Golem de Pedra com entrada cinematográfica ≤ 2 s) | Jogador novo vence a fase 1 em < 60 s desde a abertura do app; "tap-to-play" em ≤ 5 s |
| 10 | 26/06 | Telas Vitória/Derrota + `EconomySystem`: 100 moedas na fase 1, curva ×1,10^(fase−1), XP, motivo da derrota na tela | **Marco M1 — Core loop fechado.** Sessão de 15 min encadeia 5+ fases sem bug; review de go/no-go da semana 1 |

### 2.2.1 Trilha paralela da semana 1 (não bloqueia o core dos dias 1–10)

Tarefas de processo, decisão e pipeline que rodam em paralelo ao dia a dia — quase todas nascem do estudo de referências (ver `15-referencias-e-recursos.md`):

| Dia-alvo | Tarefa | Dono | Critério / observação |
|---|---|---|---|
| D1 | **Planilha de assets** (nome/URL/licença/autor) + **`THIRD-PARTY-NOTICES.md`** + checklist anti-cópia no template de PR | PM | Toda aquisição registrada desde o 1º download; **nenhum arquivo de `_research\` entra no build** (ver 15-referencias-e-recursos.md §7) |
| D1 | **Iniciar verificação de negócio da Meta** | PM | O processo demora dias e o adapter Meta só entra na S3 — por isso a verificação começa já na semana 1 (item próprio do cronograma) |
| D1–D2 | **MAR Tools** (janela de editor: setup de fase, limpar save, cheats de moedas) + painel de tuning em runtime no device | Eng | ~1 dia que acelera a montagem das 20 fases e o tuning das semanas 2–4 (padrões do estudo — ver 15-referencias-e-recursos.md §3.5) |
| D2–D3 | **Greybox com Unity Runner Template (Hub)**: validar pacing da corrida de 45–75 s e da fase 1 antes da arte própria | GD | Só greybox/referência; gera item explícito de remoção no QA da semana 4 — **nenhuma arte/cena do template pode chegar à build de soft launch** |
| D3–D5 | **Profiling do Unity Toon Shader** em celular mediano com a multidão cheia (Supply 60) | Eng/Arte | **Decisão com data marcada: D5 (21/06).** Aprovou → segue; reprovou → plano B URP_Toon (MIT) nas unidades pequenas (ver 15-referencias-e-recursos.md §4) |
| D5 | **Decisão de ícones**: Game-icons.net (CC BY 3.0, crédito obrigatório) vs pack Game Icons da Kenney (CC0) | PM | Se Game-icons: abrir os itens "tela de Créditos em Configurações" e "crédito na descrição da Play Store" (épico E11) |

### 2.3 Dias 11–30 — semana a semana

**Semana 2 (D11–D14) — Meta-jogo mínimo.** Entregas:

- As 5 tropas do MVP com stats coerentes com o baseline (Soldado HP 10 · DPS 2 · 5 m/s; demais escalam pelo Supply +10–20% de prêmio por raridade): Soldado, Arqueiro, Escudeiro, Mago, Gigante — modelos do **KayKit Adventurers (CC0)** integrados e recoloridos (Barbarian/Ranger/Knight/Mage + Gigante escalado) e animações via **retarget Humanoid da Universal Animation Library (CC0)**: locomoção, combate e mortes sem Mixamo no MVP (ver 15-referencias-e-recursos.md §5).
- Tela **Tropas** com cartas simples, raridade, nível, fragmentos (custo 10 × 2^(n−1)) e comparação de atributos.
- 4 trilhas de upgrade (Dano inicial, Vida inicial, Exército inicial, Multiplicador de recompensa) a +5%/nível, custo 100 × 1,35^n, primeira compra a 100 moedas; tela **Upgrades**.
- Tela **Inicial** (logo, Jogar, moedas, gemas, progresso) + tela **Loja** v1 (baús por moedas/gemas, 10 skins; slots de IAP existem mas ficam desligados por Remote Config até o soft launch).
- Desbloqueios por XP: nv2 Upgrades, nv3 Baús, nv4 Loja completa.
- **Ads S2:** AppLovin MAX (SDK Key) integrado em branch própria + rewarded "dobrar recompensa" funcionando + Mediation Debugger validado no device (ordem S1→S4 — ver 15-referencias-e-recursos.md §6.2).
- **Critério de saída da semana 2:** jogador ganha moedas, compra upgrade, sente a fase seguinte mais fácil; fragmentos sobem o nível de uma tropa.

**Semana 3 (D15–D21) — Conteúdo completo do MVP.** Entregas:

- 20 fases configuradas: M1 Campo Inicial (1–7), M2 Cidade Zumbi (8–14), M3 Deserto Robótico (15–20), com taxa de vitória alvo do CANON §12 validada por playtest (95% fases 1–3; ~55% nas fases de boss de mundo).
- 5 bosses do MVP: Golem de Pedra, **Gigante de Madeira** (fase 7, fraco a Fogo, + baú grande), Brutamontes Zumbi, **Zumbi Titã** (fase 14, fraco a Fogo, imune a Veneno), **Robô Escorpião** (fase 20, fraco a Raio, imune a Veneno) — cada um com ataque especial telegrafado e recompensa especial; boss de mundo dá 10 gemas; fase 10 dá baú épico + 50 gemas.
- 10 skins do Soldado = configuração de materiais/attachments com os 25+ acessórios do KayKit (CC0) — sem arte nova; arte dos 3 mundos = integração/recolor dos kits Quaternius (Nature M1, Zombie Apocalypse M2, Sci-Fi M3 — CC0) com paleta própria; bosses = Quaternius Ultimate Monsters (CC0) com escala/recolor e silhueta própria; VFX de multiplicação/portal/moedas = Kenney Particle Pack (CC0) + Unity Particle Pack retexturizado (ver 15-referencias-e-recursos.md §5).
- Áudio v1 = **curadoria, não produção**: Kenney Interface/Impact Sounds (CC0) + Freesound filtro CC0, com normalização em −16 LUFS no AudioManager; 2 músicas (run + boss). Música temática por mundo fica pós-semana-3, via OpenGameArt filtro CC0 (ver 15-referencias-e-recursos.md §5). Game feel pass: vibração leve, hit impact, câmera aproximando no boss, slow motion no golpe final.
- **Ads S3:** rewarded "reviver no boss (1×/fase)" + interstitial com as regras canônicas (fase ≥ 6, máx. 1 a cada 3, nunca após 2 derrotas, 100% Remote Config) + adapters AdMob e Unity Ads; o adapter Meta entra assim que a verificação de negócio iniciada na semana 1 concluir (§2.2.1).
- **Marco M2 (D21):** "content complete" — alguém de fora do time joga as 20 fases inteiras e responde ao questionário de validação (§2.5).

**Semana 4 (D22–D28) — Integrações, QA e build.** Entregas:

- Firebase (integrado desde a S1 — §2.2): validação evento a evento no DebugView de todos os eventos obrigatórios do BRIEF (de `tutorial_start` a `interstitial_shown`), Remote Config básico calibrado (dificuldade, moedas/fase, frequência de ads); sync Firestore do save local-first.
- **Ads/IAP S4** (MAX rodando desde a S2/S3): fluxo de consent UMP/TCF antes do init, `app-ads.txt` publicado no domínio, política de interstitial 100% via Remote Config; **RevenueCat mínimo** — entitlement `remove_ads` (US$ 4,99 + 200 gemas) + restore purchases, com slots de loja desligados por Remote Config até o soft launch (ver 15-referencias-e-recursos.md §6.2).
- Otimização: 60 fps no device de referência, download inicial ≤ 60 MB (alvo do doc 12 §2.5, ligado ao CPI ≤ US$ 0,40), tempo de boot ≤ 5 s até o "Jogar".
- QA estruturado: matriz de 10 devices, passe completo de regressão, correção de bloqueantes; **verificação de remoção do Runner Template** (nenhuma arte/cena do greybox da semana 1 na build) + auditoria da planilha de assets e do `THIRD-PARTY-NOTICES.md` (nenhum arquivo de `_research\` no build — checklist do PR, ver 15-referencias-e-recursos.md §7).
- **Marco M3 (D28):** build candidata submetida à faixa fechada da Play Store (submeter cedo absorve o tempo de review).

**Buffer (D29–D30):** correções da review interna, ajuste fino de pacing das fases 1–3, decisão go/no-go do soft launch. **Marco M4 (D30): MVP pronto.**

### 2.4 O que explicitamente FICA DE FORA do MVP

| Cortado do MVP | Onde entra |
|---|---|
| Mundos 4–10 e fases 8–10 dos mundos 1–3 | F5 / expansão (§4) |
| Elementos Luz, Sombra, Metal, Alien | Expansão item 3 |
| 14 tropas restantes (raras/épicas/lendárias além das 5 do MVP) | Expansão item 4 |
| **Sistema de mutações e portais de mutação** (asas, laser, armadura...) | Primeiro update do soft launch (§4, item 1) — é diferencial, mas o core valida sem ele |
| Trilha de upgrade de Supply (limite fica fixo em 60) | Expansão item 2 |
| Trilhas de upgrade 5–8 (Velocidade, Crítica, Dano vs boss, Resistência) | F3 |
| Telas Mapa e Eventos (progresso aparece como faixa linear na tela inicial) | Expansão itens 7 e 5 |
| Passe de Temporada, ranking, eventos diário/semanal | Expansão itens 5, 6, 8 |
| IAP completo (pacotes de gemas, baús premium); só os slots desativados existem | Ligado em F2/F3 via Remote Config + RevenueCat |
| FCM (push), iOS, localização além de PT/EN | F4 |
| Rewarded "baú extra diário", "testar lendária", "acelerar upgrade" | F3 (precisa de baús/lendárias/timer maduros) |
| Animações extra do Mixamo (dança de vitória, hit reactions) — a Universal Animation Library (CC0) eliminou o Mixamo do MVP | Pós-MVP, com a regra "FBX fora de repo público" (ver 15-referencias-e-recursos.md §5) |
| Música temática por mundo (loops M1–M3) | Pós-semana-3 / polish de F2, via OpenGameArt filtro CC0 |

### 2.5 Definição de Pronto (DoD) do MVP

O MVP está pronto quando **(a)** o escopo travado do CANON §15 está 100% implementado — 20 fases, 5 tropas, 8 portais, 5 bosses, 4 trilhas, moedas+XP+fragmentos, cartas simples, 10 skins, 7 telas, rewarded (dobrar+reviver), analytics básico, Remote Config básico, Boss Scout, Supply fixo 60 — **(b)** os objetivos de validação do BRIEF têm instrumento de medição pronto — e **(c)** o compliance de terceiros está fechado: planilha de assets completa, `THIRD-PARTY-NOTICES.md` atualizado, crédito CC-BY publicado (se Game-icons.net for a escolha da semana 1) e zero arquivos de `_research\` ou do Runner Template no build (ver 15-referencias-e-recursos.md §7):

| Objetivo de validação (BRIEF) | Instrumento de medição |
|---|---|
| Entendimento em < 3 s | Teste de 5 segundos com 10 pessoas fora do time: ≥ 8 descrevem o objetivo corretamente |
| Corrida satisfatória / multiplicação viciante | Questionário pós-playtest (escala 1–5): média ≥ 4 |
| Portais dão vontade de escolher | Evento `gate_selected` vs `gate_missed`: ≥ 90% dos pares têm escolha ativa |
| Boss gera tensão | `boss_failed`/`boss_start` entre 5% e 45% conforme curva do CANON §12 |
| Derrota dá vontade de tentar de novo | Retry imediato após `level_fail` ≥ 70% no playtest |
| Vitória recompensa bem | Conversão do rewarded "dobrar" ≥ 35% das vitórias |
| Usuário encadeia fases | ≥ 6 fases/sessão no teste fechado |
| Gera bons vídeos de anúncio | 5 criativos gravados direto da build, aprovados pelo time de UA |
| Rewarded converte bem | `rewarded_ad_completed`/`rewarded_ad_shown` ≥ 90% |
| Build estável | Crash-free ≥ 99% em 10 devices |

---

## 3. BACKLOG PRIORIZADO (entregável 20)

**Prioridades:** P0 = obrigatório no MVP · P1 = soft launch (F2/F3) · P2 = global/live ops (F4/F5).
**Estimativas:** S ≤ 1 dia-pessoa · M = 2–3 dias · L = 4–7 dias. Estimativas assumem o time do §5.

> **Economia de backlog (estudo de referências — ver 15-referencias-e-recursos.md):** itens antes estimados como produção/construção do zero viraram **integração/adaptação** — modelos 3D do MVP saem de packs CC0 (KayKit/Quaternius); animações via retarget da Universal Animation Library (CC0); UI/SFX/VFX viram curadoria + recolor; pooling usa `UnityEngine.Pool` nativo; event bus = Signals (MIT, ~200 linhas absorvidas com notice); SaveSystem nasce do SaveGameFree (MIT); Firebase usa os quickstarts oficiais (Apache 2.0) como referência. As estimativas abaixo já refletem esses cortes.

### Épico E1 — Core run & crowd (código)

| História | Prioridade | Est. |
|---|---|---|
| Movimento por swipe + câmera follow portrait | P0 | M |
| `CrowdManager`: formação, spawn, merge visual, contador HUD | P0 | L |
| GPU instancing + LOD para 200+ unidades a 60 fps | P0 | M |
| Profiling do Unity Toon Shader com multidão cheia (Supply 60) em device mediano — decisão marcada para D5 (21/06); plano B: URP_Toon (MIT) nas unidades pequenas (ver 15-referencias-e-recursos.md §4) | P0 | S |
| Infra absorvida: pooling via `UnityEngine.Pool` nativo + event bus Signals (MIT, ~200 linhas com notice) — adaptação, não construção do zero | P0 | S |
| Obstáculos e armadilhas com perda de unidades (desmonte em peças) | P0 | M |
| Supply 60 fixo + conversão de excedente em moedas com fanfarra | P0 | S |
| Trilha de upgrade de Supply (60 → 300) | P1 | S |
| Obstáculos temáticos por mundo (carros M2, serras M3, gelo M6...) | P1/P2 | M por mundo |

### Épico E2 — Portais & decisão (código + design)

| História | Prioridade | Est. |
|---|---|---|
| `GateManager` + 8 portais canônicos em pares honestos com % visível | P0 | L |
| **Boss Scout**: cartão pré-fase + lembrete in-run + geração de portais boss-aware (1 rota ótima, 1 armadilha aparente) | P0 | M |
| Chart elemental 4 elementos (+50%/−50%, lentidão do Gelo, cadeia do Raio, DoT do Veneno) | P0 | M |
| Unit tests dos 8 portais: função pura `int → int` (x2 → 2n; +10 → n+10; ÷2 → ⌈n/2⌉, incl. regra de arredondamento com contagens ímpares — doc 04) — tarefa das semanas 1–2 | P0 | S |
| Portais de mutação (3 slots, 4ª substitui a mais antiga, visível no modelo) | P1 | L |
| Portais matemáticos restantes (+50, x5, −10) e de risco avançado (sacrificar metade por 1 lendária etc.) | P1 | M |
| Portais de classe avançados (robô, ninja, dragão pequeno...) | P1/P2 | M |
| Elementos Luz/Sombra/Metal/Alien no chart + portais correspondentes | P2 | L |

### Épico E3 — Combate & bosses (código + design)

| História | Prioridade | Est. |
|---|---|---|
| `CombatSystem` v1: DPS automático, alcance por tropa, foco no boss | P0 | L |
| `BossManager`: entrada ≤ 2 s, barra gigante, ataque telegrafado, fraqueza no HUD | P0 | M |
| 5 bosses do MVP configurados via `BossConfigSO` | P0 | M |
| Slow motion no golpe final + explosão de recompensa | P0 | S |
| Reviver no boss via rewarded (1×/fase) | P0 | S |
| Arquétipos regionais (3/mundo) com escala de tamanho/vida/cor | P1/P2 | M por mundo |
| Bosses únicos M4–M10 (mecânicas especiais: fraqueza rotativa do Alien Supremo, portais invertidos da Entidade Dimensional) | P2 | L cada |

### Épico E4 — Economia & meta (código + design)

| História | Prioridade | Est. |
|---|---|---|
| `EconomySystem`: moedas (100 base, ×1,10^(fase−1)), XP, gemas, fragmentos | P0 | M |
| 4 trilhas de upgrade (+5%/nível; custo 100 × 1,35^n) | P0 | M |
| Cartas simples + evolução por fragmentos (10 × 2^(n−1), nível máx. 10) | P0 | M |
| Desbloqueio por nível de jogador (nv2–nv4) | P0 | S |
| Baús com tabela de drop (inclui lendárias grátis — anti pay-to-win) | P1 | M |
| Trilhas de upgrade 5–8 | P1 | S |
| Missões diárias (20–40 gemas/dia) | P1 | M |
| Desbloqueios nv5 (Passe) e nv6 (Eventos) | P2 | S |

### Épico E5 — UI/UX & telas (código + arte)

| História | Prioridade | Est. |
|---|---|---|
| Telas do MVP (Inicial, Gameplay/Boss, Vitória, Derrota, Tropas, Upgrades, Loja) — widgets/9-slices do Kenney UI Pack (CC0) recoloridos nos tons de raridade, sem produção de widgets (ver 15-referencias-e-recursos.md §5) | P0 | M |
| UIManager de duas pilhas (pilha de telas com history + pilha de overlays), transições slide 200 ms / fade 150 ms | P0 | M |
| ResultSequencePlayer (~2 s, pulável) + coreografia de recompensa (contagem rolando, voo de moedas, confete — sem congelar timeScale) | P0 | S |
| Feedbacks textuais (NICE → GODLIKE, MUTATION, MEGA ARMY, BOSS BREAKER) | P0 | S |
| FTUE sem tutorial longo: jogar em ≤ 5 s, fase 1 autoexplicativa | P0 | M |
| Tela Mapa (mundos, bloqueio/desbloqueio, recompensas por mundo) | P2 | L |
| Tela Eventos (diário, semanal, ranking) | P2 | L |
| Localização ES/EN completa (PT nativo) | P1 | S |

### Épico E6 — Arte & conteúdo

| História | Prioridade | Est. |
|---|---|---|
| 3 mundos do MVP: integração/recolor dos kits Quaternius — Nature (M1), Zombie Apocalypse (M2), Sci-Fi (M3), todos CC0 — montagem de cenário com paleta própria, **sem modelagem** (ver 15-referencias-e-recursos.md §5) | P0 | M |
| 5 tropas: integração do KayKit Adventurers (Barbarian/Ranger/Knight/Mage + Gigante escalado 2,5×, CC0) — recolor/escala/materiais + estados visuais de elemento (Fogo), **sem modelagem** | P0 | S |
| Animações das 5 tropas: retarget Humanoid da Universal Animation Library (CC0) — locomoção, combate e mortes; elimina o Mixamo do MVP | P0 | S |
| Animações extra (dança de vitória, hit reactions) via Mixamo — pós-MVP; regra: FBX fora de repo público (ver 15-referencias-e-recursos.md §1) | P1 | S |
| 5 bosses do MVP: seleção + escala/recolor no Quaternius Ultimate Monsters (CC0), silhueta legível e materiais próprios — integração, não modelagem | P0 | M |
| VFX: curadoria + recolor do Kenney Particle Pack (CC0) e retexturização do Unity Particle Pack URP (para não parecer asset-flip) — portais brilhantes, multiplicação, moedas, hit impact | P0 | M |
| 10 skins do Soldado = configuração de materiais/attachments com os 25+ acessórios do KayKit (CC0) — sem arte nova | P0 | S |
| Visual de mutações empilháveis (asas + laser + armadura no mesmo modelo) | P1 | L |
| 14 tropas restantes com visual evoluído por nível | P1/P2 | L |
| Mundos 4–10 (ambiente + 3 arquétipos de boss cada) | P2 | L por mundo |
| Skins avançadas (corpo inteiro, temáticas de temporada) | P2 | M por lote |

### Épico E7 — Áudio & game feel

| História | Prioridade | Est. |
|---|---|---|
| SFX core (portal, multiplicação, moedas, hit, fanfarra): curadoria Kenney Interface/Impact Sounds (CC0) + Freesound filtro CC0 + normalização em −16 LUFS no AudioManager — curadoria, não produção (ver 15-referencias-e-recursos.md §5) | P0 | S |
| 2 trilhas musicais (run + boss) | P0 | S |
| Vibração leve (haptics) em portal/boss/vitória | P0 | S |
| Áudio temático por mundo (OpenGameArt, filtro CC0 obrigatório — pós-semana-3) + stingers de boss | P1/P2 | M |

### Épico E8 — Integrações

| História | Prioridade | Est. |
|---|---|---|
| Firebase Auth anônimo + Crashlytics — S1, com EDM4U único + minSdk 23; quickstarts oficiais (Apache 2.0) como referência de integração | P0 | S |
| Save local-first (JSON + checksum + schemaVersion) + sync Firestore — base SaveGameFree (MIT): adaptação, não construção do zero (ver 15-referencias-e-recursos.md §4) | P0 | M |
| Remote Config básico (dificuldade, moedas, frequência de ads) — defaults locais desde a S1 | P0 | S |
| AppLovin MAX (decisão formal: **mantido**, CANON §13 confirmado; LevelPlay documentado como plano B — ver 15-referencias-e-recursos.md §6.1): S2 = SDK + rewarded dobrar + Mediation Debugger; S3 = reviver + interstitial com regras canônicas + adapters AdMob/Unity Ads | P0 | M |
| Verificação de negócio da Meta: **iniciar na semana 1** (demora dias); adapter Meta entra na S3 | P0 | S |
| Consent UMP/TCF antes do init + `app-ads.txt` publicado no domínio (S4) | P0 | S |
| RevenueCat mínimo (S4): entitlement `remove_ads` US$ 4,99 (+200 gemas) + restore purchases; slots de loja desligados por Remote Config até o soft launch | P0 | S |
| RevenueCat completo: Oferta inicial US$ 2,99 (48 h), pacotes de gemas, loja IAP completa | P1 | M |
| Passe de Temporada US$ 6,99/mês (infra de temporada + trilha de recompensas) | P2 | L |
| FCM (push de retorno: baú pronto, evento novo) | P2 | M |
| Cloud Functions (validação de recibo, ranking server-side) | P2 | M |

### Épico E9 — Analytics & data

| História | Prioridade | Est. |
|---|---|---|
| Todos os eventos obrigatórios do BRIEF instrumentados e validados no DebugView | P0 | M |
| Dashboard de funil (D1/D3/D7, fases/sessão, taxa de vitória por fase, portal mais escolhido, boss mais difícil) | P1 | M |
| A/B testing via Remote Config (recompensa, interstitial, curva de custo) | P1 | M |
| Eventos de monetização avançada (LTV, conversão por oferta) + `season_pass_opened/purchased` | P2 | S |

### Épico E10 — Live ops & social

| História | Prioridade | Est. |
|---|---|---|
| Evento diário (desafio com modificador + recompensa) | P2 | L |
| Evento semanal (fase especial com boss exclusivo) | P2 | L |
| Ranking (liga semanal por troféus de fase) | P2 | L |
| Ferramenta interna de calendário de eventos via Remote Config | P2 | M |

### Épico E11 — Tooling, compliance & pipeline de assets

| História | Prioridade | Est. |
|---|---|---|
| **MAR Tools** (janela de editor: setup de fase, limpar save, cheats de moedas) + painel de tuning em runtime no device — início da semana 1, ~1 dia que acelera as 20 fases e o tuning das semanas 2–4 (ver 15-referencias-e-recursos.md §3.5) | P0 | S |
| Planilha de assets (nome/URL/licença/autor), preenchida a cada aquisição — semana 1 | P0 | S |
| `THIRD-PARTY-NOTICES.md` + checklist anti-cópia no template de PR (nenhum arquivo de `_research\` entra no build — ver 15-referencias-e-recursos.md §7) — semana 1 | P0 | S |
| Decisão de ícones (D5): Game-icons.net (CC BY 3.0, crédito obrigatório) vs pack Game Icons da Kenney (CC0) | P0 | S |
| Tela de Créditos em Configurações + crédito na descrição da Play Store — **condicional**: só se a escolha for Game-icons.net (CC-BY) | P0 (condicional) | S |
| Greybox com Unity Runner Template (Hub) para validar pacing da corrida de 45–75 s e da fase 1 antes da arte própria — semana 1 | P0 | S |
| Remoção do template: **nenhuma arte/cena do Runner Template pode chegar à build de soft launch** (verificação no QA da semana 4) | P0 | S |

---

## 4. EXPANSÃO PÓS-MVP (entregável 22)

Ordem de entrega com **critério de entrada** explícito — nenhum item começa sem o gate batido. A regra geral: *retenção financia conteúdo; monetização financia meta-sistemas.*

| # | Entrega | Quando | Critério de entrada (gate) | Justificativa |
|---|---|---|---|---|
| 1 | **Mutações v1** (3 slots, visíveis, portais de mutação) | Update 1 do soft launch (F2, semana 2) | MVP no ar com crash-free ≥ 99,5% | É o diferencial visual nº 1 para criativos; entra cedo para medir impacto em CPI |
| 2 | Trilha de Supply (60→300) + trilhas de upgrade 5–8 + baús completos + missões diárias | F3 | D1 ≥ 38% (perto do alvo — profundidade de meta é o que converte D1 em D7) | Dá "motivo para mais uma fase" aos retidos |
| 3 | **4 elementos avançados** (Luz, Sombra, Metal, Alien) + portais correspondentes | F3 → F4 | D7 ≥ 10% e taxa de uso do Boss Scout ≥ 60% das fases | Só aprofunda o sistema elemental se o jogador provou que usa o que já existe |
| 4 | **Tropas épicas/lendárias restantes** (Robô, Necromante, Engenheiro, Alien, Dragão, Titã, Anjo de Guerra, Demônio Mutante, Mecha Supremo + raras que faltam) | F3 → F5, 2–3 tropas por mês | Sistema de baús e fragmentos estável; conversão rewarded ≥ 35% | Lendárias alimentam o rewarded "testar tropa lendária" e os baús — precisa da economia madura |
| 5 | **Eventos diário/semanal** + tela Eventos | F4 | **D7 ≥ 12%** e DAU ≥ 10.000 | Evento sem base ativa é custo morto; é conteúdo para quem já retém |
| 6 | **Ranking** (liga semanal) | F4/F5 | DAU ≥ 20.000 (ligas precisam de liquidez de jogadores) | Ranking vazio é pior que nenhum ranking |
| 7 | **Tela Mapa** (mundos, bosses, recompensas, bloqueio/desbloqueio) | F4 (pré-global) | ≥ 5 mundos disponíveis no jogo | Mapa só faz sentido quando há geografia para mostrar |
| 8 | **Passe de Temporada** (US$ 6,99/mês: tropa + boss + skin + recompensas diárias + baús premium) | F4/F5 | **D7 ≥ 12% e ARPDAU ≥ US$ 0,06** por 2 semanas + infra de temporada testada | Só constrói passe se a base retém e já paga; passe para base fraca destrói LTV de ads |
| 9 | **Mundos 4–10** + expansão dos mundos 1–3 para 10 fases cada (rumo às 100 fases) | F5, 1 mundo/mês | % de jogadores que terminam o último mundo ≥ 15% do MAU | Produz conteúdo na velocidade em que é consumido, não antes |
| 10 | **Skins avançadas** (corpo inteiro, temáticas, integradas ao passe e à loja) | F5 | Passe no ar com conversão ≥ 2% dos DAU | Skins premium vendem dentro do ecossistema do passe |
| 11 | **FCM** (push de valor concreto: baú pronto, evento ativo, missão nova — sem culpa, doc 14 §3.5: "suas tropas sentem sua falta" é proibido) | F4 (obrigatório pré-global) | Opt-in flow desenhado; eventos diários no ar (push precisa de motivo real) | Push sem conteúdo novo = desinstalação |
| 12 | Port iOS + localização adicional (ES/EN polido, depois SEA) | F4 → F5 | Global Android estável por 4 semanas; ARPDAU ≥ US$ 0,08 | iOS dobra o custo de QA; só com unit economics provada |

**Regra de corte da expansão:** a cada gate não batido, o item desce na fila e o esforço vai para a alavanca da métrica que falhou (retenção → conteúdo/FTUE; monetização → ofertas/rewarded; CPI → criativos).

---

## 5. Equipe mínima recomendada

### 5.1 Composição (3 pessoas + assets de loja; 4ª entra no soft launch)

| Papel | Dedicação | Responsabilidades |
|---|---|---|
| **Engenheiro Unity sênior** | Full-time | Todo o código (managers do CANON §13), integrações (Firebase, MAX, RevenueCat), otimização, CI/CD |
| **Game designer / PM híbrido** | Full-time | Configs de fases e portais (ScriptableObjects), balanceamento e economia, analytics, ASO, roteiro de criativos, gestão do backlog |
| **Artista generalista (3D/2D/VFX)** | Full-time (pode ser 60% nas semanas 1–2) | Integração/recolor dos packs CC0 (KayKit, Quaternius, Kenney — ver 15-referencias-e-recursos.md §5), retarget de animações (UAL), silhueta/materiais dos bosses, portais, UI, VFX, skins — **sem modelagem 3D no MVP** |
| **UA / criativos (4ª pessoa)** | A partir de F2, meio período ou agência | Produção dos 8 formatos de anúncio, compra de mídia, iteração de CPI |
| **Áudio** | Freelancer pontual (2–3 dias) | Curadoria de pacotes + stingers exclusivos de boss |

### 5.2 Orçamento de assets (revisado após o estudo de referências)

O estudo do doc 15 substituiu as compras planejadas por fontes CC0/gratuitas — o orçamento de ≈ US$ 500 vira reserva de contingência:

| Item | Fonte (ver 15-referencias-e-recursos.md §5) | Custo |
|---|---|---|
| 5 tropas + 10 skins (acessórios) | KayKit Adventurers (CC0) | US$ 0 (era US$ 120) |
| 3 ambientes (M1/M2/M3) | Kits Quaternius Nature / Zombie / Sci-Fi (CC0) | US$ 0 (era US$ 180) |
| 5 bosses | Quaternius Ultimate Monsters (CC0) | US$ 0 |
| Animações das tropas | Universal Animation Library (CC0) | US$ 0 |
| UI + ícones | Kenney UI Pack + Input Prompts (CC0); ícones Game-icons.net (CC-BY) ou Kenney (CC0) — decisão D5 | US$ 0 (era US$ 60) |
| SFX + músicas | Kenney Interface/Impact (CC0) + Freesound CC0 + OpenGameArt CC0 | US$ 0 (era US$ 80) |
| VFX | Kenney Particle Pack (CC0) + Unity Particle Pack (Asset Store, gratuito) | US$ 0 (era US$ 60) |
| Reserva de contingência (props pontuais via Poly Pizza CC0, SFX premium Mixkit, fontes) | Licença confirmada item a item + planilha de assets | US$ 100 |
| **Total** | | **≈ US$ 100 (era ≈ US$ 500)** |

**Justificativa:** com 3 pessoas o MVP de 30 dias fecha porque ~70% da arte vem de packs CC0 integrados e recoloridos (zero modelagem 3D no MVP) e 100% do design já está especificado neste pacote de GDD. Menos que isso (2 pessoas) exige +10 dias no MVP ou cortar M3 de antemão.

## 6. Riscos de cronograma e planos B

| # | Risco | Prob. | Impacto | Plano B (acionável) |
|---|---|---|---|---|
| 1 | **Semana 3 atrasa** (conteúdo das 20 fases não fecha até D21) | Média | Alto | **Cortar o mundo 3 do MVP**: lançar com 14 fases (M1 1–7 + M2 8–14), Zumbi Titã vira boss final; Robô Escorpião e Deserto Robótico viram Update 1. O DoD continua válido — nenhum objetivo de validação depende do M3 |
| 2 | Performance da multidão em devices fracos (< 60 fps) | Média | Alto | Cap visual de 150 unidades renderizadas + contador numérico segue real ("327 unidades", renderiza amostra); reduzir sombras via Quality tier por Remote Config; se o gargalo for o Unity Toon Shader, cair para URP_Toon (MIT) nas unidades pequenas — profiling e decisão já agendados para D5 (§2.2.1) |
| 3 | SDKs (MAX/Firebase) quebram a build | Baixa | Médio | Risco diluído pela integração faseada S1→S4 (§2.1; ver 15-referencias-e-recursos.md §6.2): Firebase na S1 e MAX na S2 dão 2–3 semanas de estabilização antes do code-freeze; versões pinadas, EDM4U único, branch isolada por SDK; fallback: lançar F2 só com AdMob via MAX (1 rede) e adicionar Meta/Unity Ads em F3 |
| 4 | CPI acima do alvo no soft launch (> US$ 0,40 BR) | Média | Alto | Banco de 8 formatos de criativo (doc de ads); pausar mídia, iterar hook em lotes de 3 criativos/semana; se CPI > 0,70 após F3 → revisão de conceito (regra §1.3) |
| 5 | D1 < 35% no teste fechado | Baixa | Crítico | Sprint exclusivo de FTUE: refazer pacing das fases 1–3, cortar qualquer fricção pré-"Jogar", reforçar fanfarra de vitória; nada de feature nova até D1 ≥ 38% |
| 6 | Bus factor = 1 engenheiro (doença/saída) | Baixa | Crítico | Documentação viva no repo (este pacote + ADRs), code review semanal pelo designer técnico, buffer D29–30; contrato de freelancer Unity de prontidão a partir da semana 2 |
| 7 | Review da Play Store atrasa o soft launch | Média | Baixo | Submeter a build candidata em D28 (M3) com features desligadas por Remote Config; updates seguintes são apenas remote |
| 8 | Integração de arte (bosses/mundos) atrasa | Baixa | Médio | Bosses já saem prontos do Quaternius Ultimate Monsters (CC0) com escala/recolor + materiais/silhueta próprios; mundos são kits Quaternius e skins são materiais/attachments do KayKit — modelagem saiu do caminho crítico (ver 15-referencias-e-recursos.md §5) |
| 9 | Balanceamento ruim (taxa de vitória fora da curva do CANON §12) | Média | Médio | Todos os números (vida de boss, recompensa, custo) em Remote Config desde o D22 — rebalancear sem build nova; telemetria por fase desde o primeiro dia do teste |
| 10 | Escopo cresce ("só mais um portal...") | Alta | Médio | CANON §15 é contrato: qualquer adição exige remoção equivalente e aprovação do PM; mutações são o exemplo-guia — diferencial enorme e mesmo assim ficou para o Update 1 |
| 11 | Verificação de negócio da Meta não conclui até a S3 | Média | Baixo | Processo iniciado na semana 1 (§2.2.1) justamente porque demora dias; se atrasar, F2 sai com AdMob + Unity Ads via MAX e o adapter Meta entra em F3 — o bidding do MAX compensa o fill no curto prazo |
| 12 | Compliance de licença falha (asset CC-BY sem crédito; arquivo de `_research\` ou do Runner Template no build) | Baixa | Alto | Processo da semana 1 (E11): planilha de assets + `THIRD-PARTY-NOTICES.md` + checklist anti-cópia no template de PR; auditoria final no QA da semana 4; preferência por CC0 reduz a superfície de risco (ver 15-referencias-e-recursos.md §1 e §7) |

---

*Fim do documento 13. Próxima revisão: ao final da pré-produção (16/06/2026), com o backlog P0 importado para a ferramenta de gestão e os marcos M1–M4 agendados.*
