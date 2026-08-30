using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiscordProxyLauncher
{
    internal sealed class MainForm : Form
    {
        private const int StartupDelaySeconds = 10;
        private static readonly TimeSpan ProxyValidationTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ProxySearchTimeout = TimeSpan.FromSeconds(15);

        private static readonly ProxyService.ProxyEndpoint[] DefaultProxies =
        {
            new ProxyService.ProxyEndpoint("181.39.25.196", 8118),
            new ProxyService.ProxyEndpoint("159.112.235.87", 80),
            new ProxyService.ProxyEndpoint("172.67.167.93", 80),
            new ProxyService.ProxyEndpoint("172.64.149.154", 80),
            new ProxyService.ProxyEndpoint("172.67.181.184", 80)
        };

        private readonly Label _statusLabel;
        private readonly ComboBox _proxyComboBox;
        private readonly Button _searchProxyButton;
        private readonly Button _runButton;
        private readonly Button _closeButton;
        private readonly ProgressBar _progressBar;
        private readonly Panel _statusDot;

        private bool _running;
        private bool _searching;

        public MainForm()
        {
            Text = "Discord Proxy Launcher";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            ClientSize = new Size(520, 444);
            StartPosition = FormStartPosition.CenterScreen;
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
                Text = "Proxy preferido (fallback automático):",
                AutoSize = true,
                Location = new Point(32, 122),
                ForeColor = Color.FromArgb(205, 205, 210)
            };

            _proxyComboBox = new ComboBox
            {
                Location = new Point(32, 145),
                Size = new Size(456, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 46, 50),
                ForeColor = Color.White
            };
            _proxyComboBox.Items.AddRange(DefaultProxies);
            _proxyComboBox.SelectedIndex = 0;

            _searchProxyButton = new Button
            {
                Text = "Buscar mais servidores proxy",
                Location = new Point(32, 185),
                Size = new Size(456, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 46, 50),
                ForeColor = Color.FromArgb(225, 225, 230),
                Cursor = Cursors.Hand
            };
            _searchProxyButton.FlatAppearance.BorderColor = Color.FromArgb(70, 71, 76);
            _searchProxyButton.FlatAppearance.BorderSize = 1;
            _searchProxyButton.Click += SearchProxyButton_Click;

            _statusDot = new Panel
            {
                Size = new Size(10, 10),
                Location = new Point(34, 249),
                BackColor = Color.FromArgb(145, 145, 150)
            };
            MakeCircular(_statusDot);

            _statusLabel = new Label
            {
                Text = "Pronto",
                AutoSize = true,
                Location = new Point(52, 244),
                ForeColor = Color.FromArgb(210, 210, 215)
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(32, 276),
                Size = new Size(456, 8),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            _runButton = new Button
            {
                Text = "Reiniciar Discord com Proxy",
                Location = new Point(32, 306),
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
                Location = new Point(32, 364),
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
                _proxyComboBox,
                _searchProxyButton,
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
            if (_running || _searching)
                return;

            _running = true;
            _runButton.Enabled = false;
            _runButton.Text = "Executando...";
            _proxyComboBox.Enabled = false;
            _searchProxyButton.Enabled = false;

            ProxyService.ProxySnapshot backup = null;
            bool proxyWasChanged = false;
            ProxyService.ProxyEndpoint activeProxy = null;

            try
            {
                ProxyService.ProxyEndpoint selectedProxy =
                    (ProxyService.ProxyEndpoint)_proxyComboBox.SelectedItem;

                activeProxy = await FindAvailableProxyAsync(selectedProxy);
                _proxyComboBox.SelectedItem = activeProxy;

                SetStatus("Salvando configuração atual...", 20, StatusKind.Working);
                backup = ProxyService.Capture();

                SetStatus("Ativando proxy " + activeProxy + "...", 30, StatusKind.Working);
                ProxyService.Enable(activeProxy.Host, activeProxy.Port);
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

                SetStatus(
                    "Concluído com " + activeProxy + " — proxy restaurado",
                    100,
                    StatusKind.Success);
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
                _proxyComboBox.Enabled = true;
                _searchProxyButton.Enabled = true;
                _running = false;
            }
        }

        private async void SearchProxyButton_Click(object sender, EventArgs e)
        {
            if (_running || _searching)
                return;

            _searching = true;
            _searchProxyButton.Enabled = false;
            _runButton.Enabled = false;
            _proxyComboBox.Enabled = false;

            try
            {
                SetStatus("Buscando proxies dos Estados Unidos...", 5, StatusKind.Working);

                IList<ProxyService.ProxyEndpoint> fetchedProxies =
                    await ProxyService.FetchProxiesAsync(ProxySearchTimeout);

                int addedCount = 0;

                foreach (ProxyService.ProxyEndpoint proxy in fetchedProxies)
                {
                    bool alreadyExists = _proxyComboBox.Items
                        .Cast<ProxyService.ProxyEndpoint>()
                        .Any(existing =>
                            string.Equals(existing.Host, proxy.Host, StringComparison.OrdinalIgnoreCase) &&
                            existing.Port == proxy.Port);

                    if (alreadyExists)
                        continue;

                    _proxyComboBox.Items.Add(proxy);
                    addedCount++;
                }

                if (addedCount == 0)
                {
                    SetStatus(
                        "Nenhum novo proxy HTTP/HTTPS encontrado",
                        0,
                        StatusKind.Warning);
                }
                else
                {
                    SetStatus(
                        addedCount + (addedCount == 1
                            ? " novo proxy adicionado"
                            : " novos proxies adicionados"),
                        100,
                        StatusKind.Success);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Falha ao buscar proxies", 0, StatusKind.Error);

                MessageBox.Show(
                    this,
                    "Não foi possível consultar novos proxies.\n\n" + ex.Message,
                    "Discord Proxy Launcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _searchProxyButton.Enabled = true;
                _runButton.Enabled = true;
                _proxyComboBox.Enabled = true;
                _searching = false;
            }
        }

        private async Task<ProxyService.ProxyEndpoint> FindAvailableProxyAsync(
            ProxyService.ProxyEndpoint selectedProxy)
        {
            if (selectedProxy == null)
                throw new InvalidOperationException("Selecione um proxy para continuar.");

            List<ProxyService.ProxyEndpoint> candidates = new List<ProxyService.ProxyEndpoint>
            {
                selectedProxy
            };

            candidates.AddRange(_proxyComboBox.Items
                .Cast<ProxyService.ProxyEndpoint>()
                .Where(proxy => proxy != selectedProxy));

            for (int index = 0; index < candidates.Count; index++)
            {
                ProxyService.ProxyEndpoint candidate = candidates[index];
                int progress = 5 + (int)((index / (double)candidates.Count) * 10);

                SetStatus(
                    "Validando " + candidate + " (" + (index + 1) + "/" + candidates.Count + ")...",
                    progress,
                    StatusKind.Working);

                if (await ProxyService.IsAvailableAsync(candidate, ProxyValidationTimeout))
                    return candidate;
            }

            throw new InvalidOperationException(
                "Nenhum proxy respondeu corretamente ao Discord. Tente novamente mais tarde.");
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_running && !_searching)
                return;

            e.Cancel = true;
            MessageBox.Show(
                this,
                _running
                    ? "Aguarde a conclusão para garantir que o proxy do Windows seja restaurado."
                    : "Aguarde a busca de servidores proxy terminar.",
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
