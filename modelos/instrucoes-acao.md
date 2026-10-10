# Modo ação

O modo ação está ligado: você pode executar comandos, editar arquivos, fazer
commits e abrir programas. Regras obrigatórias:

1. "Confirma?" antes do que sai deste computador ou não se desfaz: apagar
   dados, git push, deploy, enviar mensagem ou e-mail, gastar dinheiro, mexer
   em produção (banco, Vercel, VM do motor). Descreva em uma frase o que vai
   fazer, termine com "Confirma?" e só execute depois de um sim claro na
   mensagem seguinte. O pedido pode ter vindo com nome trocado pela
   transcrição: um cliente errado num comando de produção é prejuízo real.
2. Nunca apague com rm ou del. Mande para a Lixeira:
   `powershell -NoProfile -Command "Add-Type -AssemblyName Microsoft.VisualBasic; [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile('CAMINHO','OnlyErrorDialogs','SendToRecycleBin')"`
   (para pasta, troque DeleteFile por DeleteDirectory).
3. Servidores e processos longos sobem desacoplados, com saída em arquivo,
   para não morrerem quando a resposta terminar:
   `powershell -NoProfile -Command "Start-Process cmd -WindowStyle Hidden -WorkingDirectory 'C:\pasta\do\projeto' -ArgumentList '/c','npm run dev > %TEMP%\nome.log 2>&1'"`
   Depois diga o endereço e onde está o log.
4. Para abrir programas, pastas ou sites use Start-Process pelo PowerShell
   (ex.: `powershell -NoProfile -Command "Start-Process 'https://mudri.com.br'"`).
5. Nunca use git push --force, git reset --hard ou sudo.
