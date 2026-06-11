# THIRD-PARTY-NOTICES — Mutant Army Run

Este arquivo registra (a) a política de licenças do projeto e (b) os avisos de atribuição de
terceiros. A análise completa de cada fonte estudada está em `..\GDD\15-referencias-e-recursos.md`.
A planilha de assets (modelos, UI, áudio, VFX) é `licencas-de-assets.csv`, neste mesmo diretório.

---

## 1. Política de licenças

| Licença | Política | Regra |
|---|---|---|
| **CC0 1.0** | ✅ Livre | Usar, modificar e embarcar sem crédito. Fonte preferencial de assets. |
| **CC-BY (3.0/4.0)** | ⚠️ Permitido com crédito | Crédito obrigatório, centralizado na tela de Créditos (Configurações) + linha em `licencas-de-assets.csv`. Replicar o crédito na descrição da loja. |
| **MIT / Apache 2.0** | ✅ Permitido com atribuição | Pode copiar/adaptar código; preservar o aviso de copyright e o texto da licença (cabeçalho no fonte ou notice neste arquivo). |
| **SEM LICENÇA** | 🔍 Apenas estudo | Ausência de licença = todos os direitos reservados (Convenção de Berna). Ler para aprender o padrão; **reimplementar do zero**. Zero copy-paste — vale para TUDO em `..\_research\`. |
| **GPL / CC-BY-SA / CC-BY-NC** | ❌ Proibido | GPL e CC-BY-SA são virais (contaminariam o build inteiro); CC-BY-NC é incompatível com projeto comercial. Filtrar ANTES do download. |
| **Proprietárias específicas** (Unity EULA/UCL, DOTween, Mixkit, CraftPix, Mixamo) | ⚠️ Caso a caso | Uso comercial OK conforme os termos de cada uma; **nunca redistribuir os arquivos** (inclusive commit em repositório público). |

**Processo obrigatório de aquisição de asset:** filtro de licença na fonte → confirmar a licença na
página individual do item (tags são autodeclaradas) → registrar nome/URL/licença/autor/uso em
`licencas-de-assets.csv` antes de importar no projeto.

**Regra especial `_research\`:** os repositórios clonados em `..\_research\repos\` existem somente
para estudo de padrões e antipadrões. Nenhuma linha de código, asset, nome, string, valor de balance
ou layout de fase pode migrar de lá para este projeto — mesmo dos repositórios MIT, a arte é de
origem desconhecida e está igualmente proibida. Checklist de PR no §7 do GDD doc 15.

---

## 2. Notices de código de terceiros

### 2.1 Signals (Yanko Oliveira) — MIT — padrão reimplementado

- **Projeto:** Signals — <https://github.com/yankooliveira/signals>
- **Licença:** MIT
- **Uso neste projeto:** o barramento de eventos do jogo (`Assets\_Project\Scripts\Core\GameEvents.cs`
  e structs de payload em `EventStructs.cs`) segue o padrão de sinais fortemente tipados popularizado
  pelo projeto Signals. A implementação aqui foi **escrita do zero** (eventos C# estáticos + structs
  próprias do contrato do GDD doc 12 §3.2) — nenhuma linha foi copiada. Este notice é mantido por
  transparência e porque a política do projeto prevê absorver trechos do Signals no futuro; se isso
  ocorrer, o aviso de copyright abaixo acompanha o código adaptado.

```text
MIT License

Copyright (c) Yanko Oliveira (https://github.com/yankooliveira/signals)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### 2.2 SaveGameFree (Bayat Games) — MIT — referência conceitual

- **Projeto:** SaveGameFree — <https://github.com/BayatGames/SaveGameFree>
- **Licença:** MIT
- **Uso neste projeto:** referência conceitual para o desenho do `SaveSystem`
  (`Assets\_Project\Scripts\Meta\SaveSystem.cs`): serialização JSON local-first, gravação segura em
  disco e API de save/load. A implementação deste projeto foi **escrita do zero** com camadas
  próprias que o SaveGameFree não possui (checksum SHA-256 com salt, `schemaVersion` com migração
  incremental, gravação atômica tmp→bak→rename — Domain: `SaveChecksum.cs`, `SaveMigration.cs`).
  Nenhuma linha foi copiada; o notice abaixo é mantido por transparência e passa a ser obrigatório
  caso algum trecho venha a ser adaptado.

```text
MIT License

Copyright (c) Bayat Games (https://github.com/BayatGames/SaveGameFree)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

> Nota: ao adaptar qualquer trecho real de um projeto MIT, confirme o ano e o titular exatos no
> arquivo `LICENSE` do repositório de origem naquele momento e atualize a linha de copyright acima —
> o texto da licença acompanha o código, sempre.

---

## 3. Assets de terceiros

Todos os assets de arte, UI, animação e áudio entram pelo processo do §1 e são registrados em
**`licencas-de-assets.csv`** (colunas: `nome,url,licenca,autor,uso_no_projeto`). Assets CC-BY têm,
além da linha na planilha, crédito na tela de Créditos do jogo e na descrição da loja.

Nenhum SDK de terceiros (AppLovin MAX, Firebase, RevenueCat) está embarcado neste scaffold — os
serviços usam providers Null. Quando os SDKs forem integrados, seus próprios termos e notices serão
adicionados aqui.
