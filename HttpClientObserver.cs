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
    public static readonly List<Regex> DefaultIngoreUrlList = new List<Regex>()
        {
            new Regex("https://rt\\.services\\.visualstudio\\.com/.*"),
            new Regex("https://dc\\.services\\.visualstudio\\.com/.*"),
        };

    private IDisposable subscription;
    private bool logContent = true;
    private readonly IEnumerable<Regex> ignoreUris = null;

    /// <summary>
    /// create a new instance of HttpClientObserver
    /// prefer HttpClientObserver.SubuscribeAll
    /// </summary>
    /// <param name="logConent"></param>
    /// <param name="ignoreUris"></param>   
    public HttpClientObserver(bool logConent, IEnumerable<Regex> ignoreUris)
    {
        this.logContent = logConent;
        this.ignoreUris = ignoreUris;
    }

    /// <summary>
    ///  subscribe all DiagnosticListener.
    ///  it should only be called in development environment.
    /// </summary>
    /// <param name="logConent">whether log the content of the response.</param>
    /// <param name="ignoreUris">ignoreList the list of regex to ignore.</param>
    /// <returns>IDisposable</returns>
    public static IDisposable SubuscribeAll(bool logConent = true, IEnumerable<Regex> ignoreUris = null)
    {
        using HttpClientObserver observer = new HttpClientObserver(logConent, ignoreUris ?? DefaultIngoreUrlList);
        return DiagnosticListener.AllListeners.Subscribe(observer);
    }

    /// <inheritdoc/>
    public void OnNext(DiagnosticListener value)
    {
        if (value.Name == "HttpHandlerDiagnosticListener")
        {
            Debug.Assert(subscription == null, "should only subscribe once");
            subscription = value.Subscribe(new HttpHandlerDiagnosticListener(this.logContent, this.ignoreUris));
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
    private sealed class HttpHandlerDiagnosticListener : IObserver<KeyValuePair<string, object>>
    {
        private static readonly Type activityStopData = Type.GetType("System.Net.Http.DiagnosticsHandler+ActivityStopData, System.Net.Http", throwOnError: false);
        private static readonly PropertyInfo requestProperty = activityStopData.GetProperty("Request");

        private static readonly PropertyInfo responseProperty = activityStopData.GetProperty("Response");

        private readonly bool logContent;

        private readonly IEnumerable<Regex> ignoreList;

        /// <summary>
        /// The type is private, so we need to use reflection to access it.
        /// </summary>
        private static HttpResponseMessage ResponseAccessor(object o)
        {
            return (HttpResponseMessage)responseProperty?.GetValue(o);
        }

        /**
        * The maximum length of the content that can be written to the trace. total length of the trace is 4096 include prefix.
        * If the content is longer than this, it will be truncated.
        */
        private static void LogReponseContent(HttpResponseMessage responseMessage)
        {
            const int MaxLogLength = 4000;
            if (responseMessage?.Content == null)
            {
                return;
            }

            string logCategory = responseMessage.RequestMessage.RequestUri.ToString();
            string content = responseMessage.Content.ReadAsStringAsync()?.Result;
            string message = content.Length > MaxLogLength ? content.Substring(0, MaxLogLength) : content;
            Trace.WriteLine(message, logCategory);
        }

        private static Uri GetReqestUri(object o)
        {
            HttpRequestMessage httpRequestMessage = (HttpRequestMessage)requestProperty?.GetValue(o);
            return httpRequestMessage?.RequestUri;
        }

        public HttpHandlerDiagnosticListener(bool logContent = true, IEnumerable<Regex> ignoreList = null)
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

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key == "System.Net.Http.HttpRequestOut.Stop" && !ShouldIgnore(GetReqestUri(value.Value)))
            {
                Trace.Write(value.Value, "HttpStop");
                if (logContent)
                {
                    var response = ResponseAccessor(value.Value);
                    LogReponseContent(response);
                }
            }
        }

        private bool ShouldIgnore(Uri uri)
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
