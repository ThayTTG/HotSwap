using System.Runtime.InteropServices;

namespace HotSwap;

/// <summary>
/// O "aplicativo" propriamente dito: um ApplicationContext sem janela principal,
/// que só existe como ícone na bandeja do sistema.
/// </summary>
internal class TrayAppContext : ApplicationContext
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly NotifyIcon _trayIcon;
    private readonly ProfileManager _profileManager;
    private readonly GlobalHotkey _hotkey;
    private readonly string _jsonPath;

    /// <summary>Ícone base (embutido no .exe, ou o ícone padrão do sistema
    /// como fallback). É a instância compartilhada de <see cref="Theme.AppIcon"/>
    /// — nunca deve ser descartada aqui.</summary>
    private readonly Icon _baseIcon;

    /// <summary>Menu anterior, aguardando descarte depois de substituído.</summary>
    private ContextMenuStrip? _menu;

    /// <summary>Ligado no ItemClicked de um item liga/desliga para o menu
    /// NÃO fechar ao clicar nele (dá para ver o switch mudar).</summary>
    private bool _keepMenuOpenOnce;

    /// <summary>Wrapper gerenciado do HICON atualmente atribuído ao
    /// NotifyIcon — precisa continuar vivo enquanto estiver em uso; só é
    /// descartado depois que um novo já foi atribuído no lugar dele.</summary>
    private Icon? _currentIconWrapper;

    /// <summary>HICON bruto por trás de _currentIconWrapper, para poder
    /// destruí-lo (DestroyIcon) quando um novo é gerado — Bitmap.GetHicon()
    /// aloca um handle nativo que não é liberado sozinho pelo GC nem pelo
    /// Dispose do Icon (Icon.FromHandle não "possui" o handle).</summary>
    private IntPtr _currentBadgeHIcon = IntPtr.Zero;

    public TrayAppContext()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HotSwap");
        _jsonPath = Path.Combine(baseDir, "perfis.json");

        _profileManager = new ProfileManager(_jsonPath);
        _profileManager.Load();

        _baseIcon = Theme.AppIcon;

        _trayIcon = new NotifyIcon
        {
            Icon = _baseIcon,
            Visible = true,
            Text = BuildTooltipText(),
        };
        UpdateTrayIconForActiveProfile();

        _trayIcon.MouseDoubleClick += OnTrayDoubleClick;
        RefreshMenu();

        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += OnHotkeyPressed;
        TryActivateConfiguredHotkey(showWarningOnFailure: true);
        TryActivateConfiguredSwapHotkey(showWarningOnFailure: true);
    }

    private void OnHotkeyPressed(GlobalHotkey.HotkeySlot slot)
    {
        switch (slot)
        {
            case GlobalHotkey.HotkeySlot.ToggleMainProfile:
                ToggleMainProfile();
                break;
            case GlobalHotkey.HotkeySlot.SwapMonitorContents:
                OnSwapMonitorContents();
                break;
        }
    }

    /// <summary>
    /// Gera (e aplica ao NotifyIcon) uma versão do ícone base com um pequeno
    /// selo circular indicando qual perfil principal (1 ou 2) está ativo no
    /// momento — dá para saber de relance, sem abrir o menu.
    /// </summary>
    private void UpdateTrayIconForActiveProfile()
    {
        int index = Math.Max(0, _profileManager.Store.ActiveMainProfileIndex);

        // Desenha direto no tamanho REAL do ícone da bandeja (16 px a 100%,
        // 20 a 125%, 24 a 150%, 32 a 200%) usando o quadro do .ico desenhado
        // para esse tamanho — em vez de desenhar em 32 px e deixar o Windows
        // encolher, o que deixava o número do selo borrado.
        int size = Math.Max(16, SystemInformation.SmallIconSize.Width);

        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        using (var frame = new Icon(_baseIcon, size, size))
            g.DrawIcon(frame, new Rectangle(0, 0, size, size));

        // Selo no canto SUPERIOR direito (cor do perfil: 1 = azul, 2 = ciano),
        // com um "recorte" transparente em volta para destacar do ícone. Em
        // cima, e não embaixo, para não esconder a ponta da seta para baixo —
        // assim as duas setas de troca continuam visíveis mesmo em 16 px.
        float d = (float)Math.Round(size * 0.5);
        var badge = new RectangleF(size - d, 0, d, d);
        float gap = Math.Max(1f, size / 16f);
        CutOutCircle(bmp, badge.X + d / 2f, badge.Y + d / 2f, d / 2f + gap);
        using (var g = Graphics.FromImage(bmp))
            Glyphs.DrawNumberBadge(g, badge, index + 1, Theme.ProfileColor(index));

        IntPtr newHIcon = bmp.GetHicon();
        var newIconWrapper = Icon.FromHandle(newHIcon);

        // NotifyIcon.Icon copia os dados do ícone internamente ao ser
        // atribuído (via Shell_NotifyIcon), então é seguro liberar o wrapper
        // ANTERIOR e seu HICON logo em seguida — mas o wrapper e o HICON
        // ATUAIS precisam continuar vivos enquanto estiverem em uso. Descartar
        // o wrapper corrente (mesmo sem ele "possuir" o HICON) invalida
        // Icon.Handle internamente e derruba o app na próxima vez que o
        // Windows precisar reconsultar o ícone (ex: Explorer reiniciando e
        // reenviando TaskbarCreated).
        var oldIconWrapper = _currentIconWrapper;
        var oldHIcon = _currentBadgeHIcon;

        _trayIcon.Icon = newIconWrapper;
        _currentIconWrapper = newIconWrapper;
        _currentBadgeHIcon = newHIcon;

        oldIconWrapper?.Dispose();
        if (oldHIcon != IntPtr.Zero)
            DestroyIcon(oldHIcon);
    }

    /// <summary>
    /// Torna transparente um círculo do bitmap (com borda suavizada de 1 px).
    /// Feito pixel a pixel porque o GDI+ não "apaga" com antialiasing — um
    /// recorte via clip ficaria serrilhado num ícone de 16 px.
    /// </summary>
    private static void CutOutCircle(Bitmap bmp, float cx, float cy, float radius)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            int stride = Math.Abs(data.Stride);
            var buffer = new byte[stride * bmp.Height];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float keep = Math.Clamp(MathF.Sqrt(dx * dx + dy * dy) - radius + 0.5f, 0f, 1f);
                    if (keep < 1f)
                    {
                        int alpha = y * stride + x * 4 + 3; // BGRA
                        buffer[alpha] = (byte)(buffer[alpha] * keep);
                    }
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
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

    /// <summary>Balão de notificação "de conveniência" (sucesso/status) — só
    /// aparece se o usuário não tiver desativado as notificações no menu.</summary>
    private void NotifyInfo(string message, int timeout = 1500)
    {
        if (_profileManager.Store.Settings.NotificationsEnabled)
            _trayIcon.ShowBalloonTip(timeout, "HotSwap", message, ToolTipIcon.Info);
    }

    /// <summary>Balão de aviso/erro — sempre aparece, mesmo com notificações
    /// desativadas, porque é acionável (algo falhou e o usuário precisa saber).</summary>
    private void NotifyWarning(string message, int timeout = 4000)
    {
        _trayIcon.ShowBalloonTip(timeout, "HotSwap", message, ToolTipIcon.Warning);
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
            UpdateTrayIconForActiveProfile();
            NotifyInfo($"Alternado para {activeName}");
        }
        else
        {
            NotifyWarning(result.Message);
        }

        // Reconstrói o menu para refletir qual perfil principal está marcado como ativo.
        RefreshMenu();
    }

    /// <summary>
    /// Tenta ativar o atalho de teclado configurado em Settings. Chamado na
    /// inicialização e sempre que o usuário salva uma nova configuração de
    /// atalho pelo diálogo.
    /// </summary>
    private void TryActivateConfiguredHotkey(bool showWarningOnFailure)
    {
        var settings = _profileManager.Store.Settings;

        if (!settings.HotkeyEnabled)
        {
            _hotkey.Unregister(GlobalHotkey.HotkeySlot.ToggleMainProfile);
            return;
        }

        if (!GlobalHotkey.TryParse(settings.HotkeyString, out var modifiers, out var key))
        {
            if (showWarningOnFailure)
                NotifyWarning($"Atalho de teclado salvo ('{settings.HotkeyString}') é inválido — configure de novo pelo menu.");
            return;
        }

        if (!_hotkey.TryRegister(GlobalHotkey.HotkeySlot.ToggleMainProfile, modifiers, key, out var detail) && showWarningOnFailure)
            NotifyWarning($"Não foi possível ativar o atalho de teclado ({settings.HotkeyString}) — {detail}.");
    }

    /// <summary>
    /// Mesma lógica de <see cref="TryActivateConfiguredHotkey"/>, mas para o
    /// atalho de trocar o conteúdo entre duas telas — um slot independente,
    /// então os dois atalhos podem estar ativos ao mesmo tempo sem conflito.
    /// </summary>
    private void TryActivateConfiguredSwapHotkey(bool showWarningOnFailure)
    {
        var settings = _profileManager.Store.Settings;

        if (!settings.SwapHotkeyEnabled)
        {
            _hotkey.Unregister(GlobalHotkey.HotkeySlot.SwapMonitorContents);
            return;
        }

        if (!GlobalHotkey.TryParse(settings.SwapHotkeyString, out var modifiers, out var key))
        {
            if (showWarningOnFailure)
                NotifyWarning($"Atalho de trocar telas salvo ('{settings.SwapHotkeyString}') é inválido — configure de novo pelo menu.");
            return;
        }

        if (!_hotkey.TryRegister(GlobalHotkey.HotkeySlot.SwapMonitorContents, modifiers, key, out var detail) && showWarningOnFailure)
            NotifyWarning($"Não foi possível ativar o atalho de trocar telas ({settings.SwapHotkeyString}) — {detail}.");
    }

    /// <summary>
    /// Reconstrói o menu e descarta o anterior. O descarte é adiado (Post)
    /// porque a reconstrução normalmente acontece DENTRO do clique de um item
    /// do próprio menu antigo, que ainda está fechando nesse momento.
    /// </summary>
    private void RefreshMenu()
    {
        var old = _menu;
        _menu = BuildContextMenu();
        _trayIcon.ContextMenuStrip = _menu;

        if (old != null)
        {
            // Sem contexto de sincronização (não deveria acontecer na thread
            // da UI), deixa o menu antigo para o GC em vez de arriscar
            // descartá-lo no meio do próprio evento.
            var sync = SynchronizationContext.Current;
            if (old.Visible)
            {
                // Ex: atalho pressionado com o menu aberto — espera ele fechar.
                old.Closed += (_, _) => sync?.Post(_ => old.Dispose(), null);
            }
            else
            {
                sync?.Post(_ => old.Dispose(), null);
            }
        }
    }

    /// <summary>Resumo de um perfil principal para o menu: "1920×1080 · 144 Hz"
    /// (uma tela) ou "2 telas".</summary>
    private static string SummarizeMainProfile(MainProfileSet set, string separator)
    {
        return set.MonitorSettings.Count switch
        {
            0 => "vazio",
            1 => $"{set.MonitorSettings[0].Width}×{set.MonitorSettings[0].Height}{separator}{set.MonitorSettings[0].Hz} Hz",
            var n => $"{n} telas",
        };
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var store = _profileManager.Store;
        var settings = store.Settings;

        var menu = new ContextMenuStrip { Font = Theme.UiFont };
        int dpi = menu.DeviceDpi;
        // Largura mínima: o menu se ajusta ao texto mais longo, mas nunca
        // fica estreito a ponto de apertar o cabeçalho.
        menu.MinimumSize = new Size(Theme.Scale(300, dpi), 0);
        // ImageScalingSize já vem ajustado ao DPI pelo próprio ToolStrip —
        // os ícones são desenhados exatamente nesse tamanho (nítidos).
        int iconPx = menu.ImageScalingSize.Height;
        var itemPadding = new Padding(0, Theme.Scale(4, dpi), 0, Theme.Scale(4, dpi));

        ToolStripMenuItem Item(string text, Glyph glyph, EventHandler? onClick, string? rightText = null)
        {
            var item = new ToolStripMenuItem(text)
            {
                Padding = itemPadding,
                Image = Glyphs.Get(glyph, iconPx, Theme.TextMuted),
            };
            if (onClick != null)
                item.Click += onClick;
            if (!string.IsNullOrEmpty(rightText))
                item.ShortcutKeyDisplayString = rightText;
            return item;
        }

        // --- Cabeçalho ---
        int activeIndex = store.ActiveMainProfileIndex;
        var activeSet = activeIndex >= 0 && activeIndex < store.MainProfiles.Count ? store.MainProfiles[activeIndex] : null;
        string subtitle = activeSet != null
            ? $"{activeSet.Name} ativo  ·  {SummarizeMainProfile(activeSet, " @ ")}"
            : "Nenhum perfil principal salvo ainda";
        menu.Items.Add(new MenuHeaderItem("HotSwap", subtitle));

        // --- Perfis principais (fixos no topo) ---
        menu.Items.Add(new MenuSectionItem("Perfis principais"));
        for (int i = 0; i < store.MainProfiles.Count; i++)
        {
            var set = store.MainProfiles[i];
            int index = i;
            var color = Theme.ProfileColor(index);
            var item = new ProfileMenuItem(set.Name)
            {
                Padding = itemPadding,
                Checked = activeIndex == index,
                AccentColor = color,
                Image = Glyphs.ProfileChip(index + 1, iconPx, color),
                ShortcutKeyDisplayString = SummarizeMainProfile(set, "  ·  "),
            };
            item.Click += (_, _) => ApplyMainProfileFromMenu(index);
            menu.Items.Add(item);
        }
        menu.Items.Add(Item("Salvar estado atual como…", Glyph.Save, OnSaveCurrentAsMainProfile_Click));

        // --- Telas ---
        menu.Items.Add(new MenuSeparatorItem());
        menu.Items.Add(new MenuSectionItem("Telas"));
        foreach (var monitor in store.Monitors)
        {
            var monitorItem = Item(monitor.FriendlyName, Glyph.Monitor, null);
            BuildMonitorSubmenu(monitorItem, monitor, itemPadding);
            menu.Items.Add(monitorItem);
        }
        menu.Items.Add(Item("Trocar conteúdo das telas", Glyph.Swap, (_, _) => OnSwapMonitorContents(),
            settings.SwapHotkeyEnabled ? Theme.FormatHotkey(settings.SwapHotkeyString) : null));

        // --- Opções ---
        menu.Items.Add(new MenuSeparatorItem());
        menu.Items.Add(new MenuSectionItem("Opções"));
        menu.Items.Add(Item("Atalho de perfis…", Glyph.Keyboard, (_, _) => OnConfigureHotkey_Click(),
            settings.HotkeyEnabled ? Theme.FormatHotkey(settings.HotkeyString) : "desativado"));
        menu.Items.Add(Item("Atalho de trocar telas…", Glyph.Keyboard, (_, _) => OnConfigureSwapHotkey_Click(),
            settings.SwapHotkeyEnabled ? Theme.FormatHotkey(settings.SwapHotkeyString) : "desativado"));

        var notifyItem = new ToggleMenuItem("Notificações de sucesso")
        {
            Padding = itemPadding,
            Checked = settings.NotificationsEnabled,
            Image = Glyphs.Get(Glyph.Bell, iconPx, Theme.TextMuted),
        };
        notifyItem.CheckedChanged += (_, _) =>
        {
            settings.NotificationsEnabled = notifyItem.Checked;
            _profileManager.Save();
        };
        menu.Items.Add(notifyItem);

        menu.Items.Add(Item("Detectar monitores novamente", Glyph.Refresh, (_, _) =>
        {
            _profileManager.RefreshDetection();
            RefreshMenu();
            NotifyInfo("Monitores e resoluções atualizados.");
        }));
        menu.Items.Add(Item("Abrir pasta de perfis", Glyph.Folder, (_, _) =>
        {
            var dir = Path.GetDirectoryName(_jsonPath)!;
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start("explorer.exe", dir);
        }));
        menu.Items.Add(Item("Diagnóstico de monitores", Glyph.Info, (_, _) =>
        {
            var lines = DisplayInterop.DebugListAllDevices();
            var text = string.Join(Environment.NewLine + Environment.NewLine, lines);
            MessageBox.Show(text, "HotSwap — Diagnóstico", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }));

        menu.Items.Add(new MenuSeparatorItem());
        var exitItem = Item("Sair", Glyph.Power, (_, _) =>
        {
            _trayIcon.Visible = false;
            Application.Exit();
        });
        exitItem.Tag = "danger";
        menu.Items.Add(exitItem);

        // Clicar num item liga/desliga NÃO fecha o menu — dá para ver o
        // switch mudar e continuar usando o menu.
        menu.ItemClicked += (_, e) =>
        {
            if (e.ClickedItem is ToggleMenuItem)
                _keepMenuOpenOnce = true;
        };
        menu.Closing += (_, e) =>
        {
            if (_keepMenuOpenOnce && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                e.Cancel = true;
            _keepMenuOpenOnce = false;
        };

        Theme.StyleDropDown(menu);
        return menu;
    }

    /// <summary>
    /// Submenu de um monitor: resoluções suportadas agrupadas (cada uma com
    /// suas taxas de Hz num submenu próprio, quando há mais de uma), depois os
    /// perfis personalizados. O modo em uso agora fica marcado.
    /// </summary>
    private void BuildMonitorSubmenu(ToolStripMenuItem parent, MonitorProfiles monitor, Padding itemPadding)
    {
        var current = DisplayInterop.GetCurrentMode(monitor.DeviceName);
        bool IsCurrent(DisplayProfile p) =>
            current != null && p.Width == current.Width && p.Height == current.Height && p.Hz == current.Hz;

        var autoDetected = monitor.Profiles.Where(p => p.IsAutoDetected).ToList();
        var custom = monitor.Profiles.Where(p => !p.IsAutoDetected).ToList();

        if (autoDetected.Count > 0)
        {
            parent.DropDownItems.Add(new MenuSectionItem("Resoluções suportadas"));

            // GroupBy preserva a ordem de primeira aparição — a lista já vem
            // ordenada da maior para a menor resolução.
            foreach (var group in autoDetected.GroupBy(p => (p.Width, p.Height)))
            {
                var modes = group.OrderByDescending(p => p.Hz).ToList();
                string label = $"{group.Key.Width} × {group.Key.Height}";

                if (modes.Count == 1)
                {
                    var only = modes[0];
                    var leaf = new ToolStripMenuItem(label)
                    {
                        Padding = itemPadding,
                        Checked = IsCurrent(only),
                        ShortcutKeyDisplayString = $"{only.Hz} Hz",
                    };
                    leaf.Click += (_, _) => ApplyProfileAndNotify(only);
                    parent.DropDownItems.Add(leaf);
                    continue;
                }

                var resolutionItem = new ToolStripMenuItem(label)
                {
                    Padding = itemPadding,
                    Checked = modes.Any(IsCurrent),
                };
                foreach (var mode in modes)
                {
                    var hzItem = new ToolStripMenuItem($"{mode.Hz} Hz")
                    {
                        Padding = itemPadding,
                        Checked = IsCurrent(mode),
                    };
                    hzItem.Click += (_, _) => ApplyProfileAndNotify(mode);
                    resolutionItem.DropDownItems.Add(hzItem);
                }
                parent.DropDownItems.Add(resolutionItem);
            }
        }

        if (custom.Count > 0)
        {
            if (autoDetected.Count > 0)
                parent.DropDownItems.Add(new MenuSeparatorItem());
            parent.DropDownItems.Add(new MenuSectionItem("Personalizados"));

            foreach (var profile in custom)
            {
                var item = new ToolStripMenuItem(profile.Name)
                {
                    Padding = itemPadding,
                    Checked = IsCurrent(profile),
                    ShortcutKeyDisplayString = $"{profile.Width}×{profile.Height}  ·  {profile.Hz} Hz",
                };
                item.Click += (_, _) => ApplyProfileAndNotify(profile);
                parent.DropDownItems.Add(item);
            }
        }

        if (parent.DropDownItems.Count == 0)
            parent.DropDownItems.Add(new ToolStripMenuItem("Nenhum modo detectado") { Enabled = false, Padding = itemPadding });
    }

    private void ApplyMainProfileFromMenu(int index)
    {
        var result = _profileManager.ApplyMainProfile(index);
        _trayIcon.Text = BuildTooltipText();
        if (result.Success)
            UpdateTrayIconForActiveProfile();
        RefreshMenu();
        if (!result.Success)
            NotifyWarning(result.Message);
    }

    private void ApplyProfileAndNotify(DisplayProfile profile)
    {
        var result = _profileManager.ApplySingleProfile(profile);
        if (result.Success)
            NotifyInfo($"Aplicado: {profile.DisplayText} ({profile.MonitorFriendlyName})");
        else
            NotifyWarning(result.Message);
    }

    private void OnConfigureHotkey_Click()
    {
        var settings = _profileManager.Store.Settings;

        // Guarda o estado anterior para poder restaurar o atalho que já
        // estava funcionando, caso a nova combinação escolhida não possa ser
        // registrada (TryRegister sempre desregistra o atalho atual antes de
        // tentar o novo, então sem esse rollback o usuário ficaria sem
        // NENHUM atalho ativo até tentar de novo com sucesso).
        var previousEnabled = settings.HotkeyEnabled;
        var previousHotkeyString = settings.HotkeyString;

        using var dialog = new HotkeySettingsDialog(settings.HotkeyEnabled, settings.HotkeyString);
        if (dialog.ShowDialog() != DialogResult.OK) return;

        if (dialog.HotkeyEnabled)
        {
            if (!GlobalHotkey.TryParse(dialog.HotkeyText, out var modifiers, out var key))
            {
                NotifyWarning("Atalho inválido — escolha uma combinação com pelo menos um modificador (Ctrl/Alt/Shift/Win) e uma tecla.");
                return;
            }

            if (!_hotkey.TryRegister(GlobalHotkey.HotkeySlot.ToggleMainProfile, modifiers, key, out var detail))
            {
                // Tenta restaurar o que já funcionava antes desta tentativa
                // (TryRegister acima já desregistrou o atalho anterior antes
                // de falhar em registrar o novo). O rollback em si também
                // pode falhar (ex: a combinação anterior passou a estar em
                // uso por outro programa nesse meio-tempo) — nesse caso não
                // afirmamos que foi restaurado, e refletimos no Settings que
                // o atalho ficou de fato desativado, para o menu não mentir
                // mostrando uma combinação "ativa" que não está mais valendo.
                bool restored = previousEnabled
                    && GlobalHotkey.TryParse(previousHotkeyString, out var prevModifiers, out var prevKey)
                    && _hotkey.TryRegister(GlobalHotkey.HotkeySlot.ToggleMainProfile, prevModifiers, prevKey, out _);

                if (restored)
                {
                    NotifyWarning($"Não foi possível registrar o atalho '{dialog.HotkeyText}' — {detail}. O atalho anterior ({previousHotkeyString}) continua ativo.");
                }
                else
                {
                    settings.HotkeyEnabled = false;
                    _profileManager.Save();
                    RefreshMenu();
                    NotifyWarning($"Não foi possível registrar o atalho '{dialog.HotkeyText}' — {detail}. Nenhum atalho de teclado está ativo agora.");
                }
                return;
            }

            settings.HotkeyEnabled = true;
            settings.HotkeyString = dialog.HotkeyText;
            NotifyInfo($"Atalho definido: {settings.HotkeyString}");
        }
        else
        {
            _hotkey.Unregister(GlobalHotkey.HotkeySlot.ToggleMainProfile);
            settings.HotkeyEnabled = false;
            NotifyInfo("Atalho de teclado desativado.");
        }

        _profileManager.Save();
        RefreshMenu();
    }

    /// <summary>
    /// Executa a troca de conteúdo entre as duas telas configuradas (ou as
    /// duas primeiras detectadas, se não configurado). Chamado tanto pelo
    /// atalho de teclado quanto, futuramente, por qualquer outro gatilho.
    /// </summary>
    private void OnSwapMonitorContents()
    {
        var settings = _profileManager.Store.Settings;
        var result = WindowSwapper.SwapMonitorContents(settings.SwapMonitorA, settings.SwapMonitorB);

        if (result.Success)
            NotifyInfo($"Telas trocadas — {result.WindowsMoved} de {result.WindowsTotal} janela(s) movida(s).");
        else
            NotifyWarning(result.Message);
    }

    private void OnConfigureSwapHotkey_Click()
    {
        var settings = _profileManager.Store.Settings;
        var monitors = WindowSwapper.GetActiveMonitors();

        var previousEnabled = settings.SwapHotkeyEnabled;
        var previousHotkeyString = settings.SwapHotkeyString;

        using var dialog = new SwapScreensSettingsDialog(
            settings.SwapHotkeyEnabled, settings.SwapHotkeyString, monitors, settings.SwapMonitorA, settings.SwapMonitorB);
        if (dialog.ShowDialog() != DialogResult.OK) return;

        settings.SwapMonitorA = dialog.MonitorA;
        settings.SwapMonitorB = dialog.MonitorB;

        if (dialog.HotkeyEnabled)
        {
            if (!GlobalHotkey.TryParse(dialog.HotkeyText, out var modifiers, out var key))
            {
                NotifyWarning("Atalho inválido — escolha uma combinação com pelo menos um modificador (Ctrl/Alt/Shift/Win) e uma tecla.");
                return;
            }

            if (!_hotkey.TryRegister(GlobalHotkey.HotkeySlot.SwapMonitorContents, modifiers, key, out var detail))
            {
                // Mesmo padrão de rollback usado em OnConfigureHotkey_Click:
                // TryRegister já desregistrou o atalho anterior antes de
                // falhar em registrar o novo, então tentamos restaurar o que
                // já funcionava — e refletimos com precisão no Settings caso
                // a restauração também falhe, para o menu nunca mostrar uma
                // combinação "ativa" que não está mais valendo de verdade.
                bool restored = previousEnabled
                    && GlobalHotkey.TryParse(previousHotkeyString, out var prevModifiers, out var prevKey)
                    && _hotkey.TryRegister(GlobalHotkey.HotkeySlot.SwapMonitorContents, prevModifiers, prevKey, out _);

                if (restored)
                {
                    NotifyWarning($"Não foi possível registrar o atalho '{dialog.HotkeyText}' — {detail}. O atalho anterior ({previousHotkeyString}) continua ativo.");
                }
                else
                {
                    settings.SwapHotkeyEnabled = false;
                    _profileManager.Save();
                    RefreshMenu();
                    NotifyWarning($"Não foi possível registrar o atalho '{dialog.HotkeyText}' — {detail}. Nenhum atalho de trocar telas está ativo agora.");
                }
                return;
            }

            settings.SwapHotkeyEnabled = true;
            settings.SwapHotkeyString = dialog.HotkeyText;
            NotifyInfo($"Atalho de trocar telas definido: {settings.SwapHotkeyString}");
        }
        else
        {
            _hotkey.Unregister(GlobalHotkey.HotkeySlot.SwapMonitorContents);
            settings.SwapHotkeyEnabled = false;
            NotifyInfo("Atalho de trocar telas desativado.");
        }

        _profileManager.Save();
        RefreshMenu();
    }

    private void OnSaveCurrentAsMainProfile_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveMainProfileDialog(_profileManager.Store.MainProfiles.Select(m => m.Name).ToList());
        if (dialog.ShowDialog() != DialogResult.OK) return;

        var deviceNames = DisplayInterop.GetActiveDeviceNames();
        if (deviceNames.Count == 0)
        {
            NotifyWarning("Não foi possível detectar nenhum monitor ativo agora — nada foi salvo. Tente de novo em alguns segundos.");
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
            NotifyWarning($"Não foi possível ler a configuração atual de nenhum monitor — '{dialog.ChosenName}' NÃO foi salvo. Tente novamente.");
            return;
        }

        if (dialog.TargetIndex < _profileManager.Store.MainProfiles.Count)
            _profileManager.Store.MainProfiles[dialog.TargetIndex] = newSet;
        else
            _profileManager.Store.MainProfiles.Add(newSet);

        _profileManager.Save();
        RefreshMenu();

        if (monitorsSemModoAtual.Count > 0)
        {
            NotifyWarning($"Salvo como '{dialog.ChosenName}', mas sem estes monitores (não foi possível ler o modo atual deles): {string.Join(", ", monitorsSemModoAtual)}.");
        }
        else
        {
            NotifyInfo($"Estado atual salvo como '{dialog.ChosenName}'.");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkey.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _menu?.Dispose();
            _menu = null;

            _currentIconWrapper?.Dispose();
            if (_currentBadgeHIcon != IntPtr.Zero)
            {
                DestroyIcon(_currentBadgeHIcon);
                _currentBadgeHIcon = IntPtr.Zero;
            }

            // _baseIcon é a instância compartilhada de Theme.AppIcon (vive o
            // processo inteiro) — não é descartada aqui.
        }
        base.Dispose(disposing);
    }
}

