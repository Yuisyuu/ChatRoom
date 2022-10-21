using ChatRoom.Packet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Management.Instrumentation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ChatRoom
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static TcpClient TcpClient { get; set; }

        private static Thread Thread { get; set; }

        public MainWindow()
        {
            _ = new Mutex(true, Assembly.GetExecutingAssembly().GetName().Name, out bool isNotRunning);
            if (!isNotRunning)
            {
                _ = MessageBox.Show("你只能同时运行一个聊天室实例！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new InstanceNotFoundException("你只能同时运行一个聊天室实例！");
            }
            InitializeComponent();
        }

        private void Connect(object sender, RoutedEventArgs e)
        {
            string ip = IPBox.Text;
            LoginGrid.IsEnabled = false;
            TcpClient = new TcpClient();
            try
            {
                TcpClient.Connect(ip, 19132);
            }
            catch (SocketException ex)
            {
                _ = MessageBox.Show($"连接失败：{ex.Message}", "错误");
                LoginGrid.IsEnabled = true;
                return;
            }
            LoginGrid.Visibility = Visibility.Hidden;
            RoomGrid.IsEnabled = true;
            RoomGrid.Visibility = Visibility.Visible;
            Dictionary<string, string> beforeOne = new Dictionary<string, string>();
            Thread = new Thread(() =>
            {
                while (true)
                {
                    byte[] bytes = new byte[ushort.MaxValue];
                    try
                    {
                        NetworkStream stream = TcpClient.GetStream();
                        if (!stream.CanRead)
                        {
                            continue;
                        }
                        _ = stream.Read(bytes, 0, bytes.Length);
                    }
                    catch (IOException ex)
                    {
                        _ = Dispatcher.Invoke((Action)(() =>
                        {
                            ShowMessage($"{Environment.NewLine}已断开连接：{ex.Message}");
                            SendButton.IsEnabled = false;
                        }));
                        int count = 0;
                        while (!TcpClient.Connected)
                        {
                            TcpClient = new TcpClient();
                            _ = Dispatcher.Invoke((Action)(() =>
                            {
                                ip = IPBox.Text;
                                ShowMessage($"{Environment.NewLine}重连中：{++count}");
                            }));
                            try
                            {
                                TcpClient.Connect(ip, 19132);
                            }
                            catch (SocketException ex1)
                            {
                                _ = Dispatcher.Invoke((Action)(() => ShowMessage($"{Environment.NewLine}重连失败：{ex1.Message}")));
                            }
                        }
                        _ = Dispatcher.Invoke((Action)(() =>
                        {
                            ShowMessage($"{Environment.NewLine}已重连");
                            SendButton.IsEnabled = true;
                        }));
                        continue;
                    }
                    string receivedString = Encoding.UTF8.GetString(bytes).Replace("\0", string.Empty);
                    if (string.IsNullOrEmpty(receivedString))
                    {
                        continue;
                    }
                    switch (JsonConvert.DeserializeObject<Base<object>>(receivedString).Action)
                    {
                        case ActionType.Message:
                            Base<Message.Response> data = JsonConvert.DeserializeObject<Base<Message.Response>>(receivedString);
                            if (beforeOne.ContainsKey(data.Param.UUID) && beforeOne[data.Param.UUID] == data.Param.Message)
                            {
                                continue;
                            }
                            _ = Dispatcher.Invoke((Action)(() =>
                            {
                                if (!string.IsNullOrEmpty(ChatBox.Text))
                                {
                                    ShowMessage(Environment.NewLine);
                                }
                                if (!beforeOne.ContainsKey("user") || data.Param.UUID != beforeOne["user"])
                                {
                                    if (!string.IsNullOrEmpty(ChatBox.Text))
                                    {
                                        ShowMessage(Environment.NewLine);
                                    }
                                    ShowMessage($"{data.Param.UserName}（{data.Param.UUID}） ");
                                }
                                ShowMessage($"{data.Param.DateTime}{Environment.NewLine}{data.Param.Message}");
                            }));
                            beforeOne[data.Param.UUID] = data.Param.Message;
                            beforeOne["user"] = data.Param.UUID;
                            break;
                    }
                }
            });
            Thread.Start();
        }

        private void Send(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(InputBox.Text))
            {
                return;
            }
            NetworkStream stream = TcpClient.GetStream();
            if (!stream.CanWrite)
            {
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Base<Message.Request>()
            {
                Action = ActionType.Message,
                Param = new Message.Request()
                {
                    Message = InputBox.Text,
                    UserName = NameBox.Text
                }
            }));
            stream.Write(bytes, 0, bytes.Length);
            InputBox.Text = string.Empty;
        }

        private void EnterButtonDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter && SendButton.IsEnabled)
            {
                Send(default, default);
            }
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            Thread.Abort();
            if (TcpClient is null || !TcpClient.Connected)
            {
                return;
            }
            TcpClient.Close();
        }

        private void ShowMessage(string message)
        {
            ChatBox.Text += message;
            ChatBox.ScrollToEnd();
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                ChatBox.Width = e.NewSize.Width - 32 < 0 ? 0 : e.NewSize.Width - 32;
                InputBox.Width = e.NewSize.Width - 137 < 0 ? 0 : e.NewSize.Width - 137;
                SendButton.Margin = new Thickness(e.NewSize.Width - 122, SendButton.Margin.Top, SendButton.Margin.Right, SendButton.Margin.Bottom);
            }
            if (e.HeightChanged)
            {
                ChatBox.Height = e.NewSize.Height - 140 < 0 ? 0 : e.NewSize.Height - 140;
                InputBox.Margin = new Thickness(InputBox.Margin.Left, e.NewSize.Height - 96, InputBox.Margin.Right, InputBox.Margin.Bottom);
                SendButton.Margin = new Thickness(SendButton.Margin.Left, e.NewSize.Height - 96, SendButton.Margin.Right, SendButton.Margin.Bottom);
            }
        }
    }
}