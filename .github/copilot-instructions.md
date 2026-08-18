# SaveHub — Code Style & Conventions

These rules describe how code in this repository must be written. Apply them to
every C# file you create or edit (except auto-generated files such as
`*.Designer.cs`, `*.g.cs`, and anything under `bin/` or `obj/`).

## Language & formatting

- **Explicit types only — never `var`.** Declare the concrete type for every
  local, `foreach`/`for` variable, `out` variable, `using` variable, and tuple
  deconstruction (e.g. `IReadOnlyList<SaveEntry> saves = ...`, not `var saves`).
- **Always use braces.** Every `if`, `else`, `for`, `foreach`, `while`, and `using`
  block uses `{ }` — even for a single statement. Never write one-line bodies like
  `if (x) return;` or `if (x) { Do(); return; }`.
- **Allman braces.** The opening brace goes on its own line:

  ```csharp
  if (condition)
  {
      DoSomething();
  }
  ```

- **Methods and constructors use block bodies — no expression bodies.** Do not use
  `=>` for methods or constructors. Write:

  ```csharp
  public string Name()
  {
      return _name;
  }
  ```

  not `public string Name() => _name;`.
- **Expression-bodied *properties* are allowed** for simple read-only getters
  (e.g. `public bool IsActive => DateTimeOffset.UtcNow < ExpiresAt;`).
- Nullable reference types and implicit usings are enabled. Prefer the least
  amount of `using` directives needed; add a `using` rather than fully-qualifying a
  type when introducing an explicit type.

## Member order inside a type

Order members top-to-bottom exactly like this:

1. **Fields** (constants and `static readonly` first, then instance fields)
2. **Properties**
3. **Constructor(s)**
4. **Static methods** (e.g. factory/`Create` methods)
5. **Public methods** (instance)
6. **Private methods** (instance)
7. **Helper methods** (small private/static helpers, formatting, parsing, etc.)
8. **One-liner nested structs / classes / records** (only if they belong here — see
   below)

## File organization (one responsibility per file)

- **One primary type per file**, named after the type. Every class, interface, and
  `enum`, and every record/struct that has a body (methods, computed properties,
  etc.), lives in its own file.
- **DTOs / models get their own file.** A model that spans multiple lines must not
  share a file with another type.
- **Exception — one-liner DTOs.** A *one-liner* positional `record`/`struct`/`class`
  (parameter list only, no body) with **at most 5 parameters** may be grouped: put
  several one-liners together in one file, or place a one-liner at the bottom of the
  file of the type it most closely supports. If a would-be one-liner needs more than
  5 parameters or gains a body, promote it to a standard class/record/struct in its
  own file.
- **Single Responsibility.** Each class/struct/record does one thing. Split types
  that accumulate unrelated responsibilities.

Examples in this repo:

- `StorageFile` (`record struct StorageFile(string Path, byte[] Content)`) is a
  one-liner, so it may sit next to `PreparedSave`.
- `CoverArtSource`, `HttpCoverArtResolver`, `ICoverArtResolver` each have bodies, so
  each lives in its own file.

## Naming & structure

- PascalCase for types, methods, properties, constants; `_camelCase` for private
  fields; camelCase for locals and parameters.
- Keep all save naming/layout decisions in `SaveNaming` — never hardcode archive
  names or paths elsewhere.
- Pure, side-effect-free logic lives in `SaveHub.Core`; network/IO lives in the
  provider projects.

## Comments

- Comment only to explain what the code cannot show on its own; keep it to one
  short line. Don't restate the next line or narrate a change.
- Public API types/members keep their XML `<summary>` docs.

## Build

- Make **all** edits for a task first, then compile once with `dotnet build` to
  verify. Don't compile after each individual change.
