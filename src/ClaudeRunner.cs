using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Assistente
{
    class Resultado
    {
        public string Texto = "";
        public string SessionId;
        public bool Erro;
        public double Segundos;
    }

    // Roda `claude -p` no hub em modo stream-json e traduz os eventos para a tela.
    // O pedido vai pela entrada padrão (stdin), não pela linha de comando, para
    // acentos e caracteres especiais chegarem intactos.
    class ClaudeRunner
    {
        public event Action<string, bool> Texto;   // pedaço de texto, começa bloco novo?
        public event Action<string> Acao;          // "Lendo arquivo X", "Rodando: git log"
        public event Action<Resultado> Fim;

        Process proc;
        volatile bool parado;
        readonly StringBuilder erros = new StringBuilder();
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public bool Rodando
        {
            get
            {
                Process p = proc;
                try { return p != null && !p.HasExited; }
                catch (InvalidOperationException) { return false; }
            }
        }

        public void Enviar(Config cfg, string pergunta, string sessionId, bool modoAcao)
        {
            string claude = cfg.ResolverClaude();
            if (claude == null)
            {
                Terminar(Falha("Não achei o Claude Code neste computador. Instale-o ou informe o caminho em \"claudePath\" no config.json."), null);
                return;
            }

            var args = new List<string> { "-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages" };
            if (!string.IsNullOrEmpty(sessionId)) { args.Add("--resume"); args.Add(sessionId); }
            if (cfg.Modelo != "") { args.Add("--model"); args.Add(cfg.Modelo); }
            foreach (string pasta in cfg.PastasProjetos)
                if (Directory.Exists(pasta)) { args.Add("--add-dir"); args.Add(pasta); }
            args.Add("--append-system-prompt-file"); args.Add(cfg.MontarInstrucoes(modoAcao));
            if (modoAcao)
            {
                args.Add("--permission-mode"); args.Add("bypassPermissions");
                if (cfg.BloqueadasAcao.Count > 0) { args.Add("--disallowedTools"); args.AddRange(cfg.BloqueadasAcao); }
            }
            else
            {
                // "default" explícito: ignora um modo mais permissivo que esteja nas configurações do usuário.
                args.Add("--permission-mode"); args.Add("default");
                if (cfg.FerramentasLeitura.Count > 0) { args.Add("--allowedTools"); args.AddRange(cfg.FerramentasLeitura); }
                if (cfg.BloqueadasLeitura.Count > 0) { args.Add("--disallowedTools"); args.AddRange(cfg.BloqueadasLeitura); }
            }

            var psi = new ProcessStartInfo();
            string ext = Path.GetExtension(claude).ToLowerInvariant();
            if (ext == ".cmd" || ext == ".bat")
            {
                // Instalação pelo npm: o claude é um .cmd, que precisa passar pelo cmd.exe.
                psi.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                psi.Arguments = "/d /s /c \"" + Aspas(claude) + " " + Juntar(args) + "\"";
            }
            else
            {
                psi.FileName = claude;
                psi.Arguments = Juntar(args);
            }
            psi.WorkingDirectory = cfg.Hub;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Utf8;
            psi.StandardErrorEncoding = Utf8;
            if (cfg.GitBashPath != "") psi.EnvironmentVariables["CLAUDE_CODE_GIT_BASH_PATH"] = cfg.GitBashPath;

            parado = false;
            lock (erros) erros.Length = 0;
            DateTime inicio = DateTime.Now;
            Process p;
            try
            {
                p = new Process();
                p.StartInfo = psi;
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (erros) erros.AppendLine(e.Data); };
                p.Start();
                p.BeginErrorReadLine();
                using (var w = new StreamWriter(p.StandardInput.BaseStream, Utf8))
                    w.Write(pergunta);
            }
            catch (Exception ex)
            {
                Terminar(Falha("Não consegui iniciar o Claude Code (" + claude + "): " + ex.Message), null);
                return;
            }
            proc = p;
            bool retomou = !string.IsNullOrEmpty(sessionId);
            var t = new Thread(() => Ler(p, inicio, retomou));
            t.IsBackground = true;
            t.Start();
        }

        public void Parar()
        {
            Process p = proc;
            if (p == null) return;
            parado = true;
            try
            {
                if (p.HasExited) return;
                // /t derruba também os processos filhos (node, git bash...).
                var psi = new ProcessStartInfo("taskkill", "/pid " + p.Id + " /t /f");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (Process k = Process.Start(psi)) k.WaitForExit(5000);
            }
            catch (Exception) { }
        }

        void Ler(Process p, DateTime inicio, bool retomou)
        {
            Resultado res = null;
            string sessao = null;
            bool escreveuTexto = false, blocoNovo = false;
            var ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            try
            {
                string linha;
                while ((linha = p.StandardOutput.ReadLine()) != null)
                {
                    linha = linha.Trim();
                    if (linha.Length == 0 || linha[0] != '{') continue;
                    Dictionary<string, object> ev;
                    try { ev = ser.DeserializeObject(linha) as Dictionary<string, object>; }
                    catch (ArgumentException) { continue; }
                    if (ev == null) continue;

                    string sid = Str(ev, "session_id");
                    if (sid != null) sessao = sid;
                    string tipo = Str(ev, "type");

                    if (tipo == "stream_event")
                    {
                        var e = Obj(ev, "event");
                        string et = Str(e, "type");
                        if (et == "message_start") blocoNovo = escreveuTexto;
                        else if (et == "content_block_delta")
                        {
                            var delta = Obj(e, "delta");
                            string s = Str(delta, "text");
                            if (Str(delta, "type") == "text_delta" && !string.IsNullOrEmpty(s))
                            {
                                Disparar(Texto, s, blocoNovo);
                                escreveuTexto = true;
                                blocoNovo = false;
                            }
                        }
                    }
                    else if (tipo == "assistant")
                    {
                        foreach (object item in Lista(Obj(ev, "message"), "content"))
                        {
                            var bloco = item as Dictionary<string, object>;
                            if (Str(bloco, "type") == "tool_use")
                            {
                                Action<string> a = Acao;
                                if (a != null) a(Descrever(Str(bloco, "name"), Obj(bloco, "input")));
                                blocoNovo = escreveuTexto;
                            }
                        }
                    }
                    else if (tipo == "result")
                    {
                        res = new Resultado();
                        res.Texto = Str(ev, "result") ?? "";
                        string sub = Str(ev, "subtype") ?? "success";
                        res.Erro = Bool(ev, "is_error") || sub != "success";
                        if (res.Erro && res.Texto == "") res.Texto = "O Claude Code terminou com erro (" + sub + ").";
                    }
                }
                p.WaitForExit();
            }
            catch (Exception ex)
            {
                if (res == null) res = Falha("Erro lendo a resposta: " + ex.Message);
            }

            if (parado) res = new Resultado { Texto = "Interrompido." };
            else if (res == null)
            {
                string detalhe;
                lock (erros) detalhe = erros.ToString().Trim();
                res = Falha("O Claude Code terminou sem resposta." + (detalhe != "" ? "\n" + Cortar(detalhe, 800) : ""));
            }
            if (res.Erro && retomou) res.Texto += "\nSe o erro continuar, clique em \"Nova conversa\".";
            res.Segundos = (DateTime.Now - inicio).TotalSeconds;
            Terminar(res, sessao);
        }

        void Terminar(Resultado res, string sessao)
        {
            if (sessao != null) res.SessionId = sessao;
            Action<Resultado> f = Fim;
            if (f != null) f(res);
        }

        void Disparar(Action<string, bool> ev, string s, bool novo)
        {
            if (ev != null) ev(s, novo);
        }

        static Resultado Falha(string texto)
        {
            return new Resultado { Texto = texto, Erro = true };
        }

        static string Descrever(string nome, Dictionary<string, object> input)
        {
            switch (nome)
            {
                case "Read": return "Lendo " + NomeArquivo(Str(input, "file_path"));
                case "Edit": case "MultiEdit": return "Editando " + NomeArquivo(Str(input, "file_path"));
                case "Write": return "Escrevendo " + NomeArquivo(Str(input, "file_path"));
                case "Glob": return "Procurando arquivos: " + Str(input, "pattern");
                case "Grep": return "Buscando \"" + Str(input, "pattern") + "\"";
                case "Bash": case "PowerShell": return "Rodando: " + Cortar(Str(input, "command"), 120);
                case "WebSearch": return "Pesquisando na web: " + Str(input, "query");
                case "WebFetch": return "Abrindo " + Str(input, "url");
                case "Task": case "Agent": return "Delegando: " + Str(input, "description");
                case "TodoWrite": return "Organizando as etapas";
                default: return "Usando " + nome;
            }
        }

        static string NomeArquivo(string caminho)
        {
            if (string.IsNullOrEmpty(caminho)) return "arquivo";
            try { return Path.GetFileName(caminho); }
            catch (ArgumentException) { return caminho; }
        }

        static string Cortar(string s, int max)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        // Aspas no padrão do Windows (CommandLineToArgvW). Também protege ( ) & | < > ^
        // quando o claude é um .cmd que passa pelo cmd.exe.
        static string Aspas(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"', '(', ')', '&', '|', '<', '>', '^' }) < 0) return a;
            var sb = new StringBuilder("\"");
            int barras = 0;
            foreach (char c in a)
            {
                if (c == '\\') { barras++; continue; }
                if (c == '"') { sb.Append('\\', barras * 2 + 1); sb.Append('"'); barras = 0; continue; }
                sb.Append('\\', barras);
                barras = 0;
                sb.Append(c);
            }
            sb.Append('\\', barras * 2);
            sb.Append('"');
            return sb.ToString();
        }

        static string Juntar(List<string> args)
        {
            var partes = new List<string>();
            foreach (string a in args) partes.Add(Aspas(a));
            return string.Join(" ", partes.ToArray());
        }

        static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v)) return v as string;
            return null;
        }

        static bool Bool(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) && v is bool && (bool)v;
        }

        static Dictionary<string, object> Obj(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v)) return v as Dictionary<string, object>;
            return null;
        }

        static IEnumerable Lista(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v is IEnumerable && !(v is string)) return (IEnumerable)v;
            return new object[0];
        }
    }
}
