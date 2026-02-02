# KnotVM

**Gestore versioni Node.js per Windows** - Riscrittura in C# di [node-local](https://github.com/mlelli/node-local)

## 🎯 Obiettivi

KnotVM è una riscrittura completa di node-local in C#/.NET, mantenendo la stessa filosofia:
- ✅ **Zero privilegi amministratore**
- ✅ **Filesystem = configurazione** (folder-based)
- ✅ **Isolamento perfetto** delle installazioni
- ✅ **Auto-sync** pacchetti globali
- ✅ **Cross-shell** (PowerShell, CMD, Git Bash)

## 🏗️ Architettura

```
KnotVM/
├── src/
│   ├── KnotVM.CLI/              # Entry point, comandi CLI
│   ├── KnotVM.Core/             # Domain models, interfacce
│   └── KnotVM.Infrastructure/   # Implementazioni (filesystem, network)
├── templates/                    # Template proxy (CMD, Bash)
└── tests/                        # Unit & integration tests
```

### Stack Tecnologico

- **.NET 8.0** (LTS, cross-platform ready)
- **Spectre.Console** - UI/TUI, progress bars, tabelle
- **System.CommandLine** - Parsing CLI robusto
- **Microsoft.Extensions.DependencyInjection** - IoC container

## 🚀 Stato Sviluppo

**Versione attuale:** 0.1.0-alpha (struttura iniziale)

### Fase 1: Setup & Core ✅
- [x] Solution .NET 8.0
- [x] Progetti CLI, Core, Infrastructure
- [x] Dipendenze NuGet (Spectre.Console, System.CommandLine)
- [x] Modelli base (Installation, Configuration)
- [x] Eccezioni custom
- [x] Template proxy da node-local

### Fase 2: Core Services (In corso)
- [ ] InstallationManager
- [ ] VersionManager
- [ ] ProxyManager
- [ ] DownloadService
- [ ] CacheService

### Fase 3: Comandi CLI
- [ ] `install` - Scarica e installa versioni
- [ ] `use` - Switch tra installazioni
- [ ] `list` - Mostra installazioni locali
- [ ] `list-remote` - Mostra versioni disponibili
- [ ] `remove` - Rimuove installazioni
- [ ] `sync` - Rigenera proxy

## 📚 Documentazione

- **Architettura:** Vedi `/docs/ARCHITECTURE.md` (TODO)
- **Migrazione da node-local:** Vedi `node-local/docs/CSHARP_MIGRATION.md`
- **API Reference:** Generata con DocFX (TODO)

## 🔧 Build e Test

```bash
# Build
dotnet build

# Test
dotnet test

# Run
dotnet run --project src/KnotVM.CLI

# Publish (single-file executable)
dotnet publish src/KnotVM.CLI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 🎓 Differenze vs node-local

| Aspetto | node-local (PowerShell) | KnotVM (C#) |
|---------|------------------------|-------------|
| **Performance** | Script interpretato | Compilato AOT |
| **Startup** | ~500ms | ~50ms (dopo build) |
| **Cross-platform** | Windows only | Windows + Linux + macOS (futuro) |
| **Dependency** | PowerShell 5.0+ | .NET 8.0 runtime |
| **Testabilità** | Pester (limitato) | xUnit + Moq (completo) |
| **Manutenibilità** | Script + classi PS | OOP full, SOLID |

## 📝 License

MIT License - Vedi [LICENSE](LICENSE)

## 👨‍💻 Autore

[mlelli](https://github.com/mlelli)

---

**Nota:** Progetto in sviluppo attivo. Per la versione stabile usa [node-local](https://github.com/mlelli/node-local).