# ISLAM-Shell (Custom C# Shell)

![C#](https://img.shields.io/badge/C%23-%23239120.svg?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-5C2D91?style=for-the-badge&logo=.net&logoColor=white)

A fully-functional, lightweight Unix-style shell built entirely in C# and .NET.

This project was built as part of the [CodeCrafters "Build your own Shell" challenge](https://app.codecrafters.io/courses/shell/overview). It demonstrates low-level system interactions, process management, custom string parsing, and interactive console buffer manipulation.

## Demo

_(Note: Replace this line with the cool screenshot you took earlier! Just drag and drop the image right here into GitHub)_

## Key Features

This shell goes far beyond basic command execution. It features a custom-built execution engine capable of handling standard Bash behaviors:

- **Advanced Parsing Engine:** A state-machine Tokenizer that flawlessly handles single quotes (`'`), double quotes (`"`), and backslash escaping (`\`) without relying on basic string splits.
- **Multi-Stage Pipelines (`|`):** Connects multiple processes together (e.g., `ls | head -n 3 | findstr "txt"`) using asynchronous C# `MemoryStream` and `Task` pipelines to prevent deadlocks.
- **I/O Redirection:** Full support for redirecting standard output and standard error (`>`, `>>`, `1>`, `2>`, `2>>`).
- **Interactive REPL:** Built from the ground up using `Console.ReadKey(intercept: true)` to support real-time user input manipulation.
- **Tab Auto-Completion:** Intelligent completion for built-in commands, `PATH` executables, and complex nested file/directory paths.
- **History Persistence:** Tracks session history, allows Up/Down arrow scrolling, and persists data to a `HISTFILE` on disk via `-a`, `-w`, and `-r` flags.
- **Built-in Commands:** Native implementations of `cd`, `pwd`, `echo`, `type`, `history`, and `exit`.

## How to Run

This is a standard .NET project, running it is simple. Ensure you have the .NET SDK installed.

1. Clone the repository:

```bash
   git clone https://github.com/IslamAli-0/codecrafters-shell-csharp.git
   cd codecrafters-shell-csharp
```

2. Run the shell using the .NET CLI:

```bash
   dotnet run
```

3. (Optional) Build a standalone executable:

```bash
   dotnet publish -c Release
```

You can then find the executable in the `bin/Release` folder and run it natively from your terminal.

## Under the Hood (Architecture)

- **Tokenizer (`ParseInput`):** Instead of using `string.Split()`, the shell evaluates user input character-by-character to respect quote boundaries and escape characters before passing arguments securely via `Process.StartInfo.ArgumentList`.
- **Execution Engine:** Resolves commands by checking an internal array of built-ins first, then scans the system's `PATH` environment variable using `File.Exists` (and checks execution permissions) to launch external binaries via `System.Diagnostics.Process`.

## Acknowledgments

This project was built following the architecture and testing suite provided by [CodeCrafters](https://codecrafters.io).
