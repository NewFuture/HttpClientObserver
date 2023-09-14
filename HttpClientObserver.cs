// <copyright file="HttpClientObserver.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

namespace NewFuture;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>
/// HttpClientObserver
/// </summary>
public class HttpClientObserver : IDisposable, IObserver<DiagnosticListener>
{
    /// <summary>
    /// the default ignore list application insights urls
    /// </summary>
    public static readonly List<Regex> DefaultIgnoreUrlList = new List<Regex>()
        {
            new Regex("https://rt\\.services\\.visualstudio\\.com/.*"),
            new Regex("https://dc\\.services\\.visualstudio\\.com/.*"),
        };

    private IDisposable? subscription;
    private readonly bool logContent = true;
    private readonly IEnumerable<Regex>? ignoreUris = null;

    /// <summary>
    /// create a new instance of HttpClientObserver
    /// prefer HttpClientObserver.SubscribeAll
    /// </summary>
    /// <param name="logResponseBody"></param>
    /// <param name="ignoreUris"></param>   
    public HttpClientObserver(bool logResponseBody, IEnumerable<Regex>? ignoreUris)
    {
        this.logContent = logResponseBody;
        this.ignoreUris = ignoreUris;
    }

    /// <summary>
    ///  subscribe all DiagnosticListener.
    ///  it should only be called in development environment.
    /// </summary>
    /// <param name="logResponseBody">whether log the content of the response.</param>
    /// <param name="ignoreUris">ignoreList the list of regex to ignore.</param>
    /// <returns>IDisposable</returns>
    public static IDisposable SubscribeAll(bool logResponseBody = true, IEnumerable<Regex>? ignoreUris = null)
    {
        using HttpClientObserver observer = new HttpClientObserver(logResponseBody, ignoreUris ?? DefaultIgnoreUrlList);
        return DiagnosticListener.AllListeners.Subscribe(observer);
    }

    /// <inheritdoc/>
    public void OnNext(DiagnosticListener value)
    {
        if (value.Name == "HttpHandlerDiagnosticListener")
        {
            Debug.Assert(subscription == null, "should only subscribe once");
            var observer = new HttpHandlerDiagnosticListener(this.logContent, this.ignoreUris);
            subscription = value.Subscribe(observer);
        }
    }

    /// <inheritdoc/>
    public void OnCompleted()
    {
    }

    /// <inheritdoc/>
    public void OnError(Exception error)
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        subscription?.Dispose();
    }

    /**
     * https://www.meziantou.net/observing-all-http-requests-in-a-dotnet-application.htm
     */
    private sealed class HttpHandlerDiagnosticListener : IObserver<KeyValuePair<string, object?>>
    {
        private static readonly Type? activityStopData = Type.GetType("System.Net.Http.DiagnosticsHandler+ActivityStopData, System.Net.Http", throwOnError: false);
        private static readonly PropertyInfo? requestProperty = activityStopData?.GetProperty("Request");

        private static readonly PropertyInfo? responseProperty = activityStopData?.GetProperty("Response");

        private readonly bool logContent;

        private readonly IEnumerable<Regex>? ignoreList;

        /// <summary>
        /// The type is private, so we need to use reflection to access it.
        /// </summary>
        private static HttpResponseMessage? ResponseAccessor(object? o)
        {
            return (HttpResponseMessage?)responseProperty?.GetValue(o);
        }

        /**
        * The maximum length of the content that can be written to the trace. total length of the trace is 4096 include prefix.
        * If the content is longer than this, it will be truncated.
        */
        private static void LogResponseContent(HttpResponseMessage? responseMessage)
        {
            const int MaxLogLength = 4000;
            if (responseMessage?.Content == null)
            {
                return;
            }

            string? logCategory = responseMessage.RequestMessage?.RequestUri?.ToString();
            string content = responseMessage.Content.ReadAsStringAsync()?.Result ?? "";
            string message = content.Length > MaxLogLength ? content[..MaxLogLength] : content;
            Trace.WriteLine(message, logCategory);
        }

        private static Uri? GetRequestUri(object? o)
        {
            HttpRequestMessage? httpRequestMessage = (HttpRequestMessage?)requestProperty?.GetValue(o);
            return httpRequestMessage?.RequestUri;
        }

        public HttpHandlerDiagnosticListener(bool logContent, IEnumerable<Regex>? ignoreList)
        {
            this.logContent = logContent;
            this.ignoreList = ignoreList;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == "System.Net.Http.HttpRequestOut.Stop" && !ShouldIgnore(GetRequestUri(value.Value)))
            {
                Trace.Write(value.Value, "HttpStop");
                if (logContent)
                {
                    var response = ResponseAccessor(value.Value);
                    LogResponseContent(response);
                }
            }
        }

        private bool ShouldIgnore(Uri? uri)
        {
            if (uri != null && ignoreList != null)
            {
                foreach (var regex in ignoreList)
                {
                    if (regex.IsMatch(uri.ToString()))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
