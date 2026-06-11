# CANON — Decisões Fixas de Design · Mutant Army Run

> **Este arquivo é a fonte da verdade.** Todo documento do GDD deve usar EXATAMENTE estes nomes, números e regras. Se um documento precisar de um valor que não está aqui, ele pode criá-lo — mas nunca pode contradizer o que está aqui.
> Versão 1.0 — 2026-06-11

## 1. Identidade

- **Título de trabalho:** Mutant Army Run (avaliação de nomes finais no doc 01).
- **Tagline principal:** "Monte o exército mais absurdo possível em 60 segundos."
- **Tagline secundária:** "Escolha os portais certos, crie mutações insanas e destrua bosses gigantes."
- **Gênero:** hybrid-casual — runner de multidão com camada estratégica leve e meta-progressão.
- **Plataforma:** Android primeiro, iOS depois. Unity 2022 LTS + URP. Orientação retrato (9:16).
- **Duração-alvo:** corrida de 45–75 s + boss de 10–20 s. Fase completa ≈ 60–90 s.
- **Público:** 13–40 anos, casual, BR/LatAm/US/SEA. Sem violência gráfica (sem sangue; inimigos "desmontam" em peças/partículas).

## 2. Pilares de design (em ordem de prioridade — desempate de qualquer decisão)

1. **Legível em 3 segundos** — qualquer frame comunica: corra, escolha o portal, o exército cresce, o boss cai.
2. **Escolha inteligente, não "maior número"** — Boss Scout + elementos + Suprimento transformam cada portal num micro-puzzle.
3. **Espetáculo constante** — multiplicação visual, mutações visíveis, números gigantes, slow motion.
4. **Progressão em 3 camadas** — dentro da fase (portais), entre fases (upgrades/cartas), entre sessões (mundos/passe/eventos).

## 3. Diferenciais canônicos (o que nos separa dos clones)

### 3.1 Boss Scout (inovação central)
Antes de cada fase, um cartão de ~2 s mostra **o boss da fase, seu elemento e sua fraqueza** ("BOSS DE GELO — fraco contra FOGO 🔥"). Toda escolha de portal vira um plano, não um reflexo. Durante a corrida, tocar no ícone do boss na barra de progresso reabre o lembrete por 1 s sem pausar.
**Consequência de design:** os portais da fase são gerados levando o boss em conta (sempre existe pelo menos 1 rota "ótima" e 1 rota "armadilha aparentemente boa").

### 3.2 Suprimento (Supply) — o anti-"maior número"
O exército tem capacidade de **Suprimento**. Cada unidade tem custo de Supply (Soldado 1, Mago 4, Gigante 12...). Ao estourar o limite, o excedente é convertido automaticamente em moedas (com fanfarra visual — nunca parece punição). Resultado: x10 soldados nem sempre é melhor que +2 magos.
- Supply inicial: **60**. Upgrades de meta elevam até **300**.

### 3.3 Mutações persistentes e visíveis
Mutações (asas, laser, armadura, tamanho...) aplicam-se ao exército inteiro, ficam **visíveis nos modelos** e duram a fase inteira. Máximo de **3 mutações simultâneas** (slots); pegar a 4ª substitui a mais antiga — escolha estratégica e momento de vídeo.

### 3.4 Portais pareados e honestos
Portais aparecem **sempre em pares** (esquerda/direita), com informação honesta: número, ícone de classe/elemento ou risco com porcentagem clara ("70% x10 / 30% perde metade"). Nunca enganamos o jogador — a tensão vem da escolha, não da trapaça.

## 4. Elementos — chart canônico (8 no total; MVP usa 4)

**MVP:** Fogo, Gelo, Raio, Veneno. **Pós-MVP:** Luz, Sombra, Metal, Alien.

| Regra | Efeito |
|---|---|
| Ciclo principal | **Fogo > Gelo > Raio > Fogo** (vantagem = +50% dano) |
| Mesmo elemento vs mesmo elemento | −50% dano (Fogo vs boss de lava = péssimo) |
| Veneno | dano contínuo 3% HP/s por 4 s; **+50% vs orgânicos**, **0% vs máquinas e mortos-vivos** |
| Luz | ataques curam 2% do HP do exército; +50% vs Sombra e mortos-vivos |
| Sombra | 20% de chance de reviver unidade aliada morta como "sombra" (50% dos stats); +50% vs Luz |
| Metal | +30% defesa para a unidade; −50% de dano recebido físico; recebe +50% de Raio (conduz) |
| Alien | 25% de chance por ataque de efeito aleatório (queimar, congelar, encadear, envenenar) |
| Sem elemento | neutro, sem bônus nem penalidade |

Gelo também aplica **lentidão de 30% por 2 s** (não acumula). Raio **encadeia 50% do dano** para até 2 inimigos próximos.

## 5. Tropas — roster canônico

| Tropa | Raridade | Supply | Papel |
|---|---|---|---|
| Soldado | Comum | 1 | equilibrado, linha de frente |
| Arqueiro | Comum | 2 | dano à distância, frágil |
| Escudeiro | Comum | 3 | tanque, protege pequenos |
| Corredor | Comum | 1 | rápido, desvia melhor, fraco |
| Mago | Raro | 4 | dano em área |
| Ninja | Raro | 3 | esquiva de obstáculos/armadilhas |
| Lança-Chamas | Raro | 4 | dano de Fogo |
| Tropa Glacial | Raro | 4 | dano de Gelo + lentidão |
| Médico | Raro | 4 | cura aliados (essencial no boss) |
| Robô | Épico | 8 | alto dano e resistência; imune a Veneno |
| Gigante | Épico | 12 | muito HP e dano, lento |
| Necromante | Épico | 8 | revive tropas caídas |
| Engenheiro | Épico | 8 | constrói torreta na arena do boss |
| Alien | Épico | 8 | ataque imprevisível (elemento Alien) |
| Dragão | Lendário | 20 | dano em área + voo (ignora obstáculos de chão) |
| Titã | Lendário | 25 | enorme, forte, lento |
| Anjo de Guerra | Lendário | 18 | cura + dano de Luz |
| Demônio Mutante | Lendário | 20 | dano brutal de Sombra |
| Mecha Supremo | Lendário | 25 | laser contínuo + mísseis em área |

**Baseline de stats (nível 1):** Soldado = HP 10 · DPS 2 · velocidade 5 m/s. As demais tropas escalam a partir desse baseline proporcionalmente ao Supply (doc 03 detalha a tabela completa; manter coerência: DPS+HP totais por ponto de Supply ≈ constante +10–20% de prêmio por raridade).

**MVP (5 tropas):** Soldado, Arqueiro, Escudeiro, Mago, Gigante.

## 6. Bosses — regras canônicas

- **Toda fase termina em arena com boss.** Fases 1–9 de cada mundo usam *variantes regionais* (3 arquétipos por mundo, escalando tamanho/vida/cor). Fase 10 de cada mundo = **boss único gigante**.
- Bosses únicos por mundo: M1 **Gigante de Madeira** (fraco: Fogo) · M2 **Zumbi Titã** (fraco: Fogo e Luz; imune: Veneno) · M3 **Robô Escorpião** (fraco: Raio; imune: Veneno) · M4 **Planta Carnívora Gigante** (fraco: Fogo e Veneno) · M5 **Dragão de Lava** (fraco: Gelo; resiste: Fogo) · M6 **Rei de Gelo** (fraco: Fogo; resiste: Gelo) · M7 **Cavaleiro Colosso** (fraco: Raio — armadura conduz) · M8 **Alien Supremo** (fraqueza rotativa a cada 25% de HP, sempre exibida no HUD) · M9 **Mecha Supremo** (fraco: Raio; imune: Veneno) · M10 **Entidade Dimensional** (alterna elementos; usa os próprios portais do jogador contra ele).
- **MVP (5 bosses):** Golem de Pedra (arquétipo M1), Gigante de Madeira, Brutamontes Zumbi (arquétipo M2), Zumbi Titã, Robô Escorpião.
- Todo boss tem: barra de vida gigante, 1 ataque especial telegrafado, fraqueza elemental visível, animação de entrada ≤2 s, combate de 10–20 s, recompensa especial, chance de drop de carta/fragmento.

## 7. Mundos (10 × 10 fases = 100)

1 Campo Inicial · 2 Cidade Zumbi · 3 Deserto Robótico · 4 Floresta Mutante · 5 Vulcão dos Gigantes · 6 Reino Congelado · 7 Arena Medieval · 8 Laboratório Alienígena · 9 Planeta Mecânico · 10 Dimensão Final. (Temas e bosses conforme BRIEF.)
**No MVP:** 3 mundos enxutos — M1 fases 1–7, M2 fases 8–14, M3 fases 15–20. No release completo, expandir para 10 fases/mundo.

## 8. Economia — âncoras numéricas

- **Moedas:** vitória na fase 1 = **100 moedas**; recompensa cresce ≈ recompensa_base × 1,10^(fase−1), recalibrada por Remote Config. Rewarded ad dobra (x2).
- **Primeiro upgrade custa 100 moedas.** Curva de custo por trilha: custo(n) = 100 × 1,35^n.
- **Gemas (premium):** boss de mundo dá 10 gemas; missões diárias ≈ 20–40 gemas/dia para jogador ativo. Baú raro na loja = 300 gemas.
- **Fragmentos (por tropa):** evoluir do nível n para n+1 custa 10 × 2^(n−1) fragmentos da própria tropa + moedas. Nível máximo 10.
- **XP / nível de jogador:** desbloqueia features — nv2 Upgrades, nv3 Baús, nv4 Loja completa, nv5 Passe de Temporada, nv6 Eventos.
- **SEM sistema de energia.** Decisão canônica: nunca travar o jogador que quer jogar.
- Raridades e cores canônicas: Comum (cinza/azul claro), Raro (azul), Épico (roxo), Lendário (dourado).

## 9. Upgrades de meta — 8 trilhas

Dano inicial · Vida inicial · Velocidade · Multiplicador de recompensa · Exército inicial · Chance crítica · Dano contra boss · Resistência a obstáculos.
Efeito: **+5% por nível** (Exército inicial: +1 unidade a cada 2 níveis). Custo: 100 × 1,35^nível.
**MVP usa 4 trilhas:** Dano inicial, Vida inicial, Exército inicial, Multiplicador de recompensa.

## 10. Portais — MVP (8 tipos canônicos)

+10 · +25 · x2 · x3 · ÷2 · Virar Arqueiro · Elemento Fogo · Risco "x10 se sobreviver à zona de perigo".
(O sistema completo de portais — matemáticos, classe, elemento, mutação, risco — está no BRIEF e é detalhado no doc 04.)

## 11. Monetização — regras canônicas

- **Rewarded (sempre opcional):** dobrar recompensa · reviver no boss (1×/fase) · baú extra diário · testar tropa lendária por 1 fase · acelerar upgrade.
- **Interstitial:** só a partir da fase 6; máx. 1 a cada 3 fases; **nunca** após duas derrotas seguidas; frequência 100% controlada por Remote Config.
- **IAP âncora:** Remover Anúncios US$ 4,99 (inclui 200 gemas) · Oferta inicial US$ 2,99 (1×, primeiras 48 h) · Passe de Temporada US$ 6,99/mês.
- **Anti pay-to-win:** tudo que dá poder pode ser obtido grátis (baús grátis dropam lendárias); pagamento acelera e personaliza.

## 12. Metas de produto (soft launch)

D1 ≥ 40% · D3 ≥ 22% · D7 ≥ 12% · sessão média ≥ 8 min · ≥ 6 fases/sessão · conversão rewarded ≥ 35% dos DAU · taxa de vitória alvo: 95% (fases 1–3), 85% (fases 4–10), ~70% (meio de mundo), ~55% (fase 10 de cada mundo) · CPI alvo ≤ US$ 0,40 (BR/LatAm) e ≤ US$ 1,50 (US) · ARPDAU ≥ US$ 0,08.

## 13. Tecnologia canônica

Unity 2022 LTS + URP, C#, portrait. Firebase: Auth anônimo, Firestore (sync), Analytics, Remote Config, FCM, Crashlytics. **Ads: AppLovin MAX como mediação (AdMob, Meta, Unity Ads como redes).** IAP: RevenueCat. Save local-first (JSON com checksum) + sync Firestore.
Managers: GameManager, LevelManager, GateManager, CrowdManager, UnitManager, BossManager, CombatSystem, UpgradeSystem, EconomySystem, RewardSystem, AdsManager, IAPManager, AnalyticsManager, SaveSystem, UIManager, AudioManager, VFXManager, RemoteConfigManager.
ScriptableObjects: UnitConfigSO, BossConfigSO, LevelConfigSO, GateConfigSO, UpgradeConfigSO, RewardConfigSO, WorldConfigSO, RarityConfigSO, ElementChartSO, MutationConfigSO.

## 14. Glossário PT → EN (usar em docs e código)

Portal=Gate · Tropa/Unidade=Unit · Suprimento=Supply · Mutação=Mutation · Fragmento=Shard · Baú=Chest · Moeda=Coin · Gema=Gem · Fase=Level · Mundo=World · Chefe=Boss · Passe de Temporada=Season Pass · Exército/Multidão=Army/Crowd · Trilha de upgrade=Upgrade Track

## 15. MVP — escopo travado (30 dias)

20 fases (M1 1–7, M2 8–14, M3 15–20) · 5 tropas (§5) · 8 portais (§10) · 5 bosses (§6) · 4 trilhas de upgrade (§9) · moedas + XP + fragmentos · cartas simples · 10 skins (recolor + acessório do Soldado) · telas: inicial, gameplay (com boss), vitória, derrota, tropas, upgrades, loja · rewarded ads (dobrar + reviver) · analytics básico · Remote Config básico · Boss Scout incluído (é o diferencial — não cortar) · Supply incluído de forma simplificada: limite fixo de 60, sem trilha de upgrade de Supply no MVP.

## 16. Regras de pacing canônicas (primeiras fases)

Fase 1: impossível perder, primeiro x2 e primeiro boss morre fácil — vitória em <60 s da abertura do app. Fase 2: primeira escolha estratégica real (quantidade vs qualidade). Fase 3: primeiro boss "uau" (Golem de Pedra com entrada cinematográfica). Fase 5: desbloqueia Upgrades + primeira tropa nova (Arqueiro permanente). Fase 7: boss de mundo M1 (Gigante de Madeira) + baú grande. Fase 10: recompensa grande (baú épico + 50 gemas). Interstitials só após a fase 6.
