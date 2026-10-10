# Assistente pessoal (Claude Code) para Windows

Um app pequeno que fica perto do relógio do Windows: você aperta um atalho,
escreve o que quer (a voz vem na próxima etapa), e o **Claude Code** executa
nos seus projetos e responde. O app em si não tem inteligência: ele só entrega
o pedido para o `claude -p` e mostra a resposta conforme chega.

Adaptado do guia "Como criar seu assistente de voz com Claude Code" (Murilo
Parrillo), que é para Mac. Aqui tudo usa o que já vem no Windows: sem
instalador, sem Visual Studio, sem administrador.

## O que já funciona (etapas 1 a 3 do guia)

- **Hub com mapa de projetos**: uma pasta com um `CLAUDE.md` que descreve seus
  projetos. É o que faz o assistente "conhecer" o seu trabalho.
- **App de bandeja** com campo de texto e painel de resposta em tempo real
  (mostra também o que ele está fazendo: "Lendo arquivo X", "Rodando: git log").
- **Conversa com memória**: o pedido seguinte continua a mesma conversa.
  "Nova conversa" zera.
- **Modo somente leitura como padrão**: pode ler, buscar, consultar git e
  GitHub e pesquisar na web. Editar e escrever ficam bloqueados de verdade
  (bloqueio técnico, não só instrução).
- **Modo ação** (botão, com confirmação): executa comandos, edita, faz commit.
  Continua bloqueando `rm -rf`, `git push --force`, `git reset --hard`,
  `format`, `diskpart` etc., e pergunta "Confirma?" antes de push, deploy,
  mensagens e qualquer coisa em produção.
- Atalho global **Ctrl+Alt+Espaço** para abrir/esconder.

Próximas etapas: voz de entrada (atalho + gravação com corte por silêncio +
transcrição), voz de saída, resumo do dia, Chrome.

## Antes de começar

- Windows 10 ou 11.
- **Claude Code instalado e logado** no notebook. Teste no terminal do VS Code:
  `claude --version`.
- Git for Windows (o Claude Code no Windows usa o Git Bash).
- `gh` (GitHub CLI) logado: opcional, para perguntas sobre pull requests.

## Instalar

1. Baixe este repositório no notebook (no terminal do VS Code):

   ```
   git clone https://github.com/MudriIvan/mudri
   cd mudri
   git checkout claude/oi-639ry2
   ```

   Se já tiver clonado, só `git pull`.

2. Compile:

   ```
   build.bat
   ```

   Gera `dist\Assistente.exe`. Rodar de novo atualiza (e fecha o assistente se
   estiver aberto).

3. Abra `dist\Assistente.exe` (clique duas vezes). Na primeira vez ele cria:

   - `%USERPROFILE%\.assistente\config.json`: a configuração.
   - `%USERPROFILE%\.assistente\hub\CLAUDE.md`: o mapa de projetos, já com
     v0-mudri, motor e empresas. **Troque `[PASTA]` pelo caminho real.**
   - `%USERPROFILE%\.assistente\instrucoes-*.md`: como ele responde em cada
     modo. Pode editar.

4. Em `config.json`, ajuste **`pastasProjetos`** para a pasta onde ficam seus
   repositórios. Sem isso ele só enxerga o hub. Depois clique com o botão
   direito no ícone perto do relógio > **Sair**, e abra de novo.

5. Teste: "o que mudou ontem no v0-mudri?" ou "onde paramos na Mudri?".

Dica: para abrir com o Windows, coloque um atalho do `Assistente.exe` em
`shell:startup` (Win+R, digite `shell:startup`).

## Testar o hub sem o app (etapa 1 do guia)

Se a resposta for boa no terminal, o resto é só embalagem:

```
cd %USERPROFILE%\.assistente\hub
claude -p "o que foi publicado ontem no v0-mudri?" --add-dir C:\caminho\dos\projetos
```

Para o Claude completar o mapa com você, abra o Claude Code dentro do hub
(`claude`) e peça: "liste as pastas em C:\caminho\dos\projetos, me pergunte o
que é cada uma e como eu falo o nome dela, e atualize o CLAUDE.md, registrando
quais têm mais de um repositório dentro".

## config.json

| Campo | Para quê |
| --- | --- |
| `nome`, `usuario` | Nome do assistente (ícone e janela) e o seu |
| `pastasProjetos` | Pastas que ele pode ler além do hub (vira `--add-dir`) |
| `hub` | Pasta do mapa de projetos |
| `atalho` | Ex.: `Ctrl+Alt+Space`, `Ctrl+Shift+J`, `Win+Alt+M` |
| `claudePath` | Vazio = procura sozinho; preencha se ele não achar o Claude Code |
| `gitBashPath` | Só se o Claude Code não achar o Git Bash |
| `modelo` | Vazio = o padrão do seu Claude Code |
| `ferramentasLeitura` | Lista do que o modo leitura pode usar (padrão: Read, Glob, Grep, WebSearch, WebFetch, `git status/log/diff/show/branch`, `gh pr/issue/run list/view`, `ls`, `cd`) |
| `bloqueadasLeitura` | Padrão: Edit, Write, NotebookEdit |
| `bloqueadasAcao` | Padrão: sudo, `rm -rf`/`rm -r`, `git push --force`/`-f`, `git reset --hard`, `git clean`, dd, format, diskpart, `rd /s`, `rmdir /s`, `del /s` |

Chaves de API (transcrição e voz, nas próximas etapas) não vão no
`config.json` nem no repositório: cada uma num arquivo próprio em
`%USERPROFILE%\.assistente`.

## Segurança, sem enganação

- **Leitura** é o modo padrão e o bloqueio de escrita é técnico.
- No **modo ação** nada pede permissão. A lista proibida é uma trava técnica,
  mas funciona por começo de comando (pega `rm -rf x`, não pega um `rm`
  escondido dentro de um script). O "Confirma?" é uma instrução que o modelo
  segue, não uma trava. Deixe o modo ação desligado quando não estiver usando.
- Regras que você já deu ao Claude Code (`~/.claude/settings.json`) continuam
  valendo também aqui. Se lá estiver liberado algo que escreve, o modo leitura
  também libera.
- Notebook da empresa: o modo ação usa `--permission-mode bypassPermissions`.
  Se a TI tiver bloqueado esse modo, o Claude Code recusa e o erro aparece no
  painel.

## Erros comuns

| O que acontece | Como resolver |
| --- | --- |
| "Não achei o Claude Code" | Preencha `claudePath` com o caminho (no terminal: `where claude`) |
| Erro sobre Git Bash | Instale o Git for Windows ou preencha `gitBashPath` |
| "Não consegui registrar o atalho" | Outro programa usa a mesma combinação: troque `atalho` |
| Erro ao continuar a conversa | Clique em "Nova conversa" |
| Ele não acha um projeto | A pasta não está em `pastasProjetos` ou o mapa está desatualizado |
| `build.bat` não acha o `csc.exe` | Raro (.NET Framework 4 vem no Windows 10/11); mande o erro |
| Windows bloqueia o `.exe` | Política da empresa (AppLocker); fale com a TI ou me avise para pensarmos outra forma |

## Estrutura

```
build.bat          compila com o csc.exe do Windows
src/Program.cs     início, uma instância só, primeira execução
src/Config.cs      config.json, hub, instruções, achar o claude, atalho
src/ClaudeRunner.cs  roda claude -p (stream-json) e traduz os eventos
src/MainForm.cs    janela, bandeja, atalho global, modos
modelos/           modelos copiados na primeira execução
```
