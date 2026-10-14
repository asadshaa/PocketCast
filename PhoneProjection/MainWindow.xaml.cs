using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using PhoneProjection.Controls;
using PhoneProjection.ViewModels;

namespace PhoneProjection
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnMainWindowLoaded;
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                UpdateBackgroundVisualState();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsMirroringActive) ||
                e.PropertyName == nameof(ViewModel.SelectedDevice) ||
                e.PropertyName == nameof(ViewModel.IsBusy) ||
                e.PropertyName == nameof(ViewModel.StatusText) ||
                e.PropertyName == nameof(ViewModel.ReducedMotion))
            {
                Dispatcher.Invoke(UpdateBackgroundVisualState);
            }
        }

        private void UpdateBackgroundVisualState()
        {
            if (SignalBackground == null || ViewModel == null) return;

            if (ViewModel.ReducedMotion)
            {
                SignalBackground.PauseAnimations();
                SignalBackground.Visibility = Visibility.Hidden;
                return;
            }
            else
            {
                SignalBackground.Visibility = Visibility.Visible;
                SignalBackground.ResumeAnimations();
            }

            if (ViewModel.IsMirroringActive)
            {
                SignalBackground.SetVisualState(AnimationVisualState.CastingActive);
            }
            else if (ViewModel.IsBusy)
            {
                SignalBackground.SetVisualState(AnimationVisualState.Connecting);
            }
            else if (ViewModel.SelectedDevice != null)
            {
                if (ViewModel.SelectedDevice.IsAuthorized)
                {
                    SignalBackground.SetVisualState(AnimationVisualState.DeviceDetected);
                }
                else
                {
                    SignalBackground.SetVisualState(AnimationVisualState.ConnectionLost);
                }
            }
            else
            {
                SignalBackground.SetVisualState(AnimationVisualState.Idle);
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            // Suspend background animations when window is minimized to conserve GPU/CPU cycles
            if (WindowState == WindowState.Minimized)
            {
                SignalBackground?.PauseAnimations();
            }
            else
            {
                SignalBackground?.ResumeAnimations();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // F11 or Alt+Enter toggles fullscreen preference
            if (e.Key == Key.F11 || (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt))
            {
                if (ViewModel != null)
                {
                    ViewModel.IsFullscreen = !ViewModel.IsFullscreen;
                }
                e.Handled = true;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.Cleanup();
            }
        }
    }
}