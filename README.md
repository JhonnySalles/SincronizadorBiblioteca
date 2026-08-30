# 📚 SyncLib (Sincronizador de Biblioteca)

**SyncLib** é um aplicativo desktop moderno desenvolvido com **WinUI 3** e **.NET 9**, projetado para automatizar a organização da sua biblioteca de mídia. Ele monitora, processa e copia arquivos de vídeo, séries, animes e mangás de suas pastas de download diretamente para as pastas definitivas da sua biblioteca, garantindo padronização e organização.

---

## ✨ Funcionalidades Principais

* **🚀 Organização Automática de Mídias**
  Identifica e move Arquivos de Mídia (Filmes, Séries, Animes) e Mangás/Comics com base em padrões (Regex) inteligentes de nomenclatura, separando por temporada ou volume automaticamente.

* **🎨 Interface Moderna com WinUI 3**
  Interface de usuário construída utilizando os princípios do Fluent Design (Mica backdrop, cantos arredondados, modo escuro nativo) e o Windows App SDK.

* **⚙️ Configurações Customizáveis**
  * Configuração flexível de pastas de Origem e Destino para cada tipo de mídia.
  * Suporte à leitura de subpastas (recursividade).
  * Opção de adicionar Sufixos customizados às pastas de destino.

* **📊 Dashboard de Sincronização**
  * Listagem em tempo real de todos os arquivos pendentes encontrados na origem.
  * Cores indicativas de status (Pronto, Copiado, Erro).
  * Acompanhamento do progresso de cópia arquivo por arquivo.
  * Ações rápidas na grid: Excluir um arquivo da fila ou abrir diretamente a pasta destino do explorador de arquivos onde a mídia foi salva.

* **🗄️ Banco de Dados Local (SQLite + EF Core)**
  Utiliza um banco de dados local super leve para rastrear e memorizar os arquivos que já foram copiados. Isso impede que o mesmo arquivo seja transferido duplicado, mesmo que continue na pasta de origem!

* **📦 Arquitetura Unpackaged e Instalador MSI**
  O aplicativo roda livremente de restrições de sandbox (Unpackaged) permitindo livre acesso ao File System, e acompanha um pipeline completo para gerar instaladores `.msi` profissionais utilizando o **WiX Toolset v4**.

---

## 🛠️ Tecnologias Utilizadas

- **C# / .NET 9.0**: Backend sólido e de alta performance.
- **WinUI 3 / Windows App SDK**: Framework visual moderno e nativo do Windows 11.
- **Entity Framework Core & SQLite**: Persistência de estado local.
- **CommunityToolkit.Mvvm**: Arquitetura Model-View-ViewModel com geradores de código MVVM.
- **WiX Toolset v4**: Geração de instaladores (MSI) profissionais com integração direta ao MSBuild.

---

## 🚀 Como Compilar e Gerar a Release

O projeto inclui um script automatizado que executa a publicação contida (Self-Contained) do aplicativo e, em seguida, invoca o projeto do instalador para gerar o pacote MSI final.

Basta rodar no terminal ou dar duplo clique em:
```bat
build_release.bat
```

**O que o script faz:**
1. Roda o `dotnet publish` em modo `Release` focado em `win-x64` para dentro da pasta `Executavel`.
2. Compila o projeto `SyncLib.Installer.wixproj` (WiX 4) que coleta os binários do aplicativo.
3. Cria a pasta `Compilado` (se não existir).
4. Exporta o `SyncLibSetup.msi` finalizado, pronto para distribuição, para dentro de `Compilado\`.

---

## 📁 Estrutura do Projeto

* `SyncLib.App`: Projeto UI principal (WinUI 3). Responsável pelas telas (`MainWindow`, `DashboardPage`, `SettingsPage`) e ViewModels.
* `SyncLib.Core`: Camada de domínio contendo os modelos lógicos e contratos.
* `SyncLib.Infrastructure`: Implementações de banco de dados (`AppDbContext`), leitura de diretórios e extração Regex.
* `SyncLib.Installer`: Projeto WiX 4 contendo a lógica de empacotamento, registro de atalhos de menu iniciar e desktop para gerar o MSI.

---

*Desenvolvido para simplificar e automatizar a organização da sua biblioteca digital.*
