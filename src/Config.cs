using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Assistente
{
    // Configuração do assistente. Fica em %USERPROFILE%\.assistente, fora do
    // repositório: config.json, instruções e a pasta hub com o mapa de projetos.
    class Config
    {
        public string Nome;
        public string Usuario;
        public string ClaudePath;
        public string Hub;
        public List<string> PastasProjetos;
        public string Atalho;
        public string GitBashPath;
        public string Modelo;
        public List<string> FerramentasLeitura;
        public List<string> BloqueadasLeitura;
        public List<string> BloqueadasAcao;

        static readonly string[] PadraoLeitura = {
            "Read", "Glob", "Grep", "WebSearch", "WebFetch",
            "Bash(cd:*)", "Bash(ls:*)", "Bash(pwd)",
            "Bash(git status:*)", "Bash(git log:*)", "Bash(git diff:*)", "Bash(git show:*)",
            "Bash(git branch:*)", "Bash(git remote -v)",
            "Bash(gh pr list:*)", "Bash(gh pr view:*)", "Bash(gh pr status:*)",
            "Bash(gh issue list:*)", "Bash(gh issue view:*)", "Bash(gh run list:*)", "Bash(gh run view:*)"
        };
        static readonly string[] PadraoBloqueadasLeitura = { "Edit", "Write", "NotebookEdit" };
        static readonly string[] PadraoBloqueadasAcao = {
            "Bash(sudo:*)", "Bash(rm -rf:*)", "Bash(rm -fr:*)", "Bash(rm -r:*)",
            "Bash(git push --force:*)", "Bash(git push -f:*)", "Bash(git reset --hard:*)", "Bash(git clean:*)",
            "Bash(dd:*)", "Bash(format:*)", "Bash(diskpart:*)", "Bash(rd /s:*)", "Bash(rmdir /s:*)", "Bash(del /s:*)"
        };

        public static string Pasta
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".assistente"); }
        }

        public static string Arquivo
        {
            get { return Path.Combine(Pasta, "config.json"); }
        }

        static string PastaModelos
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modelos"); }
        }

        // Cria config.json e as instruções na primeira execução. Nunca sobrescreve.
        public static bool PrepararArquivos()
        {
            Directory.CreateDirectory(Pasta);
            bool criou = CopiarModelo("config.json", Arquivo);
            CopiarModelo("instrucoes-base.md", Path.Combine(Pasta, "instrucoes-base.md"));
            CopiarModelo("instrucoes-leitura.md", Path.Combine(Pasta, "instrucoes-leitura.md"));
            CopiarModelo("instrucoes-acao.md", Path.Combine(Pasta, "instrucoes-acao.md"));
            return criou;
        }

        static bool CopiarModelo(string nome, string destino)
        {
            if (File.Exists(destino)) return false;
            string origem = Path.Combine(PastaModelos, nome);
            if (!File.Exists(origem))
                throw new FileNotFoundException("Modelo não encontrado: " + origem + ". Rode o build.bat de novo.");
            File.Copy(origem, destino);
            return true;
        }

        public static Config Carregar()
        {
            var ser = new JavaScriptSerializer();
            var d = ser.DeserializeObject(File.ReadAllText(Arquivo, Encoding.UTF8)) as Dictionary<string, object>;
            if (d == null) throw new Exception("o arquivo não é um objeto JSON.");

            var c = new Config();
            c.Nome = Texto(d, "nome", "Max");
            c.Usuario = Texto(d, "usuario", Environment.UserName);
            c.ClaudePath = Expandir(Texto(d, "claudePath", ""));
            c.Hub = Expandir(Texto(d, "hub", Path.Combine(Pasta, "hub")));
            c.PastasProjetos = Lista(d, "pastasProjetos", new string[0]).ConvertAll<string>(Expandir);
            c.Atalho = Texto(d, "atalho", "Ctrl+Alt+Space");
            c.GitBashPath = Expandir(Texto(d, "gitBashPath", ""));
            c.Modelo = Texto(d, "modelo", "");
            c.FerramentasLeitura = Lista(d, "ferramentasLeitura", PadraoLeitura);
            c.BloqueadasLeitura = Lista(d, "bloqueadasLeitura", PadraoBloqueadasLeitura);
            c.BloqueadasAcao = Lista(d, "bloqueadasAcao", PadraoBloqueadasAcao);
            return c;
        }

        // Cria o hub com o modelo do mapa de projetos, se ainda não existir.
        public bool PrepararHub()
        {
            Directory.CreateDirectory(Hub);
            string destino = Path.Combine(Hub, "CLAUDE.md");
            if (File.Exists(destino)) return false;
            string modelo = File.ReadAllText(Path.Combine(PastaModelos, "hub-CLAUDE.md"), Encoding.UTF8);
            File.WriteAllText(destino, Substituir(modelo), new UTF8Encoding(false));
            return true;
        }

        public string Substituir(string texto)
        {
            return texto.Replace("{NOME}", Nome).Replace("{USUARIO}", Usuario);
        }

        // Junta as instruções do modo em uso num arquivo só, passado ao Claude
        // com --append-system-prompt-file (evita problema de aspas na linha de comando).
        public string MontarInstrucoes(bool modoAcao)
        {
            var sb = new StringBuilder();
            sb.AppendLine(LerInstrucao("instrucoes-base.md"));
            sb.AppendLine();
            sb.AppendLine(LerInstrucao(modoAcao ? "instrucoes-acao.md" : "instrucoes-leitura.md"));
            string destino = Path.Combine(Pasta, "instrucoes-em-uso.md");
            File.WriteAllText(destino, Substituir(sb.ToString()), new UTF8Encoding(false));
            return destino;
        }

        static string LerInstrucao(string nome)
        {
            string arquivo = Path.Combine(Pasta, nome);
            if (!File.Exists(arquivo)) arquivo = Path.Combine(PastaModelos, nome);
            return File.Exists(arquivo) ? File.ReadAllText(arquivo, Encoding.UTF8) : "";
        }

        // Acha o claude: config, instalador nativo, PATH e por último o npm.
        public string ResolverClaude()
        {
            if (ClaudePath != "") return ClaudePath;
            var candidatos = new List<string>();
            candidatos.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".local\bin\claude.exe"));
            foreach (string item in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                string dir = item.Trim().Trim('"');
                if (dir == "") continue;
                try
                {
                    candidatos.Add(Path.Combine(dir, "claude.exe"));
                    candidatos.Add(Path.Combine(dir, "claude.cmd"));
                }
                catch (ArgumentException) { }
            }
            candidatos.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\claude.cmd"));
            foreach (string c in candidatos)
                if (File.Exists(c)) return c;
            return null;
        }

        // "Ctrl+Alt+Space" → modificadores e tecla para o RegisterHotKey.
        public bool LerAtalho(out uint modificadores, out uint tecla)
        {
            modificadores = 0;
            tecla = 0;
            foreach (string parte in Atalho.Split('+'))
            {
                string p = parte.Trim();
                string s = p.ToLowerInvariant();
                if (s == "ctrl" || s == "control") modificadores |= 0x2;
                else if (s == "alt") modificadores |= 0x1;
                else if (s == "shift") modificadores |= 0x4;
                else if (s == "win") modificadores |= 0x8;
                else
                {
                    Keys k;
                    if (!Enum.TryParse(p, true, out k)) return false;
                    tecla = (uint)k;
                }
            }
            return tecla != 0;
        }

        static string Texto(Dictionary<string, object> d, string chave, string padrao)
        {
            object v;
            if (d.TryGetValue(chave, out v) && v is string && ((string)v).Trim() != "") return ((string)v).Trim();
            return padrao;
        }

        static List<string> Lista(Dictionary<string, object> d, string chave, string[] padrao)
        {
            object v;
            if (!d.TryGetValue(chave, out v) || v == null || v is string || !(v is IEnumerable)) return new List<string>(padrao);
            var lista = new List<string>();
            foreach (object item in (IEnumerable)v)
            {
                string s = item as string;
                if (!string.IsNullOrEmpty(s) && s.Trim() != "") lista.Add(s.Trim());
            }
            return lista;
        }

        static string Expandir(string caminho)
        {
            if (string.IsNullOrEmpty(caminho)) return "";
            string c = Environment.ExpandEnvironmentVariables(caminho);
            if (c.StartsWith("~")) c = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + c.Substring(1);
            return c;
        }
    }
}
