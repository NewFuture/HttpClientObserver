# HttpClientObserver

Diagnose your Http out-bound traffic with one line of code `NewFuture.HttpClientObserver.SubuscribeAll()`.

it logs all httpclient outbound into the diagnostic tools window.

## Installation

install with nuget

```bash
dotnet add package NewFuture.HttpClientObserver
```

## Usage

`NewFuture.HttpClientObserver.SubuscribeAll();` will subscribe all httpclient response

For example, you can add it to `Startup.cs` or `Program.cs` to subscribe all httpclient response in debug mode

```csharp
#if DEBUG
// subscribe all httpclient response in debug mode
NewFuture.HttpClientObserver.SubuscribeAll();
#endif
```

## arguments

-   logContent: log response content, default is `true`
-   ignoreUriList: ignore uri list in regex

Examples

```csharp
// ignore uri list in regex
NewFuture.HttpClientObserver.SubuscribeAll(ignoreUriList: new string[] { "http://localhost:5000/api/.*" });
```

```csharp
// DefaultIngoreUrlList contains some common urls, you can add your own

List<System.Text.RegularExpressions.Regex> ignoreList = new(HttpClientObserver.DefaultIngoreUrlList)
{
    new Regex("http://localhost.*")
};
HttpClientObserver.SubuscribeAll(logConent: true, ignoreUris: ignoreList);
```
