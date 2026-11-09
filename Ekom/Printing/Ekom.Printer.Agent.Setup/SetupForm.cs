using Ekom.Printer.Agent.Client;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Ekom.Printer.Agent.Setup;

internal sealed class SetupForm : Form
{
    private readonly TextBox _url = new() { Dock = DockStyle.Fill, PlaceholderText = "https://shop.example.com" };
    private readonly TextBox _station = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    private readonly TextBox _token = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 512 };
    private readonly ComboBox _printer = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _enabled = new() { Text = "Enable automatic printing", AutoSize = true };
    private readonly Label _identity = new() { AutoSize = true, MaximumSize = new Size(620, 0) };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(620, 0) };
    private readonly FlowLayoutPanel _buttons = new() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };

    public SetupForm()
    {
        Text = "Ekom Printer Agent Setup";
        MinimumSize = new Size(700, 540);
        Size = new Size(760, 590);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, AutoScroll = true };
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "Connect this warehouse printer to Ekom", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        AddField(layout, "Website URL", _url);
        AddField(layout, "Station alias (matches Ekom appsettings)", _station);
        AddField(layout, "Station credential (leave blank to keep the saved credential)", _token);
        AddField(layout, "Printer visible to the Windows service", _printer);
        layout.Controls.Add(_enabled);
        layout.Controls.Add(_buttons);
        layout.Controls.Add(_identity);
        layout.Controls.Add(_status);
        AddButton("Save settings", SaveAsync);
        AddButton("Test connection", () => SendAsync(new AgentSetupRequest("connection")));
        AddButton("Test print", async () =>
        {
            if (MessageBox.Show(this, "Print one test page using the saved printer and the actual service account?", "Test print", MessageBoxButtons.OKCancel) == DialogResult.OK)
                await SendAsync(new AgentSetupRequest("test"));
        });
        AddButton("Reload / refresh printers", () => SendAsync(new AgentSetupRequest("status"), true));
        Shown += async (_, _) => await SendAsync(new AgentSetupRequest("status"), true);
    }

    private static void AddField(TableLayoutPanel panel, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 10, 0, 3) });
        panel.Controls.Add(control);
    }

    private void AddButton(string label, Func<Task> action)
    {
        var button = new Button { Text = label, AutoSize = true };
        button.Click += async (_, _) => await action();
        _buttons.Controls.Add(button);
    }

    private async Task SaveAsync()
    {
        var settings = new AgentSettings(_url.Text.Trim().TrimEnd('/'), _station.Text.Trim(), _printer.Text, _enabled.Checked);
        try { settings.Validate(); }
        catch (ArgumentException ex) { _status.Text = ex.Message; return; }
        if (settings.Enabled && MessageBox.Show(this, "Enable automatic printing? First save with printing paused, test the connection, and confirm a service-account test page printed correctly.", "Enable automatic printing", MessageBoxButtons.OKCancel) != DialogResult.OK)
            return;
        await SendAsync(new AgentSetupRequest("save", settings, _token.Text), true);
    }

    private async Task SendAsync(AgentSetupRequest request, bool loadSettings = false)
    {
        _buttons.Enabled = false;
        _status.Text = "Contacting the Windows service...";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            // Identification prevents a pipe-name squatter from impersonating the elevated setup user.
            using var pipe = new NamedPipeClientStream(".", AgentSetupProtocol.PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(5000, timeout.Token);
            // Check the SCM-reported PID before sending any station credential to a named pipe.
            ServicePipeIdentity.Verify(pipe);
            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request) + "\n");
            await pipe.WriteAsync(payload, timeout.Token);
            await pipe.FlushAsync(timeout.Token);
            var response = await JsonSerializer.DeserializeAsync<AgentSetupResponse>(pipe, cancellationToken: timeout.Token)
                ?? throw new IOException("Empty service response.");
            _status.Text = response.Message;
            if (!response.Success) return;
            _token.Clear();
            _identity.Text = $"Service identity: {response.ServiceIdentity}\nLast successful website check: {response.LastHeartbeatUtc?.ToLocalTime().ToString("g") ?? "Never"}";
            if (loadSettings)
            {
                _printer.Items.Clear();
                _printer.Items.AddRange(response.Printers ?? []);
                if (response.Settings != null)
                {
                    _url.Text = response.Settings.ServerUrl;
                    _station.Text = response.Settings.StationAlias;
                    _printer.SelectedItem = response.Settings.PrinterName;
                    _enabled.Checked = response.Settings.Enabled;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or JsonException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _status.Text = "Could not complete the operation. Confirm Ekom Printer Agent is installed and running, then reopen setup as administrator. If a test print was requested, check the printer before requesting another copy.";
        }
        finally { _buttons.Enabled = true; }
    }
}
