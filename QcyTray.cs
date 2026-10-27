using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

public sealed partial class QcyTray
{
    Window window;
    Forms.NotifyIcon tray;
    Drawing.Icon icon;
    bool closed;
    readonly Application app;
    T Get<T>(string name) where T : class { return window.FindName(name) as T; }
    void Text(string name,string value) { Get<TextBlock>(name).Text=value; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    static Drawing.Icon HeadphoneIcon() {
        using(var bitmap=new Drawing.Bitmap(32,32)) {
            using(var g=Drawing.Graphics.FromImage(bitmap))
            using(var pen=new Drawing.Pen(Drawing.Color.White,3)) {
                g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.DrawArc(pen,6,5,20,22,180,180);
                g.FillRectangle(Drawing.Brushes.White,4,16,6,12);
                g.FillRectangle(Drawing.Brushes.White,22,16,6,12);
            }
            IntPtr handle=bitmap.GetHicon();
            try { using(var original=Drawing.Icon.FromHandle(handle)) return (Drawing.Icon)original.Clone(); }
            finally { DestroyIcon(handle); }
        }
    }

    QcyTray(Application application) {
        app=application;
        using(var stream=typeof(QcyTray).Assembly.GetManifestResourceStream("QcyWindow.xaml"))
            window=(Window)XamlReader.Load(stream);
        SetupQcy();
        Get<Button>("HidePanel").Click+=delegate { window.Hide(); };
        window.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {
            if(e.OriginalSource is TextBlock && e.GetPosition(window).Y<55) window.DragMove();
        };
        window.KeyDown+=delegate(object sender,KeyEventArgs e) {
            if(e.Key==Key.Escape) window.Hide();
            if(e.Key==Key.F5) QcyWork(null);
        };
        window.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) {
            if(!closed) { e.Cancel=true; window.Hide(); }
        };
        icon=HeadphoneIcon();
        tray=new Forms.NotifyIcon { Icon=icon,Text="QCY OpenControls · Controles",Visible=true };
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir controles",null,delegate { ShowPanel(); });
        menu.Items.Add("Sair",null,delegate { Exit(); });
        tray.ContextMenuStrip=menu;
        tray.MouseClick+=delegate(object sender,Forms.MouseEventArgs e) { if(e.Button==Forms.MouseButtons.Left) ShowPanel(); };
        app.Exit+=delegate { tray.Visible=false; tray.Dispose(); menu.Dispose(); icon.Dispose(); };
    }

    void ShowPanel() {
        if(!window.IsVisible) {
            var area=SystemParameters.WorkArea;
            window.Left=Math.Max(area.Left,area.Right-window.Width-12);
            window.Top=Math.Max(area.Top,area.Bottom-window.Height-12);
        }
        window.Show(); window.Activate(); QcyWork(null);
    }
    void Exit() { closed=true; window.Close(); app.Shutdown(); }

    [STAThread] public static int Main(string[] args) {
        if(args.Length==2 && (args[0]=="--qcy-test" || args[0]=="--qcy-session-test" || args[0]=="--qcy-controls-test")) {
            string result=null; int code=0;
            var worker=new Thread(delegate() { try { result=args[0]=="--qcy-controls-test"?QcyConnection.ControlsTest():args[0]=="--qcy-session-test"?QcySession.PersistenceTest():QcyConnection.HardwareTest(); } catch(Exception ex) { result=ex.ToString(); code=1; } });
            worker.SetApartmentState(ApartmentState.MTA); worker.Start(); worker.Join(); File.WriteAllText(args[1],result); return code;
        }
        bool first;
        using(var mutex=new Mutex(true,"Local\\QcyH3ProTray",out first)) {
            if(!first) return 0;
            var application=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
            var controller=new QcyTray(application);
            if(args.Length==2 && args[0]=="--capture") {
                controller.window.Show(); controller.window.UpdateLayout();
                var image=new System.Windows.Media.Imaging.RenderTargetBitmap(300,408,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                image.Render(controller.window);
                var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                using(var stream=File.Create(args[1])) encoder.Save(stream);
                controller.Exit(); return 0;
            }
            application.Run();
        }
        return 0;
    }
}
