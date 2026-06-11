# Mutant Army Run — Visão & Conceito (GDD Master)

> **Documento 01 do pacote de design.** Cobre os entregáveis **1 (GDD master / visão)**, **5 (lista de sistemas)** e **25 (análise de nomes)** do BRIEF.md.
> Fonte da verdade para nomes, números e regras: **CANON.md** (v1.0, 2026-06-11). Em conflito de detalhe, o CANON prevalece.
> Versão 1.0 — 2026-06-11 · Time: game design, product, UX/UI, monetização, engenharia Unity.

---

## 1. Resumo executivo

### 1.1 Elevator pitch

**Mutant Army Run** é um runner de multidão hybrid-casual em que o jogador começa cada fase com 1 unidade e, em 45–75 segundos de corrida, atravessa portais pareados que multiplicam, transformam e mutam seu exército — para esmagar um boss gigante na arena final. A diferença para os clones do gênero: **cada portal é um micro-puzzle**, não um teste de "qual número é maior". O **Boss Scout** revela a fraqueza do boss antes da fase, o **Suprimento (Supply)** torna qualidade tão valiosa quanto quantidade, e as **mutações persistentes** transformam o exército visualmente a fase inteira.

- **Tagline principal:** "Monte o exército mais absurdo possível em 60 segundos."
- **Tagline secundária:** "Escolha os portais certos, crie mutações insanas e destrua bosses gigantes."
- **Resumo em uma frase de produto:** *parece simples por fora, é inteligente por dentro* — hypercasual na aquisição, midcore leve na retenção.

### 1.2 Fantasia do jogador

"Eu sou o estrategista de um exército que cresce na minha frente em tempo real. Cada escolha minha fica **visível**: meu exército dobra, ganha asas, cospe laser — e quando o boss gigante cai em câmera lenta, foi porque **eu li a fraqueza dele e planejei a rota certa**, não porque escolhi o número maior."

Três emoções-alvo, em ordem: **poder crescente** (multiplicação visual), **esperteza recompensada** (plano do Boss Scout funcionando), **espetáculo** (boss desmontando em peças e chuva de moedas).

### 1.3 Público-alvo

| Segmento | Perfil | Motivação principal | Ocasião de jogo |
|---|---|---|---|
| Núcleo | 13–24 anos, BR/LatAm/SEA, Android mediano | Espetáculo + competição leve ("INSANE", contador subindo) | Sessões de 5–10 min, várias por dia |
| Secundário | 25–40 anos, casual, US/BR | Progressão e coleção (tropas, upgrades, passe) | Commute, pausas, fim de noite |
| Espectador | Audiência de TikTok/Reels/Shorts | "Qual portal você escolheria?" | Não joga ainda — é o funil de UA/UGC |

Restrição canônica: **sem violência gráfica** — inimigos e bosses "desmontam" em peças e partículas, sem sangue. Classificação alvo: livre/E10.

### 1.4 Plataforma e formato

- **Android primeiro, iOS depois.** Unity 2022 LTS + URP, C#, orientação **retrato (9:16)**.
- Fase completa ≈ **60–90 s** (corrida 45–75 s + boss 10–20 s). Jogável em até **5 s após abrir o app**.
- 100 fases / 10 mundos no release; **MVP de 30 dias = 20 fases / 3 mundos** (CANON §15).
- Backend: Firebase (Auth anônimo, Firestore, Analytics, Remote Config, FCM, Crashlytics) · Ads via AppLovin MAX · IAP via RevenueCat.

### 1.5 Janela de mercado — por que multiplicação visual + camada estratégica é a aposta certa em 2026

1. **O hypercasual puro encolheu; o hybrid-casual ocupou o espaço.** Desde as mudanças de privacidade (ATT, Privacy Sandbox), o CPI subiu e o LTV só-de-anúncios deixou de fechar a conta. Os estúdios que sobreviveram migraram para hybrid-casual: aquisição barata de mecânica hypercasual + retenção e monetização de meta-progressão (IAP, passe, eventos). Nosso desenho segue exatamente essa fórmula: corrida de 60 s na frente, 3 camadas de progressão atrás (CANON §2, pilar 4).
2. **A mecânica de crowd-runner continua sendo das mais eficientes em criativo de UA** — o formato "escolha entre dois portais" gera CTR alto em TikTok/Reels/Shorts até hoje —, mas a oferta atual é de clones rasos onde a resposta certa é sempre o número maior. Isso **queimou a confiança do público** e derrubou D7 da categoria. A janela: ser o primeiro crowd-runner em que o espectador do anúncio *discorda* da escolha ("o x10 era armadilha, o boss era imune!") — curiosidade que converte instalação e sustenta retenção.
3. **Profundidade leve agora é viável tecnicamente a custo baixo.** Boss Scout, Supply e elementos não exigem conteúdo caro: são regras combinatórias sobre os mesmos assets, balanceáveis 100% por Remote Config — liveops de estúdio pequeno com cara de estúdio grande.
4. **Geografia a favor.** BR/LatAm/SEA têm CPI alvo ≤ US$ 0,40 (CANON §12), público Android dominante e apetite comprovado por runners de multidão. Lançar Android-first nessas regiões valida o funil antes do CPI ≤ US$ 1,50 dos EUA.
5. **Metas de produto que provam a tese** (soft launch, CANON §12): D1 ≥ 40%, D7 ≥ 12%, sessão ≥ 8 min, ≥ 6 fases/sessão, conversão rewarded ≥ 35% dos DAU, ARPDAU ≥ US$ 0,08. São números de hybrid-casual saudável, não de hypercasual descartável.

---

## 2. Conceito central e premissa narrativa

### 2.1 Conceito central

O jogador inicia cada fase com **1 unidade**. Durante a corrida, escolhe entre **portais pareados** (esquerda/direita) que multiplicam (+10, x3), transformam (virar Arqueiro), elementam (Fogo, Gelo, Raio, Veneno), mutam (asas, laser, armadura...) ou arriscam ("70% x10 / 30% perde metade") seu exército, enquanto desvia de obstáculos e inimigos. A fase termina **sempre** em uma arena com boss gigante. Vitória paga moedas, XP, cartas, fragmentos e baús, que alimentam upgrades permanentes — e a próxima fase. O core loop completo de 12 passos está detalhado em `02-core-loop-e-progressao.md`.

### 2.2 Premissa narrativa (leve, original, sem lore pesado)

Um laboratório dimensional testando **Fendas de Replicação** — portais que copiam e recombinam tudo que os atravessa — perdeu o controle do experimento, espalhando fendas pelo mundo e criando aberrações gigantes (os bosses). O jogador comanda o último Recruta do programa de contenção: a única forma de derrubar um gigante é atravessar as próprias fendas e **construir um exército mutante no caminho até ele**. Tom: humor físico e absurdo (o exército fica orgulhoso das próprias mutações), zero texto obrigatório além de nomes de bosses e mundos.

A premissa existe para **justificar mecânica, não para contar história**: explica por que há portais (fendas vazadas), por que mutações são desejáveis (recombinação é a arma) e por que cada mundo tem um boss temático (a aberração local do experimento).

---

## 3. Pilares de design — expandidos com decisões reais

Os 4 pilares do CANON §2, **em ordem de prioridade**, desempatam qualquer decisão de design, arte, monetização ou engenharia. Abaixo, cada pilar com 2 decisões reais que ele já resolve neste pacote.

### Pilar 1 — Legível em 3 segundos

Qualquer frame do jogo (ou de um anúncio) comunica: corra, escolha o portal, o exército cresce, o boss cai.

- **Decisão A (Espetáculo vs Legibilidade → vence P1):** o VFX de Raio encadeando dano entre 60 unidades cobriria visualmente os portais seguintes da pista. Regra resultante: máximo de **12 arcos elétricos visíveis simultâneos**; o restante do encadeamento é comunicado por flash no contador de dano. O espetáculo se adapta; a leitura dos portais, nunca.
- **Decisão B (Riqueza de informação vs Legibilidade → vence P1):** proposta de portais com texto descritivo de duas linhas ("transforma 50% do exército em..."). Rejeitada. Regra de UI canônica do pacote: **todo portal exibe no máximo 1 número + 1 ícone** (e porcentagens nos de risco). Se um efeito não cabe nisso, o efeito é redesenhado — não o portal.

### Pilar 2 — Escolha inteligente, não "maior número"

Boss Scout + elementos + Supply transformam cada par de portais num micro-puzzle com resposta dependente de contexto.

- **Decisão A (Monetização vs P2 → vence P2):** proposta de IAP "x2 permanente no exército inicial" tornaria o número maior sempre correto e mataria o micro-puzzle (além de ferir o anti pay-to-win do CANON §11). Convertida em: multiplicador de **recompensa** (moedas) e cosméticos — pagar acelera e personaliza, nunca resolve o puzzle pelo jogador.
- **Decisão B (Custo de produção vs P2 → vence P2):** level design propôs fases "baratas" só com portais matemáticos. Rejeitado como regra: **toda fase a partir da 2 contém ao menos 1 par com trade-off qualitativo** (quantidade vs classe, elemento ou mutação), e toda fase é gerada considerando o boss — sempre existe ≥1 rota ótima e ≥1 armadilha aparentemente boa (CANON §3.1). Detalhe em `04-sistema-de-portais.md`.

### Pilar 3 — Espetáculo constante

Multiplicação visual, mutações visíveis, números gigantes, slow motion.

- **Decisão A (Custo técnico vs P3 → vence P3):** engenharia sugeriu cortar o slow motion do golpe final no boss por complicações de `timeScale` com física. Mantido por pilar: é a cena mais compartilhável do jogo. Técnica única do pacote: **`timeScale` global 0,3× por 0,8 s**, com `Time.fixedDeltaTime` escalado na mesma proporção para a física acompanhar sem engasgo — especificada em `12-arquitetura-unity.md` (VFXManager).
- **Decisão B (Minimalismo de UI vs P3 → vence P3):** proposta de remover os feedbacks textuais (NICE, INSANE, MUTATION, BOSS BREAKER...) para "limpar a tela". Mantidos — são exigência de game feel do BRIEF e açúcar de vídeo. Com a ressalva do Pilar 1 (que é superior): **nunca aparecem sobre portais nem sobre a barra de progresso**; têm zona reservada no terço superior da tela.

### Pilar 4 — Progressão em 3 camadas

Dentro da fase (portais) · entre fases (upgrades/cartas) · entre sessões (mundos/passe/eventos).

- **Decisão A (Produção infinita vs P4 → vence P4):** proposta de fases procedurais infinitas (mais baratas que 100 fases autorais). Rejeitada na estrutura: mundos numerados com bosses únicos dão **senso de destino** ("falta 1 fase para o Robô Escorpião") que procedural puro não dá. Compromisso: trechos procedurais **dentro** de templates autorais por mundo (`06-sistema-de-fases-e-mundos.md`).
- **Decisão B (Retenção forçada vs P4 → vence P4 + CANON):** sugestão clássica de sistema de energia para "esticar" retenção. **Proibido pelo CANON §8** ("nunca travar o jogador que quer jogar"). A retenção entre sessões vem da camada 3 legítima: baús com timer opcional, missões diárias, passe de temporada e eventos — motivos para voltar, nunca muros para sair.

---

## 4. Diferenciais — em profundidade, com exemplo jogável

### 4.1 Boss Scout (inovação central)

**O que é:** antes de cada fase, um cartão de ~2 s mostra o boss, seu elemento e sua fraqueza ("BOSS DE GELO — fraco contra FOGO"). Durante a corrida, tocar no ícone do boss na barra de progresso reabre o lembrete por 1 s **sem pausar**. Consequência estrutural: os portais da fase são gerados levando o boss em conta.

**Por que diferencia:** transforma o jogo de reativo (escolher na hora) em **planejado** (escolher pensando 40 segundos à frente). É também a âncora dos criativos de UA: o espectador que sabe a fraqueza julga as escolhas do vídeo — engajamento de comentário garantido.

**Exemplo jogável — Fase 17, Deserto Robótico (M3):** o Boss Scout mostra "SENTINELA DE SUCATA — fraco contra RAIO, imune a VENENO". No meio da corrida aparece o par: **esquerda "Elemento Veneno"** (que foi excelente o mundo inteiro contra zumbis orgânicos do M2, +50%) vs **direita "Elemento Raio"**. O jogador desatento pega o Veneno por hábito — e chega ao boss máquina causando **0% de dano elemental** (CANON §4). O jogador que leu o Scout pega Raio: +50% contra o boss e encadeamento de 50% do dano nos drones da arena. Mesma fase, mesmos portais, resultado oposto — a armadilha é honesta e a vitória é mérito de leitura.

### 4.2 Suprimento (Supply) — o anti-"maior número"

**O que é:** o exército tem capacidade de Supply (inicial **60**, até **300** via meta). Cada unidade custa Supply (Soldado 1, Mago 4, Gigante 12...). Estourou o limite? O excedente vira moedas automaticamente, **com fanfarra visual** — nunca parece punição.

**Por que diferencia:** quebra matematicamente a estratégia dominante dos clones ("sempre x10"). Com teto de quantidade, qualidade, composição e elemento passam a decidir — exatamente o "x10 soldados fracos pode ser pior que +2 magos fortes" do BRIEF, só que com regra elegante e visível.

**Exemplo jogável — Fase 11, Cidade Zumbi (M2):** jogador com **52 Soldados** (52/60 de Supply) encara o par: **esquerda "x2"** vs **direita "+2 Magos"**. O x2 geraria 104 Soldados — mas o teto corta em 60 e converte 44 pontos de Supply excedentes em moedas (~88 moedas, na taxa canônica de 2 moedas por ponto excedente — chave `supply_overflow_coin_rate` em `07-economia-e-upgrades.md`): resultado, 60 Soldados de dano pontual. A direita dá 52 Soldados + 2 Magos (52 + 8 = 60/60 exatos): DPS bruto parecido, mas o **dano em área** dos Magos atinge 3–5 zumbis por ataque nas hordas do percurso. Contra os corredores lotados do M2, a direita vence; numa fase de obstáculos esparsos, o x2 + moedas venceria. **Não há resposta universal — há leitura de fase.**

### 4.3 Mutações persistentes e visíveis

**O que é:** mutações (asas, laser, armadura, tamanho...) aplicam-se ao **exército inteiro**, ficam **visíveis nos modelos** e duram a fase toda. Máximo de **3 slots**; a 4ª mutação substitui a mais antiga.

**Por que diferencia:** nos clones, buffs são números invisíveis. Aqui, 200 soldados com asas e laser são uma **imagem nova** — conteúdo de vídeo por si só ("humano virando dragão" do BRIEF) — e o limite de 3 slots cria a decisão tardia mais dramática do jogo.

**Exemplo jogável — Fase 14, boss do M2 (Zumbi Titã):** o jogador chega ao trecho final com slots [**Armadura** (1ª), **Asas** (2ª), **Laser** (3ª)]. Último portal antes da arena: **"Tamanho Gigante"**. Pegar substitui a Armadura — a mais antiga. O Boss Scout avisou que o Zumbi Titã tem ataque em área forte (a Armadura importa), mas Tamanho aumenta o dano do exército inteiro... e a barra de progresso mostra que não há mais obstáculos de chão (as Asas viraram luxo). Melhor jogada: **recusar o portal e manter a Armadura** — recusar também é escolha. É o frame perfeito do anúncio "não escolha esse portal".

### 4.4 Portais pareados e honestos

**O que é:** portais aparecem sempre em pares esquerda/direita com informação honesta — número, ícone de classe/elemento ou risco com porcentagem explícita ("70% x10 / 30% perde metade"). **Nunca enganamos o jogador**: a tensão vem da escolha, não da trapaça.

**Por que diferencia:** os clones convertem frustração em desinstalação ao esconder malus em portais "bons". Honestidade gera confiança → o jogador aceita riscos maiores → mais momentos extremos (e mais clipes). A trapaça queima o anúncio uma vez; a tensão honesta rende para sempre.

**Exemplo jogável — Fase 19, Deserto Robótico (M3):** jogador com **18 Soldados** (18/60 de Supply) vê o par: **esquerda "+25"** (garantia: 43 unidades) vs **direita "Zona de Perigo x10"** — o arco anuncia "SOBREVIVA → x10" com 3 caveiras, e as 3 fileiras de serras e lasers dos 30 m seguintes ficam visíveis antes da escolha (catálogo em `04-sistema-de-portais.md` §3.5). Conta honesta do risco — que aqui é de **skill, não de sorteio**: o jogador médio perde ~40% do exército na zona, então sobram ~11 Soldados, que viram 110 na saída; o Supply 60 corta em 60 efetivos + **50 pontos excedentes convertidos em ~100 moedas** (2 moedas/ponto, `supply_overflow_coin_rate`). O habilidoso atravessa perdendo ~10% e sai com 60 + ~200 moedas; quem perder mais de ~3/4 do exército nas serras sai com menos que os 43 garantidos da esquerda. O risco é genuinamente atrativo e genuinamente perigoso — e os dois desfechos rendem vídeo ("atravessei a zona com o exército inteiro" ou "as serras comeram meu exército").

---

## 5. Lista de sistemas do jogo (entregável 5)

Mapa completo: cada sistema, seu propósito em 1 frase e o documento do pacote que o detalha.

| # | Sistema | Propósito em 1 frase | Detalhado em |
|---|---|---|---|
| 1 | Core Run (corrida) | Controlar movimento lateral da multidão, pista, obstáculos e ritmo de 45–75 s. | `02-core-loop-e-progressao.md` |
| 2 | Crowd (multidão) | Renderizar, animar e formatar centenas de unidades como um exército coeso e performático. | `03-sistema-de-unidades.md` / `12-arquitetura-unity.md` |
| 3 | Gates (portais) | Gerar pares honestos de portais (matemáticos, classe, elemento, mutação, risco) e aplicar seus efeitos. | `04-sistema-de-portais.md` |
| 4 | Boss Scout | Mostrar boss, elemento e fraqueza antes da fase e sob demanda na corrida, guiando o plano do jogador. | `05-sistema-de-bosses.md` |
| 5 | Supply (Suprimento) | Limitar a capacidade do exército (60→300) e converter excedente em moedas com fanfarra. | `03-sistema-de-unidades.md` |
| 6 | Elementos | Aplicar o chart canônico (Fogo > Gelo > Raio > Fogo; Veneno, Luz, Sombra, Metal, Alien) a dano e fraquezas. | `03-sistema-de-unidades.md` |
| 7 | Mutações | Aplicar até 3 mutações visíveis e persistentes ao exército inteiro, com substituição da mais antiga. | `04-sistema-de-portais.md` |
| 8 | Unidades (roster) | Definir as 19 tropas (4 raridades), stats, papéis, habilidades e evolução por fragmentos. | `03-sistema-de-unidades.md` |
| 9 | Combate | Resolver DPS, alcance, área, cura, crítico e interações elementais na corrida e na arena. | `03-sistema-de-unidades.md` |
| 10 | Bosses | Orquestrar variantes regionais e bosses únicos: entrada, telegrafia, fraqueza visível, derrota em 10–20 s. | `05-sistema-de-bosses.md` |
| 11 | Fases e mundos | Estruturar 100 fases em 10 mundos com pacing canônico, geração orientada a boss e dificuldade-alvo. | `06-sistema-de-fases-e-mundos.md` |
| 12 | Mapa de progresso | Exibir mundos, fases, bosses e recompensas com bloqueio/desbloqueio e senso de destino. | `09-telas-e-wireframes.md` |
| 13 | Upgrades de meta | Vender as 8 trilhas permanentes (+5%/nível; custo 100 × 1,35^n) que carregam a progressão entre fases. | `07-economia-e-upgrades.md` |
| 14 | Cartas e fragmentos | Evoluir tropas (nível 1–10; 10 × 2^(n−1) fragmentos) e alimentar coleção. | `07-economia-e-upgrades.md` |
| 15 | Economia | Reger moedas, gemas, XP/nível de jogador e as âncoras numéricas do CANON §8. | `07-economia-e-upgrades.md` |
| 16 | Baús | Entregar cartas/moedas/gemas em pacotes de dopamina com raridades canônicas. | `07-economia-e-upgrades.md` |
| 17 | Recompensas (RewardSystem) | Calcular e celebrar o payout de fim de fase (moedas, XP, drops, dobrar com anúncio). | `07-economia-e-upgrades.md` |
| 18 | Loja | Vender moedas, gemas, baús, skins, remover-anúncios e passe com vitrine diária. | `08-monetizacao.md` |
| 19 | Skins | Personalizar o exército (10 no MVP: recolor + acessório do Soldado) sem afetar poder. | `08-monetizacao.md` |
| 20 | Passe de Temporada | Entregar trilha mensal (US$ 6,99) com tropa, boss, skin e recompensas diárias. | `08-monetizacao.md` |
| 21 | Eventos e missões | Rodar diárias (20–40 gemas/dia), semanais e rankings que puxam a sessão de volta. | `08-monetizacao.md` |
| 22 | Ads (AdsManager) | Servir rewarded (5 placements opcionais) e interstitials limitados (fase 6+, 1/3 fases) via MAX. | `08-monetizacao.md` |
| 23 | IAP | Processar compras via RevenueCat (âncoras: US$ 4,99 / 2,99 / 6,99). | `08-monetizacao.md` |
| 24 | FTUE / Tutorial | Garantir vitória em <60 s da abertura do app e ensinar jogando, sem tutorial longo. | `02-core-loop-e-progressao.md` |
| 25 | UI / Telas | Implementar as 10 telas exigidas com hierarquia legível em 9:16. | `09-telas-e-wireframes.md` |
| 26 | Game feel / VFX / Áudio | Entregar impacto, slow motion, vibração, partículas e feedbacks textuais dentro das regras do Pilar 1. | `09-telas-e-wireframes.md` / `12-arquitetura-unity.md` |
| 27 | Save e sync | Persistir local-first (JSON + checksum) com sync Firestore e Auth anônimo. | `12-arquitetura-unity.md` |
| 28 | Analytics | Disparar os 23 eventos obrigatórios do BRIEF e alimentar as métricas de soft launch. | `11-analytics.md` |
| 29 | Remote Config | Controlar dificuldade, economia, ads e eventos sem build nova. | `12-arquitetura-unity.md` |

Correspondência com os managers canônicos (CANON §13): GameManager (1, 24), LevelManager (11), GateManager (3, 7), CrowdManager/UnitManager (2, 5, 8), BossManager (4, 10), CombatSystem (6, 9), UpgradeSystem (13, 14), EconomySystem (15, 16), RewardSystem (17), AdsManager (22), IAPManager (18, 23), AnalyticsManager (28), SaveSystem (27), UIManager (12, 19, 25), AudioManager/VFXManager (26), RemoteConfigManager (29).

---

## 6. Direção de arte

### 6.1 Estilo

**"Brinquedo mutante premium":** low-poly estilizado com shading flat de 2 tons + rim light falso (URP, sem luz dinâmica em massa). Unidades com proporção 2,5 cabeças (cabeçudas, carismáticas), animação elástica com squash & stretch exagerado. Bosses na direção oposta: massivos, 8–12x a altura de uma unidade, silhueta brutal. Sem texturas detalhadas — cor por paleta em atlas único, o que garante consistência cromática e 1 material para todo o crowd (instancing). Mundo levemente dessaturado (−20%) em relação a unidades e portais: **o gameplay é sempre a coisa mais colorida da tela**.

### 6.2 Paleta por mundo

Cada mundo tem base (ambiente), acento (props/inimigos) e a regra fixa: **portais e VFX de gameplay usam cores complementares à base do mundo** para nunca se camuflarem.

| Mundo | Base do ambiente | Acento | Contraste de portais/VFX |
|---|---|---|---|
| 1 Campo Inicial | Verde-claro, céu azul | Amarelo feno | Magenta/ciano |
| 2 Cidade Zumbi | Cinza-azulado, neblina | Verde tóxico | Laranja quente |
| 3 Deserto Robótico | Areia ocre | Ferrugem | Azul elétrico |
| 4 Floresta Mutante | Verde profundo | Roxo esporos | Âmbar |
| 5 Vulcão dos Gigantes | Basalto escuro | Laranja lava | Ciano gelo |
| 6 Reino Congelado | Branco-azulado | Azul glacial | Vermelho/dourado |
| 7 Arena Medieval | Pedra quente | Estandartes vinho | Verde-limão |
| 8 Laboratório Alienígena | Roxo escuro | Verde neon | Branco quente |
| 9 Planeta Mecânico | Grafite | Cobre/engrenagem | Verde elétrico |
| 10 Dimensão Final | Preto-violeta, céu quebrado | Fractais iridescentes | Branco puro |

### 6.3 Silhuetas legíveis

- **Por raridade (CANON §8):** Comum = forma arredondada simples, 1 cor dominante (cinza/azul claro); Raro = 1 adereço marcante (cajado, capuz) em azul; Épico = silhueta assimétrica + glow roxo sutil; Lendário = ~2x a altura, glow dourado e trail.
- **Por mutação:** cada mutação altera a silhueta de TODAS as unidades (asas, placas de armadura, canhão de laser no ombro) e tem cor de identificação própria — reconhecível em thumbnail de 100 px.
- **Teste de aceitação de arte:** screenshot 9:16 reduzido a 25% — se classe, raridade e mutações do exército não forem identificáveis, o asset volta.

### 6.4 Leitura em vídeo vertical 9:16

- Pista ocupa o **terço central** da largura; câmera atrás/acima com pitch ~32°, mostrando 8–10 m de pista — o par de portais entra na tela ≥2,5 s antes da escolha.
- Números e ícones de portal com altura ≥ **8% da tela**; legíveis com o vídeo mudo e comprimido pelo TikTok.
- Zonas de segurança de HUD: topo 12% (contador de unidades, barra de progresso com ícone do boss), base 15% (zona de toque); feedbacks textuais (NICE, INSANE, BOSS BREAKER) no terço superior, nunca sobre portais.
- Toda cena-chave (multiplicação em massa, mutação, golpe final em slow motion) é enquadrada para funcionar **sem som e sem contexto** — os 14 momentos virais do BRIEF são requisitos de câmera, detalhados em `10-ads-e-viralizacao.md`.

### 6.5 Regras de VFX

1. **Hierarquia fixa de prioridade visual:** portal > mutação > boss > combate comum > ambiente. Se dois efeitos competem, o de menor prioridade é reduzido.
2. **Código de cores reservado:** verde = ganho de unidades; vermelho = perda/dano sofrido; dourado = moedas/lendário; cor do elemento = dano elemental. Nenhum VFX ambiente pode usar esses tons puros.
3. **Slow motion é raro para ser sagrado:** apenas 2 gatilhos — golpe final no boss e mutação lendária — a 0,3× por 0,8 s (valor canônico do pacote; técnica em `12-arquitetura-unity.md`).
4. **Orçamentos:** máx. 3 sistemas de partículas de alta densidade simultâneos; 12 arcos de Raio visíveis; pooling obrigatório (zero `Instantiate` em gameplay).
5. **Conversão de Supply** (excedente → moedas) tem fanfarra própria — jorro dourado em arco até o contador — para parecer prêmio, nunca punição (CANON §3.2).

### 6.6 Premium rodando em celular mediano

Alvo: 60 fps em mid-range Android (ex.: 4 GB RAM, GPU classe Adreno 610), fallback automático 30 fps + densidade reduzida em low-end. Técnicas: GPU instancing para o crowd (1 material, atlas único); animação do crowd por vertex animation texture; acima de ~150 unidades visíveis, agrupamento visual (1 modelo representa 5, contador permanece real — o número é a verdade, o modelo é o espetáculo); luz baked + 1 direcional; sombras só em bosses e líder; LOD em 3 níveis; URP Renderer enxuto (sem post-processing caro — o look premium vem de paleta, animação elástica e VFX disciplinado, não de pós-efeitos).

---

## 7. Análise de nomes (entregável 25)

### 7.1 Critérios (0–5 por critério; 20 máx.)

- **ASO:** keywords com volume e intenção corretas (army, run, mutant, gate, boss) no título.
- **Marca:** distintividade e registrabilidade (INPI/USPTO classe de jogos); nomes puramente descritivos pontuam baixo.
- **Pronúncia global:** facilidade para BR/LatAm/US/SEA falarem e digitarem.
- **Confusão (maior = mais seguro):** risco de colisão com jogos, marcas ou franquias existentes.

### 7.2 Os 11 nomes do brief, um a um

| Nome | ASO | Marca | Pronúncia | Confusão | Total | Veredito |
|---|---|---|---|---|---|---|
| Mutant Army Run | 5,0 | 2,5 | 4,5 | 4,0 | **16,0** | Forte. 3 keywords do gênero; cognato "mutante" em PT/ES; nenhum título consolidado igual. Fraqueza: descritivo demais para marca robusta sozinho. |
| Boss Breaker Army | 3,5 | 4,0 | 4,0 | 4,0 | **15,5** | Forte. Aliteração distintiva, registrável, sinergia com o feedback in-game "BOSS BREAKER". Perde "run/mutant" no ASO. |
| Monster Gate Run | 3,5 | 2,5 | 4,0 | 3,5 | **13,5** | Mediano. "Gate" educa a mecânica, mas "monster" promete a fantasia errada (comandamos mutantes, não monstros). |
| Mutant Rush | 3,0 | 3,5 | 4,0 | 3,0 | **13,5** | Mediano. Curto e punchy, mas "Rush" é sufixo saturado em hypercasual — afoga na busca e envelhece a marca. |
| Gate Army: Evolution War | 3,0 | 2,0 | 3,0 | 2,5 | **10,5** | Fraco. Longo; dois-pontos truncam na store; "Evolution War" atrai intenção de estratégia 4X, não runner; colide com a nuvem "Art of War/Evolution". |
| Evolution Horde | 2,5 | 3,0 | 2,5 | 3,0 | **11,0** | Fraco. "Horde" é difícil para BR/SEA pronunciarem e buscarem; promete zumbi-defense. |
| Army Evolution Run | 4,0 | 1,5 | 4,0 | 2,5 | **12,0** | Fraco apesar do ASO. Sopa de keywords sem marca; é o nome que um clone barato escolheria — fere diretamente o entregável 27 do BRIEF. |
| Merge Army Rush | 3,5 | 2,0 | 4,0 | 2,5 | **12,0** | Rejeitar. "Merge" tem volume alto, mas nossa mecânica core não é merge: atrair intenção errada gera reviews ruins e D1 baixo — ASO desonesto contraria o pilar de portais honestos. |
| Portal Army | 3,5 | 1,0 | 5,0 | 1,0 | **10,5** | **Rejeitar (jurídico).** "Portal" é marca célebre da Valve em jogos; oposição quase certa, risco de remoção de store. |
| Crowd Evolution | 3,0 | 1,0 | 3,5 | 0,5 | **8,0** | **Rejeitar (colisão direta).** Existe jogo mobile homônimo bem ranqueado no mesmo subgênero: confusão máxima de usuário e de marca. |
| Clone War Run | 3,0 | 0,5 | 4,0 | 0,5 | **8,0** | **Rejeitar (jurídico).** "Clone War(s)" remete inequivocamente à franquia da Lucasfilm/Disney; risco legal inaceitável. |

### 7.3 Cinco nomes novos originais

| Nome novo | Racional | ASO | Marca | Pronúncia | Confusão | Total |
|---|---|---|---|---|---|---|
| **Mutarmy: Mutant Army Run** | Portmanteau próprio (Mutant + Army) como marca + subtítulo 100% descritivo: herda todo o ASO do título de trabalho e ganha palavra registrável e googlável. | 5,0 | 4,5 | 4,0 | 4,5 | **18,0** |
| **Boss Breaker: Mutant Run** | Funde os dois melhores nomes do brief: marca aliterada + keywords "mutant/run"; conecta com o momento de maior dopamina do jogo. | 4,0 | 4,0 | 4,0 | 4,0 | **16,0** |
| **Gatebreakers** | Curto, brandável, educa a mecânica central (gates) com energia de "quebrar". Precisa de subtítulo ASO na store ("Gatebreakers: Mutant Army Run"). | 3,0 | 4,5 | 3,5 | 4,0 | **15,0** |
| **Replica Rush: Army of Mutants** | Liga à premissa (Fendas de Replicação) com aliteração; subtítulo carrega o ASO. "Replica" é cognato em PT/ES. | 3,5 | 4,0 | 4,0 | 3,5 | **15,0** |
| **Mutant Tide** | "Maré mutante" — evoca a multidão crescendo como onda; elegante e registrável. "Tide" é a sílaba mais difícil para BR; exige subtítulo. | 3,0 | 4,0 | 3,0 | 4,0 | **14,0** |

### 7.4 Ranking final e recomendação

| # | Nome | Total | Papel |
|---|---|---|---|
| 1 | **Mutarmy: Mutant Army Run** | 18,0 | Candidato a título de release global |
| 2 | **Mutant Army Run** | 16,0 | Título de trabalho e de soft launch (já é o canônico) |
| 3 | **Boss Breaker: Mutant Run** | 16,0 | Alternativa A para teste de loja |
| 4 | **Gatebreakers** (+ subtítulo) | 15,0 | Alternativa B, marca de longo prazo |
| 5 | **Replica Rush: Army of Mutants** | 15,0 | Reserva |

**Recomendação do time:**

1. **Soft launch com "Mutant Army Run"** (mantém o CANON §1 intacto — zero retrabalho em build, store e criativos durante a validação).
2. **Registrar "Mutarmy" imediatamente** (marca distintiva, domínio e handles disponíveis com altíssima probabilidade por ser palavra inventada) e proteger o trade dress do logo.
3. **No gate de fim do soft launch**, rodar A/B de página de loja (ícone + nome): "Mutant Army Run" vs "Mutarmy: Mutant Army Run" vs "Boss Breaker: Mutant Run", decidindo pelo CVR de instalação. O subtítulo descritivo garante que nenhuma variante sacrifica keywords.
4. **Vetos definitivos** (não testar em hipótese alguma): Portal Army, Clone War Run, Crowd Evolution — risco jurídico/colisão; e Merge Army Rush — promessa de mecânica falsa.

---

## 8. Mapa do pacote de design

| Arquivo | Escopo | Entregáveis do BRIEF |
|---|---|---|
| `CANON.md` | Decisões fixas (fonte da verdade) | — |
| `01-visao-e-conceito.md` | **Este documento** — visão, pilares, diferenciais, sistemas, nomes | 1, 5, 25 |
| `02-core-loop-e-progressao.md` | Core loop 12 passos, primeiros 30 min, primeiros 7 dias, FTUE | 2, 3, 4 |
| `03-sistema-de-unidades.md` | Roster completo, stats, Supply, elementos, combate | 9 |
| `04-sistema-de-portais.md` | Taxonomia dos portais, mutações, geração de pares orientada a boss | 11 |
| `05-sistema-de-bosses.md` | Boss Scout, variantes regionais, 10 bosses únicos | 10 |
| `06-sistema-de-fases-e-mundos.md` | 100 fases / 10 mundos, obstáculos, curva de dificuldade, pipeline de níveis | 12 |
| `07-economia-e-upgrades.md` | Moedas/gemas/XP/fragmentos/baús, 8 trilhas de upgrade | 8, 13 |
| `08-monetizacao.md` | Ads, IAP, passe, loja, eventos | 14 |
| `09-telas-e-wireframes.md` | 10 telas + wireframes textuais, game feel de UI | 6, 7 |
| `10-ads-e-viralizacao.md` | Estratégia de UA, anúncios em vídeo, thumbnails, viralização | 15, 23, 24, 30 |
| `11-analytics.md` | Taxonomia de eventos, funis, métricas do soft launch, experimentação A/B | — (seção "Analytics" do BRIEF; apoia as metas do CANON §12) |
| `12-arquitetura-unity.md` | Estrutura de projeto Unity, classes C#, ScriptableObjects | 16, 17, 18 |
| `13-roadmap-e-backlog.md` | Roadmap, backlog priorizado, MVP 30 dias, expansão pós-MVP | 19, 20, 21, 22 |
| `14-riscos-e-qualidade.md` | Riscos, anti-clone, mais viciante/inteligente, game feel e compliance | 26, 27, 28, 29 |

**Critério de pronto desta visão:** qualquer pessoa do time, lendo apenas este documento + CANON.md, consegue explicar em 1 minuto o que é o jogo, por que ele vence os clones e onde encontrar o detalhe de cada sistema.
