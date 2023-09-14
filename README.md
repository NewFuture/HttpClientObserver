# HttpClientObserver

Diagnose your Http out-bound traffic with one line of code `NewFuture.HttpClientObserver.SubscribeAll()`.

It will log all `HttpClient` requests into the Diagnostic Tools Events view window.

**Highly recommend to use it in debug mode only with `#if DEBUG`** for performance and security reasons.

## Installation

install with nuget

```bash
dotnet add package NewFuture.HttpClientObserver
```

## Usage

For example, you can add it to `Startup.cs` or `Program.cs` to subscribe all HttpClient events.

### Arguments

-   `logResponseBody`: log response content, default is `true`;
-   `ignoreUris`: ignore uri list in regex, default is the common urls in `HttpClientObserver.DefaultIgnoreUrlList`.

### Examples

Basic usage

```csharp
// in Startup.cs

#if DEBUG
// subscribe all HttpClient response in debug mode
NewFuture.HttpClientObserver.SubscribeAll();
#endif
```

Ignore some urls

```csharp
// DefaultIgnoreUrlList contains some common urls, you can add your own
List<Regex> ignoreList = new(HttpClientObserver.DefaultIgnoreUrlList)
{
    new Regex("http://localhost.*")
};
// ignore uri list in regex
NewFuture.HttpClientObserver.SubscribeAll(ignoreUris: ignoreList);
```

Don't log response body

```csharp
NewFuture.HttpClientObserver.SubscribeAll(false);
```
