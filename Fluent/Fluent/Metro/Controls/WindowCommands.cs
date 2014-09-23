﻿namespace Fluent
{
    using System;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using Fluent.Metro.Native;

    /// <summary>
    /// Contains commands for <see cref="RibbonWindow"/>
    /// </summary>
    [TemplatePart(Name = "PART_Max", Type = typeof(Button))]
    [TemplatePart(Name = "PART_Close", Type = typeof(Button))]
    [TemplatePart(Name = "PART_Min", Type = typeof(Button))]
    public class WindowCommands : ItemsControl, IDisposable
    {
        private static string minimize;
        private static string maximize;
        private static string closeText;
        private static string restore;
        private System.Windows.Controls.Button min;
        private System.Windows.Controls.Button max;
        private System.Windows.Controls.Button close;
        private IntPtr user32 = IntPtr.Zero;
        private bool disposed;

        static WindowCommands()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WindowCommands), new FrameworkPropertyMetadata(typeof(WindowCommands)));
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~WindowCommands()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose(bool disposing) executes in two distinct scenarios.
        /// If disposing equals true, the method has been called directly
        /// or indirectly by a user's code. Managed and unmanaged resources
        /// can be disposed.
        /// If disposing equals false, the method has been called by the
        /// runtime from inside the finalizer and you should not reference
        /// other objects. Only unmanaged resources can be disposed.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (this.disposed)
            {
                return;
            }

            // If disposing equals true, dispose all managed
            // and unmanaged resources.
            if (disposing)
            {
                // Dispose managed resources.
            }

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            // If disposing is false,
            // only the following code is executed.
            if (this.user32 != IntPtr.Zero)
            {
                UnsafeNativeMethods.FreeLibrary(this.user32);
                this.user32 = IntPtr.Zero;
            }

            // Note disposing has been done.
            this.disposed = true;
        }

        /// <summary>
        /// Event which is raised when the window should be closed
        /// </summary>
        public event EventHandler<ClosingWindowEventHandlerArgs> ClosingWindow;

        /// <summary>
        /// Retrieves the translated string for Minimize
        /// </summary>
        public string Minimize
        {
            get
            {
                if (string.IsNullOrEmpty(minimize))
                    minimize = GetCaption(900);
                return minimize;
            }
        }

        /// <summary>
        /// Retrieves the translated string for Maximize
        /// </summary>
        public string Maximize
        {
            get
            {
                if (string.IsNullOrEmpty(maximize))
                    maximize = GetCaption(901);
                return maximize;
            }
        }

        /// <summary>
        /// Retrieves the translated string for Close
        /// </summary>
        public string Close
        {
            get
            {
                if (string.IsNullOrEmpty(closeText))
                    closeText = GetCaption(905);
                return closeText;
            }
        }

        /// <summary>
        /// Retrieves the translated string for Restore
        /// </summary>
        public string Restore
        {
            get
            {
                if (string.IsNullOrEmpty(restore))
                {
                    restore = GetCaption(903);
                }

                return restore;
            }
        }

        private string GetCaption(int id)
        {
            if (this.user32 == IntPtr.Zero)
            {
                this.user32 = UnsafeNativeMethods.LoadLibrary(Environment.SystemDirectory + "\\User32.dll");
            }

            var sb = new StringBuilder(256);
            UnsafeNativeMethods.LoadString(this.user32, (uint)id, sb, sb.Capacity);
            return sb.ToString().Replace("&", "");
        }

        /// <summary>
        /// When overridden in a derived class, is invoked whenever application code or internal processes call <see cref="M:System.Windows.FrameworkElement.ApplyTemplate"/>.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            close = GetTemplateChild("PART_Close") as System.Windows.Controls.Button;//Template.FindName("PART_Close", this) as Button;
            if (close != null)
                close.Click += CloseClick;

            max = Template.FindName("PART_Max", this) as System.Windows.Controls.Button;
            if (max != null)
                max.Click += MaximiseClick;

            min = Template.FindName("PART_Min", this) as System.Windows.Controls.Button;
            if (min != null)
                min.Click += MinimiseClick;

            this.RefreshMaximizeIconState();
        }

        /// <summary>
        /// Is called when the window should be closed
        /// </summary>
        protected void OnClosingWindow(ClosingWindowEventHandlerArgs args)
        {
            var handler = ClosingWindow;
            if (handler != null)
                handler(this, args);
        }

        private void MinimiseClick(object sender, RoutedEventArgs e)
        {
            var parentWindow = GetParentWindow();
            if (parentWindow != null)
                parentWindow.WindowState = WindowState.Minimized;
        }

        private void MaximiseClick(object sender, RoutedEventArgs e)
        {
            var parentWindow = GetParentWindow();
            if (parentWindow == null)
                return;

            parentWindow.WindowState = parentWindow.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            this.RefreshMaximizeIconState(parentWindow);
        }

        /// <summary>
        /// Upates the visual state of the maximize icon
        /// </summary>
        public void RefreshMaximizeIconState()
        {
            this.RefreshMaximizeIconState(GetParentWindow());
        }

        private void RefreshMaximizeIconState(Window parentWindow)
        {
            if (parentWindow == null)
                return;

            if (parentWindow.WindowState == WindowState.Normal)
            {
                var maxpath = (Path)max.FindName("MaximisePath");
                if (maxpath != null)
                {
                    maxpath.Visibility = Visibility.Visible;
                }

                var restorepath = (Path)max.FindName("RestorePath");
                if (restorepath != null)
                {
                    restorepath.Visibility = Visibility.Collapsed;
                }

                max.ToolTip = Maximize;
            }
            else
            {
                var restorepath = (Path)max.FindName("RestorePath");
                if (restorepath != null)
                {
                    restorepath.Visibility = Visibility.Visible;
                }

                var maxpath = (Path)max.FindName("MaximisePath");
                if (maxpath != null)
                {
                    maxpath.Visibility = Visibility.Collapsed;
                }
                max.ToolTip = Restore;
            }
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            var closingWindowEventHandlerArgs = new ClosingWindowEventHandlerArgs();
            OnClosingWindow(closingWindowEventHandlerArgs);

            if (closingWindowEventHandlerArgs.Cancelled)
                return;

            var parentWindow = GetParentWindow();
            if (parentWindow != null)
            {
                parentWindow.Close();
            }
        }

        private Window GetParentWindow()
        {
            var parent = VisualTreeHelper.GetParent(this);

            while (parent != null && !(parent is Window))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            var parentWindow = parent as Window;
            return parentWindow;
        }
    }
}