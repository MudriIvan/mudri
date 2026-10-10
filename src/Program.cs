using System;
using System.Threading;
using System.Windows.Forms;

namespace Assistente
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool primeiro;
            using (var unico = new Mutex(true, "Assistente-Claude-Code", out primeiro))
            {
                if (!primeiro)
                {
                    MessageBox.Show("O assistente já está aberto. Procure o ícone perto do relógio.", "Assistente");
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Config cfg;
                bool primeiraVez;
                try
                {
                    primeiraVez = Config.PrepararArquivos();
                    cfg = Config.Carregar();
                    primeiraVez |= cfg.PrepararHub();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao preparar a configuração em " + Config.Pasta + ":\n\n" + ex.Message,
                        "Assistente", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Application.Run(new MainForm(cfg, primeiraVez));
            }
        }
    }
}
