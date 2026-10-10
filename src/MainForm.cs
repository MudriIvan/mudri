using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Assistente
{
    // Janela do assistente + ícone na bandeja (perto do relógio).
    // Fechar a janela só esconde; "Sair" no menu do ícone encerra.
    class MainForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modificadores, uint tecla);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        const int WM_HOTKEY = 0x0312;
        const uint MOD_NOREPEAT = 0x4000;
        const int ID_ATALHO = 1;

        static readonly Color CorLeitura = Color.FromArgb(37, 99, 235);
        static readonly Color CorAcao = Color.FromArgb(220, 38, 38);
        static readonly Color CorCinza = Color.FromArgb(110, 110, 120);

        readonly Config cfg;
        readonly bool primeiraVez;
        readonly ClaudeRunner runner = new ClaudeRunner();
        string sessionId;
        bool modoAcao;
        bool saindo;
        bool avisouBandeja;
        bool recebeuTexto;
        bool atalhoOk;
        char ultimoChar = '\n';

        RichTextBox conversa;
        TextBox entrada;
        Button enviar, parar, nova;
        CheckBox acao;
        Label status;
        NotifyIcon bandeja;
        ToolStripMenuItem menuAcao;
        Icon iconeLeitura, iconeAcao;

        public MainForm(Config cfg, bool primeiraVez)
        {
            this.cfg = cfg;
            this.primeiraVez = primeiraVez;

            Font = new Font("Segoe UI", 9.5f);
            Size = new Size(560, 640);
            MinimumSize = new Size(380, 360);
            StartPosition = FormStartPosition.Manual;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
            iconeLeitura = CriarIcone(CorLeitura);
            iconeAcao = CriarIcone(CorAcao);

            // Topo: modo, nova conversa, status
            var topo = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 6, 8, 0), WrapContents = false };
            acao = new CheckBox { Appearance = Appearance.Button, AutoSize = true, AutoCheck = false, FlatStyle = FlatStyle.Flat };
            acao.Click += (s, e) => AlternarModo();
            nova = new Button { Text = "Nova conversa", AutoSize = true, FlatStyle = FlatStyle.Flat };
            nova.Click += (s, e) => NovaConversa();
            status = new Label { AutoSize = true, Padding = new Padding(6, 7, 0, 0), ForeColor = CorCinza };
            topo.Controls.Add(acao);
            topo.Controls.Add(nova);
            topo.Controls.Add(status);

            // Base: campo do pedido + botões
            var rodape = new Panel { Dock = DockStyle.Bottom, Height = 92, Padding = new Padding(8) };
            entrada = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
            entrada.KeyDown += EntradaKeyDown;
            var botoes = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 100, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8, 0, 0, 0) };
            enviar = new Button { Text = "Enviar", Width = 88, Height = 34 };
            enviar.Click += (s, e) => Enviar();
            parar = new Button { Text = "Parar", Width = 88, Height = 34, Enabled = false };
            parar.Click += (s, e) => { runner.Parar(); status.Text = "parando..."; };
            botoes.Controls.Add(enviar);
            botoes.Controls.Add(parar);
            rodape.Controls.Add(entrada);
            rodape.Controls.Add(botoes);

            // Centro: a conversa
            conversa = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = SystemColors.Window, DetectUrls = true, Font = new Font("Segoe UI", 10.5f) };
            conversa.LinkClicked += (s, e) => { try { Process.Start(e.LinkText); } catch (Exception) { } };
            var centro = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 6), BackColor = SystemColors.Window };
            centro.Controls.Add(conversa);

            // Ordem importa: o Fill entra primeiro para ocupar o que sobra.
            Controls.Add(centro);
            Controls.Add(rodape);
            Controls.Add(topo);

            // Ícone da bandeja
            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir", null, (s, e) => Mostrar());
            menu.Items.Add("Nova conversa", null, (s, e) => NovaConversa());
            menuAcao = new ToolStripMenuItem("Modo ação", null, (s, e) => AlternarModo());
            menu.Items.Add(menuAcao);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Abrir pasta de configuração", null, (s, e) => AbrirPasta(Config.Pasta));
            menu.Items.Add("Abrir mapa de projetos (hub)", null, (s, e) => AbrirPasta(cfg.Hub));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Sair", null, (s, e) => { saindo = true; Close(); });
            bandeja = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
            bandeja.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Alternar(); };

            runner.Texto += (t, novo) => NaTela(() => ReceberTexto(t, novo));
            runner.Acao += a => NaTela(() => ReceberAcao(a));
            runner.Fim += r => NaTela(() => Concluir(r));

            AtualizarModo();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            uint mods, tecla;
            if (cfg.LerAtalho(out mods, out tecla))
                atalhoOk = RegisterHotKey(Handle, ID_ATALHO, mods | MOD_NOREPEAT, tecla);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (primeiraVez)
            {
                Sistema("Primeira execução. Criei a configuração em " + Config.Pasta + " e o mapa de projetos em " + Path.Combine(cfg.Hub, "CLAUDE.md") + ".\n" +
                        "Preencha o mapa e confira o config.json (nome, pastas dos projetos), depois saia e abra o app de novo. O README explica cada campo.");
            }
            if (cfg.PastasProjetos.Count == 0)
                Sistema("Nenhuma pasta em \"pastasProjetos\" no config.json: por enquanto eu só enxergo a pasta hub.");
            foreach (string pasta in cfg.PastasProjetos)
                if (!Directory.Exists(pasta))
                    Sistema("A pasta " + pasta + " (\"pastasProjetos\" no config.json) não existe neste computador e foi ignorada.");
            if (!atalhoOk)
                Sistema("Não consegui registrar o atalho \"" + cfg.Atalho + "\" (inválido ou em uso por outro programa). Troque em \"atalho\" no config.json.");
            else
                Sistema("Atalho para abrir e fechar: " + cfg.Atalho + ". Enter envia, Shift+Enter quebra a linha.");
            entrada.Focus();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == ID_ATALHO) Alternar();
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!saindo && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!avisouBandeja)
                {
                    bandeja.ShowBalloonTip(4000, cfg.Nome, "Continuo aqui perto do relógio. Clique no ícone ou use " + cfg.Atalho + ".", ToolTipIcon.Info);
                    avisouBandeja = true;
                }
                return;
            }
            runner.Parar();
            if (atalhoOk) UnregisterHotKey(Handle, ID_ATALHO);
            bandeja.Visible = false;
            base.OnFormClosing(e);
        }

        void EntradaKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                Enviar();
            }
        }

        void Alternar()
        {
            if (Visible && Form.ActiveForm == this) Hide();
            else Mostrar();
        }

        void Mostrar()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            entrada.Focus();
        }

        void Enviar()
        {
            string pedido = entrada.Text.Trim();
            if (pedido == "" || runner.Rodando) return;
            entrada.Clear();

            Separar();
            Escrever("Você\n", CorCinza, FontStyle.Bold);
            Escrever(pedido + "\n\n", ForeColor, FontStyle.Regular);
            Escrever(cfg.Nome + (modoAcao ? " (modo ação)" : "") + "\n", modoAcao ? CorAcao : CorLeitura, FontStyle.Bold);

            recebeuTexto = false;
            enviar.Enabled = false;
            parar.Enabled = true;
            status.Text = "pensando...";
            runner.Enviar(cfg, pedido, sessionId, modoAcao);
        }

        void ReceberTexto(string texto, bool blocoNovo)
        {
            if (blocoNovo && ultimoChar != '\n') Escrever("\n", ForeColor, FontStyle.Regular);
            Escrever(texto, ForeColor, FontStyle.Regular);
            recebeuTexto = true;
            status.Text = "respondendo...";
        }

        void ReceberAcao(string descricao)
        {
            if (ultimoChar != '\n') Escrever("\n", ForeColor, FontStyle.Regular);
            Escrever("→ " + descricao + "\n", CorCinza, FontStyle.Italic);
            status.Text = descricao.Length > 50 ? descricao.Substring(0, 50) + "..." : descricao;
        }

        void Concluir(Resultado r)
        {
            if (r.SessionId != null) sessionId = r.SessionId;
            if (r.Erro)
            {
                if (ultimoChar != '\n') Escrever("\n", ForeColor, FontStyle.Regular);
                Escrever(r.Texto + "\n", CorAcao, FontStyle.Regular);
            }
            else if (!recebeuTexto && r.Texto != "")
                Escrever(r.Texto, ForeColor, FontStyle.Regular);
            if (ultimoChar != '\n') Escrever("\n", ForeColor, FontStyle.Regular);
            Escrever("(" + Math.Round(r.Segundos) + " s)\n", CorCinza, FontStyle.Regular);

            enviar.Enabled = true;
            parar.Enabled = false;
            status.Text = "";
            if (!Visible)
            {
                string resumo = r.Texto.Length > 200 ? r.Texto.Substring(0, 200) + "..." : r.Texto;
                bandeja.ShowBalloonTip(6000, cfg.Nome, resumo == "" ? "Pronto." : resumo, r.Erro ? ToolTipIcon.Error : ToolTipIcon.Info);
            }
        }

        void NovaConversa()
        {
            if (runner.Rodando) return;
            sessionId = null;
            conversa.Clear();
            ultimoChar = '\n';
            Sistema("Nova conversa.");
        }

        void AlternarModo()
        {
            if (!modoAcao)
            {
                DialogResult r = MessageBox.Show(this,
                    "No modo ação o " + cfg.Nome + " executa comandos, edita arquivos e faz commits sem pedir permissão.\n\n" +
                    "Só ficam bloqueados os comandos da lista proibida (config.json). O \"Confirma?\" antes de push, deploy ou mensagem depende de ele seguir as instruções.\n\n" +
                    "Ligar o modo ação?", cfg.Nome, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }
            modoAcao = !modoAcao;
            AtualizarModo();
            Sistema(modoAcao ? "Modo ação ligado. Desligue quando terminar." : "Modo somente leitura.");
        }

        void AtualizarModo()
        {
            Icon = modoAcao ? iconeAcao : iconeLeitura;
            bandeja.Icon = Icon;
            string titulo = cfg.Nome + (modoAcao ? " - modo ação" : " - somente leitura");
            Text = titulo;
            bandeja.Text = titulo.Length > 63 ? titulo.Substring(0, 63) : titulo;
            acao.Checked = modoAcao;
            acao.Text = modoAcao ? "Modo ação: ligado" : "Modo ação: desligado";
            acao.BackColor = modoAcao ? Color.FromArgb(254, 226, 226) : SystemColors.Control;
            acao.ForeColor = modoAcao ? CorAcao : SystemColors.ControlText;
            menuAcao.Checked = modoAcao;
        }

        void Sistema(string texto)
        {
            Separar();
            Escrever(texto + "\n", CorCinza, FontStyle.Italic);
        }

        void Separar()
        {
            if (conversa.TextLength > 0) Escrever("\n", ForeColor, FontStyle.Regular);
        }

        void Escrever(string texto, Color cor, FontStyle estilo)
        {
            if (string.IsNullOrEmpty(texto)) return;
            texto = texto.Replace("\r\n", "\n");
            conversa.SelectionStart = conversa.TextLength;
            conversa.SelectionLength = 0;
            conversa.SelectionColor = cor;
            conversa.SelectionFont = new Font(conversa.Font, estilo);
            conversa.SelectedText = texto;
            conversa.SelectionStart = conversa.TextLength;
            conversa.ScrollToCaret();
            ultimoChar = texto[texto.Length - 1];
        }

        void NaTela(Action a)
        {
            if (IsDisposed) return;
            try { BeginInvoke(a); }
            catch (InvalidOperationException) { }
        }

        static void AbrirPasta(string pasta)
        {
            try { Process.Start("explorer.exe", "\"" + pasta + "\""); }
            catch (Exception) { }
        }

        // Ícone redondo com a inicial do nome: azul no modo leitura, vermelho no modo ação.
        Icon CriarIcone(Color cor)
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (var fundo = new SolidBrush(cor))
                using (var fonte = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var formato = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);
                    g.FillEllipse(fundo, 1, 1, 30, 30);
                    string letra = cfg.Nome.Length > 0 ? cfg.Nome.Substring(0, 1).ToUpperInvariant() : "A";
                    g.DrawString(letra, fonte, Brushes.White, new RectangleF(0, 1, 32, 32), formato);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}
