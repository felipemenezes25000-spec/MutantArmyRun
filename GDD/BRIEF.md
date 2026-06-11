# BRIEF DO CLIENTE — Mutant Army Run

> Requisitos originais do cliente, consolidados. Este arquivo é a fonte dos REQUISITOS.
> As DECISÕES de design (números, nomes, regras finais) estão em CANON.md, que prevalece em caso de conflito de detalhe.

## Objetivo

Criar um jogo mobile hybrid-casual **original** (sem copiar nenhum jogo, personagem, asset, nome, marca, fase ou visual protegido), inspirado nos gatilhos de gameplay que fazem o gênero viralizar: multiplicação visual, portais, evolução rápida, chefões gigantes, decisões simples, números crescendo, explosão de recompensa e progressão constante.

- Extremamente viciante, fácil de entender em 3 segundos, bonito, satisfatório de assistir.
- Perfeito para anúncios em TikTok, Reels, Shorts, Meta Ads, Google Ads e AdMob.
- Com uma camada mais inteligente do que os clones genéricos do gênero.

## Nome

Provisório: **Mutant Army Run**. Alternativas a avaliar: Gate Army: Evolution War, Portal Army, Evolution Horde, Mutant Rush, Army Evolution Run, Clone War Run, Crowd Evolution, Monster Gate Run, Boss Breaker Army, Merge Army Rush.

## Conceito central

O jogador começa cada fase com 1 unidade simples. Durante a corrida, passa por portais que multiplicam, transformam, fundem, fortalecem ou modificam seu exército. No final da fase, o exército enfrenta um boss gigante. O objetivo é escolher os melhores portais, criar o exército mais absurdo possível e derrotar chefões cada vez maiores.

Frases centrais:
- "Monte o exército mais absurdo possível em 60 segundos."
- "Escolha os portais certos, crie mutações insanas e destrua bosses gigantes."

## Referências de mecânica (apenas inspiração, nunca cópia)

- Mob Control: multidão, conquista, tiros/unidades, progressão simples.
- Count Masters: portais matemáticos, multiplicação visual e corrida.
- Last War: lanes, combate rápido, desvio de obstáculos, guerra casual.
- Gate runner games: escolhas rápidas entre portais positivos e negativos.
- Merge games: fusão e evolução de unidades.
- Idle army games: upgrades permanentes e progressão entre fases.

## Diferencial obrigatório

A maioria desses jogos é simples demais: o jogador só escolhe o maior número. Este jogo precisa de decisões mais inteligentes, mas ainda fáceis de entender. Exemplos do cliente:
- x10 soldados fracos pode ser pior do que +2 magos fortes.
- Fogo ótimo contra inimigos de gelo, ruim contra boss de lava.
- Gigantes: muito dano, lentos. Arqueiros: longe, frágeis. Healers curam no boss. Tanques protegem pequenos. Ninjas desviam de armadilhas. Necromantes revivem. Engenheiros constroem torres. Dragões: dano em área.

"O jogo precisa parecer simples por fora, mas inteligente por dentro."

## Core loop (12 passos)

1. Entra no jogo → 2. Tela principal com "Jogar" → 3. Fase começa com 1 unidade → 4. Corrida de 30–90 s → 5. Escolhas entre portais positivos/negativos/arriscados/estratégicos → 6. Multiplica, funde, transforma, evolui → 7. Evita obstáculos/armadilhas/inimigos → 8. Arena final → 9. Boss gigante → 10. Ganha moedas, XP, cartas, fragmentos, baús, upgrades → 11. Volta à tela principal → 12. Melhora tropas, desbloqueia unidades, evolui cartas, próxima fase.

## Portais (variedade exigida)

- **Matemáticos:** +10, +25, +50, x2, x3, x5, −10, ÷2.
- **De classe:** transformar soldados em arqueiros / magos / robôs / zumbis / cavaleiros / dragões pequenos / ninjas / gigantes.
- **De elemento:** fogo, gelo, raio, veneno, luz, sombra, metal, energia alienígena.
- **De mutação:** braços extras, asas, armadura, laser, cabeças extras, tentáculos, escudo, velocidade, tamanho, ataque em área, regeneração, clonagem.
- **De risco:** x10 se sobreviver; baú raro se passar por área perigosa; mutação lendária com chance de falha; dobrar dano mas reduzir vida; dobrar quantidade mas reduzir velocidade; transformar tudo em unidade aleatória; sacrificar metade do exército por 1 gigante lendário.

## Unidades

- **Comuns:** Soldado (equilibrado), Arqueiro (distância), Escudeiro (protege linha de frente), Corredor (rápido, fraco).
- **Raras:** Mago (área), Ninja (desvia), tropa de fogo, tropa de gelo (reduz velocidade), Médico/Healer.
- **Épicas:** Robô (dano+resistência), Gigante (HP+dano), Necromante (revive), Engenheiro (torres), Alien (imprevisível).
- **Lendárias:** Dragão (área+voo), Titã (enorme/forte/lento), Anjo de Guerra (cura+dano), Demônio Mutante (dano brutal), Mecha Supremo (laser+míssil).

Cada unidade tem: vida, dano, velocidade, alcance, raridade, fraqueza, vantagem, habilidade especial, visual evoluído, nível, fragmentos para upgrade.

## Sistema elemental (simples)

Fogo vence planta/orgânico; gelo reduz velocidade; raio dano em cadeia; veneno dano contínuo; luz cura aliados; sombra clona/revive; metal aumenta defesa; energia alienígena causa mutações aleatórias.

## Bosses

Tipos: gigante de pedra, dragão de lava, robô colosso, zumbi titã, aranha mecânica, dinossauro mutante, castelo vivo, tanque gigante, alien supremo, demônio dimensional, planta carnívora gigante, rei dos goblins, boss de gelo, boss elétrico, boss final multidimensional.

Cada boss: barra de vida gigante, ataque especial, fraqueza elemental, animação de entrada, combate curto e intenso, recompensa especial, chance de drop de carta/fragmento.

## Estrutura de fases — 100 fases, 10 mundos

| Mundo | Tema | Fases | Boss final |
|---|---|---|---|
| 1 — Campo Inicial | tutorial, grama, portais simples | 1–10 | Gigante de madeira |
| 2 — Cidade Zumbi | ruas destruídas, carros, zumbis | 11–20 | Zumbi titã |
| 3 — Deserto Robótico | areia, sucata, máquinas | 21–30 | Robô escorpião |
| 4 — Floresta Mutante | plantas, veneno, monstros orgânicos | 31–40 | Planta carnívora gigante |
| 5 — Vulcão dos Gigantes | lava, fogo, pedras caindo | 41–50 | Dragão de lava |
| 6 — Reino Congelado | gelo, piso escorregadio, tempestade | 51–60 | Rei de gelo |
| 7 — Arena Medieval | castelos, cavaleiros, catapultas | 61–70 | Cavaleiro colosso |
| 8 — Laboratório Alienígena | experimentos, lasers, mutações | 71–80 | Alien supremo |
| 9 — Planeta Mecânico | fábricas, engrenagens, drones | 81–90 | Mecha supremo |
| 10 — Dimensão Final | realidade quebrada, portais, caos | 91–100 | Entidade dimensional |

## Telas exigidas (10)

1. **Inicial:** logo, jogar, moedas, gemas, progresso, loja, tropas, upgrades, passe.
2. **Gameplay:** exército correndo, portais, obstáculos, contador de unidades, barra de progresso, feedback de dano, números grandes, botão de poder especial (se existir).
3. **Boss:** arena, boss gigante, barra de vida, exército atacando, impacto, slow motion, explosão de recompensa.
4. **Vitória:** moedas, XP, sobreviventes, dano causado, baú, "dobrar com anúncio", próxima fase.
5. **Derrota:** motivo, reviver com anúncio, melhorar tropa, tentar de novo.
6. **Tropas:** cartas, raridade, nível, fragmentos, evoluir, comparação de atributos.
7. **Upgrades:** dano inicial, vida inicial, velocidade, recompensa, tamanho do exército inicial, chance crítica, dano contra boss, resistência a obstáculos.
8. **Loja:** moedas, gemas, baús, skins, sem-anúncios, passe.
9. **Mapa:** mundos, fases, bosses, recompensas por mundo, bloqueio/desbloqueio.
10. **Eventos:** diário, semanal, ranking, recompensas especiais.

## Estilo visual

Mobile casual premium: colorido, limpo, chamativo, satisfatório. Personagens simples e carismáticos; unidades pequenas e legíveis; bosses gigantes e exagerados; portais brilhantes; números grandes; cores fortes para anúncios; animações elásticas; partículas; câmera com impacto; slow motion; ótimo em vídeo vertical. Premium na aparência, leve para celulares medianos.

## Viralização — cenas desejadas

1 soldado virando 10.000 · portal errado destruindo o exército · escolha entre x100 e mutação lendária · boss quase vencendo · exército pequeno vencendo por estratégia · fusão criando unidade absurda · humano virando dragão · clone infinito · mutação caótica · último soldado vencendo o boss · portal "não escolha esse" · "só 1% passa dessa fase" · "qual portal você escolheria?" · "parece fácil, mas não é".

## Formatos de anúncio

1. Escolha ("Qual você escolheria?" x50 soldados vs +1 dragão lendário) · 2. Erro ("Não cometa esse erro") · 3. Evolução (humano→fogo→asas→laser→dragão→boss destruído) · 4. Desafio ("Você consegue vencer esse boss?") · 5. Satisfação (números crescendo, moedas explodindo) · 6. Quase-derrota (vitória no último segundo) · 7. Comparação ("100 soldados fracos vs 1 titã lendário") · 8. Curiosidade ("Esse portal parece ruim, mas é o melhor do jogo").

## Monetização

- **Ads:** rewarded para dobrar moedas / reviver antes do boss / abrir baú raro / testar unidade lendária / acelerar upgrade; interstitial leve após algumas fases, sem irritar.
- **IAP:** remover anúncios, pacotes de moedas/gemas, baús raros, skins, cartas lendárias, passe de temporada, oferta inicial.
- **Passe de temporada:** mensal — nova tropa, novo boss, nova skin, recompensas diárias, baús premium, ranking, evento especial.
- Não pode ser pay-to-win demais: F2P sente evolução; pagante acelera e personaliza.

## Economia

Moedas (upgrades básicos) · Gemas (premium) · Fragmentos (evoluir tropas) · Baús (cartas, moedas, gemas) · XP (nível do jogador) · Energia: opcional — evitar travar o jogador.

## Analytics

Eventos obrigatórios: tutorial_start, tutorial_complete, level_start, level_complete, level_fail, boss_start, boss_defeated, boss_failed, gate_selected, gate_missed, unit_unlocked, unit_upgraded, chest_opened, rewarded_ad_shown, rewarded_ad_completed, interstitial_shown, purchase_started, purchase_completed, season_pass_opened, season_pass_purchased, day_1_retention, day_3_retention, day_7_retention.

Métricas: D1/D3/D7, tempo de sessão, fases/sessão, taxa de vitória/derrota, portal mais escolhido, boss mais difícil, receita por usuário, ARPDAU, LTV, CPI, conversão de rewarded, conversão de compra.

## Tecnologia

Unity + C#, mobile-first, Android primeiro, iOS depois. Firebase: Auth anônimo, Firestore, Analytics, Remote Config, FCM, Crashlytics. AdMob ou AppLovin MAX. RevenueCat. Cloud Functions se necessário.

Remote Config controla: dificuldade, moedas por fase, dano dos bosses, vida das tropas, frequência de anúncios, preço de upgrades, eventos ativos, chance de drop, recompensa dos baús.

## Arquitetura Unity

Sistemas: GameManager, LevelManager, GateManager, UnitManager, CrowdManager, BossManager, CombatSystem, UpgradeSystem, EconomySystem, RewardSystem, AdsManager, IAPManager, AnalyticsManager, SaveSystem, UIManager, AudioManager, VFXManager, RemoteConfigManager.

ScriptableObjects para: unidades, bosses, fases, portais, upgrades, recompensas, mundos, raridades.

## MVP

20 fases, 3 mundos, 1 personagem inicial, 5 tipos de tropas, 8 tipos de portais, 5 bosses, moedas, XP, upgrade, cartas simples, 10 skins, telas (inicial, gameplay, vitória, derrota, tropas, upgrades, loja), rewarded ad, analytics básico, Remote Config básico.

**Objetivo do MVP — validar:** entendimento em <3 s; corrida satisfatória; portais dão vontade de escolher; multiplicação visual viciante; boss gera tensão; derrota dá vontade de tentar de novo; vitória recompensa bem; gera bons vídeos de anúncio; usuário encadeia fases; rewarded converte bem.

## Game feel (prioridade máxima)

Resposta rápida ao toque; movimento suave; impacto ao passar por portais; som satisfatório de multiplicação; vibração leve; explosão de moedas; hit impact no boss; câmera aproximando no boss; slow motion no golpe final; feedbacks textuais: NICE, GREAT, INSANE, GODLIKE, PERFECT, MUTATION, MEGA ARMY, BOSS BREAKER.

## Regras de produto

- Sem menu complexo demais; sem tutorial longo.
- Jogar em até 5 segundos após abrir o app.
- Fase 1: extremamente fácil e satisfatória. Fase 2: já apresenta escolha estratégica. Fase 3: boss marcante. Fase 5: desbloqueia algo novo. Fase 10: recompensa grande.
- Sempre mostrar progresso; sempre dar motivo para "mais uma fase".

## Entregáveis (30)

1. GDD completo · 2. Core loop detalhado · 3. Progressão dos primeiros 30 min · 4. Progressão dos primeiros 7 dias · 5. Lista de sistemas · 6. Lista de telas · 7. Wireframe textual de cada tela · 8. Economia · 9. Sistema de unidades · 10. Sistema de bosses · 11. Sistema de portais · 12. Sistema de fases · 13. Sistema de upgrades · 14. Monetização · 15. Estratégia de ads e viralização · 16. Estrutura de projeto Unity · 17. Classes principais em C# · 18. ScriptableObjects · 19. Roadmap · 20. Backlog por prioridade · 21. Plano do MVP em 30 dias · 22. Expansão pós-MVP · 23. Ideias de anúncios em vídeo · 24. Ideias de thumbnails · 25. Nomes melhores · 26. Riscos · 27. Como evitar parecer clone barato · 28. Como deixar mais viciante · 29. Como deixar mais inteligente · 30. Como deixar mais viral.
