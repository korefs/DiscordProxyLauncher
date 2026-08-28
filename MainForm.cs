using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiscordProxyLauncher
{
    internal sealed class MainForm : Form
    {
        private const string ProxyHost = "181.39.25.196";
        private const int ProxyPort = 8118;
        private const int StartupDelaySeconds = 10;

        private readonly Label _statusLabel;
        private readonly Button _runButton;
        private readonly Button _closeButton;
        private readonly ProgressBar _progressBar;
        private readonly Panel _statusDot;

        private bool _running;

        public MainForm()
        {
            Text = "Discord Proxy Launcher";
            ClientSize = new Size(520, 356);
            MinimumSize = new Size(520, 356);
            MaximumSize = new Size(520, 356);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(24, 25, 28);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                // O ícone da janela é apenas visual; não impede o funcionamento.
            }

            Label titleLabel = new Label
            {
                Text = "Discord Proxy Launcher",
                AutoSize = true,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                Location = new Point(30, 28),
                ForeColor = Color.White
            };

            Label descriptionLabel = new Label
            {
                Text = "Reinicia o Discord com o proxy ativo e restaura para as configurações de proxy originais.",
                AutoSize = false,
                Size = new Size(455, 46),
                Location = new Point(32, 73),
                ForeColor = Color.FromArgb(180, 180, 185)
            };

            Label proxyLabel = new Label
            {
                Text = "Proxy: " + ProxyHost + ":" + ProxyPort,
                AutoSize = true,
                Location = new Point(32, 122),
                ForeColor = Color.FromArgb(205, 205, 210)
            };

            _statusDot = new Panel
            {
                Size = new Size(10, 10),
                Location = new Point(34, 161),
                BackColor = Color.FromArgb(145, 145, 150)
            };
            MakeCircular(_statusDot);

            _statusLabel = new Label
            {
                Text = "Pronto",
                AutoSize = true,
                Location = new Point(52, 156),
                ForeColor = Color.FromArgb(210, 210, 215)
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(32, 188),
                Size = new Size(456, 8),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            _runButton = new Button
            {
                Text = "Reiniciar Discord com Proxy",
                Location = new Point(32, 218),
                Size = new Size(456, 48),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(88, 101, 242),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _runButton.FlatAppearance.BorderSize = 0;
            _runButton.Click += RunButton_Click;

            _closeButton = new Button
            {
                Text = "Fechar",
                Location = new Point(32, 276),
                Size = new Size(456, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 46, 50),
                ForeColor = Color.FromArgb(225, 225, 230),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _closeButton.FlatAppearance.BorderColor = Color.FromArgb(70, 71, 76);
            _closeButton.FlatAppearance.BorderSize = 1;
            _closeButton.Click += (sender, e) => Close();

            Controls.AddRange(new Control[]
            {
                titleLabel,
                descriptionLabel,
                proxyLabel,
                _statusDot,
                _statusLabel,
                _progressBar,
                _runButton,
                _closeButton
            });

            FormClosing += MainForm_FormClosing;
        }

        private async void RunButton_Click(object sender, EventArgs e)
        {
            if (_running)
                return;

            _running = true;
            _runButton.Enabled = false;
            _runButton.Text = "Executando...";

            ProxyService.ProxySnapshot backup = null;
            bool proxyWasChanged = false;

            try
            {
                SetStatus("Salvando configuração atual...", 10, StatusKind.Working);
                backup = ProxyService.Capture();

                SetStatus("Ativando proxy...", 25, StatusKind.Working);
                ProxyService.Enable(ProxyHost, ProxyPort);
                proxyWasChanged = true;
                await Task.Delay(750);

                SetStatus("Fechando Discord...", 40, StatusKind.Working);
                await DiscordService.CloseAsync();
                await Task.Delay(500);

                SetStatus("Abrindo Discord com proxy...", 55, StatusKind.Working);
                DiscordService.Start();

                await DiscordService.WaitUntilRunningAsync(TimeSpan.FromSeconds(30));

                for (int secondsLeft = StartupDelaySeconds; secondsLeft > 0; secondsLeft--)
                {
                    int elapsed = StartupDelaySeconds - secondsLeft;
                    int progress = 60 + (int)(elapsed / (double)StartupDelaySeconds * 30);

                    SetStatus(
                        "Discord iniciado. Mantendo proxy por " + secondsLeft + "s...",
                        progress,
                        StatusKind.Working);

                    await Task.Delay(1000);
                }

                SetStatus("Restaurando proxy original...", 95, StatusKind.Working);
                ProxyService.Restore(backup);
                proxyWasChanged = false;

                SetStatus("Concluído — proxy restaurado", 100, StatusKind.Success);
            }
            catch (Exception ex)
            {
                SetStatus("Ocorreu um erro", 0, StatusKind.Error);

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Discord Proxy Launcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (proxyWasChanged && backup != null)
                {
                    try
                    {
                        ProxyService.Restore(backup);
                        SetStatus("Proxy restaurado após erro", 100, StatusKind.Warning);
                    }
                    catch (Exception restoreException)
                    {
                        SetStatus("ATENÇÃO: proxy não restaurado", 0, StatusKind.Error);

                        MessageBox.Show(
                            this,
                            "Não consegui restaurar o proxy automaticamente.\n\n" +
                            restoreException.Message +
                            "\n\nAbra Configurações > Rede e Internet > Proxy e desative manualmente.",
                            "Atenção",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }

                _runButton.Enabled = true;
                _runButton.Text = "Reiniciar Discord com Proxy";
                _running = false;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_running)
                return;

            e.Cancel = true;
            MessageBox.Show(
                this,
                "Aguarde a conclusão para garantir que o proxy do Windows seja restaurado.",
                "Discord Proxy Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void SetStatus(string text, int progress, StatusKind kind)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => SetStatus(text, progress, kind)));
                return;
            }

            _statusLabel.Text = text;
            _progressBar.Value = Math.Max(0, Math.Min(100, progress));

            switch (kind)
            {
                case StatusKind.Success:
                    _statusDot.BackColor = Color.FromArgb(64, 180, 110);
                    break;
                case StatusKind.Warning:
                    _statusDot.BackColor = Color.FromArgb(230, 170, 55);
                    break;
                case StatusKind.Error:
                    _statusDot.BackColor = Color.FromArgb(225, 75, 75);
                    break;
                default:
                    _statusDot.BackColor = Color.FromArgb(88, 101, 242);
                    break;
            }
        }

        private static void MakeCircular(Control control)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddEllipse(0, 0, control.Width, control.Height);
            control.Region = new Region(path);
        }

        private enum StatusKind
        {
            Working,
            Success,
            Warning,
            Error
        }
    }
}
