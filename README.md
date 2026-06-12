# Mutant Army Run

> **"Monte o exército mais absurdo possível em 60 segundos."**

Jogo mobile **hybrid-casual** original — um *runner* de multidão onde você atravessa portais que **multiplicam, transformam, fundem e mutam** seu exército, e enfrenta **bosses gigantes** no fim de cada fase. Inspirado nos gatilhos de viralização do gênero (multiplicação visual, portais, evolução rápida, números crescendo), mas com uma camada estratégica leve que o diferencia dos clones: **Boss Scout** (você vê a fraqueza do boss antes da fase), **Suprimento** (cada tropa tem custo — quantidade nem sempre vence qualidade) e **mutações visíveis**.

**Engine:** Unity 6 (6000.4) + URP · **Plataforma-alvo:** Android primeiro, iOS depois · **Orientação:** retrato 9:16.

---

## 📦 Conteúdo do jogo

| | |
|---|---|
| **Mundos** | 10 temáticos (campo, cidade zumbi, deserto, floresta tóxica, vulcão, gelo, medieval, lab alienígena, planeta mecânico, dimensão final) |
| **Fases** | 100 desenhadas + infinito procedural |
| **Tropas** | 19 (Soldado → Mecha Supremo), 4 raridades |
| **Portais** | 26 (matemáticos, classe, elemento, mutação, risco) |
| **Mutações** | 9 visíveis (asas, armadura, laser, gigantismo…) |
| **Bosses** | 19 (10 de mundo + variantes regionais) |
| **Meta** | 8 upgrades, cartas/evolução, baús, missões diárias, season pass |
| **Telas** | menu, tropas, upgrades, loja, mapa, diário, configurações, eventos, passe |

---

## 🗂️ Estrutura do repositório

```
.
├── GDD/                  # Documento de design completo (16 docs) — CANON.md é a fonte da verdade
├── MutantArmyRun/        # Projeto Unity 6 (abrir esta pasta no Unity Hub)
│   └── Assets/_Project/Scripts/
│       ├── Domain/       # Lógica pura (sem UnityEngine) — testada por dotnet
│       ├── Core/ Gameplay/ Meta/ Services/ UI/ Editor/
├── tests/                # 3 projetos xUnit (.NET 8) que compilam o Domain — rodam SEM o Unity
└── docs/                 # Plano de implementação
```

A arquitetura isola toda a **lógica determinística** (portais, suprimento, elementos, economia, save) no assembly `MutantArmy.Domain`, que **não depende do UnityEngine** e é compilado tanto pelo Unity quanto por projetos xUnit — então a parte onde bugs custam caro fica sob rede de testes que rodam em segundos, sem abrir a engine.

---

## ▶️ Como rodar

### Jogar
Abra `MutantArmyRun/` no **Unity Hub** (Unity 6000.4.x). Na primeira vez, no menu **MAR Tools**, rode `Setup Project` e `Create MVP Content` (e os demais factories de visual/áudio). Aperte **Play** na cena `Boot`.

### Testes do Domain (sem Unity)
```bash
dotnet test tests/Domain.Gameplay.Tests
dotnet test tests/Domain.Flow.Tests
dotnet test tests/Domain.Persistence.Tests
```
**327 testes** cobrindo a matemática de portais, suprimento, elementos, economia, save e missões.

### Testes do Unity (EditMode + PlayMode)
Window → General → Test Runner. 18 EditMode + 4 PlayMode (o loop completo jogado por um piloto automático).

---

## 📊 Status

Feature-completo como **MVP jogável de ponta a ponta** (menu → fase → portais → boss → recompensa → meta). Pendente de produção/negócio: integração dos SDKs reais de anúncios/compras (hoje atrás de *providers* Null, o jogo roda 100% sem eles), build Android assinado, e troca de alguns assets *placeholder*. Detalhes em `GDD/` e `MutantArmyRun/README.md`.

## ⚖️ Licença

Código do jogo: **proprietário** (ver [LICENSE](LICENSE)). Assets de terceiros são CC0/MIT — ver [`MutantArmyRun/THIRD-PARTY-NOTICES.md`](MutantArmyRun/THIRD-PARTY-NOTICES.md).
