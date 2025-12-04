# PlayBox para Unity

Sistema de controle físico para o Editor da Unity, projetado para desenvolvimento e testes em Realidade Estendida (XR).

---

## Índice

- [Introdução](#introdução)
- [Funcionalidades do sistema](#funcionalidades-do-sistema)
- [Versões da Unity suportadas](#versões-da-unity-suportadas)
- [Como criar / instalar](#como-criar--instalar)
  - [1. Montagem do hardware com Arduino Pro Micro](#1-montagem-do-hardware-com-arduino-pro-micro)
    - [1.1. Componentes](#11-componentes)
    - [1.2. Diagrama do circuito](#12-diagrama-do-circuito)
  - [2. Unity](#2-unity)
    - [2.1. Release](#21-release)
    - [2.2. Scripts C#](#22-scripts-c)
- [Como usar](#como-usar)
  - [1. Parear dispositivos Bluetooth no Windows](#1-parear-dispositivos-bluetooth-no-windows)
  - [2. Uso em projetos na Unity](#2-uso-em-projetos-na-unity)

---

## Introdução

O **PlayBox** é um sistema de **hardware + software** criado para agilizar o ciclo de testes em projetos de **Realidade Estendida (XR)** desenvolvidos com a **Unity**.

Em vez de tirar o headset (HMD) toda vez que você precisa clicar em **Play**, **Pause** ou **Restart** no Editor, o PlayBox permite acionar essas funções por meio de uma pequena caixa física com botões, baseada em um **Arduino Pro Micro** e **Bluetooth (HC-05 / HC-06)**.

Este repositório contém:

- Firmware para o **Arduino Pro Micro**;
- **Scripts C# para Unity** (Editor/runtime);
- Instruções para montagem do hardware e integração com a Unity.

---

## Funcionalidades do sistema

O PlayBox foi pensado para reduzir o atrito ao iterar e testar cenas em XR/VR. As principais funcionalidades são:

- **Controle do Editor da Unity via botões físicos**
  - Botão **Play/Stop** → entra e sai do *Play Mode*;
  - Botão **Restart** → reinicia a cena atual;
  - Botão **Pause** → pausa / retoma a execução do jogo.

- **Conexão sem fio via Bluetooth (HC-05 ou HC-06)**
  - Comunicação serial com o PC por meio de uma porta COM virtual;
  - Um serviço em C# na Unity que recebe e interpreta os comandos.

- **Janela personalizada no Editor**
  - Seleção da porta serial e baud rate;
  - Botões para **Connect / Disconnect / Reconnect**;
  - Exibição de status (desabilitado, conectando, conectado etc.).

- **Feedback visual no hardware**
  - LED aceso quando o sistema está ativo;
  - LED pisca quando um comando de botão é enviado.

O objetivo é permitir que você **controle o ciclo de execução** (Play / Restart / Pause) sem precisar interagir diretamente com a interface do Editor da Unity, mantendo o foco dentro da experiência XR/VR.

---

## Versões da Unity suportadas

O PlayBox foi desenvolvido e testado principalmente com:

- **Unity 2022.3 LTS**
- **Unity 6 / 6000.x** (testes limitados)

Em princípio, qualquer versão **Unity 2021.3+** com suporte a .NET Standard 2.x e Editor para Windows deve funcionar, mas nem todas as combinações foram exaustivamente testadas.

> **Recomendação:** use **Unity 2022.3 LTS** ou superior.

---

## Como criar / instalar

### 1. Montagem do hardware com Arduino Pro Micro

#### 1.1. Componentes

Lista básica de componentes:

- **1x Arduino Pro Micro** (5V)
- **1x módulo Bluetooth HC-05** (ou **HC-06**)
- **1x bateria 5V** (ou alimentação USB equivalente)
- **1x LED verde**
- **1x chave liga/desliga** (power switch)
- **3x botões do tipo push button** (botoeira)
- **3x resistores de 10kΩ** (para pull-down dos botões)
- **1x resistor de 330Ω** (para o LED)
- **1x caixa plástica** (case)
- **Fios** (jumpers ou fios rígidos/flexíveis)

Dica: prototipe tudo em uma protoboard antes de soldar na caixa definitiva.

#### 1.2. Diagrama do circuito

O diagrama elétrico do sistema está disponível em: [aqui](src/circuit-diagram/circuit_diagram.png).

<p align="center">
  <img src="src/circuit-diagram/circuit_diagram.png" alt="Diagrama do Circuito" width="400" />
</p>

**Mapeamento de pinos sugerido (ajuste de acordo com o firmware):**

- **Botões:**
  - `PIN 2` → botão **Play/Stop**
  - `PIN 3` → botão **Restart**
  - `PIN 15` → botão **Pause**

- **LED:**
  - `PIN 14` → LED do sistema  
    - LED em série com um **resistor de 330Ω** para GND.

- **Módulo Bluetooth (HC-05 / HC-06):**
  - `TX (Arduino Pro Micro)` → `RX (HC-05)`
  - `RX (Arduino Pro Micro)` → `TX (HC-05)`
  - `VCC (HC-05)` → 5V
  - `GND (HC-05)` → GND

- **Botões (exemplo de ligação):**
  - Um lado de cada botão → 5V  
  - Outro lado → pino digital (2, 3 ou 15) **e** resistor de 10kΩ para GND  
  - O resistor de 10kΩ atua como **pull-down**, mantendo o pino em nível LOW quando o botão não está pressionado.

---

### 2. Unity

> **⚠️ Importante:** para o PlayBox funcionar corretamente na Unity, é necessário alterar o **API Compatibility Level** do projeto:  
> Em `Edit > Project Settings > Player > Other Settings > Api Compatibility Level`, selecione **.NET Framework** em vez de **.NET Standard 2.1**.

#### 2.1. Release

1. Acesse a página de **Releases** deste repositório no GitHub.
2. Baixe o pacote desejado, por exemplo:
   - `PlayBoxUnity-1.0.1.unitypackage`  →  [Ir para Releases](https://github.com/CamiloJr/unity-playbox/releases).  
   ou  
   - Um `.zip` contendo a pasta `Assets/...`.
3. Na Unity:
   - Abra **Assets > Import Package > Custom Package...**
   - Selecione o `.unitypackage` baixado;
   - Importe todos os arquivos necessários.

#### 2.2. Scripts C#

Se preferir clonar ou baixar o repositório diretamente:

1. Faça o **clone** ou **download** deste repositório.
2. Localize a pasta que contém os scripts do PlayBox (por exemplo:  
   `Assets/PiXR/PlayBox/` ou estrutura semelhante).
3. Copie essa pasta para dentro da pasta `Assets` do seu projeto Unity.
4. Aguarde a Unity recompilar os scripts.

Depois disso, você deverá ter acesso a:

- Script de serviço principal (por exemplo, `PlayBoxService.cs`);
- Janela personalizada de Editor (por exemplo, `PlayBoxWindow.cs`);
- Quaisquer prefabs ou assets auxiliares necessários.

> Ajuste os nomes acima de acordo com a estrutura real do seu projeto.

---

## Como usar

### 1. Parear dispositivos Bluetooth no Windows

1. Ligue o PlayBox (bateria ou USB) com o módulo HC-05/HC-06 conectado.
2. Coloque o módulo em modo de pareamento, se necessário (depende da configuração atual).
3. No **Windows**:
   - Abra **Configurações > Dispositivos > Bluetooth e dispositivos**;
   - Ative o Bluetooth;
   - Procure pelo dispositivo (por exemplo, `HC-05`) e clique em **Emparelhar**.
4. Descubra qual **porta COM** foi atribuída:
   - Abra o **Gerenciador de Dispositivos**;
   - Vá em **Portas (COM & LPT)**;
   - Anote a porta COM (por exemplo, `COM4`, `COM9` etc.).

Essa porta será usada dentro da Unity para conectar ao PlayBox.

---

### 2. Uso em projetos na Unity

1. Abra o projeto Unity que já contém os scripts do PlayBox.
2. Certifique-se de que:
   - O hardware do PlayBox está ligado;
   - O módulo Bluetooth está emparelhado com o Windows;
   - Você sabe qual é a porta COM correspondente.

3. Na Unity, abra a janela do PlayBox (exemplo):
   - **Window > PiXR > PlayBox**  
     > Ajuste de acordo com o caminho real do menu no seu projeto.
     <p align="center">
        <img src="src/imgs/playbox-menu.png" alt="Menu do PlayBox" width="380" />
      </p>

4. Na janela do PlayBox:
   - Habilite o sistema (por exemplo, marcando **Enabled** ou similar);
   - Selecione a **porta COM** correta (por exemplo, `COM9`);
   - Defina o **baud rate** (por exemplo, `9600`);
   - Clique em **Connect**.

5. Quando o status indicar **Connected**, teste os botões:

   - **Botão Play/Stop**  
     - Entra e sai do *Play Mode* no Editor.

   - **Botão Restart**  
     - Reinicia a cena atual.

   - **Botão Pause**  
     - Pausa / retoma a execução do jogo.

6. Fluxo típico em XR/VR:

   - Prepare a cena e conecte o PlayBox;
   - Coloque o headset;
   - Quando estiver pronto, pressione o botão **Play** na caixa;
   - Use **Restart** para repetir o teste sem tirar o headset;
   - Use **Pause** se precisar congelar a simulação em um momento específico.


