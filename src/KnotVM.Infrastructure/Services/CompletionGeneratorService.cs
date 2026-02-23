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
        _knot_completion() {
            local cur prev opts
            COMPREPLY=()
            cur="${COMP_WORDS[COMP_CWORD]}"
            prev="${COMP_WORDS[COMP_CWORD-1]}"

            # Comandi principali
            local commands="list list-remote install use remove rename run sync cache doctor version completion"

            # Completamento primo livello (comandi)
            if [ $COMP_CWORD -eq 1 ]; then
                COMPREPLY=( $(compgen -W "${commands}" -- ${cur}) )
                return 0
            fi

            # Completamento per comandi specifici
            case "${prev}" in
                use|remove|rename)
                    local aliases="{{aliasesStr}}"
                    COMPREPLY=( $(compgen -W "${aliases}" -- ${cur}) )
                    return 0
                    ;;
                install)
                    local versions="{{versionsStr}}"
                    COMPREPLY=( $(compgen -W "${versions}" -- ${cur}) )
                    return 0
                    ;;
                completion)
                    COMPREPLY=( $(compgen -W "bash zsh powershell fish" -- ${cur}) )
                    return 0
                    ;;
            esac
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

        return $$"""
        #compdef knot
        # Zsh completion for knot
        # Aggiungere a ~/.zshrc:
        #   fpath=(~/.zsh/completions $fpath)
        #   autoload -Uz compinit && compinit
        # Poi eseguire:
        #   knot completion zsh > ~/.zsh/completions/_knot
        _knot() {
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
                'doctor:Diagnostica installazione'
                'version:Mostra versione KnotVM'
                'completion:Genera script completamento'
            )

            local -a installed_aliases
            installed_aliases=({{aliasesZsh}})

            local -a popular_versions
            popular_versions=({{versionsZsh}})

            local -a shells
            shells=('bash:Bash (4.0+)' 'zsh:Zsh (5.0+)' 'powershell:PowerShell (5.1+)' 'fish:Fish (3.0+)')

            _arguments -C \
                '1: :->command' \
                '*::arg:->args'

            case $state in
                command)
                    _describe 'knot commands' commands
                    ;;
                args)
                    case $words[1] in
                        use|remove|rename)
                            _describe 'installed aliases' installed_aliases
                            ;;
                        install)
                            _describe 'node versions' popular_versions
                            ;;
                        completion)
                            _describe 'shells' shells
                            ;;
                    esac
                    ;;
            esac
        }

        _knot "$@"
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
            $"$knotCommands = @('list', 'list-remote', 'install', 'use', 'remove', 'rename', 'run', 'sync', 'cache', 'doctor', 'version', 'completion')",
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
            "    if ($prev -eq '--alias' -or $prev -eq '--to') {",
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
            "    # Completamento primo argomento del sottocomando",
            "    if ($count -eq 2 -or ($count -eq 3 -and $wordToComplete -ne '')) {",
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
            "        'install'     { @('--alias', '--latest', '--latest-lts', '--force') }",
            "        'use'         { @('--auto') }",
            "        'remove'      { @('--force') }",
            "        'rename'      { @('--from', '--to') }",
            "        'run'         { @('--with-version') }",
            "        'list'        { @('--path') }",
            "        'list-remote' { @('--lts', '--all', '--limit') }",
            "        'sync'        { @('--force') }",
            "        'cache'       { @('--list', '--clear', '--clean') }",
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

        var aliasCompletions = string.Join("\n",
            aliases.Select(a => $"complete -c knot -f -n '__fish_seen_subcommand_from use remove rename' -a '{a}'"));

        var versionCompletions = string.Join("\n",
            versions.Select(v => $"complete -c knot -f -n '__fish_seen_subcommand_from install' -a '{v}'"));

        return $$"""
        # Fish completion for knot
        # Installazione:
        #   knot completion fish > ~/.config/fish/completions/knot.fish

        # Disabilita completamento file per tutti i sottocomandi knot
        complete -c knot -f

        # Comandi principali
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'list' -d 'Mostra versioni Node.js installate'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'list-remote' -d 'Mostra versioni disponibili remote'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'install' -d 'Installa una versione Node.js'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'use' -d 'Cambia versione Node.js attiva'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'remove' -d 'Rimuovi versione installata'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'rename' -d 'Rinomina un alias'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'run' -d 'Esegui comando con versione specifica'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'sync' -d 'Sincronizza file proxy'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'cache' -d 'Gestisci cache download'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'doctor' -d 'Diagnostica installazione'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'version' -d 'Mostra versione KnotVM'
        complete -c knot -f -n 'not __fish_seen_subcommand_from list list-remote install use remove rename run sync cache doctor version completion' -a 'completion' -d 'Genera script di completamento shell'

        # Completamento shell per 'completion'
        complete -c knot -f -n '__fish_seen_subcommand_from completion' -a 'bash' -d 'Bash (4.0+)'
        complete -c knot -f -n '__fish_seen_subcommand_from completion' -a 'zsh' -d 'Zsh (5.0+)'
        complete -c knot -f -n '__fish_seen_subcommand_from completion' -a 'powershell' -d 'PowerShell (5.1+)'
        complete -c knot -f -n '__fish_seen_subcommand_from completion' -a 'fish' -d 'Fish (3.0+)'

        # Completamento alias installati per 'use', 'remove', 'rename'
        {{aliasCompletions}}

        # Completamento versioni per 'install'
        {{versionCompletions}}
        """;
    }
}
