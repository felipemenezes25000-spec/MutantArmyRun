# 14 — Riscos, Anti-Clone, Vício & Inteligência · Mutant Army Run

> Cobre os entregáveis **26 (Riscos)**, **27 (Como evitar parecer clone barato)**, **28 (Como deixar mais viciante)** e **29 (Como deixar mais inteligente)** do BRIEF, mais o **checklist testável de game feel** e o capítulo de **compliance** (LGPD/GDPR/COPPA, lojas, políticas de anúncio).
> Fonte da verdade de nomes/números: `CANON.md`. Requisitos: `BRIEF.md`. Referências cruzadas: doc 01 (nomes/marca), doc 03 (tropas/stats), doc 04 (portais).
> Versão 1.0 — 2026-06-11.

---

## 1. Riscos do projeto (entregável 26)

### 1.1 Metodologia

- **Probabilidade (P):** Baixa (≤25%), Média (25–60%), Alta (>60%) — estimada para a janela MVP + soft launch (90 dias).
- **Impacto (I):** Baixo (atraso <1 semana ou perda <5% de KPI), Médio (atraso 1–3 semanas ou KPI 5–20% fora do alvo), Alto (ameaça o go/no-go do projeto).
- **Score = P × I** (1–3 × 1–3). Riscos com score ≥ 6 têm plano de contingência detalhado (§1.3).
- Cada risco tem **dono** (papel, não pessoa) e **sinal de alerta mensurável** — um risco sem sinal instrumentado é um risco não gerenciado.
- Ritual: revisão da matriz **toda sexta** na weekly de produto; qualquer sinal de alerta disparado vira item de pauta obrigatório.

### 1.2 Matriz de riscos

| ID | Categoria | Risco | P | I | Score | Sinal de alerta (mensurável) | Mitigação concreta |
|---|---|---|---|---|---|---|---|
| R-01 | Mercado/UA | CPI acima do alvo (gênero crowd-runner saturado em UA) | Alta | Alto | 9 | CPI > US$ 0,60 (BR) ou > US$ 2,00 (US) após 2 semanas de teste de criativos | Testar ≥8 conceitos de criativo ANTES do soft launch (matriz no doc de ads); Boss Scout como hook nos 3 primeiros segundos de todo vídeo; alocar 20% do budget a formatos "escolha" e "erro" que historicamente têm CTR maior no gênero |
| R-02 | Mercado/UA | Retenção D1 abaixo de 40% (CANON §12) | Média | Alto | 6 | Coorte de soft launch com D1 < 35% por 3 dias seguidos | Funil do tutorial 100% instrumentado (tutorial_start→complete, level_1→3); taxa de vitória 95% nas fases 1–3 controlada por Remote Config; "jogar em ≤5 s" como requisito de aceite (GF-01) |
| R-03 | Mercado/UA | ARPDAU < US$ 0,08 — monetização não fecha a conta de UA | Média | Alto | 6 | ARPDAU < US$ 0,05 na 2ª semana de soft launch | Conversão de rewarded é a alavanca nº 1 (alvo ≥35% dos DAU): reposicionar os 5 placements via Remote Config sem precisar de update; interstitials só sobem de frequência se D3 estiver ≥ alvo |
| R-04 | Mercado/UA | Publisher/concorrente grande lança feature parecida com Boss Scout durante nossa produção | Baixa | Médio | 2 | Monitoramento quinzenal de top-100 casual nas lojas (responsável: PM) | MVP em 30 dias e soft launch imediato; nosso diferencial é o **par** Boss Scout + Supply (difícil copiar os dois rápido); acelerar registro de marca (doc 01) |
| R-05 | Design | Jogadores ignoram o Boss Scout e jogam só "maior número" — a camada inteligente não é percebida | Média | Alto | 6 | % de escolhas da rota ótima não cresce entre fase 3 e fase 10 (evento gate_selected com flag `optimal`) ; teste de usabilidade: <6 de 10 jogadores explicam a fraqueza do boss após a fase 3 | Fase 2 ensina por consequência (portal de Fogo visivelmente derrete o boss de gelo regional); lembrete re-abrível na barra de progresso (CANON §3.1); recompensa visível ("SUPER EFETIVO!" + bônus de moedas) ao explorar fraqueza — BOSS BREAKER segue o trigger canônico do doc 09 §4.2 (golpe final no boss) |
| R-06 | Design | Dificuldade mal calibrada → churn em fase específica | Alta | Médio | 6 | Taxa de vitória fora dos alvos do CANON §12 (95/85/~70/~55) em qualquer fase; spike de level_fail + desinstalação na mesma coorte | HP de boss, dano e moedas por fase 100% em Remote Config; dashboard de funil por fase desde o dia 1 do soft launch; ajuste semanal com changelog de balanceamento |
| R-07 | Design | Supply percebido como punição ("o jogo roubou minhas tropas") | Média | Médio | 4 | Reviews citando "perdi unidades"; queda na escolha de portais de quantidade após fase 5 | Conversão do excedente SEMPRE com fanfarra (chuva de moedas + "+120 MOEDAS!" — CANON §3.2); tutorial do Supply mostra o ganho, nunca a perda; nunca usar vermelho/ícone de erro na conversão |
| R-08 | Design | Economia inflaciona: jogadores maximizam as 4 trilhas do MVP cedo demais e perdem objetivo | Média | Médio | 4 | >15% dos jogadores D7 com as 4 trilhas no nível em que o custo > saldo médio ganho/dia; moedas acumuladas crescendo sem gasto | Curva 100 × 1,35^n validada em planilha contra ganho/fase (100 × 1,10^(fase−1)); sink extra: skins por moedas; recalibração por Remote Config |
| R-09 | Técnico | Performance: multidão de 300–500 unidades derruba FPS em devices medianos | Alta | Alto | 9 | FPS médio < 55 no device de referência Android; tempo de frame > 20 ms na cena do boss | GPU instancing + pooling desde o primeiro protótipo; LOD agressivo (unidade vira impostor billboard além de 25 m); **cap visual** de unidades renderizadas com contador "x N" estilizado acima da multidão; budget de partículas por cena (ver §5.3) |
| R-10 | Técnico | Crash/ANR acima dos limiares do Google Play (Android vitals) → loja derruba visibilidade orgânica | Média | Alto | 6 | Crash rate ≥ 1,09% ou ANR ≥ 0,47% de sessões (limiares de "bad behavior" do Play) no Crashlytics/Play Console | Crashlytics ativo desde a build 1; smoke test em device farm (mín. 8 devices: 2 low-end, 4 mid, 2 high) a cada release candidate; gate de release: 99% crash-free sessions |
| R-11 | Técnico | Breaking change ou conflito entre SDKs (AppLovin MAX, Firebase, RevenueCat) atrasa release | Média | Médio | 4 | Build quebra após bump de SDK; warnings de deprecação nos dashboards das redes | Versões pinadas; branch isolado para upgrades de SDK com checklist de regressão (ads carregam, IAP restaura, eventos chegam); nunca atualizar SDK na semana de release |
| R-12 | Técnico | Save corrompido / perda de progresso → reviews 1 estrela e churn de pagantes | Baixa | Alto | 3 | Evento `save_checksum_fail` > 0,1% das sessões; tickets de suporte com "perdi progresso" | Local-first JSON com checksum + cópia de backup atômica (escreve em temp, valida, renomeia); sync Firestore como restauração; teste de migração de save em toda mudança de schema |
| R-13 | Legal/IP | Alegação de trade dress / semelhança excessiva com jogos do gênero | Baixa | Alto | 3 | Notificação extrajudicial; comentários "isso é cópia de X" em volume nos anúncios | Auditoria anti-clone (§2.8) antes do soft launch; mascote, paleta, logo e bosses 100% originais; nenhuma fase, nome ou layout reproduzido de referência; parecer jurídico sobre o nome final (doc 01) |
| R-14 | Legal/IP | Asset de áudio/fonte/modelo sem licença comercial entra no build | Média | Médio | 4 | Asset sem entrada na planilha de licenças no check de release | **Registro de licença por asset** (planilha: asset, origem, licença, comprovante); só CC0 ou licença comercial comprovada; fontes com licença de app embarcado; check automatizado de assets novos no pipeline |
| R-15 | Plataforma/lojas | Rejeição ou strike por política de anúncios enganosos (fake ads) nas redes (Google Ads, Meta, TikTok) | Média | Alto | 6 | Reprovação de criativo; aviso de "misleading claim" no dashboard da rede | Política interna anti-fake-ads (§6.3): ≥90% de todo criativo é captura real do build; toda mecânica mostrada existe no jogo; build de captura dedicada com cheats apenas cosméticos (câmera livre), nunca mecânicos |
| R-16 | Plataforma/lojas | Data Safety (Play) / Privacy Labels (App Store) incorretos ou consent ausente → rejeição ou remoção | Média | Alto | 6 | Aviso de policy no Play Console / App Store Connect; SDKs coletando dado não declarado | Mapa de dados por SDK (§6.1) mantido pelo engenheiro líder; Google UMP/CMP certificado para EEA/UK antes de inicializar ads; ATT + SKAdNetwork no iOS; revisão dos formulários a cada novo SDK |
| R-17 | Equipe | Scope creep estoura o MVP de 30 dias | Alta | Médio | 6 | Burndown com desvio > 20% no fim da semana 2; qualquer item fora do CANON §15 em sprint | Escopo travado por CANON §15; **lista de corte pré-acordada** (ordem: 10 skins → cartas simples → 1 mundo do MVP); itens INEGOCIÁVEIS: Boss Scout, Supply simplificado, game feel §5 |
| R-18 | Equipe | Bus factor: conhecimento crítico concentrado em 1 dev Unity | Média | Médio | 4 | Férias/ausência bloqueia merge por >2 dias; áreas do código sem segundo revisor | Este pacote de GDD como documentação executável; code review obrigatório; build automatizada (CI gera AAB/IPA sem passos manuais); pasta `Docs/` no repositório espelhando decisões |

### 1.3 Planos de contingência — riscos score ≥ 6

| Risco | Gatilho de contingência | Plano B |
|---|---|---|
| R-01 (CPI) | CPI BR > US$ 0,60 após 3 ondas de criativos novos | Pivotar mix geográfico para SEA/LatAm puro; reduzir alvo de escala e estender soft launch 4 semanas; se CPI seguir >2× alvo com D7 no alvo, buscar publisher com força de UA |
| R-02 (D1) | D1 < 32% após 2 rodadas de ajuste de tutorial | War-room de FTUE: replays de sessão dos 10 primeiros minutos, reescrever fases 1–3 (não o jogo); decisão go/no-go só DEPOIS de consertar o funil — nunca escalar UA com D1 quebrado |
| R-03 (ARPDAU) | ARPDAU < US$ 0,05 com retenção no alvo | Adicionar 2 placements rewarded de alto valor (baú extra pós-boss, reroll de portal); testar interstitial em frequência 1/2 fases SÓ na coorte D7+ via Remote Config |
| R-05 (Boss Scout ignorado) | <50% de escolhas ótimas na fase 10 | Reforço em 3 camadas: (1) portal ótimo ganha aura da cor da fraqueza, (2) dano com vantagem mostra "SUPER EFETIVO!", (3) recompensa de moedas +25% por vitória com counter elemental |
| R-06 (dificuldade) | Funil quebrado em uma fase específica por 1 semana | Hotfix por Remote Config em <24 h (HP do boss −15%); rebalance estrutural da fase no patch seguinte |
| R-09 (performance) | <55 FPS no device de referência na semana 3 do MVP | Ativar plano de degradação: cap visual 150 unidades + contador "x N"; partículas tier baixo; sombras off em low-end; se insuficiente, alvo 30 FPS estável em low-end com detecção automática de tier |
| R-10 (vitals) | Crash-free < 99% em RC | Release bloqueado (gate §7); bissecção por Crashlytics; rollout escalonado 5% → 20% → 100% |
| R-15/R-16 (políticas) | Strike ou rejeição | Resposta em 48 h com correção + processo: revisão de TODOS os criativos/formulários ativos, não só o reprovado |
| R-17 (escopo) | Desvio >20% na semana 2 | Aplicar lista de corte na ordem pré-acordada na própria weekly — sem renegociação ad hoc |

### 1.4 Donos por categoria

Mercado/UA → PM + monetization designer · Design → game designer · Técnico → engenheiro Unity líder · Legal/IP → PM (com assessoria jurídica externa) · Plataforma/lojas → engenheiro líder + PM · Equipe → PM.

---

## 2. Como evitar parecer clone barato (entregável 27)

Princípio: clone barato é reconhecido em 3 sinais — **assets genéricos, game feel mole e anúncio mentiroso**. Atacamos os três com regras verificáveis.

1. **Mascote e silhueta proprietários.** O herói inicial (o Soldado que vira exército) tem design próprio com **teste da silhueta**: o recorte 100% preto do personagem deve ser reconhecível e distinto de qualquer referência do gênero (proporção, acessório de cabeça e arma característicos — direção no doc de arte). O mesmo teste vale para os 10 bosses únicos (CANON §6): nenhum boss pode ser confundido com criatura de outro jogo em thumbnail de 128 px.
2. **Diferencial mecânico visível no primeiro vídeo.** Todo criativo e o trailer da loja abrem com o **Boss Scout** ("BOSS DE GELO — fraco contra FOGO") nos primeiros 3 segundos, seguido da escolha de portal informada. É o nosso "isto não é mais um runner de número" — e é filmável, o que clones de número puro não têm.
3. **Polish de game feel como requisito, não como sobra.** A seção §5 é um checklist de aceite com números. Build sem hitstop no boss, sem slow motion no golpe final ou com input >50 ms **não passa de RC**. Game feel é o que separa "premium casual" de "asset flip" aos olhos do jogador em 10 segundos.
4. **Nome e marca registráveis.** O nome final (avaliação no doc 01) passa por: busca de anterioridade INPI (classe 9 e 41) + USPTO + lojas (nenhum jogo relevante com nome confundível), domínio e handles disponíveis, e zero palavras que evoquem marca existente. Logo com lettering próprio — nunca fonte gratuita sem modificação.
5. **Zero assets "de loja" sem retrabalho.** Regra de pipeline: nenhum asset comprado (modelos, VFX, SFX) entra no build sem **repaint na nossa paleta + ajuste de silhueta/material**. Itens de identidade (herói, bosses, portais, logo, UI kit, jingle de multiplicação) são **sempre originais**, feitos internamente ou por encomenda exclusiva. A planilha de licenças (R-14) marca cada asset como `original` / `retrabalhado` / `proibido em identidade`.
6. **Paleta e assinatura audiovisual próprias.** Paleta master definida no doc de arte (cores de raridade do CANON §8 como âncora) aplicada a UI, VFX e mundos; **som-assinatura da multiplicação** (escala ascendente própria, com pitch subindo por portal consecutivo) que vira marca sonora nos vídeos — o jogador reconhece o jogo de olhos fechados.
7. **UX premium.** Transições com easing em todas as telas (≤300 ms), microinterações em todo botão (escala + som), textos curtos com tom próprio (sem "Congratulations!" genérico), loading mascarado por animação do mascote, e **zero telas mortas**: toda tela tem um próximo passo óbvio (BRIEF "Regras de produto").
8. **Auditoria anti-clone pré-soft-launch (gate de release).** Checklist executado por alguém de fora do time: (a) screenshot de cada tela lado a lado com os 5 jogos referência — nenhum layout pode ser sobreponível; (b) nenhum nome de fase/tropa/boss colide com conteúdo de terceiros; (c) teste cego com 5 pessoas: "que jogo é este?" — resposta não pode ser o nome de um concorrente; (d) criativos ativos auditados contra a política anti-fake-ads (§6.3).
9. **Anti-fake-ads como posicionamento.** Nossos anúncios mostram gameplay real porque o gameplay real É o anúncio (multiplicação, mutação visível, boss gigante). Diferencial honesto: o jogador que instala pelo vídeo encontra exatamente aquilo — o que protege D1 (R-02) e a reputação da marca ao mesmo tempo.

---

## 3. Como deixar mais viciante — loops de compulsão ÉTICOS (entregável 28)

Princípio: retenção vem de **antecipação honesta**, não de engano. Todo gancho deste capítulo passa no teste: *"se o jogador entender 100% de como funciona, o gancho continua funcionando?"* Se a resposta é não, é dark pattern e está proibido (§3.6).

### 3.1 Variable rewards com odds públicas (baús)

Recompensa variável é o gancho mais forte do gênero — usamos com **odds publicadas dentro do jogo** (botão "ⓘ Chances" em todo baú) e idênticas às reais do servidor.

- **Fonte única dos números:** as drop tables por tipo de baú (Comum/Raro/Épico/Lendário) e o pity de Lendário (contador global de 50 pacotes, contando igualmente baús grátis e pagos) estão definidos **exclusivamente no doc 07 §4**. Este capítulo não redefine odds nem garantias — define apenas as regras de transparência abaixo.
- **Odds públicas:** o botão "ⓘ Chances" em todo baú exibe as odds reais do doc 07 §4, versionadas e auditáveis (config assinada no Remote Config — §3.6).
- **Pity visível:** contador exibido ao jogador ("Lendário garantido em: N pacotes" — regra do doc 07 §4) — a garantia vira gancho de retorno, não segredo.
- Abertura com antecipação curta (1,5 s de "carregamento" do baú com glow da raridade) e **skip por toque** — antecipação sim, fricção não.
- Conforme CANON §11, baús grátis dropam lendárias e o pity conta nelas também (doc 07 §4): F2P sente que o próximo baú sempre pode ser O baú.

### 3.2 Near-miss honesto no boss

A luta mais memorável é a que quase se perde. Fabricamos isso por **balanceamento, nunca por trapaça em runtime**:

- Alvo de tuning: na mediana das vitórias, o boss morre com o exército entre **10% e 35%** do tamanho de entrada na arena (telemetria `boss_defeated` com `survivors_pct`).
- O ataque especial telegrafado do boss (CANON §6) é agendado para ~70% da luta — o momento "vai dar errado!" existe em toda luta, mas é **evitável por leitura** (§4.2.4).
- **PROIBIDO** rubber banding oculto: o jogo nunca altera dano/HP secretamente durante a luta para fabricar drama. Os números mostrados são os números reais. (O custo de mentir: o jogador percebe em uma semana e a confiança morre — e confiança é o que sustenta rewarded ≥35%.)
- Derrota por pouco mostra "O BOSS FICOU COM 8% DE VIDA!" + oferta de reviver via rewarded (1×/fase, CANON §11) — o near-miss converte em ad view e em "mais uma tentativa" sem alterar a luta.

### 3.3 Streaks diários com proteção

- **Sequência de Login:** calendário de 7 dias conforme o doc 02 §5.5 (d1 100 moedas → d2 10 gemas → d3 10 fragmentos de Soldado → d4 baú comum extra → d5 20 gemas → d6 15 fragmentos de Arqueiro → d7 **carta épica garantida + 60 gemas**, o prêmio que ancora a retenção D6–D7), reiniciando em ciclo melhor a cada 4 semanas.
- **Proteção de streak:** 1 falta por semana é perdoada automaticamente ("Escudo de Sequência" — recarrega toda segunda-feira). Perder o streak por viajar/adoecer é a forma nº 1 de churn ressentido; a proteção custa pouco e preserva o hábito.
- Streak **nunca** é condição para conteúdo de gameplay — só acelera economia. Quem volta depois de 1 mês não está travado, só recomeça o ciclo.
- Missões diárias (20–40 gemas/dia, CANON §8) com 3 slots: 2 fáceis ("vença 3 fases") + 1 direcionada que ensina profundidade ("vença 1 boss explorando a fraqueza elemental") — o hábito diário também treina o jogador inteligente (§4).

### 3.4 "Mais uma fase" — próximo desbloqueio sempre visível

Regra de produto do BRIEF transformada em sistema: **em toda tela de vitória, o jogador vê no mínimo 2 dos ganchos abaixo**, escolhidos por prioridade:

| Gancho | Exemplo na UI | Distância máxima |
|---|---|---|
| Marco de fase | "Faltam 2 fases para o BAÚ ÉPICO da fase 10" | sempre ≤3 fases |
| Desbloqueio de feature | "Nível 3: BAÚS — faltam 120 XP" (CANON §8) | barra de XP sempre visível |
| Teaser do próximo boss | silhueta escura do boss de mundo + "?" | a partir da fase 5 do mundo |
| Upgrade quase acessível | "Faltam 35 moedas para Dano Inicial nv 4" | exibido quando saldo ≥70% do custo |
| Tropa nova | "Arqueiro desbloqueia na fase 5" (CANON §16) | ≤2 fases |

- O mapa de mundos mostra o próximo mundo com nome + tema visível (e cadeado), nunca um vazio.
- Botão "PRÓXIMA FASE" é o elemento mais proeminente da tela de vitória — a decisão default é continuar.

### 3.5 Session hooks (ganchos de retorno)

| Gancho | Cadência | Mecânica | Por que é ético |
|---|---|---|---|
| Baú Diário Grátis | 1×/dia | reset 00:00 local, badge na tela inicial | grátis de verdade, sem condição |
| Missões diárias | diário | 3 slots, 20–40 gemas | metas claras, sem timer agressivo |
| Baú extra via rewarded | 1×/dia | CANON §11 | opcional, valor claro antes de assistir |
| Teste de lendária | 1 fase | "experimente o Dragão por 1 fase" via rewarded | demo honesta, não empréstimo com pegadinha |
| Evento semanal | semanal (nv 6+) | modificador de regras + ranking | mesma gameplay, sem paywall de entrada |
| Push (FCM) | máx. 1/dia | "Seu Baú Diário chegou" / streak em risco às 19h local | opt-in, frequência capada, sem culpa ("suas tropas sentem sua falta" está PROIBIDO) |

- **SEM sistema de energia** (CANON §8): o maior gancho é poder jogar quanto quiser. Sessões longas são bem-vindas, não taxadas.

### 3.6 Limites éticos — lista de proibições (hard rules)

1. **Odds enganosas:** proibido qualquer diferença entre odds exibidas e odds reais; odds versionadas e auditáveis (config assinada no Remote Config).
2. **Timers falsos:** proibido countdown que reinicia ou oferta "última chance" que reaparece idêntica. A Oferta Inicial é 1× nas primeiras 48 h (CANON §11) e **realmente** não volta.
3. **Pressão sobre menores:** público 13+; sem mensagens de culpa, sem comparação social em compra ("seus amigos já compraram"), sem chat/UGC; toda compra exige confirmação da loja (Google/Apple) — nunca compra em 1 toque dentro de fluxo de derrota.
4. **Fluxo de derrota limpo:** na tela de derrota só existem reviver via rewarded, melhorar tropa e tentar de novo (BRIEF tela 5) — **proibido** oferta de IAP na tela de derrota (momento de frustração ≠ momento de venda).
5. **Sem rubber banding mentiroso** (§3.2) e sem portais com informação falsa (CANON §3.4 — porcentagens reais).
6. **Ads:** interstitial nunca após duas derrotas seguidas (CANON §11); rewarded sempre mostra a recompensa antes do clique; sem botão de fechar escondido em criativos próprios.
7. **Direito de sair:** progresso nunca exige login social; conta anônima com sync; exclusão de dados disponível (§6.1).

---

## 4. Como deixar mais inteligente — profundidade sem complexidade (entregável 29)

### 4.1 A regra de ouro

> **"Cada escolha tem resposta certa contextual, nunca absoluta."**

Operacionalização: para todo par de portais gerado (CANON §3.4), deve existir um contexto plausível (boss da fase, mutações ativas, Supply restante, composição atual) em que **cada um dos dois lados é a melhor escolha**. Se um lado é sempre melhor em qualquer contexto, o par está mal desenhado. Isso é validável por ferramenta (§4.4) — não é só uma frase de filosofia.

### 4.2 As cinco fontes de profundidade (todas legíveis em 3 s — pilar 1 do CANON)

**4.2.1 Counter-play elemental.** O chart do CANON §4 (Fogo > Gelo > Raio > Fogo; mesmo elemento = −50%) + Boss Scout transformam o elemento de "skin de dano" em **plano de fase**. Exemplo jogável: fase 12 (Cidade Zumbi), Boss Scout anuncia "BRUTAMONTES ZUMBI — fraco contra FOGO, imune a VENENO". O par final oferece `Elemento Veneno` (que seria ótimo contra os inimigos orgânicos da pista) vs `+10 Soldados`. O jogador que leu o Scout sabe: Veneno carrega a corrida mas **zera no boss** — escolha contextual, não óbvia.

**4.2.2 Builds por composição + mutações.** Com Supply 60 (MVP) e custos do CANON §5, o jogador monta arquétipos reais: *enxame* (60 Soldados — DPS distribuído, frágil a área), *qualidade* (5 Gigantes = 60 Supply — tanque lento), *híbrida* (1 Gigante + 2 Magos + 40 Soldados). Os 3 slots de mutação (CANON §3.3) viram a "build por cima da build": Armadura favorece enxame (multiplicada por corpo), Laser favorece qualidade (poucas unidades com uptime alto). A 4ª mutação substituir a mais antiga cria a decisão "abro mão de Asas para pegar Laser?" — profundidade com regra de uma linha.

**4.2.3 Skill expression — rotas de risco.** O portal de risco do MVP ("x10 se sobreviver à zona de perigo", CANON §10) é a expressão de habilidade pura: a zona exige micro-desvio ativo, e o x10 só vale para quem atravessa com o exército coeso. Camadas adicionais: obstáculos da pista punem desatenção proporcionalmente (perder 5 unidades na fase 2 é nada; na fase 18 com 3 mutações é tragédia), e jogadores hábeis "fazem a curva" para colher fileiras de moedas fora da linha central. Mesmo input (arrastar), tetos de execução diferentes.

**4.2.4 Leitura do boss.** Todo boss tem 1 ataque especial telegrafado (CANON §6): círculo vermelho no chão 1,2 s antes do impacto — mover o exército para fora é a diferença entre vitória folgada e near-miss. Bosses avançados elevam a leitura sem elevar o vocabulário: Alien Supremo (M8) troca de fraqueza a cada 25% de HP **sempre exibida no HUD** — o desafio é re-priorizar, não decifrar.

**4.2.5 Decisões de Suprimento.** O Supply (CANON §3.2) cria a pergunta que clones não têm: *"cabe?"*. Estourar o limite converte excedente em moedas — o que habilita a **rota de farm deliberada**: numa fase fácil, o jogador experiente pega TODOS os multiplicadores, estoura o Supply de propósito e sai com o triplo de moedas. Quantidade vs qualidade vs dinheiro: três respostas certas, dependendo do objetivo da corrida.

### 4.3 Guard-rails de complexidade (para continuar "simples por fora")

- **1 conceito novo por mundo, no máximo** — M1 ensina portais+boss, M2 ensina counter elemental de verdade, M3 ensina imunidades (Robô Escorpião imune a Veneno).
- Toda regra cabe em **1 linha de tooltip** ("Fogo: +50% contra Gelo") — se precisa de parágrafo, simplifica-se a regra, não o texto.
- Nenhuma decisão de corrida exige parar para ler: ícone + número + cor resolvem (pilar "legível em 3 segundos").
- A camada inteligente é **opcional para vencer** nas fases de taxa de vitória alta (1–9 de cada mundo) e **necessária** nas fases 10 — o casual progride, o engajado é recompensado, ninguém é expulso.
- Teste de mesa mensal: 5 jogadores novos; se algum não souber explicar a fraqueza do boss após 3 fases, a comunicação (não a mecânica) volta para a mesa.

### 4.4 Validador de fases (ferramenta de design)

Script de editor (Unity) que roda sobre cada `LevelConfigSO` antes do commit:

1. **Existe ≥1 rota ótima e ≥1 armadilha aparentemente boa** (CANON §3.1) — simulação rápida da corrida com política gulosa "maior número" vs política "counter elemental": a gulosa deve perder ou quase perder nas fases 10; a informada vence dentro do alvo de taxa de vitória.
2. **Nenhum par dominado** (§4.1): para cada par, o simulador testa 4 contextos (enxame/qualidade/farm/counter) — cada lado precisa vencer em ≥1 contexto.
3. **Supply-check:** a fase não pode oferecer rota em que o jogador chegue ao boss abaixo do piso mínimo de poder (vitória impossível) sem ter passado por um aviso visual claro.
4. Saída: relatório `level_validation.json` anexado ao PR da fase.

---

## 5. Game feel — checklist de requisitos testáveis

Tradução da lista do BRIEF ("Game feel — prioridade máxima") em critérios de aceite. **Devices de referência:** Android mid (classe Snapdragon 680 / 4 GB RAM / Android 12) e Android low (classe Helio G35 / 3 GB). Medições com Unity Profiler + ferramenta de input latency + checklist manual de QA. Status de cada item entra no gate de release (§7).

### 5.1 Checklist (gate de RC: 100% dos itens P0 aprovados)

| ID | Pri | Requisito (BRIEF) | Critério de aceite testável |
|---|---|---|---|
| GF-01 | P0 | Jogar em até 5 s após abrir o app | Cold start → tela inicial interativa ≤ 4 s no device mid; toque em "Jogar" → controle do exército ≤ 1,5 s adicional |
| GF-02 | P0 | Resposta rápida ao toque | Latência swipe→início do movimento ≤ 50 ms (≤3 frames a 60 FPS) medida com gravação 240 fps; zero frames de input descartados |
| GF-03 | P0 | Movimento suave | 60 FPS médios (≥55 no p5) no device mid em cena de pico (300 unidades + portal + VFX); low-end: 30 FPS estáveis com tier gráfico automático; multidão segue o líder com damping, sem teleporte nem jitter visível (>2 px/frame em repouso) |
| GF-04 | P0 | Impacto ao passar por portais | No frame da travessia: flash do portal + punch de escala 1,15× por 100 ms no contador + SFX + número rolando (não troca seca); tudo dispara em ≤ 50 ms da colisão |
| GF-05 | P0 | Som satisfatório de multiplicação | SFX em camadas com **pitch ascendente por portal consecutivo positivo** (reseta em portal negativo); jingle-assinatura próprio (§2.6); mix nunca clipa com 10 eventos simultâneos |
| GF-06 | P1 | Vibração leve | Haptic transient leve (≤20 ms) ao cruzar portal; médio (≤40 ms) em hit forte no boss e ao estourar Supply; **nunca** vibra na derrota; toggle em opções; total ≤ 12 vibrações/min |
| GF-07 | P0 | Explosão de moedas | Vitória/baú: ≥20 moedas físicas com trail voam ao contador do HUD; contador incrementa em rolagem (≤1,2 s do início ao valor final); SFX de "chuva" com fade |
| GF-08 | P0 | Hit impact no boss | Golpes fortes: hitstop 50–80 ms + flash branco de 2 frames no material do boss + shake de câmera (amplitude ≤0,3 m, ≤150 ms) + números de dano agregados (máx. 5 popups simultâneos, agregação "x12 hits") |
| GF-09 | P0 | Câmera aproximando no boss | Entrada da arena: dolly-in de 0,8 s (FOV 60→48) sincronizado com a animação de entrada do boss (≤2 s, CANON §6); punch-in adicional de 0,2 s a cada 25% de HP perdido |
| GF-10 | P0 | Slow motion no golpe final | Golpe que zera o HP do boss: **timeScale global 0,3 por 0,8 s** (valor canônico único do pacote, conforme implementado no doc 12/VFXManager — docs 01 §6.5, 05 §2.1/§2.4, 06 §2.4 e 09 §4.3 usam exatamente esta spec) + zoom no ponto de impacto + partículas de "desmonte" do boss (sem sangue — CANON §1); áudio com filtro lowpass durante o slow-mo; pular com toque |
| GF-11 | P0 | Feedbacks textuais | Conforme tabela canônica do doc 09 §4.2 (ver §5.2): popup em ≤100 ms do trigger, duração 0,7 s com easing elástico, fila de exibição (nunca 2 sobrepostos), tipografia da marca |
| GF-12 | P1 | Multiplicação visual legível | Unidades novas surgem com stagger de 30–60 ms (efeito "brotando"), não em bloco instantâneo; acima de 150 unidades visíveis, excedente representado pelo contador "x N" sem perda de fantasia |
| GF-13 | P1 | Mutações visíveis | Aplicar mutação troca o visual de 100% das unidades em ≤0,5 s com burst de partículas; os 3 slots aparecem como ícones no HUD; substituição da mais antiga tem animação própria (CANON §3.3) |
| GF-14 | P1 | Conversão de Supply | Estouro de Supply: fanfarra de 0,8 s + chuva de moedas + texto "+N MOEDAS!" em dourado; **proibido** vermelho, X ou som negativo (CANON §3.2) |
| GF-15 | P2 | Microinterações de UI | Todo botão: resposta visual+sonora em ≤80 ms; transições de tela ≤300 ms; nenhuma tela sem estado de loading mascarado |

### 5.2 Feedbacks textuais — referência canônica e critérios de aceite

A tabela canônica de triggers dos feedbacks textuais (NICE, GREAT, INSANE, GODLIKE, PERFECT, MUTATION, MEGA ARMY, BOSS BREAKER) é **exclusivamente a do doc 09 §4.2** (spec de UI) — este documento e o doc 05 apenas a referenciam como critério de aceite, sem redefinir gatilhos. Nota de design: MEGA ARMY dispara ao **estourar o Supply** (conversão em moedas, doc 09 §4.2) — nunca um piso fixo de 200 unidades, inatingível no MVP com Supply 60.

Critérios de aceite deste documento (aplicados sobre a tabela do doc 09 §4.2):

- Popup em ≤100 ms do trigger; duração 0,7 s com easing elástico; fila de exibição (nunca 2 sobrepostos); tipografia da marca.
- Cada trigger dispara no máximo 1×/corrida exceto NICE/GREAT/INSANE (que escalam); telemetria registra exibições para correlacionar com retenção.

### 5.3 Budgets técnicos de suporte ao feel

- Partículas: ≤ 80 sistemas ativos/cena, ≤ 4 draw calls de VFX por portal cruzado.
- Áudio: ≤ 24 voices simultâneas, ducking automático do BGM em slow-mo e fanfarras.
- GC: zero alocação por frame no gameplay (pooling obrigatório — ver doc de arquitetura).

---

## 6. Compliance e políticas

### 6.1 Privacidade — LGPD · GDPR · COPPA

| Tema | Regra | Implementação |
|---|---|---|
| Base legal / consent | GDPR (EEA/UK): consent prévio para ads personalizados; LGPD (BR): base legal + transparência; COPPA (US): não coletar dados de <13 sem consentimento parental | **Google UMP / CMP certificado TCF 2.2** integrado via AppLovin MAX, exibido ANTES da primeira inicialização dos SDKs de ads na EEA/UK; fluxo de consentimento revisável em Opções > Privacidade |
| Público-alvo | Jogo classificado 13+ (CANON §1: público 13–40); **não** direcionado a crianças | Declarar target audience 13+ no Play Console (fora do programa Families); sem conteúdo/linguagem infantil no marketing; se métricas indicarem audiência <13 relevante, ativar ads não personalizados por padrão e reavaliar rating |
| iOS | ATT obrigatório para tracking; atribuição via SKAdNetwork | Prompt ATT com pre-prompt explicativo; MAX e Firebase configurados para operar sem IDFA em caso de recusa |
| Mapa de dados | Todo dado coletado documentado por SDK | Planilha viva: Firebase Analytics (eventos, device ID), Crashlytics (logs de crash), MAX/redes (ad ID), RevenueCat (recibos); DPAs assinados com cada fornecedor |
| Direitos do titular | Acesso/exclusão (LGPD art. 18, GDPR art. 17) | Botão "Excluir meus dados" em Opções → Cloud Function que apaga o doc do usuário no Firestore + instruções de reset de ad ID; e-mail de contato na política de privacidade |
| Política de privacidade | Obrigatória nas duas lojas | URL pública, em PT e EN, listando SDKs e finalidades; versionada junto com releases |

### 6.2 Classificação etária

- Questionário **IARC** preenchido com: violência fantasiosa leve, sem sangue (inimigos desmontam em peças/partículas — CANON §1), compras no app, anúncios de terceiros, sem interação social/UGC.
- Resultado esperado: **ClassInd 10 · PEGI 7 · ESRB E10+** (validar no questionário real; qualquer mudança de conteúdo que ameace subir o rating — ex.: sangue, sustos — é vetada por design).
- Descritores de loja: "Compras no aplicativo" + "Contém anúncios" sempre visíveis e corretos.

### 6.3 Política anti-fake-ads (redes: Google Ads, Meta, TikTok, AdMob/MAX)

1. **≥90% de cada criativo é captura real do build** (build de captura com câmera livre e contas demo; cheats apenas cosméticos, nunca de mecânica inexistente).
2. **Toda mecânica mostrada existe no jogo** na forma mostrada — multiplicação, mutações, Boss Scout, bosses. Exageros permitidos: edição, ritmo, zoom. Proibidos: minigame inexistente, fail-bait de mecânica que não está no produto, UI falsa.
3. Texto dos anúncios sem claims falsos ("grátis" só se for; sem "vença dinheiro real").
4. Checklist de aprovação de criativo (PM + game designer) antes de subir para qualquer rede; criativos reprovados por rede ⇒ processo do R-15.
5. Benefício colateral: criativo = gameplay protege D1 (quem instala encontra o que viu) — anti-fake-ads aqui é política de retenção, não só de compliance.

### 6.4 Requisitos de loja

| Loja | Requisito | Nosso plano |
|---|---|---|
| Google Play | Target API level vigente; AAB; Data Safety preenchido; Android vitals (crash ≥1,09% / ANR ≥0,47% = penalidade de visibilidade); declaração de ads; ordens de compra via Play Billing | CI gera AAB; vitals como gate de release (§7); IAP via RevenueCat sobre Play Billing; formulário Data Safety derivado do mapa de dados §6.1 |
| App Store | ATT (guideline 5.1.2); precisão de metadata (2.3 — screenshots reais); IAP obrigatório para bens digitais (3.1.1); **divulgação de odds de loot boxes pagos (3.1.1)**; Privacy Nutrition Labels | Screenshots/preview do build real; odds dos baús já são públicas in-game (§3.1) — exibidas também na descrição da loja quando exigido; labels derivadas do mapa de dados |
| Ambas | Restauração de compras; preço/moeda local; suporte acessível | RevenueCat (restore + recibos); página de suporte com e-mail |

### 6.5 Mecânicas de sorte — postura regulatória

- Baús com odds públicas + pity visível (§3.1) já atendem Apple/Google e as exigências de transparência em discussão em vários mercados (UE, BR/PL de loot boxes). Posição do estúdio: **transparência por padrão** — se uma jurisdição exigir mais (ex.: banir baú pago), a economia se adapta removendo a compra de baú e mantendo baús de gameplay, sem refazer o jogo.
- Portais de risco usam porcentagens reais exibidas (CANON §3.4) e **não envolvem dinheiro real** — não configuram aposta.
- Sem mecânica de cash-out, sem moeda intercambiável por dinheiro, sem trade entre jogadores (remove vetores de gambling e de fraude).

---

## 7. Gates de qualidade (Definition of Done de release)

Uma build só vira release candidate / vai à loja se:

1. **Game feel:** 100% dos itens P0 da tabela §5.1 aprovados em device mid e low.
2. **Estabilidade:** ≥99% crash-free sessions na build RC (device farm + dogfood), ANR < 0,3%.
3. **Anti-clone:** auditoria §2.8 executada e assinada (1×/release maior).
4. **Compliance:** UMP/ATT funcionais, Data Safety/labels atualizados, planilha de licenças sem pendência (R-14), odds in-game = odds em config.
5. **Telemetria:** todos os eventos do BRIEF (level_start, gate_selected, boss_defeated, rewarded_ad_completed etc.) chegando no Firebase DebugView.
6. **Riscos:** matriz §1.2 revisada na semana do release; nenhum sinal de alerta de score ≥6 ativo sem plano em execução.

> Estes gates existem para proteger as três moedas do projeto: **confiança do jogador** (ética §3.6, honestidade §3.2), **confiança das plataformas** (compliance §6) e **identidade da marca** (anti-clone §2). Perder qualquer uma delas custa mais caro do que qualquer atraso de release.
