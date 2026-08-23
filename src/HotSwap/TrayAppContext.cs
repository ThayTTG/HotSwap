namespace HotSwap;

/// <summary>
/// O "aplicativo" propriamente dito: um ApplicationContext sem janela principal,
/// que só existe como ícone na bandeja do sistema.
/// </summary>
internal class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ProfileManager _profileManager;
    private readonly string _jsonPath;

    public TrayAppContext()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HotSwap");
        _jsonPath = Path.Combine(baseDir, "perfis.json");

        _profileManager = new ProfileManager(_jsonPath);
        _profileManager.Load();

        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = BuildTooltipText(),
        };

        _trayIcon.MouseDoubleClick += OnTrayDoubleClick;
        _trayIcon.ContextMenuStrip = BuildContextMenu();
    }

    private static Icon LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(iconPath))
            return new Icon(iconPath);

        // Fallback: ícone padrão do sistema, para o app nunca falhar por falta de .ico.
        return SystemIcons.Application;
    }

    private string BuildTooltipText()
    {
        var idx = _profileManager.Store.ActiveMainProfileIndex;
        var name = idx < _profileManager.Store.MainProfiles.Count
            ? _profileManager.Store.MainProfiles[idx].Name
            : "HotSwap";
        // NotifyIcon.Text tem limite de 127 caracteres.
        var text = $"HotSwap — ativo: {name}";
        return text.Length > 127 ? text[..127] : text;
    }

    private void OnTrayDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ToggleMainProfile();
    }

    private void ToggleMainProfile()
    {
        var result = _profileManager.ToggleMainProfile();
        _trayIcon.Text = BuildTooltipText();

        var activeName = _profileManager.Store.MainProfiles.Count > 0
            ? _profileManager.Store.MainProfiles[_profileManager.Store.ActiveMainProfileIndex].Name
            : "?";

        if (result.Success)
        {
            _trayIcon.ShowBalloonTip(1500, "HotSwap", $"Alternado para {activeName}", ToolTipIcon.Info);
        }
        else
        {
            _trayIcon.ShowBalloonTip(4000, "HotSwap", result.Message, ToolTipIcon.Warning);
        }

        // Reconstrói o menu para refletir qual perfil principal está marcado como ativo.
        _trayIcon.ContextMenuStrip = BuildContextMenu();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        // --- Perfis principais (fixos no topo) ---
        for (int i = 0; i < _profileManager.Store.MainProfiles.Count; i++)
        {
            var set = _profileManager.Store.MainProfiles[i];
            var index = i;
            var item = new ToolStripMenuItem(set.Name)
            {
                Checked = _profileManager.Store.ActiveMainProfileIndex == index,
                CheckOnClick = false,
            };
            item.Click += (_, _) =>
            {
                var result = _profileManager.ApplyMainProfile(index);
                _trayIcon.Text = BuildTooltipText();
                _trayIcon.ContextMenuStrip = BuildContextMenu();
                if (!result.Success)
                    _trayIcon.ShowBalloonTip(4000, "HotSwap", result.Message, ToolTipIcon.Warning);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripMenuItem("Salvar estado atual como...", null, OnSaveCurrentAsMainProfile_Click));

        menu.Items.Add(new ToolStripSeparator());

        // --- Perfis por monitor (inclui os detectados automaticamente) ---
        foreach (var monitor in _profileManager.Store.Monitors)
        {
            var monitorMenu = new ToolStripMenuItem(monitor.FriendlyName);

            var autoDetected = monitor.Profiles.Where(p => p.IsAutoDetected).ToList();
            var custom = monitor.Profiles.Where(p => !p.IsAutoDetected).ToList();

            if (autoDetected.Count > 0)
            {
                var autoHeader = new ToolStripMenuItem("Perfis padrão (detectados)") { Enabled = false };
                monitorMenu.DropDownItems.Add(autoHeader);

                foreach (var profile in autoDetected)
                {
                    var subItem = new ToolStripMenuItem(profile.DisplayText);
                    subItem.Click += (_, _) => ApplyProfileAndNotify(profile);
                    monitorMenu.DropDownItems.Add(subItem);
                }
            }

            if (custom.Count > 0)
            {
                monitorMenu.DropDownItems.Add(new ToolStripSeparator());
                var customHeader = new ToolStripMenuItem("Perfis personalizados") { Enabled = false };
                monitorMenu.DropDownItems.Add(customHeader);

                foreach (var profile in custom)
                {
                    var subItem = new ToolStripMenuItem(profile.Name);
                    subItem.Click += (_, _) => ApplyProfileAndNotify(profile);
                    monitorMenu.DropDownItems.Add(subItem);
                }
            }

            menu.Items.Add(monitorMenu);
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Detectar novamente", null, (_, _) =>
        {
            _profileManager.RefreshDetection();
            _trayIcon.ContextMenuStrip = BuildContextMenu();
            _trayIcon.ShowBalloonTip(1500, "HotSwap", "Monitores e resoluções atualizados.", ToolTipIcon.Info);
        }));
        menu.Items.Add(new ToolStripMenuItem("Abrir pasta de perfis...", null, (_, _) =>
        {
            var dir = Path.GetDirectoryName(_jsonPath)!;
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start("explorer.exe", dir);
        }));
        menu.Items.Add(new ToolStripMenuItem("Diagnóstico de monitores...", null, (_, _) =>
        {
            var lines = DisplayInterop.DebugListAllDevices();
            var text = string.Join(Environment.NewLine + Environment.NewLine, lines);
            MessageBox.Show(text, "HotSwap — Diagnóstico", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }));

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Sair", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            Application.Exit();
        }));

        return menu;
    }

    private void ApplyProfileAndNotify(DisplayProfile profile)
    {
        var result = _profileManager.ApplySingleProfile(profile);
        if (result.Success)
            _trayIcon.ShowBalloonTip(1500, "HotSwap", $"Aplicado: {profile.DisplayText} ({profile.MonitorFriendlyName})", ToolTipIcon.Info);
        else
            _trayIcon.ShowBalloonTip(4000, "HotSwap", result.Message, ToolTipIcon.Warning);
    }

    private void OnSaveCurrentAsMainProfile_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveMainProfileDialog(_profileManager.Store.MainProfiles.Select(m => m.Name).ToList());
        if (dialog.ShowDialog() != DialogResult.OK) return;

        var deviceNames = DisplayInterop.GetActiveDeviceNames();
        if (deviceNames.Count == 0)
        {
            _trayIcon.ShowBalloonTip(4000, "HotSwap",
                "Não foi possível detectar nenhum monitor ativo agora — nada foi salvo. Tente de novo em alguns segundos.",
                ToolTipIcon.Warning);
            return;
        }

        var newSet = new MainProfileSet { Name = dialog.ChosenName };
        var monitorsSemModoAtual = new List<string>();

        foreach (var deviceName in deviceNames)
        {
            var current = DisplayInterop.GetCurrentMode(deviceName);
            if (current == null)
            {
                monitorsSemModoAtual.Add(DisplayInterop.GetFriendlyName(deviceName));
                continue;
            }
            newSet.MonitorSettings.Add(new DisplayProfile
            {
                Name = dialog.ChosenName,
                IsMain = true,
                MonitorDeviceName = deviceName,
                MonitorFriendlyName = DisplayInterop.GetFriendlyName(deviceName),
                Width = current.Width,
                Height = current.Height,
                Hz = current.Hz,
                BitsPerPixel = current.BitsPerPixel,
                PosX = current.PosX,
                PosY = current.PosY,
                Orientation = current.Orientation,
            });
        }

        // Nunca salva um perfil principal vazio: antes disso era possível
        // clicar "Salvar" e o app dizer "sucesso" mesmo sem ter conseguido
        // ler o modo atual de nenhum monitor (ex: se GetActiveDeviceNames/
        // GetCurrentMode falharem momentaneamente), deixando um perfil
        // "fantasma" que depois recusa aplicar com "não tem nenhuma
        // configuração de monitor salva".
        if (newSet.MonitorSettings.Count == 0)
        {
            _trayIcon.ShowBalloonTip(4000, "HotSwap",
                $"Não foi possível ler a configuração atual de nenhum monitor — '{dialog.ChosenName}' NÃO foi salvo. Tente novamente.",
                ToolTipIcon.Warning);
            return;
        }

        if (dialog.TargetIndex < _profileManager.Store.MainProfiles.Count)
            _profileManager.Store.MainProfiles[dialog.TargetIndex] = newSet;
        else
            _profileManager.Store.MainProfiles.Add(newSet);

        _profileManager.Save();
        _trayIcon.ContextMenuStrip = BuildContextMenu();

        if (monitorsSemModoAtual.Count > 0)
        {
            _trayIcon.ShowBalloonTip(4000, "HotSwap",
                $"Salvo como '{dialog.ChosenName}', mas sem estes monitores (não foi possível ler o modo atual deles): {string.Join(", ", monitorsSemModoAtual)}.",
                ToolTipIcon.Warning);
        }
        else
        {
            _trayIcon.ShowBalloonTip(1500, "HotSwap", $"Estado atual salvo como '{dialog.ChosenName}'.", ToolTipIcon.Info);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
