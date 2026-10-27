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

// Protocol derived from the user's H3 Pro HCI capture. No OpenQCY dependency.
public sealed class QcyState
{
    public byte[] Mode;
    public int Balance;
    public bool GameMode;
    public static QcyState ParseFields(byte[] fields) {
        int? balance=null, game=null;
        for(int offset=0;offset<fields.Length;) {
            int length=fields[offset++];
            if(length<1 || offset+length>fields.Length) throw new InvalidOperationException("Resposta de controles incompleta.");
            byte key=fields[offset];
            if(key==0x0c || key==5) {
                if(length!=2) throw new InvalidOperationException("Formato de controle inesperado.");
                if(key==0x0c) balance=fields[offset+1]; else game=fields[offset+1];
            }
            offset+=length;
        }
        if(!balance.HasValue || balance<0 || balance>100 || (game!=1 && game!=2)) throw new InvalidOperationException("Balanço ou Game mode não informado pelo fone.");
        return new QcyState { Balance=balance.Value, GameMode=game==2 };
    }
}

public sealed class QcyConnection : IDisposable
{
    BluetoothLEDevice device;
    GattDeviceService service;
    GattCharacteristic writer, reader;
    readonly AutoResetEvent received = new AutoResetEvent(false);
    byte[] reply;
    static int nextSequence=Environment.TickCount;
    byte expectedOpcode;
    bool disposed;
    bool subscribed;
    bool statusSubscribed;
    volatile bool connectionLost;
    public bool IsConnected { get { return !disposed && !connectionLost && device!=null && device.ConnectionStatus==BluetoothConnectionStatus.Connected; } }
    byte expected;
    readonly object gate = new object();
    public static string ModeName(byte[] state) {
        return state[0]==1?"ANC on":state[0]==2?"ANC off":state[0]==3?"Ambient sound":"Modo desconhecido";
    }
    static T Wait<T>(IAsyncOperation<T> operation) {
        try {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            while(operation.Status==AsyncStatus.Started && clock.ElapsedMilliseconds<18000) Thread.Sleep(20);
            if(operation.Status==AsyncStatus.Started) { operation.Cancel(); throw new TimeoutException("O fone não respondeu. Feche o app QCY e tente novamente."); }
            return operation.GetResults();
        } finally { operation.Close(); }
    }
    public void Connect() {
        ulong address=0; bool scanning=true;
        using(var discovered=new ManualResetEvent(false)) {
            var watcher=new BluetoothLEAdvertisementWatcher { ScanningMode=BluetoothLEScanningMode.Active };
            TypedEventHandler<BluetoothLEAdvertisementWatcher,BluetoothLEAdvertisementReceivedEventArgs> handler=delegate(BluetoothLEAdvertisementWatcher s,BluetoothLEAdvertisementReceivedEventArgs a) {
                string name=a.Advertisement.LocalName ?? "";
                if(name.StartsWith("QCY H3 Pro",StringComparison.OrdinalIgnoreCase) && name.EndsWith("-APP",StringComparison.OrdinalIgnoreCase)) {
                    lock(gate) { if(scanning) { address=a.BluetoothAddress; discovered.Set(); } }
                }
            };
            watcher.Received+=handler;
            try { watcher.Start(); if(!discovered.WaitOne(12000)) throw new InvalidOperationException("H3 Pro não encontrado. Ligue o fone e feche o app QCY no celular."); }
            finally { lock(gate) scanning=false; watcher.Stop(); watcher.Received-=handler; }
        }
        device=Wait(BluetoothLEDevice.FromBluetoothAddressAsync(address));
        if(device==null) throw new InvalidOperationException("O Windows não conseguiu acessar o H3 Pro.");
        var services=Wait(device.GetGattServicesForUuidAsync(new Guid("0000a002-0000-1000-8000-00805f9b34fb"),BluetoothCacheMode.Uncached));
        if(services.Status!=GattCommunicationStatus.Success || services.Services.Count!=1) throw new InvalidOperationException("Serviço de controle indisponível.");
        service=services.Services[0];
        var chars=Wait(service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached));
        if(chars.Status!=GattCommunicationStatus.Success) throw new InvalidOperationException("Não foi possível consultar os controles.");
        writer=chars.Characteristics.FirstOrDefault(c=>c.Uuid==new Guid("00000001-0000-1000-8000-00805f9b34fb"));
        reader=chars.Characteristics.FirstOrDefault(c=>c.Uuid==new Guid("00000002-0000-1000-8000-00805f9b34fb"));
        if(writer==null || reader==null) throw new InvalidOperationException("Controles incompatíveis com este fone.");
        reader.ValueChanged+=OnValue; subscribed=true;
        if(Wait(reader.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify))!=GattCommunicationStatus.Success)
            throw new InvalidOperationException("Não foi possível receber o estado do fone.");
        Exchange(new byte[]{0xff,0xff,0xff,0xff,1},3);
        device.ConnectionStatusChanged+=OnConnectionStatus;
        statusSubscribed=true;
        OnConnectionStatus(device,null);
    }
    void OnConnectionStatus(BluetoothLEDevice sender,object args) {
        lock(gate) {
            if(!disposed && sender.ConnectionStatus!=BluetoothConnectionStatus.Connected) {
                connectionLost=true;
                received.Set();
            }
        }
    }
    void OnValue(GattCharacteristic sender,GattValueChangedEventArgs args) {
        byte[] bytes=new byte[args.CharacteristicValue.Length];
        using(var data=DataReader.FromBuffer(args.CharacteristicValue)) data.ReadBytes(bytes);
        lock(gate) {
            if(!disposed && bytes.Length>=10 && bytes[0]==0xfe && bytes[1]==0xdc && bytes[2]==0xba && bytes[3]==0 && bytes[4]==expectedOpcode && bytes[7]==0 && bytes[8]==expected && bytes[bytes.Length-1]==0xef) {
                reply=bytes; received.Set();
            }
        }
    }
    byte[] Exchange(byte[] body,byte opcode=0xff) {
        byte[] packet=new byte[9+body.Length];
        byte[] prefix={0xfe,0xdc,0xba,0xc0,opcode,0,(byte)(body.Length+1)};
        Array.Copy(prefix,packet,prefix.Length);
        lock(gate) { expected=unchecked((byte)Interlocked.Increment(ref nextSequence)); expectedOpcode=opcode; reply=null; received.Reset(); packet[7]=expected; }
        Array.Copy(body,0,packet,8,body.Length); packet[packet.Length-1]=0xef;
        using(var data=new DataWriter()) {
            data.WriteBytes(packet);
            if(Wait(writer.WriteValueAsync(data.DetachBuffer(),GattWriteOption.WriteWithoutResponse))!=GattCommunicationStatus.Success)
                throw new InvalidOperationException("Falha ao enviar o comando ao fone.");
        }
        if(!received.WaitOne(5000)) throw new TimeoutException("Sem confirmação do fone. Tente atualizar.");
        lock(gate) {
            if(connectionLost || reply==null) throw new InvalidOperationException("O H3 Pro foi desconectado. Ligue o fone e tente novamente.");
            return reply;
        }
    }
    public byte[] ReadMode() {
        byte[] result=Exchange(new byte[]{0xfe,1,0x17});
        if(result.Length!=15 || result[9]!=0x17 || result[10]!=3) throw new InvalidOperationException("Resposta de modo inesperada.");
        return new byte[]{result[11],result[12],result[13]};
    }
    public byte[] SetMode(byte[] state) {
        if(state==null || state.Length!=3 || state[0]<1 || state[0]>3) throw new ArgumentException("Modo inválido.");
        byte[] ack=Exchange(new byte[]{0x17,3,state[0],state[1],state[2]});
        if(ack.Length!=10) throw new InvalidOperationException("Comando não confirmado.");
        byte[] actual=null;
        for(int attempt=0;attempt<10;attempt++) {
            Thread.Sleep(500); actual=ReadMode();
            if(actual.SequenceEqual(state)) { Thread.Sleep(2500); return actual; }
        }
        throw new InvalidOperationException("Estado divergente: "+BitConverter.ToString(actual));
    }
    public QcyState ReadControls() {
        var reply=Exchange(new byte[]{0,0,0x1f,0x60},0xc1);
        return QcyState.ParseFields(reply.Skip(9).Take(reply.Length-10).ToArray());
    }
    public QcyState SetControl(byte key,int value) {
        if((key==0x0c && (value<0 || value>100)) || (key==5 && value!=1 && value!=2) || (key!=0x0c && key!=5)) throw new ArgumentException("Valor de controle inválido.");
        var ack=Exchange(new byte[]{2,key,(byte)value},0xc0);
        if(ack.Length!=11 || ack[9]!=0) throw new InvalidOperationException("O fone recusou a alteração.");
        for(int i=0;i<8;i++) {
            Thread.Sleep(250);
            var state=ReadControls();
            if((key==0x0c?state.Balance:(state.GameMode?2:1))==value) return state;
        }
        throw new InvalidOperationException("Alteração não confirmada pelo fone.");
    }
    public static string ControlsTest() {
        var lines=new List<string>();
        using(var q=new QcyConnection()) {
            q.Connect(); var original=q.ReadControls();
            lines.Add("Initial balance="+original.Balance+" game="+original.GameMode);
            try {
                foreach(int value in new[]{0,25,50,75,100,50}) {
                    var state=q.SetControl(0x0c,value);
                    if(state.GameMode!=original.GameMode) throw new Exception("Balance changed game mode");
                    lines.Add("PASS balance readback="+state.Balance);
                }
                foreach(int value in new[]{2,1}) {
                    var state=q.SetControl(5,value);
                    if(state.Balance!=50) throw new Exception("Game mode changed balance");
                    lines.Add("PASS game readback="+state.GameMode);
                }
            } finally {
                try { q.SetControl(0x0c,original.Balance); }
                finally { q.SetControl(5,original.GameMode?2:1); }
            }
            var restored=q.ReadControls();
            lines.Add("Restored balance="+restored.Balance+" game="+restored.GameMode);
        }
        return string.Join(Environment.NewLine,lines);
    }

    public void Dispose() {
        lock(gate) { if(disposed) return; disposed=true; received.Dispose(); }
        if(device!=null && statusSubscribed) device.ConnectionStatusChanged-=OnConnectionStatus;
        if(reader!=null && subscribed) reader.ValueChanged-=OnValue;
        if(service!=null) service.Dispose();
        if(device!=null) device.Dispose();
    }
    public static string HardwareTest() {
        var lines=new List<string>();
        using(var q=new QcyConnection()) {
            q.Connect(); byte[] original=q.ReadMode();
            lines.Add("Initial: "+BitConverter.ToString(original));
            try {
                foreach(var state in new[]{new byte[]{2,0,0},new byte[]{3,1,4},new byte[]{1,1,2}}) {
                    var actual=q.SetMode(state); lines.Add("PASS readback: "+ModeName(actual)+" "+BitConverter.ToString(actual)); Thread.Sleep(800);
                }
            } finally { q.SetMode(original); lines.Add("Restored: "+BitConverter.ToString(original)); }
        }
        using(var q=new QcyConnection()) { q.Connect(); lines.Add("PASS reconnect: "+ModeName(q.ReadMode())); }
        return string.Join(Environment.NewLine,lines);
    }
}

// Serializes all requests and retains a healthy GATT connection across menu opens.
public sealed class QcySession : IDisposable
{
    readonly object gate=new object();
    QcyConnection connection;
    bool disposed;
    public int ConnectionCount { get; private set; }
    public byte[] Execute(byte[] requested) { return Run(q=>requested==null?q.ReadMode():q.SetMode(requested)); }
    public QcyState Controls(byte[] mode,int? balance,bool? game) {
        return Run(delegate(QcyConnection q) {
            var actualMode=mode==null?q.ReadMode():q.SetMode(mode);
            if(balance.HasValue) q.SetControl(0x0c,balance.Value);
            if(game.HasValue) q.SetControl(5,game.Value?2:1);
            var state=q.ReadControls(); state.Mode=actualMode; return state;
        });
    }
    T Run<T>(Func<QcyConnection,T> operation) {
        lock(gate) {
            if(disposed) throw new ObjectDisposedException("QcySession");
            try {
                if(connection==null || !connection.IsConnected) {
                    Reset();
                    connection=new QcyConnection();
                    connection.Connect();
                    ConnectionCount++;
                }
                return operation(connection);
            } catch { Reset(); throw; }
        }
    }
    void Reset() { if(connection!=null) { connection.Dispose(); connection=null; } }
    public void Dispose() { lock(gate) { if(disposed) return; disposed=true; Reset(); } }
    public static string PersistenceTest() {
        using(var session=new QcySession()) {
            var lines=new List<string>();
            for(int i=0;i<4;i++) {
                lines.Add("Read "+(i+1)+": "+QcyConnection.ModeName(session.Execute(null)));
                if(i<3) Thread.Sleep(1500);
            }
            if(session.ConnectionCount!=1) throw new InvalidOperationException("Session unexpectedly reconnected: "+session.ConnectionCount);
            lines.Add("PASS: 4 queries, 1 connection; no mode changes.");
            session.Dispose();
            try { session.Execute(null); throw new InvalidOperationException("Disposed session accepted a query."); }
            catch(ObjectDisposedException) { lines.Add("PASS: closed session rejects queries."); }
            return string.Join(Environment.NewLine,lines);
        }
    }
}

