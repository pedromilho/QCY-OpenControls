using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public sealed partial class QcyTray
{
    bool english, persistLanguage=true;
    string trayStatus="Controles";
    readonly Dictionary<TextBlock,string> originalText=new Dictionary<TextBlock,string>();
    readonly Dictionary<DependencyObject,string> labels=new Dictionary<DependencyObject,string>();
    readonly Dictionary<FrameworkElement,string> tips=new Dictionary<FrameworkElement,string>();
    readonly Dictionary<Button,string> buttons=new Dictionary<Button,string>();
    readonly string languageFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QCY OpenControls","language.txt");
    static readonly Dictionary<string,string> translations=new Dictionary<string,string> {
        {"Controles","Controls"},{"Indisponível","Unavailable"},{"Abrir controles","Open controls"},{"Sair","Exit"},
        {"Idioma","Language"},{"Recolher para a bandeja","Minimize to tray"},{"Balanço L/R","L/R balance"},
        {"Balanço esquerdo e direito","Left/right balance"},{"Centro","Center"},{"Centralizar","Center"},{"Atualizar","Refresh"},
        {"Conectado","Connected"},{"Atualizando estado…","Refreshing status…"},{"Conectando ao H3 Pro…","Connecting to H3 Pro…"},
        {"Aplicando e confirmando…","Applying and confirming…"},
        {"ANC desligado","ANC off"},{"ANC ligado","ANC on"},{"Som ambiente","Ambient sound"},{"Modo jogo","Game mode"},
        {"Resposta de controles incompleta.","Incomplete control response."},{"Formato de controle inesperado.","Unexpected control format."},
        {"Balanço ou Game mode não informado pelo fone.","Headphones did not report balance or game mode."},
        {"O fone não respondeu. Feche o app QCY e tente novamente.","Headphones did not respond. Close the QCY app and try again."},
        {"H3 Pro não encontrado. Ligue o fone e feche o app QCY no celular.","H3 Pro not found. Turn it on and close the QCY mobile app."},
        {"O Windows não conseguiu acessar o H3 Pro.","Windows could not access the H3 Pro."},
        {"Serviço de controle indisponível.","Control service unavailable."},{"Não foi possível consultar os controles.","Could not read controls."},
        {"Controles incompatíveis com este fone.","Controls are incompatible with these headphones."},
        {"Não foi possível receber o estado do fone.","Could not receive headphone status."},
        {"Falha ao enviar o comando ao fone.","Failed to send the command to the headphones."},
        {"Sem confirmação do fone. Tente atualizar.","No confirmation from the headphones. Try refreshing."},
        {"O H3 Pro foi desconectado. Ligue o fone e tente novamente.","H3 Pro disconnected. Turn it on and try again."},
        {"Resposta de modo inesperada.","Unexpected mode response."},{"Modo inválido.","Invalid mode."},
        {"Comando não confirmado.","Command not confirmed."},{"Valor de controle inválido.","Invalid control value."},
        {"O fone recusou a alteração.","Headphones rejected the change."},{"Alteração não confirmada pelo fone.","Change not confirmed by the headphones."}
    };
    string Translate(string text) {
        // Normalize the original English mode names to their Portuguese labels.
        if(text=="ANC off") text="ANC desligado";
        if(text=="ANC on") text="ANC ligado";
        if(text=="Ambient sound") text="Som ambiente";
        if(text=="Game mode") text="Modo jogo";
        string translated;
        if(english && translations.TryGetValue(text,out translated)) return translated;
        if(english && text.StartsWith("Estado divergente: ")) return "Unexpected state: "+text.Substring(19);
        return text;
    }
    void Remember(DependencyObject node) {
        var block=node as TextBlock;
        if(block!=null && !originalText.ContainsKey(block)) originalText[block]=block.Text;
        var button=node as Button;
        if(button!=null && button.Content is string) buttons[button]=(string)button.Content;
        var element=node as FrameworkElement;
        if(element!=null && element.ToolTip is string) tips[element]=(string)element.ToolTip;
        var label=AutomationProperties.GetName(node);
        if(!String.IsNullOrEmpty(label)) labels[node]=label;
        foreach(var child in LogicalTreeHelper.GetChildren(node)) {
            var dependency=child as DependencyObject;
            if(dependency!=null) Remember(dependency);
        }
    }
    void UpdateTrayText() { tray.Text="QCY OpenControls · "+Translate(trayStatus); }
    void ApplyLanguage() {
        foreach(var pair in originalText) pair.Key.Text=Translate(pair.Value);
        foreach(var pair in buttons) pair.Key.Content=Translate(pair.Value);
        foreach(var pair in tips) pair.Key.ToolTip=Translate(pair.Value);
        foreach(var pair in labels) AutomationProperties.SetName(pair.Key,Translate(pair.Value));
        tray.ContextMenuStrip.Items[0].Text=Translate("Abrir controles");
        tray.ContextMenuStrip.Items[1].Text=Translate("Sair");
        UpdateTrayText();
    }
    void SetupLanguage() {
        Remember(window);
        try { english=File.Exists(languageFile) && File.ReadAllText(languageFile).Trim()=="EN"; } catch(IOException) {} catch(UnauthorizedAccessException) {}
        var picker=Get<ComboBox>("LanguagePicker");
        picker.SelectedIndex=english?1:0;
        picker.SelectionChanged+=delegate {
            english=picker.SelectedIndex==1; ApplyLanguage();
            if(!persistLanguage) return;
            try { Directory.CreateDirectory(Path.GetDirectoryName(languageFile)); File.WriteAllText(languageFile,english?"EN":"PT-BR"); }
            catch(IOException) {} catch(UnauthorizedAccessException) {}
        };
        ApplyLanguage();
    }
}
