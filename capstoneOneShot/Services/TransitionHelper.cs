using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace capstoneOneShot.Services
{
    public static class TransitionHelper
    {
        /// <summary>
        /// Call this in the constructor of any Window you want to fade in automatically when shown.
        /// </summary>
        public static void ApplyFadeInTransition(Window window)
        {
            window.Opacity = 0;
            window.IsVisibleChanged += (s, e) =>
            {
                if (window.IsVisible)
                {
                    var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
                    window.BeginAnimation(UIElement.OpacityProperty, anim);
                }
            };
        }

        /// <summary>
        /// Fades the window out and then invokes an action (like opening another window), then Closes it.
        /// </summary>
        public static void FadeOutAndClose(Window window, Action nextAction = null)
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            anim.Completed += (s, e) =>
            {
                nextAction?.Invoke();
                window.Close();
            };
            window.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        /// <summary>
        /// Fades the window out and then invokes an action (like opening another window), then Hides it.
        /// </summary>
        public static void FadeOutAndHide(Window window, Action nextAction = null)
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            anim.Completed += (s, e) =>
            {
                nextAction?.Invoke();
                window.Hide();
            };
            window.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }
}
