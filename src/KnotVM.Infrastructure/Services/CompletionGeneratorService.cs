using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione del generatore di script di completamento shell per KnotVM.
/// Supporta Bash, Zsh, PowerShell e Fish.
/// Le versioni remote popolari vengono cachate per 24 ore.
/// </summary>
public class CompletionGeneratorService(
    IInstallationsRepository installationsRepository,
    IPathService pathService,
    IFileSystemService fileSystem) : ICompletionGenerator
{
    private const string PopularVersionsCacheFileName = "completion-versions.txt";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private static readonly string[] DefaultPopularVersions =
        ["lts", "latest", "current", "18", "20", "22"];

    /// <inheritdoc/>
    public async Task<string> GenerateCompletionScriptAsync(ShellType shellType)
    {
        return shellType switch
        {
            ShellType.Bash => await GenerateBashCompletionAsync().ConfigureAwait(false),
            ShellType.Zsh => await GenerateZshCompletionAsync().ConfigureAwait(false),
            ShellType.PowerShell => await GeneratePowerShellCompletionAsync().ConfigureAwait(false),
            ShellType.Fish => await GenerateFishCompletionAsync().ConfigureAwait(false),
            _ => throw new KnotVMException(
                KnotErrorCode.InvalidArgument,
                $"Shell type '{shellType}' non supportato per il completamento")
        };
    }

    /// <inheritdoc/>
    public Task<IEnumerable<string>> GetInstalledAliasesAsync()
    {
        var aliases = installationsRepository
            .GetAll()
            .Select(i => i.Alias)
            .OrderBy(a => a);

        return Task.FromResult<IEnumerable<string>>(aliases);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<string>> GetPopularVersionsAsync()
    {
        var cacheFile = Path.Combine(pathService.GetCachePath(), PopularVersionsCacheFileName);

        if (fileSystem.FileExists(cacheFile))
        {
            var cacheAge = DateTime.UtcNow - fileSystem.GetFileLastWriteTime(cacheFile);
            if (cacheAge < CacheTtl)
            {
                var cached = fileSystem.ReadAllTextSafe(cacheFile);
                var versions = cached.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                return Task.FromResult<IEnumerable<string>>(versions);
            }
        }

        // Salva versioni di default nella cache
        fileSystem.WriteAllTextSafe(cacheFile, string.Join('\n', DefaultPopularVersions));

        return Task.FromResult<IEnumerable<string>>(DefaultPopularVersions);
    }

    private async Task<string> GenerateBashCompletionAsync()
    {
        var aliases = await GetInstalledAliasesAsync().ConfigureAwait(false);
        var versions = await GetPopularVersionsAsync().ConfigureAwait(false);

        var aliasesStr = string.Join(" ", aliases);
        var versionsStr = string.Join(" ", versions);

        return $$"""
        # Bash completion for knot
        # Aggiungere a ~/.bashrc:
        #   source <(knot completion bash)
        # oppure:
        #   knot completion bash > /etc/bash_completion.d/knot

        # Ritorna 0 (true) se è già stato fornito un argomento posizionale oltre il sottocomando.
        _knot_has_positional() {
            local i
            for ((i=2; i < COMP_CWORD; i++)); do
                [[ "${COMP_WORDS[i]}" != -* ]] && return 0
            done
            return 1
        }

        _knot_completion() {
            local cur prev sub
            COMPREPLY=()
            cur="${COMP_WORDS[COMP_CWORD]}"
            prev="${COMP_WORDS[COMP_CWORD-1]}"
            sub="${COMP_WORDS[1]}"

            local commands="list list-remote install use remove rename run sync cache auto-detect doctor version completion"

            # Completamento sottocomando (primo argomento)
            if [[ $COMP_CWORD -eq 1 ]]; then
                COMPREPLY=( $(compgen -W "${commands}" -- "${cur}") )
                return 0
            fi

            # Opzioni che richiedono un valore libero: sopprimi completamento filesystem
            case "${prev}" in
                --alias|--to|--directory)
                    COMPREPLY=()
                    return 0
                    ;;
                --from|--with-version)
                    local _a="{{aliasesStr}}"
                    COMPREPLY=( $(compgen -W "${_a}" -- "${cur}") )
                    return 0
                    ;;
                --limit)
                    COMPREPLY=()
                    return 0
                    ;;
            esac

            # Completamento contestuale per sottocomando
            case "${sub}" in
                install)
                    local opts="--alias --latest --latest-lts --from-file"
                    if [[ "${cur}" == -* ]] || _knot_has_positional; then
                        COMPREPLY=( $(compgen -W "${opts}" -- "${cur}") )
                    else
                        local _v="{{versionsStr}}"
                        COMPREPLY=( $(compgen -W "${_v} ${opts}" -- "${cur}") )
                    fi
                    ;;
                use)
                    local opts="--auto"
                    if [[ "${cur}" == -* ]] || _knot_has_positional; then
                        COMPREPLY=( $(compgen -W "${opts}" -- "${cur}") )
                    else
                        local _a="{{aliasesStr}}"
                        COMPREPLY=( $(compgen -W "${_a} ${opts}" -- "${cur}") )
                    fi
                    ;;
                remove)
                    local opts="--force"
                    if [[ "${cur}" == -* ]] || _knot_has_positional; then
                        COMPREPLY=( $(compgen -W "${opts}" -- "${cur}") )
                    else
                        local _a="{{aliasesStr}}"
                        COMPREPLY=( $(compgen -W "${_a} ${opts}" -- "${cur}") )
                    fi
                    ;;
                rename)
                    COMPREPLY=( $(compgen -W "--from --to" -- "${cur}") )
                    ;;
                run)
                    if [[ "${cur}" == -* ]] || _knot_has_positional; then
                        COMPREPLY=( $(compgen -W "--with-version --auto" -- "${cur}") )
                    fi
                    ;;
                sync)
                    COMPREPLY=( $(compgen -W "--force" -- "${cur}") )
                    ;;
                cache)
                    COMPREPLY=( $(compgen -W "--list --clear --clean" -- "${cur}") )
                    ;;
                list)
                    COMPREPLY=( $(compgen -W "--path -p" -- "${cur}") )
                    ;;
                list-remote)
                    COMPREPLY=( $(compgen -W "--lts --all --limit" -- "${cur}") )
                    ;;
                auto-detect)
                    COMPREPLY=( $(compgen -W "--directory" -- "${cur}") )
                    ;;
                completion)
                    COMPREPLY=( $(compgen -W "bash zsh powershell fish" -- "${cur}") )
                    ;;
            esac

            return 0
        }

        complete -F _knot_completion knot
        """;
    }

    private async Task<string> GenerateZshCompletionAsync()
    {
        var aliases = await GetInstalledAliasesAsync().ConfigureAwait(false);
        var versions = await GetPopularVersionsAsync().ConfigureAwait(false);

        var aliasesZsh = string.Join(" ", aliases.Select(a => $"'{a}'"));
        var versionsZsh = string.Join(" ", versions.Select(v => $"'{v}'"));
        var versionsFlat = string.Join(" ", versions);

        return $$"""
        #compdef knot
        # Zsh completion for knot
        #
        # Metodo 1 - source diretto in ~/.zshrc (richiede compinit già caricato):
        #   source <(knot completion zsh)
        #
        # Metodo 2 - file nel fpath (consigliato per installazioni pulite):
        #   fpath=(~/.zsh/completions $fpath)
        #   autoload -Uz compinit && compinit
        #   knot completion zsh > ~/.zsh/completions/_knot
        _knot() {
            local context state state_descr line
            typeset -A opt_args

            local -a commands
            commands=(
                'list:Mostra versioni Node.js installate'
                'list-remote:Mostra versioni disponibili remote'
                'install:Installa una versione Node.js'
                'use:Cambia versione Node.js attiva'
                'remove:Rimuovi una versione installata'
                'rename:Rinomina un alias'
                'run:Esegui comando con versione specifica'
                'sync:Sincronizza file proxy'
                'cache:Gestisci cache download'
                'auto-detect:Rileva versione Node.js dal progetto'
                'doctor:Diagnostica installazione'
                'version:Mostra versione KnotVM'
                'completion:Genera script di completamento shell'
            )

            local -a installed_aliases
            installed_aliases=({{aliasesZsh}})

            local -a popular_versions
            popular_versions=({{versionsZsh}})

            _arguments -C \
                '1: :->command' \
                '*::arg:->args'

            case $state in
                command)
                    _describe 'knot commands' commands
                    ;;
                args)
                    case $words[1] in
                        install)
                            _arguments \
                                '1:versione Node.js:({{versionsFlat}})' \
                                "--alias[Alias per l'installazione]:nome alias:" \
                                '--latest[Installa la versione latest]' \
                                '--latest-lts[Installa la versione LTS più recente]' \
                                '--from-file[Leggi versione da .node-version / .nvmrc]'
                            ;;
                        use)
                            _arguments \
                                "1:alias installato:(${installed_aliases[*]})" \
                                '--auto[Rileva versione automaticamente]'
                            ;;
                        remove)
                            _arguments \
                                "1:alias installato:(${installed_aliases[*]})" \
                                '--force[Forza rimozione]'
                            ;;
                        rename)
                            _arguments \
                                "--from[Alias sorgente]:alias:(${installed_aliases[*]})" \
                                '--to[Nuovo nome alias]:nuovo nome:'
                            ;;
                        run)
                            _arguments \
                                '1:comando da eseguire:' \
                                "--with-version[Versione da usare]:alias:(${installed_aliases[*]})" \
                                '--auto[Rileva versione automaticamente]'
                            ;;
                        list)
                            _arguments \
                                '--path[Mostra percorso installazione]' \
                                '-p[Mostra percorso installazione]'
                            ;;
                        list-remote)
                            _arguments \
                                '--lts[Mostra solo versioni LTS]' \
                                '--all[Mostra tutte le versioni]' \
                                '--limit[Numero massimo di risultati]:numero:'
                            ;;
                        sync)
                            _arguments '--force[Forza sincronizzazione proxy]'
                            ;;
                        cache)
                            _arguments \
                                '--list[Elenca contenuto cache]' \
                                '--clear[Pulisci tutta la cache]' \
                                '--clean[Rimuovi voci orfane]'
                            ;;
                        auto-detect)
                            _arguments '--directory[Directory del progetto]:directory:_directories'
                            ;;
                        completion)
                            _arguments '1:shell:(bash zsh powershell fish)'
                            ;;
                    esac
                    ;;
            esac
        }

        # --- Registrazione ---
        # 'compdef' registra _knot quando il file viene source-ato direttamente
        # (es. source <(knot completion zsh) in ~/.zshrc).
        # Il tag '#compdef knot' in cima gestisce invece il caricamento via fpath/autoload.
        # NON richiamare _knot qui: _arguments è disponibile solo durante un evento
        # di completamento, non al momento del source.
        if (( $+functions[compdef] )); then
            compdef _knot knot
        else
            # compinit non ancora caricato: ritarda la registrazione al primo compinit
            autoload -Uz add-zsh-hook 2>/dev/null
            function _knot_register_deferred() {
                compdef _knot knot
                add-zsh-hook -d precmd _knot_register_deferred
            }
            add-zsh-hook precmd _knot_register_deferred 2>/dev/null || compdef _knot knot 2>/dev/null || true
        fi
        """;
    }

    private async Task<string> GeneratePowerShellCompletionAsync()
    {
        var aliases = await GetInstalledAliasesAsync().ConfigureAwait(false);
        var versions = await GetPopularVersionsAsync().ConfigureAwait(false);

        var aliasesPs = string.Join(", ", aliases.Select(a => $"'{a}'"));
        var versionsPs = string.Join(", ", versions.Select(v => $"'{v}'"));

        var lines = new[]
        {
            "# PowerShell completion for knot",
            "# Aggiungere al proprio profilo PowerShell ($PROFILE):",
            "#   knot completion powershell >> $PROFILE",
            $"$knotCommands = @('list', 'list-remote', 'install', 'use', 'remove', 'rename', 'run', 'sync', 'cache', 'auto-detect', 'doctor', 'version', 'completion')",
            $"$knotAliases  = @({aliasesPs})",
            $"$knotVersions = @({versionsPs})",
            "$knotShells   = @('bash', 'zsh', 'powershell', 'fish')",
            "",
            "Register-ArgumentCompleter -Native -CommandName knot -ScriptBlock {",
            "    param($wordToComplete, $commandAst, $cursorPosition)",
            "    $elements = $commandAst.CommandElements",
            "    $count    = $elements.Count",
            "    # Completamento sottocomando (primo argomento)",
            "    if ($count -le 1 -or ($count -eq 2 -and $wordToComplete -ne '')) {",
            "        $knotCommands | Where-Object { $_ -like \"$wordToComplete*\" } | ForEach-Object {",
            "            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)",
            "        }",
            "        return",
            "    }",
            "    $sub  = $elements[1].ToString()",
            "    # Individua il token precedente a quello che si sta digitando",
            "    $prev = if ($count -ge 2) { $elements[$count - 1].ToString() } else { '' }",
            "    # Opzioni che accettano un valore libero (nuovo nome alias/destinazione).",
            "    # NOTA: non fare 'return' vuoto - PS farebbe FS-fallback se il result-set e' vuoto.",
            "    # Restituire un placeholder mantiene il controllo senza inquinare con file di sistema.",
            "    if ($prev -eq '--alias' -or $prev -eq '--to' -or $prev -eq '--directory') {",
            "        $val = if ($wordToComplete) { $wordToComplete } else { '<name>' }",
            "        [System.Management.Automation.CompletionResult]::new($val, $val, 'ParameterValue', \"Custom name for $prev\")",
            "        return",
            "    }",
            "    # Opzioni che accettano un alias installato (--from per rename, --with-version per run)",
            "    if ($prev -eq '--from' -or $prev -eq '--with-version') {",
            "        $matching = @($knotAliases | Where-Object { $_ -like \"$wordToComplete*\" })",
            "        if ($matching.Count -gt 0) {",
            "            $matching | ForEach-Object { [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', \"Alias: $_\") }",
            "        } else {",
            "            $val = if ($wordToComplete) { $wordToComplete } else { '<alias>' }",
            "            [System.Management.Automation.CompletionResult]::new($val, $val, 'ParameterValue', 'Installed alias name')",
            "        }",
            "        return",
            "    }",
            "    # Completamento primo argomento del sottocomando (salta se si sta digitando un'opzione)",
            "    if (($count -eq 2 -or ($count -eq 3 -and $wordToComplete -ne '')) -and $wordToComplete -notlike '-*') {",
            "        if (@('use','remove','rename') -contains $sub) {",
            "            $knotAliases | Where-Object { $_ -like \"$wordToComplete*\" } | ForEach-Object {",
            "                [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', \"Alias: $_\")",
            "            }",
            "            return",
            "        }",
            "        if ($sub -eq 'install') {",
            "            $knotVersions | Where-Object { $_ -like \"$wordToComplete*\" } | ForEach-Object {",
            "                [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', \"Version: $_\")",
            "            }",
            "            return",
            "        }",
            "        if ($sub -eq 'completion') {",
            "            $knotShells | Where-Object { $_ -like \"$wordToComplete*\" } | ForEach-Object {",
            "                [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', \"Shell: $_\")",
            "            }",
            "            return",
            "        }",
            "    }",
            "    # Completamento opzioni per sottocomandi noti (evita fallback sul filesystem)",
            "    $opts = switch ($sub) {",
            "        'install'     { @('--alias', '--latest', '--latest-lts', '--from-file') }",
            "        'use'         { @('--auto') }",
            "        'remove'      { @('--force') }",
            "        'rename'      { @('--from', '--to') }",
            "        'run'         { @('--with-version', '--auto') }",
            "        'list'        { @('--path', '-p') }",
            "        'list-remote' { @('--lts', '--all', '--limit') }",
            "        'sync'        { @('--force') }",
            "        'cache'       { @('--list', '--clear', '--clean') }",
            "        'auto-detect' { @('--directory') }",
            "        default       { @() }",
            "    }",
            "    if ($opts.Count -gt 0) {",
            "        $opts | Where-Object { $_ -like \"$wordToComplete*\" } | ForEach-Object {",
            "            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)",
            "        }",
            "    }",
            "}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private async Task<string> GenerateFishCompletionAsync()
    {
        var aliases = await GetInstalledAliasesAsync().ConfigureAwait(false);
        var versions = await GetPopularVersionsAsync().ConfigureAwait(false);

        var aliasesStr = string.Join(" ", aliases);

        var aliasCompletions = string.Join("\n",
            aliases.Select(a => $"complete -c knot -f -n '__fish_seen_subcommand_from use remove rename' -a '{a}'"));

        var versionCompletions = string.Join("\n",
            versions.Select(v => $"complete -c knot -f -n '__fish_seen_subcommand_from install' -a '{v}'"));

        var aliasOptionCompletions = string.Join("\n",
            aliases.Select(a => $"complete -c knot -f -n '__fish_seen_subcommand_from rename run' -a '{a}'"));

        const string allSubcmds = "list list-remote install use remove rename run sync cache auto-detect doctor version completion";

        return $$"""
        # Fish completion for knot
        # Installazione:
        #   knot completion fish > ~/.config/fish/completions/knot.fish

        # Disabilita completamento file per tutti i sottocomandi knot
        complete -c knot -f

        # Comandi principali
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'list'        -d 'Mostra versioni Node.js installate'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'list-remote' -d 'Mostra versioni disponibili remote'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'install'     -d 'Installa una versione Node.js'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'use'         -d 'Cambia versione Node.js attiva'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'remove'      -d 'Rimuovi versione installata'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'rename'      -d 'Rinomina un alias'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'run'         -d 'Esegui comando con versione specifica'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'sync'        -d 'Sincronizza file proxy'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'cache'       -d 'Gestisci cache download'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'auto-detect' -d 'Rileva versione Node.js dal progetto'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'doctor'      -d 'Diagnostica installazione'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'version'     -d 'Mostra versione KnotVM'
        complete -c knot -f -n 'not __fish_seen_subcommand_from {{allSubcmds}}' -a 'completion'  -d 'Genera script di completamento shell'

        # Opzioni per 'install'
        complete -c knot -f -n '__fish_seen_subcommand_from install' -l alias       -r -d 'Alias per l'\''installazione'
        complete -c knot -f -n '__fish_seen_subcommand_from install' -l latest         -d 'Installa la versione latest'
        complete -c knot -f -n '__fish_seen_subcommand_from install' -l latest-lts     -d 'Installa la versione LTS più recente'
        complete -c knot -f -n '__fish_seen_subcommand_from install' -l from-file      -d 'Leggi versione da .node-version / .nvmrc'

        # Opzioni per 'use'
        complete -c knot -f -n '__fish_seen_subcommand_from use' -l auto -d 'Rileva versione automaticamente'

        # Opzioni per 'remove'
        complete -c knot -f -n '__fish_seen_subcommand_from remove' -l force -d 'Forza rimozione'

        # Opzioni per 'rename': --from con alias installati, --to testo libero
        complete -c knot -f -n '__fish_seen_subcommand_from rename' -l from -r -a '{{aliasesStr}}' -d 'Alias sorgente'
        complete -c knot -f -n '__fish_seen_subcommand_from rename' -l to   -r                      -d 'Nuovo nome alias'

        # Opzioni per 'run': --with-version con alias installati
        complete -c knot -f -n '__fish_seen_subcommand_from run' -l with-version -r -a '{{aliasesStr}}' -d 'Versione da usare'
        complete -c knot -f -n '__fish_seen_subcommand_from run' -l auto            -d 'Rileva versione automaticamente'

        # Opzioni per 'list'
        complete -c knot -f -n '__fish_seen_subcommand_from list' -l path -s p -d 'Mostra percorso installazione'

        # Opzioni per 'list-remote'
        complete -c knot -f -n '__fish_seen_subcommand_from list-remote' -l lts           -d 'Mostra solo versioni LTS'
        complete -c knot -f -n '__fish_seen_subcommand_from list-remote' -l all           -d 'Mostra tutte le versioni'
        complete -c knot -f -n '__fish_seen_subcommand_from list-remote' -l limit      -r -d 'Numero massimo di risultati'

        # Opzioni per 'sync'
        complete -c knot -f -n '__fish_seen_subcommand_from sync' -l force -d 'Forza sincronizzazione proxy'

        # Opzioni per 'cache'
        complete -c knot -f -n '__fish_seen_subcommand_from cache' -l list  -d 'Elenca contenuto cache'
        complete -c knot -f -n '__fish_seen_subcommand_from cache' -l clear -d 'Pulisci tutta la cache'
        complete -c knot -f -n '__fish_seen_subcommand_from cache' -l clean -d 'Rimuovi voci orfane'

        # Opzioni per 'auto-detect'
        complete -c knot -f -n '__fish_seen_subcommand_from auto-detect' -l directory -r -d 'Directory del progetto'

        # Completamento shell per 'completion'
        complete -c knot -f -n '__fish_seen_subcommand_from completion' -a 'bash'       -d 'Bash (4.0+)'
        complete -c knot -f -n '__fish_seen_subcommand_from completion' -a 'zsh'        -d 'Zsh (5.0+)'
        complete -c knot -f -n '__fish_seen_subcommand_from completion' -a 'powershell' -d 'PowerShell (5.1+)'
        complete -c knot -f -n '__fish_seen_subcommand_from completion' -a 'fish'       -d 'Fish (3.0+)'

        # Completamento alias installati per 'use', 'remove', 'rename' (posizionale)
        {{aliasCompletions}}

        # Completamento versioni per 'install' (posizionale)
        {{versionCompletions}}
        """;
    }
}
