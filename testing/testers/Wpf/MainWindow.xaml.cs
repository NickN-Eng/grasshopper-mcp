using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GH_MCP.Client;
using GH_MCP.Client.Logging;
using GH_MCP.Client.Models;
using Newtonsoft.Json;

namespace GH_MCP.Tester.Wpf
{
    public partial class MainWindow : Window
    {
        private GrasshopperClient? _client;
        private bool _connected = false;
        private readonly List<HistoryItem> _history = new();

        // Parameter input controls for dynamic form
        private readonly Dictionary<string, TextBox> _parameterInputs = new();

        public MainWindow()
        {
            InitializeComponent();
            ConfigureLogging();
            UpdateParameterInputs();
        }

        private void ConfigureLogging()
        {
            var logDir = FindLogsDirectory();
            if (logDir != null)
            {
                McpLogger.Configure(logDir, $"tester-wpf-{DateTime.Now:yyyyMMdd-HHmmss}.log", LogLevel.Debug);
                Title = $"GH_MCP Tester - Logging to {McpLogger.LogFilePath}";
            }
        }

        private static string? FindLogsDirectory()
        {
            // Try to find logs directory relative to repo root
            var current = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                var logsPath = Path.Combine(current, "logs");
                if (Directory.Exists(logsPath))
                    return logsPath;

                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            // Create logs in current directory as fallback
            var fallback = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        #region Connection

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var host = HostTextBox.Text.Trim();
            if (!int.TryParse(PortTextBox.Text.Trim(), out var port))
            {
                MessageBox.Show("Invalid port number", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _client = new GrasshopperClient(host, port);

            try
            {
                ConnectButton.IsEnabled = false;
                StatusText.Text = "Connecting...";

                var success = await _client.TestConnectionAsync();
                _connected = success;

                if (success)
                {
                    StatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
                    StatusText.Text = $"Connected to {host}:{port}";
                    ConnectButton.IsEnabled = false;
                    DisconnectButton.IsEnabled = true;
                }
                else
                {
                    StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    StatusText.Text = "Connection failed";
                    ConnectButton.IsEnabled = true;
                    MessageBox.Show("Failed to connect. Is Grasshopper running with GH_MCP component?",
                        "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                StatusText.Text = "Connection error";
                ConnectButton.IsEnabled = true;
                MessageBox.Show($"Connection error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            _client?.Dispose();
            _client = null;
            _connected = false;

            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusText.Text = "Disconnected";
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
        }

        #endregion

        #region Command Execution

        private void CommandTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateParameterInputs();
        }

        private void UpdateParameterInputs()
        {
            if (ParametersPanel == null) return;

            ParametersPanel.Children.Clear();
            _parameterInputs.Clear();

            var commandType = (CommandTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrEmpty(commandType)) return;

            var parameters = GetParametersForCommand(commandType);
            foreach (var param in parameters)
            {
                var label = new TextBlock
                {
                    Text = $"{param.Name}:",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 5, 0)
                };

                var textBox = new TextBox
                {
                    Width = param.Width,
                    Text = param.DefaultValue,
                    ToolTip = param.Description
                };

                // Wire up TextChanged event to update JSON preview
                textBox.TextChanged += (s, e) => UpdateRequestJson();

                _parameterInputs[param.Name] = textBox;

                ParametersPanel.Children.Add(label);
                ParametersPanel.Children.Add(textBox);
            }

            UpdateRequestJson();
        }

        private List<ParameterDefinition> GetParametersForCommand(string commandType)
        {
            return commandType switch
            {
                "add_component" => new List<ParameterDefinition>
                {
                    new("type", "Number Slider", 150, "Component type (e.g., Number Slider, Panel, Circle)"),
                    new("x", "100", 60, "X coordinate"),
                    new("y", "100", 60, "Y coordinate")
                },
                "connect_components" => new List<ParameterDefinition>
                {
                    new("sourceId", "", 250, "Source component ID"),
                    new("targetId", "", 250, "Target component ID"),
                    new("sourceParam", "", 80, "Source parameter (optional)"),
                    new("targetParam", "", 80, "Target parameter (optional)")
                },
                "get_component_info" => new List<ParameterDefinition>
                {
                    new("componentId", "", 300, "Component ID")
                },
                "save_document" or "load_document" => new List<ParameterDefinition>
                {
                    new("path", "", 400, "File path")
                },
                "search_components" or "get_available_patterns" => new List<ParameterDefinition>
                {
                    new("query", "", 300, "Search query")
                },
                "get_component_parameters" => new List<ParameterDefinition>
                {
                    new("componentType", "Number Slider", 200, "Component type")
                },
                "validate_connection" => new List<ParameterDefinition>
                {
                    new("sourceId", "", 200, "Source component ID"),
                    new("targetId", "", 200, "Target component ID"),
                    new("sourceParam", "", 80, "Source parameter"),
                    new("targetParam", "", 80, "Target parameter")
                },
                "create_pattern" => new List<ParameterDefinition>
                {
                    new("description", "", 400, "Pattern description")
                },
                // Script component commands
                "get_script_components" => new List<ParameterDefinition>(),
                "get_script_code" => new List<ParameterDefinition>
                {
                    new("component_id", "", 300, "Script component GUID")
                },
                "set_script_code" => new List<ParameterDefinition>
                {
                    new("component_id", "", 300, "Script component GUID"),
                    new("code", "", 400, "C# source code"),
                    new("compile", "false", 60, "Compile after setting (true/false)")
                },
                "compile_script" => new List<ParameterDefinition>
                {
                    new("component_id", "", 300, "Script component GUID")
                },
                "investigate_script_component" => new List<ParameterDefinition>
                {
                    new("component_id", "", 300, "Script component GUID")
                },
                "test_script_compilation" => new List<ParameterDefinition>
                {
                    new("component_id", "", 300, "Script component GUID")
                },
                // Debug commands
                "dump_canvas" => new List<ParameterDefinition>(),
                "inspect_component" => new List<ParameterDefinition>
                {
                    new("component_id", "", 300, "Component GUID to inspect")
                },
                _ => new List<ParameterDefinition>()
            };
        }

        private void UpdateRequestJson()
        {
            var commandType = (CommandTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrEmpty(commandType)) return;

            var command = BuildCommand(commandType);
            RequestTextBox.Text = JsonConvert.SerializeObject(command, Formatting.Indented);
        }

        private GrasshopperCommand BuildCommand(string commandType)
        {
            var parameters = new Dictionary<string, object?>();

            foreach (var kvp in _parameterInputs)
            {
                var value = kvp.Value.Text.Trim();
                if (string.IsNullOrEmpty(value)) continue;

                // Try to parse as number
                if (float.TryParse(value, out var numValue))
                {
                    parameters[kvp.Key] = numValue;
                }
                else
                {
                    parameters[kvp.Key] = value;
                }
            }

            return new GrasshopperCommand(commandType, parameters);
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_connected || _client == null)
            {
                // Auto-connect
                _client = new GrasshopperClient(HostTextBox.Text, int.Parse(PortTextBox.Text));
                _connected = true;
            }

            try
            {
                SendButton.IsEnabled = false;
                ResponseStatus.Text = "Sending...";
                ResponseStatus.Foreground = new SolidColorBrush(Colors.Yellow);

                // Parse command from request text box (allows manual editing)
                GrasshopperCommand? command;
                try
                {
                    command = JsonConvert.DeserializeObject<GrasshopperCommand>(RequestTextBox.Text);
                }
                catch
                {
                    // If parsing fails, build from form
                    var commandType = (CommandTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                    command = BuildCommand(commandType);
                }

                if (command == null)
                {
                    ResponseTextBox.Text = "Invalid command";
                    return;
                }

                var response = await _client.SendCommandAsync(command);

                // Update response
                ResponseTextBox.Text = JsonConvert.SerializeObject(response, Formatting.Indented);

                if (response.Success)
                {
                    ResponseStatus.Text = "SUCCESS";
                    ResponseStatus.Foreground = new SolidColorBrush(Colors.LimeGreen);
                }
                else
                {
                    ResponseStatus.Text = $"ERROR: {response.Error}";
                    ResponseStatus.Foreground = new SolidColorBrush(Colors.Red);
                }

                // Add to history
                AddToHistory(command, response);
            }
            catch (Exception ex)
            {
                ResponseTextBox.Text = JsonConvert.SerializeObject(new { error = ex.Message }, Formatting.Indented);
                ResponseStatus.Text = "EXCEPTION";
                ResponseStatus.Foreground = new SolidColorBrush(Colors.Red);
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        #endregion

        #region History

        private void AddToHistory(GrasshopperCommand command, GrasshopperResponse response)
        {
            var item = new HistoryItem
            {
                Timestamp = DateTime.Now,
                Command = command,
                Response = response
            };

            _history.Insert(0, item);

            HistoryComboBox.Items.Insert(0, new ComboBoxItem
            {
                Content = $"[{item.Timestamp:HH:mm:ss}] {command.Type}",
                Tag = item
            });

            // Keep only last 50 items
            while (_history.Count > 50)
            {
                _history.RemoveAt(_history.Count - 1);
                HistoryComboBox.Items.RemoveAt(HistoryComboBox.Items.Count - 1);
            }
        }

        private void HistoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HistoryComboBox.SelectedItem is ComboBoxItem item && item.Tag is HistoryItem historyItem)
            {
                RequestTextBox.Text = JsonConvert.SerializeObject(historyItem.Command, Formatting.Indented);
                ResponseTextBox.Text = JsonConvert.SerializeObject(historyItem.Response, Formatting.Indented);

                if (historyItem.Response.Success)
                {
                    ResponseStatus.Text = "SUCCESS (from history)";
                    ResponseStatus.Foreground = new SolidColorBrush(Colors.LimeGreen);
                }
                else
                {
                    ResponseStatus.Text = $"ERROR (from history): {historyItem.Response.Error}";
                    ResponseStatus.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            _history.Clear();
            HistoryComboBox.Items.Clear();
        }

        #endregion

        #region Helper Classes

        private class ParameterDefinition
        {
            public string Name { get; }
            public string DefaultValue { get; }
            public int Width { get; }
            public string Description { get; }

            public ParameterDefinition(string name, string defaultValue, int width, string description)
            {
                Name = name;
                DefaultValue = defaultValue;
                Width = width;
                Description = description;
            }
        }

        private class HistoryItem
        {
            public DateTime Timestamp { get; set; }
            public GrasshopperCommand Command { get; set; } = null!;
            public GrasshopperResponse Response { get; set; } = null!;
        }

        #endregion
    }
}
