# QCY Controls

Aplicativo independente para controlar o QCY H3 Pro pela bandeja do Windows.

## Recursos

- ANC ligado, desligado e som ambiente.
- Balanço esquerdo/direito e Game mode.
- Confirmação dos ajustes pela leitura de retorno do fone.
- Sessão Bluetooth LE persistente, com reconexão após falhas.

Clique no ícone de fone na bandeja para abrir o painel. **×** ou **Esc** recolhe o painel; botão direito → **Sair** encerra o aplicativo. O Windows determina se o ícone aparece diretamente ou nos ícones ocultos.

## Requisitos

Windows 10/11 com .NET Framework 4.8, Bluetooth LE e um QCY H3 Pro ligado. Feche o app QCY no celular para liberar o canal de controle. Outros modelos não foram implementados. Não depende do Codex, Python ou do widget.

## Compilar e executar

No PowerShell, dentro desta pasta:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-qcy.ps1
& '.\QCY Controls.exe'
```

A compilação usa o compilador C# e as bibliotecas do Windows. O executável é gerado nesta pasta e não é versionado. Encerre o aplicativo pelo menu da bandeja antes de recompilar.

Para criar atalhos na área de trabalho e no menu Iniciar e abrir o aplicativo:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-qcy.ps1
```

Os atalhos apontam para esta pasta. A inicialização com o Windows não é ativada automaticamente.

## Verificação

Após compilar:

```powershell
powershell -ExecutionPolicy Bypass -File .\qcy-tests.ps1
```

Esse teste valida respostas capturadas, campos desconhecidos, limites e mensagens inválidas, sem acessar o fone.

Diagnósticos opcionais de hardware:

```powershell
& '.\QCY Controls.exe' --qcy-session-test session.txt
& '.\QCY Controls.exe' --qcy-test modes.txt
& '.\QCY Controls.exe' --qcy-controls-test controls.txt
```

O primeiro verifica a persistência da conexão sem alterar o modo. Os outros alteram temporariamente modos ou controles e tentam restaurar os ajustes originais ao terminar; execute apenas durante um teste deliberado.

## Estrutura

- `QcyTray.cs`: aplicativo, ícone e ciclo de vida.
- `QcyWindow.xaml` e `QcyPanel.cs`: interface e ações dos controles.
- `QcyControls.cs`: protocolo Bluetooth LE, validação e sessão.

O protocolo foi derivado de uma captura do H3 Pro; não depende do OpenQCY. Capturas privadas e artefatos de diagnóstico não fazem parte do repositório. ANC utiliza o perfil já implementado para esse aparelho; EQ, bateria e outros perfis não estão disponíveis.
