using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PhoneProjection.Controls
{
    public enum AnimationVisualState
    {
        Idle,
        DeviceDetected,
        Connecting,
        CastingActive,
        ConnectionLost,
        Stopped
    }

    public partial class SignalWaveBackground : UserControl
    {
        private Storyboard? _waveStoryboard;
        private Storyboard? _particleStoryboard;
        private AnimationVisualState _currentState = AnimationVisualState.Idle;

        public SignalWaveBackground()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            StartBackgroundAnimations();
            ApplyVisualState(AnimationVisualState.Idle);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopBackgroundAnimations();
        }

        private void StartBackgroundAnimations()
        {
            try
            {
                // Wave Vertical Undulation
                _waveStoryboard = new Storyboard
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };

                var wave1Anim = new DoubleAnimation
                {
                    From = -6.0,
                    To = 6.0,
                    Duration = new Duration(TimeSpan.FromSeconds(5.5)),
                    AutoReverse = true,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(wave1Anim, Wave1Transform);
                Storyboard.SetTargetProperty(wave1Anim, new PropertyPath(TranslateTransform.YProperty));
                _waveStoryboard.Children.Add(wave1Anim);

                var wave2Anim = new DoubleAnimation
                {
                    From = 6.0,
                    To = -6.0,
                    Duration = new Duration(TimeSpan.FromSeconds(4.2)),
                    AutoReverse = true,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(wave2Anim, Wave2Transform);
                Storyboard.SetTargetProperty(wave2Anim, new PropertyPath(TranslateTransform.YProperty));
                _waveStoryboard.Children.Add(wave2Anim);

                _waveStoryboard.Begin();

                // Particle Signal Flow (Left-to-Right)
                _particleStoryboard = new Storyboard
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };

                var p1Anim = new DoubleAnimation
                {
                    From = 74.0,
                    To = 920.0,
                    Duration = new Duration(TimeSpan.FromSeconds(6.0))
                };
                Storyboard.SetTarget(p1Anim, Particle1Transform);
                Storyboard.SetTargetProperty(p1Anim, new PropertyPath(TranslateTransform.XProperty));
                _particleStoryboard.Children.Add(p1Anim);

                var p2Anim = new DoubleAnimation
                {
                    From = 74.0,
                    To = 920.0,
                    Duration = new Duration(TimeSpan.FromSeconds(4.5)),
                    BeginTime = TimeSpan.FromSeconds(2.0)
                };
                Storyboard.SetTarget(p2Anim, Particle2Transform);
                Storyboard.SetTargetProperty(p2Anim, new PropertyPath(TranslateTransform.XProperty));
                _particleStoryboard.Children.Add(p2Anim);

                _particleStoryboard.Begin();
            }
            catch { }
        }

        public void SetVisualState(AnimationVisualState state)
        {
            if (_currentState == state) return;
            _currentState = state;
            ApplyVisualState(state);
        }

        private void ApplyVisualState(AnimationVisualState state)
        {
            var cyanBrush = (Brush)FindResource("SignalAccentBrush");
            var blueBrush = (Brush)FindResource("PrimaryBrush");
            var emeraldBrush = (Brush)FindResource("SuccessBrush");
            var warningBrush = (Brush)FindResource("WarningBrush");
            var errorBrush = (Brush)FindResource("ErrorBrush");

            switch (state)
            {
                case AnimationVisualState.Idle:
                    WavePath2.Stroke = blueBrush;
                    WavePath2.Opacity = 0.25;
                    Particle1.Opacity = 0.4;
                    Particle2.Opacity = 0.3;
                    if (_particleStoryboard != null) _particleStoryboard.SetSpeedRatio(1.0);
                    break;

                case AnimationVisualState.DeviceDetected:
                    WavePath2.Stroke = cyanBrush;
                    WavePath2.Opacity = 0.45;
                    Particle1.Opacity = 0.7;
                    Particle2.Opacity = 0.6;
                    if (_particleStoryboard != null) _particleStoryboard.SetSpeedRatio(1.2);
                    break;

                case AnimationVisualState.Connecting:
                    WavePath2.Stroke = warningBrush;
                    WavePath2.Opacity = 0.6;
                    Particle1.Opacity = 0.85;
                    Particle2.Opacity = 0.75;
                    if (_particleStoryboard != null) _particleStoryboard.SetSpeedRatio(1.8);
                    break;

                case AnimationVisualState.CastingActive:
                    WavePath2.Stroke = cyanBrush;
                    WavePath2.Opacity = 0.65;
                    Particle1.Fill = cyanBrush;
                    Particle1.Opacity = 0.95;
                    Particle2.Opacity = 0.85;
                    if (_particleStoryboard != null) _particleStoryboard.SetSpeedRatio(2.2);
                    break;

                case AnimationVisualState.ConnectionLost:
                    WavePath2.Stroke = errorBrush;
                    WavePath2.Opacity = 0.35;
                    Particle1.Fill = errorBrush;
                    Particle1.Opacity = 0.5;
                    if (_particleStoryboard != null) _particleStoryboard.SetSpeedRatio(0.6);
                    break;

                case AnimationVisualState.Stopped:
                    ApplyVisualState(AnimationVisualState.Idle);
                    break;
            }
        }

        public void PauseAnimations()
        {
            _waveStoryboard?.Pause();
            _particleStoryboard?.Pause();
        }

        public void ResumeAnimations()
        {
            _waveStoryboard?.Resume();
            _particleStoryboard?.Resume();
        }

        private void StopBackgroundAnimations()
        {
            _waveStoryboard?.Stop();
            _particleStoryboard?.Stop();
        }
    }
}
