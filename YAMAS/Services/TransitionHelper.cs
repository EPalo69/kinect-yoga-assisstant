using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace YAMAS.Services
{
    public static class TransitionHelper
    {
        /// <summary>
        /// Call this in the constructor of any Window you want to fade in automatically when shown.
        /// </summary>
        public static void ApplyFadeInTransition(Window window)
        {
            window.IsVisibleChanged += (s, e) =>
            {
                if (window.IsVisible && window.Content is UIElement content)
                {
                    // If content is already fully visible and we just initialized, animate it
                    content.Opacity = 0;
                    var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
                    content.BeginAnimation(UIElement.OpacityProperty, anim);
                }
            };
        }

        /// <summary>
        /// Fades the window content out and then invokes an action (like opening another window), then Closes it.
        /// </summary>
        public static void FadeOutAndClose(Window window, Action nextAction = null)
        {
            if (window.Content is UIElement content)
            {
                var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                anim.Completed += (s, e) =>
                {
                    nextAction?.Invoke();
                    window.Close();
                };
                content.BeginAnimation(UIElement.OpacityProperty, anim);
            }
            else
            {
                nextAction?.Invoke();
                window.Close();
            }
        }

        /// <summary>
        /// Fades the window content out and then invokes an action (like opening another window), then Hides it.
        /// </summary>
        public static void FadeOutAndHide(Window window, Action nextAction = null)
        {
            if (window.Content is UIElement content)
            {
                var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                anim.Completed += (s, e) =>
                {
                    nextAction?.Invoke();
                    window.Hide();
                    
                    // Reset content opacity so it's visible if the window is shown again
                    content.BeginAnimation(UIElement.OpacityProperty, null);
                    content.Opacity = 1;
                };
                content.BeginAnimation(UIElement.OpacityProperty, anim);
            }
            else
            {
                nextAction?.Invoke();
                window.Hide();
            }
        }
    }
}
