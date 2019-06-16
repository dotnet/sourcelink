> Copyright (c) Microsoft.  All Rights Reserved.  This document is licensed under the GNU GENERAL PUBLIC LICENSE, Version 2.0.  See [License.txt](License.txt) in the containing directory for license information.

# Git documentation addendum

This document clarifies git behavior that's not clearly captured in the current git [documentation](https://git-scm.com).

## General concepts

_Whitespace_ character is one of ` `, `\t`, `\n`, `\f`, `\r`, `\v`.

## Locating repository containing a path

Spec: https://git-scm.com/docs/gitrepository-layout

_Common directory_ contains at least `objects` and `refs` subdirectories.

_Git directory_ is a directory that contains `HEAD` file and is either a _common directory_, 
or contains `commondir` file that contains a path (relative to the _git directory_) to the _common directory_.

When searching for a repository given an initial path `/a/b` the folowing paths are considered:
 - `/a/b/.git`
 - `/a/b/`
 - `/a/.git`
 - `/a`
 - `/.git`
 - `/`
 
Only paths on the same device as the initial path are considered.

Before the search is performed the initial path is normalized. On Windows `\` directory separators are converted to `/`.
The resulting paths (e.g. _git directory_, _common directory_, etc.) also have separators normalized to `/`.

The search finds the first path on the above list that is either
 1) a path to a valid _git directory_, or
 2) a path to a `.git` file. The content of the file must start with `gitdir:` prefix followed by a path
    that is a valid _git directory_, otherwise the search fails. On Windows any `\` characters in the path 
    are replaced with `/`. A relative path is considered relative to the directory containing the `.git` file.
    
    _libgit2_: The trailing _whitespace_ characters are trimmed from the path. _git_ doesn't trim any whitespace.

If the search does not find any matching path the search fails.

Repository is a [_linked working tree_](https://git-scm.com/docs/git-worktree#_description) if its _git directory_ 
is different from its _common directory_ and `gitdir` file is present in the _git directory_. 
The file contains an absolute path to the _working directory_ path. The trailing _whitespace_ characters are trimmed from the path.

For other repositories, the _working directory_ is specified in `core.worktree` configuration entry, if present.
Otherwise it's the parent directory of the path found by the search (i.e. directory containing _git directory_ or `.git` file).

Repository is considered _bare_ if the configuration entry `core.bare` is _true_ and the repository is not a _linked working tree_.

## Directories

### Windows git install directory

libgit2 finds the  first of the following directories:

Find directory on `%PATH%` that contains `git.exe`. If it exists, the parent directory is the install directory (the files are expected to be under `bin` or `cmd` subdirectory).

Find directory on `%PATH%` that contains `git.cmd`. If it exists, the parent directory is the install directory (the files are expected to be under `bin` or `cmd` subdirectory).

If the registry value `InstallLocation` under key `HKEY_LOCAL_MACHINE:SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1` exists it is the install directory.


### System directories

#### Windows

`{git-install-directory}\etc`

> git actually uses a subdirectory `{git-install-directory}\mingw64\etc` of the install directory.

#### Unix 

`/etc`

### Global directories

#### Windows

`%HOME%`, `%HOMEDRIVE%%HOMEPATH%`, `%USERPROFILE%`

#### Unix

`$HOME` or directory containin password file

### XDG directories

#### Windows
  First that exists: `%XDG_CONFIG_HOME%\git`, `%APPDATA%\git`, `%LOCALAPPDATA%\git`, `%HOME%\.config\git`, `%HOMEDRIVE%%HOMEPATH%\.config\git`, `%USERPROFILE%\.config\git`

> git doesn't seem to use `%APPDATA%\git` and `%LOCALAPPDATA%\git`

#### Unix 

If real and effective user ids of the current process are the same:
- if `$XDG_CONFIG_HOME` is set then `$XDG_CONFIG_HOME/git` otherwise, if `$HOME` is set then `$HOME/.config/git`, otherwise error.
- Otherwise directory containing password file `.config/git`.

### Program Data directory

#### Windows

`%PROGRAMDATA%\Git`

#### Unix 

N/A.

## Loading configuration from files

Configuration files: https://git-scm.com/docs/git-config#FILES
  
| Configuration | File location                                             |
|---------------|-----------------------------------------------------------|
| local         | `config` file in _common directory_                       |
| global        | `.gitconfig` file in _global directories_                 |
| XDG           | `config` file in _XDG directories_                        |
| system        | `gitconfig` file in _system directories_                  |
| programdata   | `config` file in _Program Data directory_ (Windows only)  |
    
## Configuration file format

Spec: https://git-scm.com/docs/git-config#_configuration_file

### Parsing details

File may start with an optional BOM.

Optional _whitespace_ before section start `[`. 

Section and subsection names can be separated by multiple _whitespace_ characters.

No _whitespace_ allowed between `"` terminating subsection name and `]`.

Variables outside of a section are allowed (can't be queried via `git config --get` but appear in `git config --list`).

If fully qualified variable name already exists it's considered multi-valued.

### Includes

Recursive includes stop at depth 10.

#### Conditional includes

If the path starts with `./` (`./` or `.\` on Windows) then join the directory containing the current configuration file with the path.
If the path starts with `~/` (`~\` on Windows) then expand tilde to home directory.
If the path is not absolute, i.e. does not start with `/` (`[a-zA-Z]:[/\]` on Windows), join `**` with the path.

If the path ends with a directory separator then append `**` to the path.

Match the path pattern against the _git directory_ of the repository using `fnmatch` with no flags other than case sensitivity
(no `FNM_PATHNAME` or `FNM_LEADING_DIR`).

> libgit2 [bug](https://github.com/libgit2/libgit2/issues/5068): libgit2 uses `FNM_PATHNAME|FNM_LEADING_DIR` `fnmatch` flags, while git does not.

The _git directory_ path is matched it its normalized form (`/` directory separators), but the pattern specified in the condition is not.
Therefore the pattern must use `/` for directory separators.

## .gitignore files

Spec: https://git-scm.com/docs/gitignore#_description

File may start with an optional BOM.

Order:
- Default patterns: `.`, `..`, `.git`
- inner-most `.gitignore` file (in the directory containing the file being checked)
  ..
- outer-most `.gitignore` file (search stops at _working directory_)
- `info/exclude` file under _common directory_
- `core.excludesFile` in the configuration

### Patterns

Spec: https://git-scm.com/docs/gitignore#_pattern_format

An empty line or line containing _whitespace_ only are considered blank lines and ignored.
Line starting with `#` character (preceded by any number of _whitespace_ characters) is also ignored.

Processing pattern:
  - Pattern starting with `!` is a _negative pattern_. Remove leading `!` character.
  - Trim trailing ` ` and `\t` characters.
  - Pattern ending with `/` is a _directory pattern_. Remove trailing `/` character. 
  - Pattern containing `/` after trailing `/` is removed is _full path pattern_. 
  - Remove leading `/` character.

### Matching

The input path must use `/` directory separators, `\` in the path is not considered a directory separator even on Windows.

If the input path is relative, combine with _working directory_ base. Trim trailing `/`. 
The path is considered a _directory path_ if the input path ends with `/` or the path exists on disk and it's a directory.

Pattern specified in a `.gitignore` file can only match path under the directory containing the `.gitignore` file.

If path is not a _directory path_ and the pattern is a _directory patterns_ it doesn't match.

Match is case-insensitive iff `core.ignorecase` is _true_.

If the pattern is _full path pattern_:
- match against the path under the directory containing the `.gitignore` file 
  (e.g. if the input path is `a/b/c` and pattern `b/**` is in `a/.gitignore` file match the pattern against path `b/c`),
- wildcards `?`, `*` and `[]` in the pattern do not match `/` in the path.

If the pattern is not _full path pattern_:
- match against last component of the path (file or directory name)

If the path specified initially doesn't match any ignored path, then the parent directory is looked up, until the parent directory is the _working directory_.

For example, pattern `a/b/*` matches input path `a/b/c/d/file` like so:

- `a/b/*` does not directly match `a/b/c/d/file` since `*` does not match `/` in  _full path pattern_,

- `a/b/*` does not directly match directory path `a/b/c/d`,

- `a/b/*` directly matches directory path `a/b/c`, 

therefore the input path matches the pattern.


