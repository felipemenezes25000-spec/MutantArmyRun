# 15 — Referências e Recursos · Mutant Army Run

> Documento derivado do CANON.md (v1.0). Consolida o estudo real de código dos repositórios clonados em `C:\Users\Felipe\Downloads\jogo test\_research\repos`, a avaliação de assets/SDKs e as decisões de stack do MVP de 30 dias.
> Regra de ouro deste documento: **referência é mapa, nunca material de construção** — exceto quando a licença explicitamente permite (ver §1).

---

## 1. Política de licenças do projeto

| Licença | Política | Resumo da regra |
|---|---|---|
| **CC0 1.0** | ✅ Livre | Usar, modificar e embarcar sem crédito. Fonte preferencial de assets. |
| **CC-BY (3.0/4.0)** | ⚠️ Permitido com crédito | Crédito centralizado na tela de Créditos (Configurações) + planilha de assets. |
| **MIT / Apache 2.0** | ✅ Permitido com atribuição | Pode copiar/adaptar código; preservar o aviso de copyright no fonte. |
| **SEM LICENÇA** | 🔍 Apenas estudo | Todos os direitos reservados. Ler para aprender; **reimplementar do zero**. Zero copy-paste. |
| **GPL / CC-BY-SA / CC-BY-NC** | ❌ Proibido | Virais (contaminam o build) ou incompatíveis com uso comercial. |
| **Proprietárias específicas** (Unity EULA/UCL, DOTween, Mixkit, CraftPix, Mixamo) | ⚠️ Caso a caso | Uso comercial OK; **nunca redistribuir os arquivos** (inclusive em repo público). |

**Por que CC0 é livre:** o autor renunciou a todos os direitos — não há obrigação jurídica nenhuma, nem de crédito, nem de compartilhar mudanças; é a única categoria onde podemos recolorir, recortar, redistribuir no build e até esquecer a origem sem risco, o que elimina por completo a burocracia de compliance num time pequeno com 30 dias de prazo.

**Por que CC-BY exige crédito centralizado:** a licença é permissiva (uso comercial e modificação liberados), mas a atribuição é condição legal do uso — sem ela a licença cai e viramos infratores; centralizar tudo numa única tela de Créditos (entrada em Configurações, acessível pela engrenagem da tela inicial — doc 09) + planilha de assets no repositório garante que nenhum item fique sem crédito quando o build for empacotado, e replicar na descrição da Google Play é a redundância barata que nos protege.

**Por que MIT exige atribuição:** o MIT permite tudo (copiar, modificar, vender), mas tem uma única condição: manter o aviso de copyright e o texto da licença junto ao código — é trivial de cumprir (um cabeçalho de comentário ou um arquivo `THIRD-PARTY-NOTICES.md` no repo) e nos dá acesso direto ao melhor código estudado (Movement.cs do MobControl-Clone, GateManager do HyperCasualRunningGame, formação filotáxica do CountMaster).

**Por que SEM LICENÇA = apenas estudo:** ausência de licença não significa domínio público — significa o oposto: todos os direitos reservados por padrão (Convenção de Berna); qualquer linha copiada é violação de copyright, e como vários desses repos são clones de jogos comerciais (Count Masters, Mob Control), copiar deles é risco dobrado (do autor do repo E do dono do jogo original); por isso a regra é binária: pode-se aprender o padrão, o algoritmo, o conceito — e escrever a nossa implementação do zero, com nossos nomes e nossa estrutura.

**Por que GPL é proibido:** a GPL (e a CC-BY-SA) é viral — qualquer código/asset GPL linkado no build obrigaria a liberar o código-fonte do Mutant Army Run inteiro sob GPL, destruindo o modelo de negócio F2P com IAP; a CC-BY-NC é proibida pelo motivo simétrico (somos um projeto comercial). No OpenGameArt e Freesound, o filtro de licença é obrigatório **antes** do download.

**Processo obrigatório de aquisição:** todo asset entra pelo filtro CC0 do itch.io / busca filtrada das demais fontes → **confirmar a licença na página individual do item** (tags são autodeclaradas) → registrar nome/URL/licença/autor na planilha de assets do repositório. Evitar assets de IA sem disclosure (filtro "AI Assistance" do itch.io) — alinhado ao requisito de originalidade do BRIEF.

---

## 2. Repositórios estudados

Clones locais em `C:\Users\Felipe\Downloads\jogo test\_research\repos\<nome>`. Política conforme §1.

| Repositório | Licença | Política de reuso | Principais lições | O que evitar |
|---|---|---|---|---|
| **MobControl-Clone** (satas20) | MIT | Código reutilizável | Input drag-âncora (Movement.cs) pronto para o nosso controle; semente da dedup de portal (cloneSource); waves como dados (tempo+composição); escala∝HP como leitura visual de vida | Find por tag em Update de cada unidade; Rigidbody dinâmico movido por transform; Instantiate dentro de trigger de portal; bug que muta a posição do castelo; sem fluxo de vitória |
| **HyperCasualRunningGame** (Open Video Game Library) | MIT | Código reutilizável | A melhor referência de GatePair: flag `wasUsed` consome o par inteiro na 1ª unidade; efeito aplicado UMA vez no manager central; multiplicação como delta; preview de portal via OnValidate; painel de tuning em runtime no device | Multidão física (AddForce por unidade); KillPlayer O(n) com VFX por unidade; combate 1v1 por coroutine (bug admitido em TODO); CheckFail todo frame sem validar estado; fases hand-built |
| **CountMaster** (UnityToBrain) | MIT | Código reutilizável | Semântica "portal calcula TOTAL-ALVO, manager reconcilia"; formação filotáxica (girassol) O(1) por índice; tween OutBack de reagrupamento; contador TMP world-space por evento | `transform.childCount` como verdade do exército; contador cosmético em coroutine paralela; portal x1 (escolha morta); rótulo sem prefixo "+"; god class + Library/ commitada |
| **Count-Master-Clone-Game** (onur-kantar) | SEM LICENÇA | Apenas estudo | TeamLeader: contador int com funil único de mutação (o melhor padrão de contagem); GatePair com controlador-pai e `isTaken`; ledger de slots de formação; hierarquia Movement desacoplada | Bug "x2 que triplica" (delta vs alvo); float para contagem; capacidade fixa de slots que dessincroniza UI; Destroy do par dentro do trigger; uma cena por nível |
| **count-masters** (kubray14) | SEM LICENÇA | Apenas estudo | Catálogo de antipadrões que justifica nosso GateConfigSO/CrowdManager; conceito do "blob orgânico" (a imitar sem física); cuidado igual nos pipelines de adição E remoção de unidades | Índice estático compartilhado entre portais; rótulo desacoplado da lógica; zero exclusividade de par; 3 fontes de verdade de contagem; loop de remoção que retém referências destruídas |
| **InfiniteRunner3D** (dgkanatsios) | SEM LICENÇA | Apenas estudo | Interface IInputDetector (nullable) com implementação de teclado p/ editor; segmento-prefab com âncoras (Transform[]); zona segura pós-spawn; swipe por ângulo+cross product; enum de GameState central | Zero pooling; despawn por timer fixo (10 s); câmera filha do player; singleton com `new MonoBehaviour()`; UI por frame; geração 100% aleatória sem seed |
| **EndlessRunnerSampleGame / Trash Dash** (Unity) | SEM LICENÇA (Asset Store EULA) | Apenas estudo | State machine em PILHA (revive preserva a corrida); save versionado com migração por gate de versão; Addressables por label/mundo; trackSeed determinístico; Modifier hooks; lifecycle de consumível; taxonomia de analytics com transactionId; guard anti duplo-clique no popup | Save binário sem checksum; `using UnityEditor` fora de ifdef (quebra build); singletons espalhados; strings mágicas; segmentos sem pool; alocações por frame no Tick; balance hardcoded; tutorial entrelaçado no loop |
| **Unity3d-RunnerTemplate-2023** (re-upload do template oficial) | SEM LICENÇA (conteúdo Unity re-hospedado — risco dobrado) | Apenas estudo | Fluxo-de-jogo-como-grafo-de-estados com eventos ScriptableObject; RunWallet (moeda temp vs comitada; XP sempre comitada); guarda de progressão "só avança na fronteira"; tela de resultado passiva; safe area resolvida 1× no root; contra-escala do rótulo do portal | UIManager de View única (sem overlays); resultado com timeScale=0 (mataria nossa coreografia); tela de vitória mostra total em vez do delta; SaveManager = PlayerPrefs cru; progressão incrementada em callback de UI; README promete ads que não existem |
| **Unity-3D-HyperCasual_MobileGame** (LoviceSunuwar) | SEM LICENÇA | Apenas estudo | Drag por `touch.deltaPosition` (não posição absoluta — sem teleporte); trigger "Out" atrás do player p/ métrica de desvio; juice de 1 linha (PunchScale) no impacto | GameObject.Find por string em cada spawn; comparação por nome; UI como fonte da verdade; lógica de derrota dentro do obstáculo; InvokeRepeating por string |
| **Boids** (keijiro) | MIT (no cabeçalho dos 2 scripts; **arte sem licença**) | Código reutilizável (só os .cs) | Boids completo é overkill: só a SEPARAÇÃO sobrevive (falloff `Clamp01(1−d/r)`); padrão "âncora do bando" (líder = transform); damping exponencial `Exp(−k·dt)`; individualidade barata via Perlin + Animator.speed variado | OverlapSphere por agente/frame (GC); 1 MonoBehaviour+Update por unidade; collider por unidade só p/ vizinhança; divisão por zero com 0 vizinhos (NaN) |
| **unity-crowd-simulation** (keijiro) | SEM LICENÇA | Apenas estudo | Handoff "IA → cinemática" (RideOnEscalator) = entrada na arena do boss; animação dirigida por velocidade real; metering de vazão 1-por-intervalo = template da conversão de Supply excedente em moedas com cadência | NavMeshAgent por unidade de multidão; Find por string por agente; 1 coroutine viva por agente; UnityScript (nada compilável — leitura de padrão apenas) |
| **Boids-MirzaBeig** | "Do whatever you want with this." (grant informal no README) | Código reutilizável (guardar evidência: print/commit hash; preferir reimplementar) | A arquitetura certa do CrowdManager: struct puro em lista, sim em FixedUpdate / render interpolado em Update, layout pronto p/ Jobs/Burst; soft bounds por eixo = limites laterais da pista; "CACHE IS KING" | O(n²) sem grid espacial; alocação no hot path (List dentro de struct); Magnitude sem guard de zero; OnGUI por frame; o renderer por partículas não serve p/ nossos modelos com mutações |
| **Boids-Unity** (BrianLDev) | SEM LICENÇA | Apenas estudo | JobsTemplate.cs: ciclo schedule-early/complete-late, Dispose obrigatório, IJobParallelForTransform; prova concreta de que "refatorar para manager depois" mata a feature — nascer centralizado | MonoBehaviour por boid com Dictionary alocado por frame; pacotes preview mortos no manifest; estado mutável dentro de ScriptableObject; singleton via FindObjectsOfType |

---

## 3. Lições de código por sistema nosso

Cada lição cita o repositório e a classe de origem. Política de reuso conforme §1 (MIT = pode adaptar com notice; SEM LICENÇA = reimplementar do zero).

### 3.1 CrowdManager (multidão, Supply, formação)

1. **Manager central, nunca Update por unidade.** O CrowdManager itera/ticka um array central de unidades — jamais 1 MonoBehaviour com Update próprio por soldado (anti-exemplo medido: `Boid.cs` do Boids-Unity, com Dictionary alocado por frame; e `BoidBehaviour.cs` do keijiro/Boids, com OverlapSphere por agente). Com Supply 60 no MVP a versão managed single-thread basta; o layout de dados (structs/arrays SoA: positions, velocities, typeId, hp) já nasce pronto para migrar para `NativeArray` + `IJobParallelFor` (ciclo de vida conforme `JobsTemplate.cs` do Boids-Unity: schedule cedo, Complete no LateUpdate, Dispose no OnDestroy).
2. **Sim/render desacoplado.** Simulação em tick fixo + camada visual interpolando por frame (`Boids2D_Simulator.cs`/`Boids2D_Renderer.cs` do Boids-MirzaBeig). Nossa render usa instancing/modelos com mutações visíveis (CANON §3.3) — copiamos o desacoplamento, não o renderer por partículas.
3. **Formação: filotaxia + ledger de slots, não física.** Fórmula de girassol `x = k·√i·cos(i·θ)` (CountMaster, `PlayerManager.FormatStickMan()` — MIT, adaptável) gera slot O(1) para n arbitrário; cada unidade conhece seu slot e converge continuamente para ele — formação auto-curativa (conceito do `TeammatePoint`/`CreateTeamPoints.cs` do Count-Master-Clone — reimplementar, sem licença), mas com slots por fórmula sob demanda (a capacidade fixa precomputada deles dessincroniza a UI quando estoura). Tropas grandes (Gigante, Supply 12) ordenadas por raio: grandes no centro.
4. **Só separação local sobrevive do boids.** Alinhamento e coesão são dados de graça pelo corredor + slots; manter apenas o kernel de separação com falloff linear `Clamp01(1 − d/raio)` (keijiro/Boids, `BoidBehaviour.GetSeparationVector()` — MIT) com guard de zero vizinhos e d mínimo. Vizinhança via grid uniforme em XZ reconstruído 1×/frame — nunca `Physics.OverlapSphere` por unidade.
5. **Âncora do bando.** Um `CrowdAnchor` (transform-líder) segue o input lateral; o bando inteiro herda direção/posição dele (padrão do seed do controller em keijiro/Boids). Limites da pista por soft bounds por eixo (força constante quando |x| > meia-largura — `GetBoundingForce` do Boids-MirzaBeig), sem colliders.
6. **Contagem: int único com funil único de mutação.** `humanCount` explícito mutado apenas por Join/Leave, com evento `OnCountChanged` — o padrão `TeamLeader.cs` do Count-Master-Clone (reimplementar). Anti-exemplos: `transform.childCount` como verdade (CountMaster), float + 3 fontes paralelas (`Creator.cs` do count-masters), contador cosmético em coroutine (CountMaster).
7. **Supply é o nosso diferencial e entra no funil.** `JoinArmy(unit)`: se `supplyAtual + custo > limite`, converte em moedas com fanfarra (CANON §3.2) — nunca falha silenciosa. A conversão de excedente usa **metering de vazão** (1 unidade por intervalo, cadência fixa) para virar espetáculo sequencial, não um frame-spike — conceito do `EscalatorEntrance.js` do unity-crowd-simulation (reimplementar).
8. **Pooling obrigatório, por tipo, com Reset de estado.** Pool por tipo de unidade com `Reset()` (HP, elemento, mutação visual) no Get, pré-alocado no pico esperado na carga da fase (como as 256 moedas do `Pooler.cs` do Trash Dash — estudar, reimplementar; ou `Dictionary<prefabId, Queue<GameObject>>`, corrigindo a busca linear por tag do ObjectPooler do Count-Master-Clone). Em lote: remoções iterando de trás pra frente, 1 VFX agregado (anti-exemplo: KillPlayer O(n) + 2 partículas por unidade do HyperCasualRunningGame).
9. **Individualidade barata.** Perlin por unidade modulando velocidade + `Animator.speed` randomizado (keijiro/Boids, `Start()/Update()`) e spawn com scatter ao redor do líder fluindo ao slot (Count-Master-Clone) — 200 unidades parecem indivíduos a custo O(1).
10. **Spawn de multiplicação:** offsets aleatórios ao redor do líder dão a "explosão" visual certa (MobControl-Clone, `DoorScript.cs`), mas via pool + Supply check ANTES de spawnar — nunca Instantiate dentro do trigger. A separação local espalha as unidades sobrepostas em ~0,5 s sozinha (Boids-MirzaBeig).

### 3.2 GateManager (portais pareados)

1. **Consumo one-shot do PAR com flag no manager do par.** `GateManager.wasUsed` do HyperCasualRunningGame (`GateManager.cs`/`GateController.cs` — MIT): a 1ª unidade que toca consome o par inteiro; chamadas seguintes (inclusive do irmão) retornam. Callbacks de física são síncronos — sem race. Nossa versão troca o bool por evento `OnGateConsumed(GateData)` para o CrowdManager. Reset do estado no respawn se reusarmos chunks via pool (lá `wasUsed` nunca reseta).
2. **Par por referência serializada, nunca índice de filho.** Controlador-pai com `isTaken` (Count-Master-Clone, `HumanCreatorController.cs` — reimplementar) — desabilitar os DOIS colliders sincronicamente no frame do consumo e animar o descarte depois; nunca `Destroy` do pai dentro do trigger (risco do 2º collider processar o mesmo frame) nem `GetChild(0)/GetChild(1)` hardcoded (CountMaster).
3. **Efeito = função pura int→int, com semântica de TOTAL-ALVO.** O portal expõe `int Apply(int current)` no GateConfigSO; o CrowdManager reconcilia atual→alvo num único funil (spawn/despawn da diferença + regra de Supply). Origem: `MakeStickMan()` do CountMaster (MIT). Caso de teste negativo obrigatório: o bug "x2 que triplica" do Count-Master-Clone (`HumanCreator.cs` aplica xN como delta). **Testes unitários de portal são obrigatórios no MVP**: x2 → f(n)=2n; +10 → f(n)=n+10; ÷2 → f(n)=⌈n/2⌉ (regra de arredondamento de ímpares definida no doc 04). Multiplicação aplicada como delta no spawn (`AddPlayer(count·(n−1))` do HyperCasualRunningGame) — barato, o pool absorve.
4. **Dado vive NO portal; rótulo é renderizado do dado.** `SetFormula(mode, num)` troca material + texto TMP e roda em OnValidate → designer vê a fórmula na cena sem play mode (HyperCasualRunningGame). Anti-exemplo terminal: arrays estáticos hardcoded com índice compartilhado e texto digitado à mão na cena (count-masters, `AddManager.cs`/`MultiplyManager.cs`) — a antítese do pilar "portais honestos" (CANON §3.4). Sempre "+N", "xN", "÷N" explícitos (CountMaster exibia número sem prefixo — ilegível em 3 s, viola o pilar 1).
5. **Legibilidade independe da escala:** contra-escala do rótulo quando o mesh do portal escala (`Gate.SetScale` do Unity3d-RunnerTemplate-2023 — conceito).
6. **Pares gerados pelo LevelManager a partir de LevelConfigSO/GateConfigSO + seed**, considerando o boss da fase (Boss Scout: sempre 1 rota ótima + 1 armadilha — CANON §3.1). Nada de portais hand-built na cena (HyperCasualRunningGame) nem `Random.Range` no Start (CountMaster sorteava x1 — portal morto). O `trackSeed` determinístico do Trash Dash (`TrackManager.cs`) viabiliza reprodução em QA.
7. **Dedup por exército, não por unidade.** A marca `cloneSource` do MobControl-Clone (`DoorScript.cs`) é a semente da ideia; generalizamos: 1 evento por exército (1 trigger no portal detectando o layer/líder da multidão), nunca N OnTriggerEnter × N clones.
8. **Largura paramétrica:** propagação `StageController.UpdateStageSize → SetWidth` reposicionando os meios-portais (HyperCasualRunningGame) — útil para larguras de pista por mundo no retrato.

### 3.3 Runner core / pista (LevelManager, input, câmera, segmentos)

1. **Input: drag-âncora contínuo, sem lanes.** Pipeline GetInput→delta re-ancorado por frame→clamp de deslocamento por frame→clamp de X mundial (`Movement.cs` do MobControl-Clone — MIT, reaproveitável direto). Detalhe que evita teleporte: usar `touch.deltaPosition`/delta normalizado por `Screen.width`, nunca posição absoluta (`PlayerMovement.cs` do Unity-3D-HyperCasual; `InputCanvasController.cs` do HyperCasualRunningGame). Implementar sobre o novo Input System/Enhanced Touch com interface estilo `IInputDetector` (InfiniteRunner3D) + implementação de teclado para testes no editor.
2. **Segmento-prefab com âncoras, populado pelo LevelManager.** Prefab de segmento expõe âncoras de GatePair (esq/dir) e obstáculos como `Transform[]` (contrato do `PathSpawnCollider`/`StuffSpawner` do InfiniteRunner3D) — mas populadas por LevelConfigSO/GateConfigSO, nunca por Random no Start (Boss Scout exige portais planejados).
3. **Spawn por distância, reciclagem por distância.** Manter N segmentos à frente do líder; reciclar (pool) quando ficar X metros atrás. Anti-padrões documentados: spawn por trigger (frágil com velocidade variável) e despawn por timer fixo (`TimeDestroyer.cs` do InfiniteRunner3D — o chão some sob o player se a velocidade muda).
4. **Zona de segurança** após cada portal/curva para o exército se reagrupar — generalização da regra "nunca obstáculo no primeiro spawn point" (`StuffSpawner.cs` do InfiniteRunner3D).
5. **Câmera: rig independente com damping, nunca filha do player.** Follow suavizado do centróide da multidão (com cache do frame anterior para quando a lista zera — anti-NaN do `PlayerManager.PlayerCenterPos()` do HyperCasualRunningGame) + enquadramento dinâmico conforme o exército cresce. Suavização framerate-independente por `Exp(−k·dt)` (keijiro). Anti-exemplo: Main Camera com `m_Father` no player (straightPathsLevel.unity do InfiniteRunner3D) herdando solavanco do pulo.
6. **Estados de fase no GameManager, transições validadas.** Enum/máquina explícita Run → GateChoice → BossArena → Victory/Defeat — nunca `CheckFail` setando estado todo frame sem checar o atual (HyperCasualRunningGame, `GameManager.cs`), nunca derrota via `SceneManager.LoadScene` dentro do script do inimigo (MobControl-Clone). O padrão "seção trava o runner (Play_inactive), resolve, devolve via CheckPass" (HyperCasualRunningGame, `EnemyManager.cs`) é o esqueleto da transição corrida→arena.
7. **Soft reset, não reload de cena.** Com pooling, retry de fase de 60–90 s deve resetar estado, não recarregar cena (anti-exemplo: todos os clones). Níveis = LevelConfigSO carregados numa cena única de gameplay (anti-exemplo: 1 cena por nível do Count-Master-Clone).
8. **Mundo-se-move é alternativa registrada** para fases muito longas/float precision (Unity-3D-HyperCasual, `Obstacles.cs`); no MVP o exército avança (fases de 45–75 s não sofrem de precision).

### 3.4 CombatSystem / Boss (BossManager, elementos, arena)

1. **Agregados HP/DPS com ticks, não colisão unidade-a-unidade.** Aquisição de alvo centralizada (CombatSystem + partição espacial) aplicando o chart elemental (CANON §4 / ElementChartSO) no cálculo do tick. Os anti-exemplos confirmam a decisão: cooldown de contato por par espelhado (MobControl-Clone, `PlayerController.cs`/`EnemyController.cs` — com Find por tag em Update), pareamento 1v1 por coroutine com bug admitido em TODO (HyperCasualRunningGame, `PlayerController.cs` linhas 56–65), e corrida cosmética de contadores (CountMaster).
2. **Waves da arena como dados ordenados em BossConfigSO.** Lista ordenada de eventos com ponteiro de próximo (`if spawnTimer >= next.time`) — nunca `(int)timer == x` em polling (contraexemplo perfeito: `EnemyCastleScript.cs` do MobControl-Clone, que ainda por cima tinha a classe certa `EnemySpawnEvents` criada e nunca usada). Encerramento de onda por contagem de eventos `OnDeath` assinados por spawned (HyperCasual-Engine, `CharacterSpawningWave.cs`) — mesmo mecanismo detecta "exército do jogador zerado".
3. **Entrada na arena = handoff sim→cinemática.** Congelar a simulação de separação, easing das unidades até slots de formação de combate, movimento determinístico — estrutura do `RideOnEscalator()` (unity-crowd-simulation, `EscalatorAI.js` — reimplementar). Entrada do boss ≤2 s (CANON §6).
4. **Boss nunca é "unidade com stats inflados".** O MobControl-Clone (prefab BigEnemy) confirma o vácuo que o Boss Scout preenche: nosso boss exige telegraph, fraqueza elemental visível e entrada cinematográfica. Escala ∝ HP (o `getHit()` do isBig) entra só como COMPLEMENTO da barra de vida gigante canônica — e nos inimigos comuns "desmontando em peças" sem sangue (CANON §1).
5. **Telegraphs e cooldowns como timers puros testáveis.** Classe C# sem MonoBehaviour com `Tick(dt)` externo (HyperCasual-Engine, `Countdown.cs`) — unit-testável para o ataque especial telegrafado.
6. **Fluxo de fase por Task/Decision orientado a eventos** (HyperCasual-Engine, `State.cs`/`Decision.cs`/`WaveEnded.cs`): Run → BossIntro → BossFight → Result, com Decision "BossDied" escutando `BossManager.OnDeath` — implementado com enums/classes tipadas, nunca strings.
7. **`Unit.OnDeath` como ponto de extensão** (HyperCasual-Engine, `Health.cs`): alimenta CrowdManager (Supply liberado), VFXManager (desmonte em peças) e Analytics sem acoplamento — mas com pool + estado "dying" para a animação, nunca `Destroy` direto.
8. **Medidor de carga como mini-loop de dopamina** (MobControl-Clone, `Shooting.cs` — MIT): ações baratas acumulam → libera espetáculo ao soltar; adaptável como medidor de fúria/ult na arena ou fanfarra do overflow de Supply.
9. **Hits por máscara de layer + chart elemental**, nunca dano genérico em 4 callbacks de contato (anti-exemplo: `DamageOnTouch` do HyperCasual-Engine). Registrar o resultado da fase numa struct desacoplada estilo `DeathEvent` (Trash Dash, `CharacterCollider.cs`): serve analytics, tela de vitória e missões ao mesmo tempo.

### 3.5 Fluxo de UI / meta (UIManager, RewardSystem, SaveSystem, progressão)

1. **Fluxo-de-jogo-como-grafo-de-estados com eventos ScriptableObject.** O GameManager monta explicitamente Boot → Home → BossScout(OVL-01) → Run(SCR-02) → BossArena(SCR-03) → Victory(SCR-04)/ReviveOffer(OVL-05) → Defeat(SCR-05), com transições por eventos (Unity3d-RunnerTemplate-2023, `SequenceManager.AddLevelPeripheralStates` + `EventLink` — reimplementar). Detalhe fino a replicar: o EventLink difere a transição para o frame seguinte, então listeners síncronos (commit de economia, analytics) rodam ANTES da tela aparecer — ordem garantida "dados prontos → tela mostra". Armadilha documentada: `AbstractGameEvent.Reset()` logo após Raise zera o payload para listeners assíncronos.
2. **State machine em PILHA para revive.** Empurrar GameOver/ReviveOffer SOBRE o estado da corrida preserva a run — exatamente o que o revive no boss por rewarded (1×/fase, CANON §11) precisa; replicar o trio `isRerun + SecondWind + invencibilidade temporária` e o guard anti duplo-clique `m_GameoverSelectionDone` (Trash Dash, `GameManager.cs`/`GameState.cs` — reimplementar).
3. **RunWallet: moedas temp vs comitadas; XP sempre comitada.** Ganhos da corrida (incl. conversão de Supply) acumulam num run wallet comitado só na vitória; "DOBRAR ×2 com anúncio" = comitar 2× o wallet; derrota descarta moedas mas nunca a XP (suaviza o SCR-05 sem tom punitivo). Origem: `Inventory.OnWin/OnLose` do Runner Template. Exceção canônica: moedas de pickup/overflow **creditadas na hora com fanfarra** (lição do changelog 1.1 do Trash Dash: "keep what was gathered").
4. **Tela de resultado mostra o DELTA, é passiva e nunca congela timeScale.** "+100 moedas" como número principal (não o total — anti-exemplo do Runner Template); propriedades-setter preenchidas por sistema externo ANTES de exibir; coreografia (confete, contagem rolando, voo de moedas — ResultSequencePlayer pulável de ~2 s) exige NÃO pausar timeScale (ou animar com unscaled time) — o `PauseState` com timeScale=0 do template mataria tudo.
5. **UIManager com DUAS pilhas (telas + overlays).** O `UIManager.Show<T>` de View única do template não suporta OVL-01..06 sobre SCR-02/03/04 (doc 09); precisamos de pilha de telas com history + pilha de overlays, com transições (slide 200 ms / fade 150 ms). Aproveitar: registro automático de Views, safe area resolvida UMA vez no root (`UIUtils.ResizeToSafeArea` — nosso P10), botão base com som embutido (`HyperCasualButton` — metade do nosso P8).
6. **Progressão: "só avança se fase == fronteira"** (`OnWinScreenDisplayed` do template) — rejogar fase antiga com recompensa reduzida sem corromper progresso (SCR-09); mas a mutação do progresso pertence ao LevelManager/SaveSystem, nunca a um callback de exibição de UI. `LevelSelectButton.SetData(index, unlocked, callback)` é o padrão dos nós do mapa.
7. **SaveSystem: JSON + checksum + schemaVersion com migração incremental.** A lição mais valiosa de produção do estudo: gates `if (ver >= N)` campo a campo, patch de dados legados, NewSave como fallback (Trash Dash, `PlayerData.cs` — trocando binário por JSON). Gravação atômica (temp+rename), flush em `OnApplicationFocus(false)` + `OnApplicationQuit` (HyperCasual-Engine, `GameManager.cs`), save assíncrono com dirty flag centralizado em transições de estado (nunca I/O síncrono no meio da corrida). Base de código: SaveGameFree (MIT) + nossa camada de checksum + merge Firestore. Anti-exemplos: PlayerPrefs cru (template), persistência fantasma (ScoreScript do MobControl-Clone), bug de chave duplicada + cast `is T` sobre JObject (SaveLoadManager do HyperCasual-Engine — testar exatamente isso na nossa suíte).
8. **Missões diárias como classes C# puras com factory por enum, serializadas no save**, N ativas com reposição no claim (Trash Dash, `Missions.cs`) — modelo direto das missões de gemas (CANON §8). Mutações = lifecycle Started/Tick/Ended com ícone HUD auto-gerenciado (Trash Dash, `Consumable.cs`); a 4ª substitui a mais antiga (CANON §3.3).
9. **Analytics com taxonomia transacional**: transactionId GUID + contexto + saldo final em toda transação de economia; dados coletados sempre, envio sob flag (struct desacoplada) — Trash Dash, `ShopItemList.cs`/`IAPHandler.cs`.
10. **Conteúdo por label Addressables (1 label por mundo)** carregado em Dictionary com flag `loaded` consultada pela UI, e disciplina rígida de ReleaseInstance em todo caminho de despawn (Trash Dash, `ThemeDatabase.cs`) — modelo dos WorldConfig/UnitConfig por mundo.
11. **Tooling barato desde a semana 1**: [MenuItem] de cheats (limpar save, dar moedas — Trash Dash), janela "MAR Tools" com setup de fase por código com Undo + limpar PlayerPrefs/persistentDataPath (HyperCasual-Engine, `MagicButtons.cs`), custom inspector com sliders para posicionar portais/obstáculos (Trash Dash, `TrackSegmentEditor`), pintor de prefabs com preview fantasma + grid snap (HyperCasual-Engine, `LevelCreator.cs`), e painel de tuning em runtime no device para Supply/economia (HyperCasualRunningGame, `FieldBox.cs`/`GateElement.cs`).
12. **Bootstrap/composition root com ordem explícita para os 18 managers** (CANON §13) — nunca singletons `public static X i` setados em Awake sem ordem (HyperCasualRunningGame), nunca `instance = new MonoBehaviour()` (InfiniteRunner3D), nunca FindObjectsOfType no getter (Boids-Unity). Estados/labels por enum/const/AssetReference tipado, nunca string mágica.

---

## 4. Stack recomendada final

Avaliação da sugestão do cliente (CANON §13). **Veredito geral: a stack canônica está correta e fica mantida.** Concordâncias, divergências e refinamentos:

| Camada | Sugestão do cliente (CANON §13) | Veredito | Por quê |
|---|---|---|---|
| Engine | Unity 2022 LTS + URP, C#, portrait | ✅ Concordamos | Compatível com todos os assets/SDKs avaliados (Firebase exige 2021 LTS+; Unity Toon Shader e Particle Pack suportam URP/2022). |
| Backend | Firebase (Auth anônimo, Firestore, Analytics, Remote Config, FCM, Crashlytics) | ✅ Concordamos | Apache 2.0 + plano Spark gratuito cobre o soft launch com folga (50K MAU Auth, 50K leituras/dia Firestore). FCM adiado para F4. |
| Mediação de ads | AppLovin MAX (AdMob, Meta, Unity Ads como redes) | ✅ Concordamos — **mantido após avaliação formal** | Ver §6. Único ajuste: docs migraram para support.axon.ai — atualizar links do §13. |
| IAP | RevenueCat | ✅ Concordamos | SDK MIT; free tier (US$ 2.500 MTR/mês) cobre todo o soft launch (ARPDAU IAP projetado US$ 0,032). |
| Save | Local-first JSON + checksum + sync Firestore | ✅ Concordamos, com refinamento | Adicionar schemaVersion + migração incremental e gravação atômica (lições do Trash Dash). Base: SaveGameFree (MIT). Serialização: JsonUtility com wrappers; decisão na semana 2 entre wrappers vs Full Serializer (MIT) vs Newtonsoft se os dicionários de fragmentos travarem. |
| Tween | (não especificado pelo cliente) | ➕ Adição: **DOTween free** | Licença Demigiant permite uso comercial sem restrição (embarcar no APK ≠ redistribuição); coração do game feel desde a semana 1. LeanTween (MIT) é plano B só se precisarmos redistribuir engine de tween modificado — cenário improvável. Um único tween engine no projeto. |
| Shader | (não especificado) | ➕ Adição: **Unity Toon Shader (UCL)** com fallback **URP_Toon (MIT)** | UCL permite uso comercial em projetos Unity (nosso caso exato). Com centenas de unidades: variante simplificada nas pequenas, shader completo em bosses/tropas grandes; se o profiling em celular mediano reprovar (semana 1), URP_Toon nas unidades da multidão (MIT = podemos forkar). |
| Pooling | (não especificado) | ➕ Adição: **`UnityEngine.Pool` nativo** (ObjectPool<T>, Unity 2021+) | Zero dependência; RecyclerKit (MIT) fica como referência de API. Pooling é obrigatório (pilar do espetáculo "1 vira 10.000"). |
| Eventos | (não especificado) | ➕ Adição: **Signals (MIT, ~200 linhas absorvidas no projeto)** + eventos ScriptableObject para fluxo de UI | Desacopla os 18 managers (gate_selected → Analytics/VFX/Audio/UI sem referências cruzadas). Copiar com notice MIT, tratar como código nosso. |
| SO Architecture | 10 ScriptableObjects canônicos | ✅ Concordamos | ScriptableObject-Architecture (MIT) como referência de padrão (adotar o padrão, copiar só o que usarmos). Regra dura: SOs read-only em runtime; estado vivo no manager (anti-exemplo: BoidSettings mutado por sliders). |
| Conteúdo | (não especificado) | ➕ Adição: **Addressables com 1 label por mundo** | Modelo do Trash Dash: carregar só os configs/assets do mundo atual, ReleaseInstance ao trocar. |
| Multidão | (não especificado) | ➕ Decisão técnica: **unidades cinemáticas em manager central; layout de dados Jobs-ready; sem física por unidade, sem NavMesh** | Consenso de TODOS os repos de crowd estudados (ver §3.1). Supply 60 do MVP roda single-thread; teto de 300 (pós-MVP) já coberto pelo layout SoA + grid espacial. |
| Template de partida | (não especificado) | ⚠️ **Unity Runner Template (Hub) só como greybox/referência** | Acelera a validação do pacing da fase 1 na semana 1, mas NUNCA shippar arte/greybox dele (termos do Hub + exigência de originalidade do BRIEF). O re-upload de terceiro no GitHub (Unity3d-RunnerTemplate-2023) é apenas-estudo, risco dobrado. |
| Proibições | — | ❌ **UnityCsReference: leitura apenas** (Reference-Only License — nenhuma linha no jogo). ❌ GPL/CC-BY-SA/CC-BY-NC em qualquer camada. | Texto literal da licença Unity proíbe modificar/redistribuir; deixar explícito no onboarding do repo. |

**Onde divergimos do cliente:** em nada estrutural. Os ajustes são (a) atualizar os links de documentação do MAX no §13 (rebrand Axon), (b) explicitar as camadas que o CANON não nomeava (tween, shader, pooling, eventos, Addressables, arquitetura de multidão) e (c) registrar formalmente os planos B (LevelPlay para mediação, LeanTween para tween, URP_Toon para shader, Full Serializer para save) para que troca futura não vire debate.

---

## 5. Starter pack de assets do MVP

Mapeamento necessidade do MVP (CANON §15) → fonte específica → licença/requisito. Tudo registrado na planilha de assets.

| Necessidade do MVP | Asset / Fonte | Licença | Requisito |
|---|---|---|---|
| **5 tropas** — Soldado | KayKit Adventurers: **Barbarian** | CC0 1.0 | Nenhum (não revender o pack cru) |
| Arqueiro | KayKit Adventurers: **Ranger** (tem arco) | CC0 1.0 | Nenhum |
| Escudeiro | KayKit Adventurers: **Knight** (tem escudo) | CC0 1.0 | Nenhum |
| Mago | KayKit Adventurers: **Mage** | CC0 1.0 | Nenhum |
| Gigante | Barbarian em escala 2.5× + recolor, OU monstro do Quaternius Ultimate Monsters | CC0 1.0 | Nenhum |
| 10 skins (recolor + acessório do Soldado) | 25+ acessórios/armas do KayKit Adventurers | CC0 1.0 | Nenhum |
| **Animações das tropas** (locomoção 8-dir, combate, morte) | Quaternius **Universal Animation Library** (FBX/GLB, Humanoid retarget) | CC0 1.0 | Nenhum — elimina Mixamo do MVP |
| Animações extra (dança de vitória, hit reactions) — pós-MVP | Mixamo (Adobe) | Termos Adobe | Sem crédito; FBX **fora de repo público**; não redistribuir standalone |
| **3 mundos** — M1 Campo Inicial | Quaternius **Ultimate Stylized Nature Pack** | CC0 1.0 (conferir na página do pack) | Nenhum |
| M2 Cidade Zumbi | Quaternius **Zombie Apocalypse Kit** | CC0 1.0 | Nenhum |
| M3 Deserto Robótico | Quaternius **Modular Sci-Fi Megakit + Animated Mech Pack** | CC0 1.0 | Nenhum |
| Props pontuais que faltarem (carros, sucata, pedras) | Poly Pizza — **somente filtro CC0** (poly.pizza/search/CC0) | CC0 (varia por modelo!) | Conferir modelo a modelo; CC-BY excepcional → copiar /credits para a tela de Créditos |
| **5 bosses** — Golem de Pedra, Gigante de Madeira, Brutamontes Zumbi, Zumbi Titã, Robô Escorpião | Quaternius **Ultimate Monsters** (50 monstros animados), escalando tamanho/vida/cor (variantes regionais, CANON §6) | CC0 1.0 | Nenhum; extras viram tropas épicas pós-MVP |
| **UI das 7 telas** (inicial, gameplay/HUD, vitória, derrota, tropas, upgrades, loja) | Kenney **UI Pack** (430 elementos), recolorido nos tons de raridade (cinza/azul/roxo/dourado) | CC0 1.0 | Nenhum |
| Ícones de gesto do tutorial (arrastar, tap no Boss Scout) | Kenney **Input Prompts** (1500 ícones; absorveu o antigo "Onscreen Controls", que hoje dá 404) | CC0 1.0 | Nenhum |
| Ícones de sistema (4 elementos, 4 trilhas, classes de portal, Supply) | **Game-icons.net** — OU pack "Game Icons" da Kenney (CC0) | **CC BY 3.0** (única fonte com crédito OBRIGATÓRIO) | Crédito "Icons made by {Lorc, Delapouite…}. Available on https://game-icons.net" na tela de Créditos + descrição da Play Store; **decidir na semana 1** se trocamos pela Kenney para zerar a obrigação |
| **SFX** — UI (clique, navegação, baú, compra) | Kenney **Interface Sounds** (100 sons) | CC0 1.0 | Nenhum |
| SFX — impactos (hit no boss, portais, desmonte sem sangue, golpe final) | Kenney **Impact Sounds** (130 sons) | CC0 1.0 | Nenhum |
| SFX — game feel núcleo (moeda, whoosh de portal, pop de multiplicação, fanfarras, rugido de boss) | **Freesound.org — somente filtro CC0**; normalizar em −16 LUFS no AudioManager | Varia (CC0/CC-BY/CC-BY-NC) | CC-BY-NC PROIBIDO; CC-BY → attribution list gerada pelo site → tela de Créditos |
| SFX premium + risers para criativos de ads | **Mixkit** | Mixkit Free License (proprietária) | Sem crédito; embarcar no build/vídeos OK; nunca redistribuir os arquivos |
| Música de fundo por mundo (loops M1/M2/M3) — fase de polish | **OpenGameArt.org — filtro CC0 apenas** | Varia (CC0…GPL) | CC-BY-SA e GPL PROIBIDOS; CC-BY → crédito completo (título+autor+link+modificações) |
| **VFX** — partículas (multiplicação, moedas, desmonte, telegraph, brilho de portal) | Kenney **Particle Pack** (80 arquivos, sprites) | CC0 1.0 | Nenhum |
| VFX 3D — explosões de hit nos bosses, glow dos 8 portais, impacto de Fogo | **Unity Particle Pack** (Asset Store, URP) | Asset Store EULA ("Extension Asset") | Sem crédito; não redistribuir como pacote; **retexturizar/recolorir para não parecer asset-flip** |
| Look cel-shading + outline (tropas e bosses) | **Unity Toon Shader** (com.unity.toonshader, UPM); fallback leve: URP_Toon (ChiliMilk) | UCL / MIT | UCL: só em projetos Unity, manter notices; MIT: manter copyright; profiling em celular mediano na semana 1 |
| Arte 2D para loja/criativos UA (pós-MVP) | CraftPix freebies | Licença CraftPix | Crédito opcional; nunca redistribuir os fontes; não usar para treinar IA |

**Pipeline de sourcing:** filtro CC0 do itch.io (https://itch.io/game-assets/assets-cc0) como porta de entrada → confirmar licença na página individual → planilha. Foi assim que KayKit, Quaternius e a UAL foram validados.

---

## 6. SDKs / backend — decisão de mediação, ordem de integração e armadilhas

### 6.1 Decisão de mediação de ads: **MANTER AppLovin MAX (CANON §13 confirmado)**

Avaliação objetiva das três opções para um hybrid-casual indie:

1. **Facilidade:** MAX e LevelPlay empatam na integração Unity; a UI de mediação do AdMob é a mais trabalhosa das três; o MAX vence no QA com o **Mediation Debugger** on-device.
2. **Fill/eCPM:** MAX é o padrão do gênero e faz bidding em tempo real exatamente com o nosso trio AdMob + Meta + Unity Ads; a Meta monetiza melhor via MAX do que via mediação AdMob — crucial para o mix 40% BR/LatAm + 35% SEA do doc 08.
3. **Conta:** AppLovin é grátis e imediata; AdMob exige aprovação; LevelPlay exige conta Unity Cloud.
4. **Consent:** o MAX tem fluxo TCF/UMP integrado (menos código); o LevelPlay exige APIs manuais (setConsent/setMetaData); a mediação AdMob tem o melhor UMP nativo, mas perde nos demais critérios.

**Plano B documentado:** Unity LevelPlay — seria a escolha se Unity Ads fosse nossa rede dominante ou quiséssemos pipeline 100% UPM sem EDM4U. Trocar agora geraria retrabalho no AdsManager sem ganho mensurável de fill (e o doc 13 já fixa "MAX servindo rewarded em produção" como gate do soft launch F2).
**Consequências práticas:** o plugin C# do AdMob NÃO entra no build (AdMob entra como rede via adapter nativo do MAX); o que permanece necessário é criar a conta AdMob e os ad units (rewarded dobrar, rewarded reviver, interstitial) que o adapter consome. **Ajuste ao §13:** atualizar os links de docs do MAX — migraram de developers.applovin.com para support.axon.ai (rebrand Axon, redirect 301 confirmado).

**Placements canônicos no AdsManager (doc 08):** `rewarded_double_reward` (Vitória, dobra moedas §8) · `rewarded_revive_boss` (Derrota, 1×/fase §11) · `interstitial_level_end` (fase ≥6, 1 a cada 3 fases, caps 3/sessão e 9/dia, nunca após 2 derrotas — 100% Remote Config).

### 6.2 Ordem de integração no MVP (semanas do doc 13)

| Semana | Integração |
|---|---|
| **S1** | Firebase via .unitypackage por produto (Analytics, Remote Config, Crashlytics, Auth, Firestore — só os usados): registrar app Android, google-services.json, `CheckAndFixDependenciesAsync()` no bootstrap antes do primeiro `level_start`; Auth anônimo silencioso no 1º boot (regra "jogar em 5 s"); Remote Config com `SetDefaultsAsync` → fetch no boot → activate na próxima sessão, defaults empacotados idênticos ao CANON (chaves do doc 08 §9: ads_inter_min_level=6, ads_inter_level_interval=3, ads_inter_session_cap=3, ads_inter_daily_cap=9, ads_inter_after_rewarded_s=60, recompensa_base, vida de boss, iap_catalog_version). **EDM4U único** + minSdk 23 explícito. |
| **S2** | AppLovin MAX: SDK Key + rewarded "dobrar" funcionando + Mediation Debugger no device. |
| **S3** | Rewarded "reviver" + interstitial com a política do doc 08 §3 + adapters AdMob e Unity Ads. **Meta por último** — a verificação de negócio na Meta demora dias; iniciar o processo na S1. |
| **S4** | Fluxo de consent UMP/TCF antes do init + ATT (fase iOS) + app-ads.txt publicado no domínio + RevenueCat com escopo mínimo: entitlement `remove_ads` (US$ 4,99 + 200 gemas, zera interstitials) + restore purchases; slots de loja desligados por Remote Config até o soft launch. Loja IAP completa só na F4. |

### 6.3 Armadilhas conhecidas

- **minSdk Android = 23 explícito** no Player Settings (Firebase e GMA exigem; não confiar no default da Unity).
- **EDM4U duplicado** entre Firebase e MAX: manter UMA cópia, Custom Main Gradle Template + Jetifier habilitado, Force Resolve após cada adapter novo. RevenueCat também entra nessa resolução (purchases-hybrid-common).
- **Tamanho de build:** cada adapter soma 2–6 MB no AAB — começar com 3 redes, R8/minify ligado, medir a cada adapter.
- **Consent GDPR antes de carregar qualquer ad** — sem TCF string, o fill na UE desaba e o Google pode limitar serving; declarar tudo no Data Safety da Play; público 13+ → sem tag child-directed.
- **Nunca clicar em ads reais em teste** (ban AdMob): test mode + dispositivos de teste registrados.
- **Remote Config offline-first:** defaults locais obrigatórios via SetDefaultsAsync — o jogo funciona antes do 1º fetch e sem rede.
- **Cotas Spark:** Firestore 1 GiB / 50K leituras/dia / 20K escritas/dia — folga no soft launch (save pequeno, sync em transições), mas monitorar; acima disso, Blaze.
- **Quickstarts Firebase (Apache 2.0):** pode copiar/adaptar (preservando cabeçalho), mas NUNCA reutilizar bundle IDs/google-services.json dos samples.
- **Não subestimar o AdsManager:** nenhum template de runner traz ads prontos (o README do Runner Template promete e não entrega) — rewarded de dobrar/reviver é integração nossa, do zero.

---

## 7. Aviso final anti-cópia

Ligado diretamente ao requisito de **originalidade do BRIEF** e à identidade do CANON §1: o Mutant Army Run compete num gênero de clones — nossa defesa (e nosso pitch) é sermos o original entre eles. Por isso, do material de `_research\repos` e de qualquer referência:

**NUNCA migrar para o projeto:**

1. **Nomes** — nomes de jogos, classes, prefabs, cenas, variáveis ou strings dos repos estudados ("MobControl", "CountMaster", "StickMan", "Trash Dash"…). Nosso vocabulário é o glossário do CANON §14 (Gate, Unit, Supply, Mutation…).
2. **Arte, áudio, modelos, texturas, fontes e cenas** de QUALQUER repo clonado — mesmo dos MIT (a licença MIT dos repos cobre o código; a arte deles é de origem desconhecida ou de terceiros — ex.: o Agent.fbx do keijiro/Boids não tem licença; o Trash Dash é Asset Store EULA; o Runner Template re-hospedado é conteúdo proprietário da Unity).
3. **Layout de fases, sequências de portais, valores de balance e curvas** dos jogos/clones estudados — nossas fases nascem do LevelConfigSO + Boss Scout (1 rota ótima + 1 armadilha), com nossos números (CANON §8–§10).
4. **Código de repositórios SEM LICENÇA** — nem uma linha, nem "só essa função": Count-Master-Clone-Game, count-masters, InfiniteRunner3D, EndlessRunnerSampleGame, Unity3d-RunnerTemplate-2023, Unity-3D-HyperCasual_MobileGame, unity-crowd-simulation, Boids-Unity. Estudar → fechar o arquivo → escrever a nossa versão.
5. **Código do UnityCsReference** — proibição literal da licença (reference only); ler para entender, implementar do zero.
6. **FBX do Mixamo em repo público** — commit em repo público = redistribuição proibida.

**Checklist de PR (colar no template do repositório):** (a) algum arquivo novo veio de `_research\`? → rejeitar; (b) código adaptado de repo MIT? → notice presente em `THIRD-PARTY-NOTICES.md`; (c) asset novo? → linha na planilha de assets com licença confirmada na página de origem; (d) nome/string parece vindo de outro jogo? → renomear pelo glossário §14.

Os clones existem para nos ensinar **onde os outros erraram** — o catálogo de antipadrões do §3 é, na prática, a especificação invertida do nosso código. O que vai para o build é 100% nosso ou 100% licenciado, registrado e creditado. Sem exceções, nem na semana 4 com o prazo apertando.
