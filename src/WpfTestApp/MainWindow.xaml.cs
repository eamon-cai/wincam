﻿using SmartReplayApp.Utilities;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfTestApp.Native;

namespace WpfTestApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ICapture capture;
        bool running;
        DispatcherTimer showMouseTimer;
        bool captured;
        int x = -1; 
        int y = -1;

        public MainWindow()
        {
            InitializeComponent();
            UpdateButtonState();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            StopMouseTimer();
            running = false;
            base.OnClosing(e);
        }

        bool updateBusyThrottle = false;

        private void ShowStatus(string msg)
        {
            if (updateBusyThrottle) { return; }
            updateBusyThrottle = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusText.Text = msg;
                this.updateBusyThrottle = false;
            }));
        }

        private async void OnCapture(object sender, RoutedEventArgs e)
        {
            this.CaptureButton.IsEnabled = false;
            if (this.capture != null)
            {
                ShowStatus("Terminating previous capture...");
                running = false;
                while (this.capture != null)
                {
                    await Task.Delay(100);
                }
            }
            running = true;
            using var c = new Capture();
            this.capture = c;
            List<int> averageFps = new List<int>();
            try
            {
                ShowStatus("Initializing...");
                await c.StartCapture(x, y);
                UpdateButtonState();
                int ignore = 2;
                await Task.Run(async () =>
                {
                    var throttle = new Throttle(60);
                    var start = Environment.TickCount;
                    int count = 0;
                    while (running)
                    {
                        await Dispatcher.InvokeAsync(new Action(() =>
                        {
                            var img = c.CaptureImage();
                            this.CapturedImage.Source = img;
                            if (img != null)
                            {
                                count++;
                            }
                        }), DispatcherPriority.Background);

                        var end = Environment.TickCount;
                        if (end > start + 1000)
                        {
                            var fps = count;
                            count = 0;
                            start = end;
                            ShowStatus($"{fps} fps");
                            if (ignore == 0)
                            {
                                averageFps.Add(fps);
                            }
                            else
                            {
                                ignore--;
                            }
                        }
                        throttle.Step();
                    }
                });
            }
            catch (TaskCanceledException) 
            {
                Debug.WriteLine("Task cancelled");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Capture Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            this.CaptureButton.IsEnabled = true;
            this.capture = null;
            running = false;
            this.UpdateButtonState();
            if (averageFps.Count > 0)
            {
                int avg = averageFps.Sum() / averageFps.Count;
                this.ShowStatus($"average fps {avg}");
            }
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
            this.running = false;
            this.UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            CaptureButton.IsEnabled = this.x != -1;
            CaptureButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
            StopButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        }

        #region WindowMousePicker

        private void StopMouseTimer()
        {
            if (showMouseTimer != null)
            {
                showMouseTimer.Stop();
                showMouseTimer = null;
            }
        }


        private void OnPickWindowChecked(object sender, RoutedEventArgs e)
        {
            if (this.CaptureMouse())
            {
                captured = true;
                ShowStatus("Move mouse and click on the window you want to capture");
                showMouseTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, OnShowMouse, this.Dispatcher);
            }
        }

        private void OnShowMouse(object sender, EventArgs e)
        {
            var mousePos = User32.GetScreenCursorPos();
            PositionText.Text = $"{mousePos.X},{mousePos.Y}";
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            StopMouseTimer();
            if (captured)
            {
                PickWindowButton.IsChecked = false;
                captured = false;
                this.ReleaseMouseCapture();
                e.Handled = true;
                var mousePos = User32.GetScreenCursorPos();
                this.x = (int)mousePos.X;
                this.y = (int)mousePos.Y;
                this.UpdateButtonState();
                POINT pt = new POINT() { X = x, Y = y };
                nint hwnd = User32.WindowFromPoint(pt);
                if (hwnd == nint.Zero)
                {
                    MessageBox.Show($"No window found at location {x}, {y}", "No Window Found", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    var bounds = User32.GetClientScreenRect(hwnd);
                    ShowStatus($"Picked hwnd {hwnd:x8} bounds {bounds}");
                }
            }
            else
            {
                base.OnMouseDown(e);
            }
        }
        #endregion

        private void OnNewWindowClicked(object sender, RoutedEventArgs e)
        {
            new MainWindow().Show();
        }
    }
}