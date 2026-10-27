using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;

public sealed partial class QcyTray
{
    bool qcyBusy;
    bool qcyReady;
    bool qcySyncing;
    QcyState qcyLastState;
    DispatcherTimer qcyBalanceTimer;
    readonly QcySession qcySession=new QcySession();
    void SetupQcy() {
        Get<Button>("QcyRefresh").Click+=delegate { QcyWork(null); };
        Get<Button>("QcyNormal").Click+=delegate { QcyWork(new byte[]{2,0,0}); };
        Get<Button>("QcyTransparent").Click+=delegate { QcyWork(new byte[]{3,1,4}); };
        Get<Button>("QcyAnc").Click+=delegate { QcyWork(new byte[]{1,1,2}); };
        var balance=Get<Slider>("QcyBalance");
        qcyBalanceTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(350) };
        qcyBalanceTimer.Tick+=delegate {
            if(balance.IsMouseCaptureWithin || qcyBusy) return;
            qcyBalanceTimer.Stop();
            if(qcyLastState!=null && (int)balance.Value!=qcyLastState.Balance) QcyWork(null,(int)balance.Value,null);
        };
        balance.ValueChanged+=delegate {
            Text("QcyBalanceValue",BalanceLabel((int)balance.Value));
            if(!qcySyncing && qcyReady) { qcyBalanceTimer.Stop(); qcyBalanceTimer.Start(); }
        };
        Get<Button>("QcyBalanceReset").Click+=delegate { balance.Value=50; };
        Get<CheckBox>("QcyGame").Click+=delegate { if(!qcySyncing) QcyWork(null,null,Get<CheckBox>("QcyGame").IsChecked==true); };
        window.Closed+=delegate { qcyBalanceTimer.Stop(); ThreadPool.QueueUserWorkItem(delegate { qcySession.Dispose(); }); };
    }
    static string BalanceLabel(int value) { return value==50?"Centro":value<50?"L "+(50-value):"R "+(value-50); }
    void RenderQcyControls(QcyState state) {
        qcySyncing=true;
        try {
            Get<Slider>("QcyBalance").Value=state.Balance;
            Text("QcyBalanceValue",BalanceLabel(state.Balance));
            Get<CheckBox>("QcyGame").IsChecked=state.GameMode;
        } finally { qcySyncing=false; }
    }
    void QcyWork(byte[] requested,int? balance=null,bool? game=null) {
        if(qcyBusy || closed) return;
        if(qcyBalanceTimer.IsEnabled && !balance.HasValue) balance=(int)Get<Slider>("QcyBalance").Value;
        qcyBalanceTimer.Stop();
        qcyBusy=true;
        Get<TextBlock>("QcyStatus").Visibility=Visibility.Visible;
        Text("QcyStatus",requested==null && !balance.HasValue && !game.HasValue?(qcyReady?"Atualizando estado…":"Conectando ao H3 Pro…"):"Aplicando e confirmando…");
        Get<StackPanel>("QcyModes").IsEnabled=false;
        Get<StackPanel>("QcyExtra").IsEnabled=false;
        Get<Button>("QcyRefresh").IsEnabled=false;
        ThreadPool.QueueUserWorkItem(delegate {
            byte[] actual=null; QcyState state=null; string error=null;
            try { state=qcySession.Controls(requested,balance,game); actual=state.Mode; }
            catch(Exception ex) { error=ex.Message; }
            if(closed) return;
            window.Dispatcher.BeginInvoke(new Action(delegate {
                if(closed) return;
                qcyBusy=false;
                qcyReady=actual!=null;
                if(state!=null) qcyLastState=state;
                if(qcyLastState!=null) RenderQcyControls(qcyLastState);
                Get<StackPanel>("QcyExtra").IsEnabled=state!=null;
                Text("QcyStatus",error ?? "Conectado");
                Get<TextBlock>("QcyStatus").Visibility=Visibility.Visible;
                Get<StackPanel>("QcyModes").IsEnabled=actual!=null;
                Get<Button>("QcyRefresh").IsEnabled=true;
                var names=new[]{"QcyAnc","QcyNormal","QcyTransparent"};
                for(int i=0;i<names.Length;i++) {
                    Get<TextBlock>(names[i]+"Mark").Visibility=actual!=null && actual[0]==i+1?Visibility.Visible:Visibility.Hidden;
                }
                tray.Text=actual==null?"QCY Controls · Indisponível":"QCY Controls · "+QcyConnection.ModeName(actual);
            }));
        });
    }
}
