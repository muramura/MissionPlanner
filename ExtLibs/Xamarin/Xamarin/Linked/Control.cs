using System;
using System.Drawing;
using SkiaSharp;
using Xamarin.Controls;

namespace System.Windows.Forms
{
    [Flags]
    public enum ControlStyles
    {
        UserPaint = 1,
        AllPaintingInWmPaint = 2,
        DoubleBuffer = 4,
        OptimizedDoubleBuffer = 8,
        ResizeRedraw = 16,
        SupportsTransparentBackColor = 32,
        Opaque = 64
    }

    public class Control : MySKCanvasView
    {
        public bool InvokeRequired { get; internal set; }
        public virtual string Text { get; set; } = "";
        public virtual bool Visible { get; set; } = true;
        public virtual bool Enabled { get; set; } = true;
        public virtual Xamarin.Controls.Cursor Cursor { get; set; } = Xamarin.Controls.Cursors.Default;

        public void SetStyle(ControlStyles flag, bool value) { }

        protected virtual void OnPaint(PaintEventArgs e) { }
        protected virtual void OnPaintBackground(PaintEventArgs e) { }
    }

    public class UserControl : Control
    {
        public object Invoke(Action p0)
        {
            Xamarin.Forms.Device.BeginInvokeOnMainThread(p0);
            return null;
        }
    }

    public class Label : Control { }

    public enum ImageLayout
    {
        None,
        Tile,
        Center,
        Stretch,
        Zoom
    }

    [Flags]
    public enum Keys
    {
        None = 0,
        Control = 131072,
        Shift = 65536,
        Alt = 262144,
        A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73, J = 74,
        K = 75, L = 76, M = 77, N = 78, O = 79, P = 80, Q = 81, R = 82, S = 83, T = 84,
        U = 85, V = 86, W = 87, X = 88, Y = 89, Z = 90
    }

    public class KeyEventArgs : EventArgs
    {
        public Keys KeyCode { get; set; }
        public Keys KeyData { get; set; }
        public bool Alt { get; set; }
        public bool Control { get; set; }
        public bool Shift { get; set; }
        public bool Handled { get; set; }
        public Keys Modifiers => (Alt ? Keys.Alt : Keys.None) | (Control ? Keys.Control : Keys.None) | (Shift ? Keys.Shift : Keys.None);
    }

    [Flags]
    public enum MouseButtons
    {
        None = 0,
        Left = 1048576,
        Right = 2097152,
        Middle = 4194304
    }

    public class MouseEventArgs : EventArgs
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Clicks { get; set; }
        public int Delta { get; set; }
        public MouseButtons Button { get; set; }
        public Point Location => new Point(X, Y);

        public MouseEventArgs(MouseButtons button = MouseButtons.None, int clicks = 0, int x = 0, int y = 0, int delta = 0)
        {
            Button = button;
            Clicks = clicks;
            X = x;
            Y = y;
            Delta = delta;
        }
    }

    public class PaintEventArgs : EventArgs, IDisposable
    {
        public IGraphics Graphics { get; }
        public Rectangle ClipRectangle { get; }

        public PaintEventArgs(IGraphics gg, Rectangle clientRectangle)
        {
            Graphics = gg;
            ClipRectangle = clientRectangle;
        }

        public void Dispose() { }
    }
}

namespace MissionPlanner.Utilities
{
    public class TFR 
    { 
        public System.Collections.Generic.List<TFRItem> tfrs = new System.Collections.Generic.List<TFRItem>(); 
    }
    public class TFRItem 
    { 
        public string NAME = ""; 
        public System.Collections.Generic.List<System.Collections.Generic.List<GMap.NET.PointLatLng>> GetPaths() => new System.Collections.Generic.List<System.Collections.Generic.List<GMap.NET.PointLatLng>>(); 
    }
}
